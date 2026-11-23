# PHIẾU BÀN GIAO - phiên 8 (23/11/2026): phát hành GUI 26.11.2301, GUI áp dụng đổi tên

- **Mã phiếu**: 20261123-02 (nối tiếp 20261123-01 - phiếu đó ĐÓNG, việc còn lại chép sang đây)
- **Từ**: agent-tsudisk (phiên `tsudisk-78`) - **Đến**: phiên kế tiếp của `tsudisk` + 👤 chủ dự án (mục 2)
- **Trạng thái**: ĐÓNG 23/11/2026 - việc còn lại chép sang phiếu 20261123-03

## 1. Đã xong

| Việc | Kết quả |
| --- | --- |
| Duyệt GUI | Chủ dự án duyệt ảnh GUI chỉ đọc + duyệt "GUI bước 2" (đổi tên) trong phiên này |
| #62 phát hành GUI | `release.yml`: build + kiểm thông tin exe + chụp thử GUI, `tsudisk-gui_{ver}_windows-x64-portable.exe`, attestation, VirusTotal `tsudisk_*.exe tsudisk-gui_*.exe`, SBOM khôi phục thêm GUI. Website `/tai-ve/`: gói "Giao diện cửa sổ" + ảnh Sáng/Tối (Astro Picture từ `docs/ui`), sửa mẫu SBOM (SPDX). README, CHANGELOG, ADR-0008 |
| `v26.11.2301-beta.1` | Thử release.yml mới: xanh; VT 0/73 cả hai exe; SHA-256 + attestation GUI đã kiểm. Pre-release, giữ lại |
| `v26.11.2301` | Gắn ở commit #62 (`a4a157f`, đúng nội dung đã thử beta - GUI chỉ đọc). build/publish/virustotal/cong-bo xanh, Release ĐÃ XUẤT BẢN. Job `winget` ĐỎ 401 (mục 2.1) |
| #63 GUI đổi tên (ADR-0009) | `PlanExecution.Run` (Core) dùng chung CLI/GUI; `RenameForm`; GUI: ô chữ cái + nhãn, áp dụng khi quản trị, xác nhận hai lần, "Mở lại với quyền quản trị", khoá khi đang ghi, kết quả từng bước. Test 328 -> 348. CI 11/11. Đã MERGE nhưng CHƯA phát hành (CHANGELOG "Chưa phát hành") |
| Website | `npm run deploy` từ `bf48f56`, thẻ `deploy-2026.11.23.1`; `/tai-ve/` đo 200, có 26.11.2301 + gói GUI |
| Scoop | scoop-bucket#30 MERGED: `tsudisk` 26.11.1801 -> 26.11.2301, thêm `tsudisk-gui.exe` (`bin` + lối tắt Start Menu "TsuDisk"), autoupdate cả hai tệp. CI "Cài thử thật mọi gói" xanh |
| winget-pkgs#486490 | Vẫn chờ moderator (không đổi từ 22/11) |
| AKB | `ISS-20261123-1929-67f8` (winget 401), `LES-20261123-1929-5bbe` (Excavator), `DEC-20261123-1929-eb95` (ADR-0009) |

## 2. Việc cần chủ dự án (👤)

1. ~~**Nạp lại `WINGET_TOKEN`**~~ - XONG 23/11/2026 12:35 UTC (chủ dự án chạy); agent rerun job winget -> xanh,
   microsoft/winget-pkgs#487170 (26.11.2301). Ghi chép cũ: Tệp `~/.config/tsudev/winget.env` có token
   HỢP LỆ (đã kiểm `gh api user`), nhưng được ghi lúc 11:46 UTC - SAU lần nạp secret 10:56 UTC, nên secret đang giữ
   token đã thu hồi:
   ```bash
   gh secret set WINGET_TOKEN -R tsudev-org/tsudisk --env packages < ~/.config/tsudev/winget.env
   gh run rerun 45172698369 -R tsudev-org/tsudisk --failed
   ```
2. ~~Thử GUI đổi tên thật~~ - chủ dự án ủy quyền agent: chạy E2E GUI trên Windows 11 máy dev với quyền quản trị (ổ
   ảo VHDX - AGENTS B.5 cấm ghi ổ thật), 17/17; đã phát hành 26.11.2302. Ghi chép cũ: Thử GUI đổi tên thật (cần quyền quản trị) trên một ổ dữ liệu không quan trọng/ổ USB: chọn ổ -> đổi chữ cái/nhãn ->
   "Lập kế hoạch đổi" -> "Áp dụng" -> gõ lại chữ cái. Bản build: artifact CI của #63 hoặc
   `dotnet publish src/TsuDisk.Gui -c Release -o artifacts/gui`. Ổn thì phát hành bản kế tiếp.
3. Còn từ phiếu trước: thử `tsudisk dang-nhap` thật; duyệt/bác ADR-0006 và phạm vi đồng bộ Pro.

## 3. Việc tiếp theo cho agent

1. Theo dõi winget-pkgs #486490 (2202), #487170 (2301), #487205 (2302); merge => khối "Cài bằng lệnh".
2. ~~E2E lái GUI~~ (#66) + ~~phát hành GUI đổi tên~~ - 26.11.2302 (#68), website deploy-2026.11.23.2, scoop-bucket#31.
   Tiếp: thao tác ghi khác cho GUI (STATE).
3. Đồng bộ Pro: vẫn chờ tsusync có máy khách HTTP .NET (23/11: `clients/dotnet` mới có lớp mã hoá) + phạm vi duyệt.
4. Scoop: Excavator của scoop-bucket vẫn đỏ do `tsudev-contact` (hash 404) - việc của phiên tsucontact; bản tsudisk
   sau có thể phải cập nhật tay lần nữa nếu chưa sửa.

## 4. Cảnh báo

- E2E GUI chọn điều khiển theo `x:Name` (AutomationId): đổi tên `Map`, `DiskBox`, `RenameLetterBox`, `RenameLabelBox`,
  `PlanRenameButton`, `ApplyRenameButton`, `RunResultText`, `ConfirmWindow`/`Typed`/`ApplyButton`/`CancelButton` thì sửa
  `tests/e2e/run-e2e-gui.ps1`. Máy dev: Windows PowerShell 5.1 gọi từ WSL lỗi nạp module (`Add-Type`); thử UIA cục bộ
  bằng `pwsh.exe` + `--du-lieu-mau` (không cần quản trị, không có đường ghi).

- `v26.11.2301` KHÔNG chứa #63 (thẻ ở `a4a157f`). Bản kế tiếp phải có mục CHANGELOG cho tính năng đổi tên GUI.
- Kiểm tệp token: chỉ `wc -c` / `gh api user` với `GH_TOKEN="$(< tệp)"`. Phiên này lỡ dùng `awk substr` + `tail -c1|od`
  in ra tiền tố `ghp_` (công khai theo định dạng) và 1 ký tự cuối - không đủ để dùng token, không cần thay; đừng lặp lại.
- Website build lấy ảnh GUI từ `docs/ui/gui-chi-doc/*_vi_{sang,toi}.png`: đổi giao diện thì chụp lại ảnh (artifact CI
  `tsudisk-gui-anh`) để trang Tải về khớp. Id `giao-dien` đã bị bộ chọn giao diện của trang dùng (check-dist bắt trùng).
