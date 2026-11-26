<img src="assets/brand/app/icon-128.png" alt="TsuDisk" height="56">

# TsuDisk

[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/16949/badge)](https://www.bestpractices.dev/projects/16949)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/tsudev-org/tsudisk/badge)](https://scorecard.dev/viewer/?uri=github.com/tsudev-org/tsudisk)

Quản lý phân vùng ổ cứng Windows **an toàn trước hết**: phân tích vùng trống, lập kế hoạch xem trước, mở rộng ổ
(kể cả khi phân vùng Recovery chắn đường), có nhật ký từng bước và tự hoàn tác khi lỗi. Một tệp `tsudisk.exe` duy
nhất, không cần cài .NET.

Một sản phẩm của [TsuDEV](https://tsudev.com). Trang sản phẩm: <https://disk.tsudev.com>

**Miễn phí, mã nguồn mở (Apache 2.0), không quảng cáo; TsuDEV Pro là tuỳ chọn để ủng hộ tác giả.** Mọi chức năng của
TsuDisk dùng được không cần tài khoản, không cần trả phí. Dữ liệu chỉ ở máy của bạn: TsuDisk không đồng bộ hay gửi
thông tin đĩa, nhật ký, bản sao lưu lên internet.

**Hạng sản phẩm: C - Ngoại tuyến** (theo `.standards/docs/AUTH_AND_ACCOUNT.md` mục 1): chạy trọn vẹn không cần
Internet, không tài khoản, không gửi dữ liệu đi đâu. Đăng nhập tài khoản TsuDEV (`tsudisk dang-nhap`) là tùy chọn, chỉ
khi bạn tự gõ lệnh (TsuDisk không bao giờ nhắc đăng nhập), để nhận huy hiệu TsuDEV Pro/Ủng hộ; chưa đăng nhập thì
TsuDisk không kết nối mạng lần nào.

## Vì sao có TsuDisk

Disk Management của Windows chỉ cho **Extend Volume** khi vùng trống nằm ngay sau ổ. Trên rất nhiều máy, phân vùng
Recovery (WinRE) nằm chắn giữa ổ C: và vùng trống, nên nút Extend bị mờ. Các công cụ phân vùng thương mại giải quyết
bằng cách **dời dữ liệu** - thao tác rủi ro nhất khi quản lý ổ đĩa. TsuDisk làm theo quy trình Microsoft khuyến nghị
(KB5028997): tắt WinRE, dời phân vùng Recovery ra sau ổ đích, mở rộng ổ, bật lại WinRE - **không di chuyển một byte
dữ liệu nào** của các phân vùng khác, không cần khởi động lại.

## Cài đặt

Tải `tsudisk_<phiên bản>_windows-x64-portable.exe` ở <https://disk.tsudev.com/tai-ve/> (kèm SHA-256), hoặc qua Scoop:

```powershell
scoop bucket add tsudev https://github.com/tsudev-org/scoop-bucket
scoop install tsudev/tsudisk
```

Giao diện cửa sổ (bản xem trước): tải thêm `tsudisk-gui_<phiên bản>_windows-x64-portable.exe` ở cùng trang. Cửa sổ
hiện bản đồ đĩa, phân tích vùng trống, kế hoạch mở rộng kèm lệnh `tsudisk` tương ứng; đổi được chữ cái/nhãn ổ dữ
liệu, tạo phân vùng mới trong vùng trống và mở rộng ổ bằng vùng trống liền kề ngay trong ứng dụng (cần quyền quản trị,
xác nhận hai lần). Thu nhỏ, xoá phân vùng, WinRE và tuỳ chọn nâng cao vẫn chạy bằng lệnh `tsudisk`.

## Cách dùng

Mở **Terminal (Admin)** cho các lệnh có ghi đĩa. Lệnh chỉ đọc chạy được với quyền thường.

```powershell
tsudisk danh-sach                 # Liệt kê đĩa, phân vùng, vùng trống
tsudisk phan-tich                 # Mỗi vùng trống gộp được vào ổ nào, vì sao
tsudisk ke-hoach C:               # Lập kế hoạch mở rộng C:, in từng bước, KHÔNG thay đổi gì
tsudisk mo-rong C: --chay-thu     # Chạy các bước không ghi đĩa (quét, sao lưu) để kiểm tra
tsudisk mo-rong C:                # Thực hiện: hỏi xác nhận bằng cách gõ lại chữ cái ổ
tsudisk ke-hoach-winre            # Kế hoạch nới rộng Recovery khi Windows Update báo lỗi 0x80070643 (KB5034441)
tsudisk mo-rong-winre             # Thực hiện (Admin), gõ WINRE để xác nhận
tsudisk ke-hoach-doi-ten E: --chu-cai F --nhan "Dữ liệu"   # Kế hoạch đổi chữ cái và/hoặc nhãn ổ, KHÔNG thay đổi gì
tsudisk doi-ten E: --chu-cai F --nhan "Dữ liệu"            # Thực hiện (Admin), gõ lại chữ cái ổ cũ để xác nhận
tsudisk ke-hoach-thu-nho D: --giam-mb 20480    # Kế hoạch thu nhỏ ổ NTFS, nhường 20 GB vùng trống ngay sau ổ
tsudisk thu-nho D: --giam-mb 20480             # Thực hiện (Admin), gõ lại chữ cái ổ để xác nhận
tsudisk ke-hoach-xoa E:           # Kế hoạch xóa ổ dữ liệu E:, in lệnh khôi phục, KHÔNG thay đổi gì
tsudisk xoa-phan-vung E:          # Thực hiện (Admin), gõ lại "E: <nhãn>" để xác nhận; ổ đang được dùng thì dừng
tsudisk ke-hoach-tao-phan-vung --dia 0 --vung 1 --nhan "Kho"  # Kế hoạch tạo + định dạng ổ mới trong vùng trống
tsudisk tao-phan-vung --dia 0 --vung 1 --nhan "Kho"           # Thực hiện (Admin), gõ TAO để xác nhận
tsudisk nhat-ky                   # Xem lần chạy gần nhất và việc cần làm nếu bị đứt giữa chừng
tsudisk suc-khoe                  # Sức khỏe ổ: trạng thái, nhiệt độ, độ mòn, lỗi đọc/ghi (S.M.A.R.T.)
tsudisk kiem-tra-gpt              # Kiểm tra toàn vẹn bảng GPT trên đĩa (Admin)
tsudisk kiem-tra-mbr2gpt          # Đĩa Windows MBR chuyển sang GPT được không (mbr2gpt /validate, chỉ đọc, Admin)
tsudisk sao-luu                   # Sao lưu sector GPT thô + bố cục + SHA-256 (Admin)
tsudisk xuat-du-lieu D:\sao-luu   # Xuất nhật ký, bản sao lưu GPT, cài đặt ra một tệp .zip kèm manifest SHA-256
tsudisk tai-khoan                 # Tài khoản TsuDEV và gói Pro (tùy chọn); dang-nhap / dang-xuat
```

Tùy chọn của `ke-hoach` / `mo-rong` / `doi-ten` / `thu-nho` / `tao-phan-vung` / `xoa-phan-vung`:

| Tùy chọn | Ý nghĩa |
| --- | --- |
| `--chay-thu` | Chỉ chạy bước không ghi đĩa: quét hệ thống tệp, sao lưu bảng phân vùng và BCD |
| `--dong-y` | Không hỏi xác nhận (kịch bản tự động); `xoa-phan-vung` không nhận - dùng `--xac-nhan "X: nhãn"` |
| `--recovery-mb N` | Kích thước Recovery mới, 512-4096 MB, mặc định 1024 (đủ chỗ cho các bản cập nhật WinRE) |
| `--recovery tao-lai-rong` | Cho phép xóa và tạo lại **rỗng** một Recovery không chứa WinRE đang dùng |
| `--chu-cai Y` | (`doi-ten`) chữ cái mới, C-Z, chưa được dùng ở đâu; không đổi được chữ cái ổ Windows |
| `--nhan TÊN` | (`doi-ten`) nhãn mới: NTFS/exFAT tối đa 32 ký tự, FAT32 tối đa 11 ký tự không dấu; `--nhan=` để xóa nhãn |
| `--giam-mb N` | (`thu-nho`) số MB nhường ra. Chỉ NTFS; ổ phải còn trống ít nhất min(2 GB, 25%), ổ Windows ít nhất 10 GB (ADR-0004) |
| `--vung K` | (`tao-phan-vung`) vùng trống thứ K trên đĩa (đĩa có nhiều vùng trống thì bắt buộc; kế hoạch bị từ chối sẽ liệt kê các vùng) |
| `--kich-thuoc-mb N` | (`tao-phan-vung`) kích thước ổ mới, mặc định chiếm hết vùng trống; tối thiểu 64 MB |
| `--he-tep NTFS\|exFAT\|FAT32` | (`tao-phan-vung`) mặc định NTFS; FAT32 tối đa 32 GB. Chữ cái mặc định là chữ trống đầu tiên từ D |
| `--chi-o-ao` | Chỉ thao tác trên ổ ảo VHD/VHDX (kiểm thử) |
| `--dia N` | (`danh-sach`, `phan-tich`) chỉ xem đĩa số N |
| `--thu-muc-nhat-ky DIR` | Nơi lưu nhật ký, mặc định `%ProgramData%\TsuDisk\nhat-ky` |
| `--json` | In kế hoạch hoặc bố cục dạng JSON để tích hợp |

Bí danh tiếng Anh: `list`, `analyze`, `plan`, `extend`, `plan-winre`, `grow-winre`, `plan-rename`, `rename`, `plan-shrink`, `shrink`, `plan-create`, `create`, `journal`, `health`, `verify-gpt`, `backup`, `export`, `--dry-run`, `--yes`, `--disk`, `--virtual-only`, `--letter`, `--label`, `--extent`, `--size-mb`, `--fs`, `--shrink-mb`.

### Mã thoát

| Mã | Nghĩa |
| --- | --- |
| `0` | Thành công (hoặc chạy thử xong) |
| `1` | Từ chối lập kế hoạch hoặc lỗi trước khi ghi đĩa |
| `2` | Sai cú pháp |
| `3` | Thiếu quyền quản trị |
| `4` | Thất bại giữa chừng, **đã hoàn tác** về trạng thái an toàn |
| `5` | Thất bại và hoàn tác không trọn vẹn - làm theo gợi ý của `tsudisk nhat-ky` |
| `6` | Cần cập nhật: đã có bản mới trên kênh cài đặt của bạn (chỉ khi đã đăng nhập; `phien-ban`, `tro-giup`, `dang-xuat`, `xuat-du-lieu` vẫn chạy) |

## Cơ chế an toàn

0. **Không ghi lên ổ đang hỏng.** Bước đầu của mọi kế hoạch đọc sức khỏe ổ (HealthStatus, trạng thái vận hành,
   S.M.A.R.T.); ổ có dấu hiệu hỏng (lỗi đọc/ghi không sửa được, Predictive Failure, mòn 100%) thì dừng. Bảng GPT trên
   đĩa cũng phải toàn vẹn (CRC32, header dự phòng) và khớp Windows, nếu không cũng dừng.
1. **Chỉ chạm đúng những gì cần.** Kế hoạch chỉ được xóa phân vùng Recovery, chỉ được mở rộng ổ dữ liệu (thu nhỏ
   CHỈ được phép trong `mo-rong-winre`: tối đa 4 GB, ổ phải còn đủ trống, Windows phải xác nhận phần đó dời được), không bao giờ chạm EFI, MSR, ổ động, Storage Spaces.
2. **Mô phỏng trước khi làm.** Mọi kế hoạch được chạy "trên giấy" ra bố cục dự kiến, rồi kiểm bất biến: mọi phân vùng
   khác giữ nguyên vị trí, kích thước và loại; không chồng lấn; offset căn lề 1 MiB. Vi phạm là hủy.
3. **Dấu vân tay bố cục.** Bố cục thật khác lúc lập kế hoạch (có công cụ khác vừa sửa đĩa) thì dừng trước bước đầu.
4. **Kiểm lại trước từng bước ghi.** Trước khi xóa, đọc lại phân vùng: đúng số, đúng offset, đúng loại Recovery,
   đúng kích thước. Sau khi mở rộng, đọc lại kích thước thật.
5. **Sao lưu trước khi đổi.** Bố cục phân vùng (JSON), sector GPT thô đầu và cuối đĩa (kèm SHA-256), BCD
   (`bcdedit /export`), trạng thái WinRE và một bản sao `Winre.wim` được lưu trong thư mục nhật ký của lần chạy.
6. **Hoàn tác tự động.** Lỗi giữa chừng thì chạy các bước bù theo thứ tự ngược: tạo lại Recovery đúng chỗ cũ, bật
   lại WinRE.
7. **Nhật ký nguyên tử.** Ghi `journal.json` sau mỗi chuyển trạng thái; mất điện giữa chừng thì `tsudisk nhat-ky`
   biết chính xác bước nào đã xong và chỉ ra việc cần làm.
8. **Đối chiếu sau cùng.** Đọc lại bảng phân vùng, so với bố cục dự kiến từng byte.
9. **Không nới quyền.** Công cụ hệ thống (`reagentc`, `bcdedit`, `diskpart`) gọi bằng đường dẫn tuyệt đối trong
   System32, tham số truyền tách rời, không qua shell.

TsuDisk **từ chối có giải thích** mọi bố cục cần dời dữ liệu (vùng trống không liền kề), ổ FAT32/exFAT, ổ động, đĩa
chỉ-đọc, và Recovery không chứa WinRE đang dùng (trừ khi bạn chủ động chọn `--recovery tao-lai-rong`).

## Kiểm thử

- **Test đơn vị** trên Linux/Windows: mô phỏng đĩa trong bộ nhớ, tiêm lỗi từng bước để kiểm hoàn tác, và một ca
  tái hiện đúng từng byte lần mở rộng ổ C: thật ngày 16/11/2026 (EFI | MSR | C: | Recovery | 60 GB trống | G: | E:).
- **E2E trên ổ ảo VHDX** (`tests/e2e/run-e2e.ps1`), chạy trong CI Windows và trên máy dev: dựng đĩa 3 GB có EFI,
  dữ liệu, Recovery, vùng trống; chạy `tsudisk` thật; kiểm phân vùng khác không xê dịch và tệp canh gác khớp SHA-256.
- **E2E lái giao diện** (`tests/e2e/run-e2e-gui.ps1`): mở `tsudisk-gui.exe --chi-o-ao` (chỉ thấy và chỉ ghi ổ ảo), bấm
  qua UI Automation để đổi chữ cái + nhãn, tạo phân vùng trong vùng trống rồi mở rộng ổ trên một VHDX, kiểm lời từ chối,
  hủy/gõ sai không đổi gì, gõ đúng thì làm đúng và dữ liệu nguyên vẹn.

```bash
dotnet test tests/TsuDisk.Core.Tests                         # mọi nền tảng
dotnet publish src/TsuDisk.Cli -c Release -o artifacts/publish   # ra tsudisk.exe
```

## Kiến trúc

Ports & Adapters: `TsuDisk.Core` (logic thuần, `net10.0`, test trên Linux) - `TsuDisk.Windows` (Storage Management
API, công cụ hệ thống) - `TsuDisk.Cli` (`tsudisk.exe`). Chi tiết: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
Lộ trình tính năng và các dự án tham khảo: [`docs/ROADMAP.md`](docs/ROADMAP.md).

Muốn tự làm bằng tay từng lệnh: [`docs/HUONG-DAN-MO-RONG-O-C.md`](docs/HUONG-DAN-MO-RONG-O-C.md).

## Đánh số phiên bản

`YY.M.DDNN` theo `.standards/docs/VERSIONING.md`: `26.11.1701` là bản thứ 01 ngày 17/11/2026.

## Giấy phép

Apache 2.0 - xem [`LICENSE`](LICENSE) và [`NOTICE`](NOTICE). Quyền riêng tư: [`PRIVACY.md`](PRIVACY.md).
Báo lỗ hổng: [`SECURITY.md`](SECURITY.md). Đóng góp: [`CONTRIBUTING.md`](CONTRIBUTING.md). Hồ sơ OpenSSF Best
Practices: [`docs/best-practices.json`](docs/best-practices.json).

## English summary

TsuDisk is a **safety-first Windows partition manager** (Apache-2.0): it analyses free space, previews a plan, and
extends a volume - including drive C: when the Recovery (WinRE) partition sits in the way - following Microsoft's
procedure (KB5028997) without moving any data of other partitions. It is a single `tsudisk.exe`, works fully offline,
has no account and sends no data anywhere.

- **Install**: download `tsudisk_<version>_windows-x64-portable.exe` from <https://disk.tsudev.com/tai-ve/> or the
  [Releases](https://github.com/tsudev-org/tsudisk/releases) page (with a Sigstore-signed `SHA256SUMS.txt`, SBOM and
  build provenance), or `scoop install tsudev/tsudisk`.
- **Use** (Vietnamese commands with English aliases, `tsudisk help`): `tsudisk list`, `tsudisk analyze`,
  `tsudisk plan C:` (prints every step, changes nothing), `tsudisk extend C: --dry-run`, `tsudisk extend C:` (asks you
  to retype the drive letter), `tsudisk verify-gpt`, `tsudisk journal`.
- **Safety**: refuses failing disks and inconsistent GPTs, simulates every plan and checks invariants, fingerprints
  the layout, re-reads before every write, backs up GPT/BCD/WinRE first, rolls back automatically, keeps an atomic
  journal, and calls system tools by absolute System32 path without a shell.
- **Develop**: `dotnet build TsuDisk.slnx -c Release` and `dotnet test tests/TsuDisk.Core.Tests`; end-to-end tests run
  on a virtual VHDX disk. See [CONTRIBUTING.md](CONTRIBUTING.md). Report vulnerabilities privately as described in
  [SECURITY.md](SECURITY.md); changes are listed in [CHANGELOG.md](CHANGELOG.md).
