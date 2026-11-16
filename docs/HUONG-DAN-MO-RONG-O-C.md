# Hướng dẫn mở rộng ổ C bằng vùng trống (Unallocated) - an toàn cho các ổ khác

> Dựa trên lần thực hiện thật ngày 16/11/2026 trên một máy thật: ổ C từ 265,11 GB lên 325,01 GB,
> không khởi động lại, không ảnh hưởng tới các ổ dữ liệu khác, WSL Ubuntu hay các dịch vụ đang chạy.
> **Cách nhanh và an toàn nhất hiện nay: dùng TsuDisk** - `tsudisk ke-hoach C:` để xem kế hoạch, `tsudisk mo-rong C:`
> để thực hiện. TsuDisk tự động hóa đúng quy trình dưới đây, kèm kiểm bất biến, nhật ký và hoàn tác.
> Tài liệu này giữ lại để hiểu từng bước và để tự làm bằng tay khi cần.

---

## 1. Vì sao lần làm này không ảnh hưởng tới dữ liệu ổ khác

Ổ cứng (GPT) có một **bảng phân vùng**. Mỗi phân vùng chỉ là một dòng ghi "bắt đầu ở byte X, dài Y byte".
Toàn bộ thao tác dựa trên 6 nguyên tắc:

| # | Nguyên tắc | Cách đã áp dụng |
|---|---|---|
| 1 | **Chỉ động vào vùng nằm giữa C: và phân vùng kế tiếp** | Chỉ xoá/tạo lại phân vùng Recovery và dùng 60 GB trống phía sau nó. Dòng của G:, E:, EFI, MSR trong bảng phân vùng **không bị sửa**. |
| 2 | **Không di chuyển dữ liệu** | Mở rộng NTFS chỉ "nối thêm" không gian trống vào cuối C:, không chép/dời file nào. Vì vậy chạy được ngay khi Windows đang hoạt động (online), không cần khởi động lại. |
| 3 | **Không mở rộng sang vùng không liền kề** | 737 MB ở cuối ổ đĩa (sau E:) được để nguyên: muốn gộp vào C: phải dời toàn bộ G: và E: (150 GB). Đó chính là thao tác dễ gây mất dữ liệu nhất. |
| 4 | **Kiểm tra trước, dừng ngay khi có điều bất thường** | Script kiểm tra kiểu ổ (GPT), vị trí Recovery, kích thước vùng trống, BitLocker, lỗi hệ thống tệp, vị trí WinRE. Sai một điều là dừng **trước khi** sửa gì. Có chế độ chạy thử `-DryRun`. |
| 5 | **Sao lưu những gì sắp bị xoá/sửa** | Bảng phân vùng (txt + xml), BCD (`bcdedit /export`), ảnh WinRE (`Winre.wim`) đều được lưu trước khi thay đổi. |
| 6 | **Tự khôi phục khi lỗi giữa chừng + kiểm tra sau** | Lỗi sau khi xoá Recovery thì script tạo lại Recovery ở đúng vị trí cũ và bật lại WinRE. Cuối cùng so sánh vị trí/kích thước G:, E: trước-sau. |

**WSL Ubuntu:** đĩa của Ubuntu là một file `ext4.vhdx` nằm trên C:
(`%LOCALAPPDATA%\wsl\{...}`), giới hạn ảo 1 TB. Mở rộng C: chỉ làm file này có thêm chỗ
để lớn lên, **không cần** `wsl --shutdown` hay `wsl --manage --resize`. Vì vậy terminal, Postgres (5432),
Redis (6379) và `~/projects/*` không bị gián đoạn.

---

## 2. Trước khi làm: xác định bạn đang ở trường hợp nào

Mở **Disk Management** (`Win + X` → Disk Management), nhìn thứ tự các khối trên Disk 0:

| Bố cục | Cách xử lý | Rủi ro |
|---|---|---|
| `C:` → **Unallocated** (liền kề ngay sau C:) | Chuột phải C: → **Extend Volume**. Xong. | Rất thấp |
| `C:` → **Recovery** → **Unallocated** (trường hợp lần này) | Làm theo **Mục 4 hoặc Mục 5** | Thấp (có quy trình bên dưới) |
| Unallocated **nằm trước C:**, hoặc bị chặn bởi ổ dữ liệu (D:, G:...) | Phải **dời phân vùng** bằng phần mềm bên thứ ba. **Sao lưu toàn bộ ổ trước.** Cân nhắc kỹ: thường không đáng. | Cao |
| Unallocated nhỏ ở cuối ổ, liền sau ổ dữ liệu (như 737 MB sau E:) | Để nguyên, hoặc Extend vào chính ổ liền kề đó (E:) | Thấp nếu Extend ổ liền kề |

> ⚠️ **Không bao giờ** xoá/sửa phân vùng EFI (≈100-300 MB, FAT32) hay MSR (16 MB): máy sẽ không khởi động được.

---

## 3. Chuẩn bị (bắt buộc)

1. **Sao lưu dữ liệu quan trọng** ra nơi khác (ổ ngoài/đám mây). Quy trình an toàn nhưng sao lưu vẫn là lớp bảo vệ cuối cùng.
2. **Laptop cắm sạc.** Mất điện giữa lúc ghi bảng phân vùng là rủi ro lớn nhất.
3. Đóng các công cụ quản lý ổ đĩa khác (Disk Management, phần mềm phân vùng) để tránh xung đột.
4. Mở **PowerShell với quyền Administrator**: `Win + X` → *Terminal (Admin)*.
   (Từ WSL thì gọi `Start-Process powershell -Verb RunAs`; Windows sẽ hiện hộp thoại UAC.)
5. Kiểm tra BitLocker: `manage-bde -status C:`.
   Nếu *Protection On*: mở rộng vẫn được hỗ trợ, nhưng cẩn thận hơn thì tạm dừng trước:
   `Suspend-BitLocker -MountPoint C: -RebootCount 1`, xong việc thì `Resume-BitLocker -MountPoint C:`.
   **Lưu khoá khôi phục (Recovery Key)** trước khi làm.

---

## 4. Cách A - Dùng TsuDisk (khuyến nghị)

```powershell
tsudisk phan-tich              # xem vùng trống nào gộp được vào ổ nào
tsudisk ke-hoach C:            # in kế hoạch từng bước, không thay đổi gì
tsudisk mo-rong C: --chay-thu  # quét + sao lưu, không ghi đĩa
tsudisk mo-rong C:             # thực hiện (Terminal Admin), gõ lại chữ cái ổ để xác nhận
```

Phần còn lại của mục này mô tả script PowerShell bản đầu `tools/extend-c.ps1` (tiền thân của TsuDisk), dành cho bố
cục **C: -> Recovery -> Unallocated** trên **Disk 0**. Mọi bố cục khác đều bị từ chối, không sửa gì.

### Bước 1. Chạy thử (không thay đổi gì)
```powershell
cd <thư mục chứa extend-c.ps1>
powershell -NoProfile -ExecutionPolicy Bypass -File .\extend-c.ps1 -DryRun
```
Đọc kết quả, phải thấy đủ các dòng:
- `Recovery = partition N (... MB); free gap after it = XX GB; next partition = ...`
- `C: aaa GB -> bbb GB` (kích thước dự kiến hợp lý)
- `Scan result: NoErrorsFound`
- `WinRE currently at partition: N` (trùng số Recovery ở trên)
- `=== DRY RUN OK - no changes made ===`

Có dòng `!!! ERROR` thì **dừng lại**, đọc lý do (xem Mục 7).

### Bước 2. Chạy thật
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\extend-c.ps1
```
Thời gian khoảng 2 phút. Kết quả đúng phải có `=== SUCCESS ===`.

### Bước 3. Kiểm tra
Làm theo **Mục 6**.

### Script làm gì (theo thứ tự)
| Giai đoạn | Hành động | Có thay đổi ổ? |
|---|---|---|
| Kiểm tra | GPT; phân vùng ngay sau C: là Recovery; vùng trống ≥ 50 GB; quét NTFS `Repair-Volume -Scan`; BitLocker; vị trí WinRE | Không |
| Sao lưu | `partitions-before-*.txt/.xml`, `bcd-backup-*`, `reagentc /info` | Không |
| 1 | `reagentc /disable` (Windows tự chép `Winre.wim` về `C:\Windows\System32\Recovery`) + chép thêm bản dự phòng | Không đổi phân vùng |
| 2 | Xoá phân vùng Recovery cũ | **Có** |
| 3 | Mở rộng C: tới (cuối vùng trống − 1 GB), căn theo MB | **Có** (chỉ C:) |
| 4 | Tạo Recovery mới 1 GB ngay sau C:, NTFS, đặt GPT type + thuộc tính ẩn/bảo vệ | **Có** |
| 5 | `reagentc /enable` rồi xác nhận WinRE nằm đúng phân vùng mới | Không |
| Lỗi giữa chừng | Tạo lại Recovery ở vị trí cũ (nếu đã xoá) + bật lại WinRE | Khôi phục |

> Đổi số đĩa/đường dẫn: script cố định `Disk 0` và ổ `C`. Log/sao lưu được ghi vào **thư mục chứa script**.

---

## 5. Cách B - Làm thủ công từng lệnh (hiểu rõ từng bước)

Chạy trong **PowerShell (Admin)**. Sau mỗi bước, kiểm tra kết quả rồi mới sang bước tiếp theo.

### Bước 0. Ghi lại hiện trạng (để đối chiếu và khôi phục)
```powershell
$B = "C:\DiskBackup-$(Get-Date -f yyyyMMdd-HHmm)"; New-Item -ItemType Directory $B | Out-Null
Get-Partition -DiskNumber 0 | Sort-Object Offset |
  Format-Table PartitionNumber,DriveLetter,Offset,Size,GptType -AutoSize | Out-String -Width 250 |
  Tee-Object "$B\partitions-before.txt"
bcdedit /export "$B\bcd-backup"
reagentc /info | Tee-Object "$B\reagentc-before.txt"
```
Ghi lại: số phân vùng Recovery (dòng có GptType `{de94bba4-...}`), cùng **Offset/Size** của tất cả ổ dữ liệu khác.

### Bước 1. Kiểm tra sức khoẻ ổ C
```powershell
Repair-Volume -DriveLetter C -Scan     # phải ra NoErrorsFound
```
Có lỗi thì chạy `chkdsk C: /f` (cần khởi động lại), sửa xong mới tiếp tục.

### Bước 2. Tắt WinRE (bảo toàn ảnh khôi phục)
```powershell
reagentc /disable
Test-Path C:\Windows\System32\Recovery\Winre.wim     # PHẢI là True
Copy-Item C:\Windows\System32\Recovery\Winre.wim "$B\Winre.wim"
```
Ra `False` thì **dừng lại**, chạy `reagentc /enable` và không làm tiếp.

### Bước 3. Xoá phân vùng Recovery cũ
```text
diskpart
  list disk
  select disk 0
  list partition                 ← xác định ĐÚNG số phân vùng Recovery (≈ 500 MB-1 GB, nằm sau C:)
  select partition 4             ← thay 4 bằng số đúng
  detail partition               ← kiểm tra lại: Type = de94bba4-06d1-4d40-a16a-bfd50179d6ac
  delete partition override
  exit
```
> ⚠️ Đây là bước dễ nhầm nhất: luôn dùng `detail partition` để xác nhận **trước** khi `delete`.

### Bước 4. Mở rộng C: (chừa 1 GB cho Recovery mới)
```powershell
$max = (Get-PartitionSupportedSize -DriveLetter C).SizeMax
$new = [math]::Floor(($max - 1GB) / 1MB) * 1MB
"{0:N2} GB" -f ($new/1GB)          # kiểm tra con số hợp lý
Resize-Partition -DriveLetter C -Size $new
```

### Bước 5. Tạo phân vùng Recovery mới ngay sau C:
```powershell
$c = Get-Partition -DriveLetter C
($c.Offset + $c.Size) / 1KB        # = OFFSET_KB, dùng ở lệnh dưới
```
```text
diskpart
  select disk 0
  create partition primary offset=OFFSET_KB
  format quick fs=ntfs label="Recovery"
  set id=de94bba4-06d1-4d40-a16a-bfd50179d6ac override
  gpt attributes=0x8000000000000001
  exit
```
Chỉ định `offset` để diskpart **không** đặt nhầm phân vùng vào vùng trống khác (ví dụ 737 MB cuối ổ).

### Bước 6. Bật lại WinRE
```powershell
reagentc /enable
reagentc /info      # Windows RE status: Enabled; location ...\partitionN\Recovery\WindowsRE
```

---

## 6. Kiểm tra sau khi làm (cả hai cách)

```powershell
Get-Partition -DiskNumber 0 | Sort-Object Offset | Format-Table PartitionNumber,DriveLetter,Offset,Size,GptType -AutoSize
Get-Volume | Format-Table DriveLetter,FileSystemLabel,HealthStatus,Size,SizeRemaining
reagentc /info
```
Danh sách kiểm tra:
- [ ] **Offset và Size của mọi ổ dữ liệu khác (G:, E:...) giống hệt** file `partitions-before`
- [ ] C: lớn hơn đúng như dự kiến, `HealthStatus = Healthy`
- [ ] Có phân vùng Recovery `{de94bba4-...}` khoảng 1 GB ngay sau C:
- [ ] `Windows RE status: Enabled`
- [ ] Trong WSL: `df -h /mnt/c` thấy dung lượng trống tăng; dịch vụ vẫn chạy (`ss -ltn`)
- [ ] (Nếu đã tạm dừng) `Resume-BitLocker -MountPoint C:`

Kết quả lần 16/11/2026 để tham khảo:

| Phân vùng | Offset (byte) | Trước | Sau |
|---|---|---|---|
| C: | 227 540 992 | 265,11 GB | **325,01 GB** |
| Recovery | 284 890 759 168 → **349 203 070 976** | 917 MB | **1 024 MB** |
| G: | 350 276 812 800 | 120 GB | 120 GB (không đổi) |
| E: | 479 124 783 104 | 30 GB | 30 GB (không đổi) |

---

## 7. Xử lý sự cố

| Tình huống | Cách xử lý |
|---|---|
| Script báo `Partition right after C: is not a Recovery partition` | Bố cục khác. Xem lại Mục 2, **không** ép script chạy. |
| `C: volume scan reported ...` | `chkdsk C: /f`, khởi động lại, chạy lại script. |
| `reagentc /disable` lỗi | Không làm tiếp. Kiểm tra `reagentc /info`; WinRE có thể đang hỏng, cần sửa riêng. |
| Lỗi sau khi đã xoá Recovery, trước khi mở rộng C: | Script tự tạo lại Recovery ở đúng vị trí cũ. Làm tay: Bước 5 với offset = Offset cũ / 1024 và `size=` (MB) cũ, rồi `reagentc /enable`. |
| Lỗi sau khi đã mở rộng C: nhưng chưa tạo Recovery | Không mất dữ liệu. Chỉ cần làm Bước 5 + Bước 6. Kể cả không tạo Recovery, `reagentc /enable` vẫn dùng được `Winre.wim` trên C:. |
| `reagentc /enable` báo lỗi do mất `Winre.wim` | Chép lại từ bản dự phòng: `Copy-Item <backup>\Winre.wim C:\Windows\System32\Recovery\Winre.wim` rồi `reagentc /enable`. |
| Không khởi động được (hiếm, chỉ khi lỡ tay sửa EFI/BCD) | Khởi động từ USB cài Windows → Repair → Command Prompt → `bcdedit /import <bản bcd-backup>` hoặc `bcdboot C:\Windows`. |

---

## 8. Dọn dẹp

Dùng máy bình thường vài ngày. Nếu `reagentc /info` vẫn báo *Enabled* thì có thể xoá file dự phòng
`Winre-backup-*.wim` (≈ 783 MB) để lấy lại dung lượng. Giữ lại các file log, `partitions-*` và `bcd-backup-*` (rất nhỏ).

## 9. Tóm tắt nhanh (lần sau)

1. Disk Management: xác nhận bố cục `C: → Recovery → Unallocated`.
2. Sao lưu dữ liệu quan trọng, cắm sạc, lưu khoá BitLocker (nếu có).
3. PowerShell (Admin): `.\extend-c.ps1 -DryRun` → đọc kết quả.
4. `.\extend-c.ps1` → thấy `=== SUCCESS ===`.
5. Đối chiếu Mục 6: các ổ khác **không đổi Offset/Size**, WinRE *Enabled*.
