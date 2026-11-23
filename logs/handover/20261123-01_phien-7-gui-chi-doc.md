# PHIẾU BÀN GIAO - phiên 7 (23/11/2026): GUI WPF bản đầu chỉ đọc

- **Mã phiếu**: 20261123-01 (nối tiếp 20261122-02 - phiếu đó ĐÓNG, việc còn lại chép sang đây)
- **Từ**: agent-tsudisk (phiên `tsudisk-64`) - **Đến**: phiên kế tiếp của `tsudisk` + 👤 chủ dự án (mục 2)
- **Trạng thái**: ĐÓNG 23/11/2026 - việc còn lại chép sang phiếu 20261123-02

## 1. Đã xong

| Việc | Kết quả |
| --- | --- |
| winget-pkgs#486490 | Validation 10/10 xanh (`Validation-Completed`, `Azure-Pipeline-Passed`); bot: chờ moderator cộng đồng duyệt. Không có gì phải sửa |
| Đăng nhập thật (THREAT_MODEL mục 4) | Chủ dự án CHƯA thử (ô cuối chưa tích, không có `%LOCALAPPDATA%\tsudev\tsudisk`) - giữ nguyên |
| TsuSync .NET (PRO-D3) | Có ở `tsudev-org/tsusync` `clients/dotnet` (`Tsudev.Sync`) nhưng CHỈ lớp mã hoá (libsodium, vector), CHƯA có máy khách HTTP (`/v1/...`, JWS, 401 stale, 402). Phạm vi đồng bộ chưa duyệt => KHÔNG làm |
| #57 GUI chỉ đọc | `src/TsuDisk.Gui` -> `tsudisk-gui.exe` (ADR-0008). Core/Presentation (token, ngôn ngữ/giao diện, phân trang, bản đồ, `PlanText` dùng chung CLI), `Units.FormatIn`. Test 285 -> 328. Chạy thật trên Windows 11 máy dev (mẫu + ổ thật, chỉ đọc). Ảnh `docs/ui/gui-chi-doc/`. CI Windows chụp vi/en × Sáng/Tối, artifact `tsudisk-gui-anh` |
| AKB | Bài học `LES-20261123-0315-3845` (WPF trên Linux, ảnh trọn trang, bẫy ThemeMode/CA1305/WinExe) |

## 2. Việc cần chủ dự án (👤)

1. ~~Nạp lại `WINGET_TOKEN`~~ - XONG 23/11/2026 10:56 UTC: chủ dự án đã chạy `gh secret set WINGET_TOKEN ... --env
   packages < ~/.config/tsudev/winget.env` (token mới sau khi token cũ bị thu hồi). Bản phát hành tới: nếu job winget
   vẫn 401 thì kiểm lại nội dung tệp.
2. Xem ảnh GUI `docs/ui/gui-chi-doc/` (hoặc chạy `tsudisk-gui.exe` từ artifact CI) -> đồng ý thì phiên sau đưa GUI vào
   bản phát hành.
3. Còn từ phiếu 20261122-02: thử `tsudisk dang-nhap` thật; duyệt/bác ADR-0006 và phạm vi đồng bộ Pro.

## 3. Việc tiếp theo cho agent

1. winget-pkgs#486490: theo dõi moderator; merge => khối "Cài bằng lệnh" (PACKAGING 8.1, sinh từ danh mục nếu có).
2. Khi chủ dự án duyệt giao diện: thêm `tsudisk-gui.exe` vào `release.yml` (tên tệp theo VERSIONING, ví dụ
   `tsudisk-gui_{ver}_windows-x64-portable.exe`; VirusTotal quét `tsudisk*.exe`, SHA256SUMS, attestation), gói Scoop
   (`bin` thêm GUI + `shortcuts`), website trang Tải về + ảnh GUI.
3. GUI bước 2: nối `PlanRunner` cho `doi-ten` (rủi ro Thấp) - ADR bổ sung, xác nhận hai lần, cần quyền quản trị.
4. Đồng bộ Pro: chỉ khi tsusync có máy khách HTTP .NET + chủ dự án duyệt phạm vi (PRO2 mục 2.2).

## 4. Cảnh báo

- `ResxParityTests.KeysUsedInGui_Exist` quét `{g:T X}` và `l["X"]`/`Loc.Instance.Format("X"` trong mã GUI - ví dụ trong
  chú thích cũng bị tính (đã gặp).
- Chế độ chụp không đọc/ghi tệp cài đặt; lỗi ghi ra `<thư-mục>/loi.txt` (WinExe không có console).
- Cảnh báo của phiếu 20261122-02 mục 4 vẫn đúng (ProHook nuốt lỗi; backend Render ngủ; khối ví dụ trang chủ).

## 5. Kết quả CI / merge

- #57 MERGED 23/11/2026 (squash). CI lần 1 đỏ ở cổng kiểm mục 7.1 (ký tự gạch dài trong `ResxParityTests`), sửa bằng
  `(char)0x2014`; lần 2 xanh 11/11. Artifact `tsudisk-gui-anh`: 8 ảnh (mẫu + ổ thật runner, runner có quyền quản trị).
- Không gắn thẻ, không deploy website: GUI chưa vào bản phát hành (ADR-0008 mục 7); CHANGELOG ghi ở "Chưa phát hành".
