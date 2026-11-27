# PHIẾU DD2 - Rà quét rác Ubuntu + Windows (chỉ đọc) và lệnh `quet-rac` (27/11/2026)

- **Từ**: agent-tsudisk - **Trạng thái**: MỞ (chờ 👤 mục 3) - nối tiếp [`20261126-DD1`](20261126-DD1_phoi-hop-don-dia.md)
- Không ghi giá trị bí mật nào vào phiếu này (chỉ tên tệp, quyền, kích thước).

## 1. Số đo (27/11/2026 ~20:00)

| Mục | Số đo | Ghi chú |
| --- | --- | --- |
| Ổ C | 326 GB, dùng 301 GB, còn **25 GB (93%)** | từ 22 GB sau DD1 |
| `ext4.vhdx` (Ubuntu, nằm trên C) | **190,7 GB**; bên trong `df /` dùng 149 GB (còn 808 GB trống) | ~40 GB thu hồi được bằng co VHDX; đây là đòn bẩy duy nhất lớn |
| Windows rác thật | SoftwareDistribution\Download 1,2 GB, Temp 0,9 GB (đã sạch từ DD1), Thùng rác 0,35 GB | tổng ~2,5 GB |
| `Windows\Installer` 2,4 GB, Package Cache 0,3 GB | KHÔNG xóa | gỡ/sửa phần mềm cần |
| Ubuntu: `~/projects` 77 GB, `~/tools` 16 GB, `.gradle` 9,3 GB, `Android` 8,4 GB, `.rustup` 6,3 GB, `.nuget` 5,5 GB | tất cả có chủ | dọn bằng công cụ của từng dự án; KHÔNG đụng `.nuget` (khóa NuGet của tsudisk) |
| Ubuntu: journal 816 MB, `/var/cache` 277 MB, apt 128 gói | cần `sudo` (không có) | 👤 mục 3.3 |

Kết luận: dọn rác trong Ubuntu gần như không trả dung lượng cho C. Trả lại C chủ yếu bằng co VHDX (DD1 mục 3).

## 2. Đã làm (chỉ việc chắc chắn là rác của chính agent)

- Xóa 10 tệp `~/.claude.json.tmp.*` cũ > 1 ngày (bản sao cấu hình Claude bị bỏ lại; nằm rải ở `~`).
- Xóa `%TEMP%\tsudisk-int7` (112 MB: bản exe thử INT7 + ảnh chụp, tái tạo được).
- `chmod 600` cho `~/.api-key-noi-bo-khong-commit/20261111-token1.txt` (đang 644; thư mục cha đã 700).
- KHÔNG xóa gì khác: phần còn lại thuộc phiên/dự án khác hoặc cần chủ dự án quyết (mục 3).

## 3. Việc 👤 chủ dự án (theo mức ưu tiên)

1. 🔐 **HARD STOP 4(a)**: `~/.api-key-noi-bo-khong-commit/20261111-token1.txt` là token THÔ (không `KEY=VALUE`,
   `noleak-inspect` mã 3). Agent không đọc/dùng. Việc: tự sửa thành `TEN_BIEN=gia_tri` (hoặc ủy quyền rõ cho agent chạy
   `noleak-inspect --to-env`), hoặc xoay + xóa nếu không còn dùng. Quyền đã siết 600.
2. 🔐 `C:\khoa\doc-cccd.jks`: kho khóa ký Java nằm bản rõ trên NTFS (không chmod được, mọi tiến trình Windows đọc được).
   Việc: chuyển vào Linux `~/.config/tsudev/` quyền 600 hoặc mã hóa `age -p`; xác định dự án sở hữu (`C:\build\cccd-reader`?)
   rồi lập phiếu XK ở repo đó. Không phải khóa phát hành của hệ tsudev theo ghi nhận hiện có (chưa kiểm).
3. 👤 Co VHDX + bật sparse (DD1 mục 3.2-3.3): `sudo fstrim -av`, rồi PowerShell: `wsl --shutdown`,
   `wsl --manage Ubuntu --set-sparse true` (agent không tự chạy được vì sẽ cắt phiên). Dự kiến C còn ~65 GB.
4. 👤 Phần mềm đã gỡ còn sót (chưa xóa, cần bạn xác nhận từng mục):
   - **TeamViewer**: `C:\Program Files\TeamViewer` còn, `%LOCALAPPDATA%\TeamViewer` 114 MB, Roaming MRU - phần mềm điều khiển
     từ xa, nên gỡ hẳn nếu không dùng. **RustDesk** và **UltraViewer** (đang chạy dịch vụ `UltraViewer_Service`) cũng là điều
     khiển từ xa: dùng thì giữ, không dùng thì gỡ.
   - Bitdefender (`C:\ProgramData\Bitdefender*`, các tệp `*.uninstall.*.bdinstall.*`), WPS/Kingsoft (`KingsoftData`,
     Roaming `kingsoft`), CocCoc, MPC-HC, Skype, Rufus, GitKrakenCLI 185 MB: không thấy trong danh sách đã cài.
   - `C:\build\cccd-reader` 359 MB, `C:\Temp\sumy-pptr` 159 MB, `C:\OneDriveTemp`: thư mục dự án/tạm ngoài chuẩn.
   - ⛔ `Infatica` (`C:\ProgramData\lrpjava`, Roaming `lrpjava`/`Precipitator`, tác vụ `\infatica` đang `Disabled`): CHƯA xóa;
     chờ `tsuguard` + 👤 chốt pháp y (DD1 3b).
5. Ubuntu cần `sudo`: `sudo journalctl --vacuum-size=100M`, `sudo apt-get clean`.

## 4. Thay đổi trong TsuDisk (để lần sau không xóa nhầm)

- Lệnh mới `quet-rac` (chỉ đọc) + `Core/Cleanup/JunkPolicy` + ADR-0013 + 37 test (`JunkPolicyTests`): 600 -> 637 đạt.
  Mọi ca ở mục 1-3 trên (token, `.jks`, `.vhdx`, Infatica, Installer, Thùng rác) là ca test thật.
- Test chặn đường xóa trong mã quét (`Cleanup_code_has_no_delete_path`).
- Chưa làm: chế độ xóa thật (cần ADR riêng), `--json`, nút trên GUI.

## 5. HƯỚNG DẪN TỪNG BƯỚC giải phóng ổ C (chủ dự án chạy; `wsl --shutdown` để cuối)

Mục tiêu: C từ 25 GB trống lên khoảng 65-75 GB. Thứ tự quan trọng: dọn TRONG Ubuntu trước, rồi mới co VHDX.
Dữ liệu người dùng không bị đụng: chỉ xóa cache/build tái tạo được.

### Bước 0 - Kiểm tra trước (30 giây)
Đóng mọi cửa sổ Claude/VS Code/terminal đang chạy build. Trong Ubuntu: `df -h /` ghi lại số "Used" (hiện 149 GB).

### Bước 1 - Dọn rác tái tạo được trong Ubuntu (không cần sudo)
Gõ từng lệnh sau (có `!` đầu dòng nếu chạy trong phiên Claude). Mỗi lệnh chỉ xóa thứ có thể dựng lại bằng `npm ci`:
```bash
# 1a. node_modules >= 14 ngày không ai dùng, đã gitignore, có package-lock.json (~5,8 GB). Khôi phục: cd <repo> && npm ci
cd ~/projects && rm -rf quansu-xanuicam/node_modules app-la-ban-a9/node_modules ubnd-ttpvhcc-qr/node_modules \
  ubnd-ap-an-hoa/node_modules wedding-sumy/node_modules mieubathuong/node_modules \
  tsucontact/website/node_modules lotus-share/website/node_modules
# 1b. cache thử nghiệm (~0,5 GB), không phiên nào dùng
rm -rf ~/.cache/tmp-cong ~/.cache/lotus-tmp
```
Tùy chọn (chậm lần build kế tiếp, nhưng nhiều dung lượng nhất): `cd ~/projects/tsucwico && cargo clean` (12,6 GB) và
`cd ~/projects/tsuguard && cargo clean` (1,8 GB) CHỈ khi hai phiên đó không còn làm việc; `target` đang được ghi hôm nay.
Đừng xóa `~/.nuget` (khóa NuGet của tsudisk), `~/.rustup`, `~/tools`, `~/Android` khi chưa chắc.

### Bước 2 - Dọn cần sudo (Ubuntu)
```bash
sudo journalctl --vacuum-size=100M      # journal 816 MB -> 100 MB
sudo apt-get clean                      # gói .deb tải về
sudo fstrim -av                         # BẮT BUỘC: báo cho VHDX biết khối nào đã trống (xem số "trimmed")
```
Chạy `df -h /` lần nữa: "Used" phải giảm so với Bước 0.

### Bước 3 - Dọn rác Windows (PowerShell chạy "Run as administrator")
```powershell
# 3a. Cập nhật Windows đã tải (1,2 GB): dừng dịch vụ, xóa, bật lại - Windows tự tải lại khi cần
Stop-Service wuauserv, bits -Force
Remove-Item C:\Windows\SoftwareDistribution\Download\* -Recurse -Force -ErrorAction SilentlyContinue
Start-Service wuauserv, bits
# 3b. Thùng rác (0,35 GB) - XEM TRƯỚC, đây là tệp bạn từng xóa: mở Thùng rác kiểm tra rồi mới làm
Clear-RecycleBin -DriveLetter C -Force
```
KHÔNG xóa `C:\Windows\Installer`, `Package Cache`, `hiberfil.sys`, thư mục `lrpjava`/`Precipitator` (xem mục 3.4).
Gỡ phần mềm đã bỏ qua Settings > Apps (TeamViewer, WPS, Bitdefender...) để gỡ sạch đúng cách thay vì xóa thư mục tay.

### Bước 4 - Co VHDX (bước thu hồi lớn nhất, ~40-75 GB)
Mở PowerShell thường (không cần Admin cho cách A):
```powershell
wsl --shutdown                                   # đóng MỌI thứ chạy trong WSL (Claude, VS Code...) - bạn chạy cuối cùng
wsl --manage Ubuntu --set-sparse true            # WSL 3.0.5 đã hỗ trợ; từ giờ VHDX tự trả dung lượng, không cần co tay nữa
```
Nếu lệnh báo cần `--allow-unsafe` hoặc cảnh báo hỏng dữ liệu: KHÔNG ép, dùng cách B (đáng tin hơn):
```powershell
wsl --shutdown
# PowerShell "Run as administrator"
diskpart
  select vdisk file="C:\Users\devng\AppData\Local\wsl\{c3304ba3-a024-4a5c-a690-5ac0f41f96d4}\ext4.vhdx"
  attach vdisk readonly
  compact vdisk
  detach vdisk
  exit
```
(Cách khác nếu có Hyper-V: `Optimize-VHD -Path "<đường dẫn trên>" -Mode Full`.) Cần chừng vài chục phút, ổ C cần ít nhất
vài GB trống tạm; hiện có 25 GB nên đủ.

### Bước 5 - Kiểm chứng
```powershell
Get-PSDrive C                                    # Free tăng lên
(Get-Item "C:\Users\devng\AppData\Local\wsl\{c3304ba3-a024-4a5c-a690-5ac0f41f96d4}\ext4.vhdx").Length/1GB   # nhỏ lại
wsl -d Ubuntu -- df -h /                         # đĩa Ubuntu vẫn nguyên, dữ liệu còn
```
Rồi mở lại repo: `cd ~/projects/tsudisk && dotnet test tests/TsuDisk.Core.Tests` (kỳ vọng 637 đạt). Repo nào đã xóa
`node_modules` thì `npm ci` khi cần làm việc lại.

### Nếu có sự cố
Chưa chạy được WSL sau co: `wsl --shutdown` rồi `wsl -d Ubuntu`; dữ liệu nằm trong VHDX nên không mất do `compact`.
Muốn an toàn tuyệt đối trước Bước 4: sao lưu mã nguồn quan trọng (đã có trên GitHub) và `~/agent-kb` (đã có bản sao
`/mnt/e/tsudev-sao-luu`). Ghi lại kết quả vào mục 6 dưới đây.

## 6. Kết quả sau khi chạy (👤/agent điền)
- [ ] Bước 1-2: Ubuntu Used trước/sau: ___ / ___ GB.
- [ ] Bước 3: Windows đã dọn.
- [ ] Bước 4-5: ext4.vhdx trước/sau: 190,7 / ___ GB; C trống trước/sau: 25 / ___ GB.
