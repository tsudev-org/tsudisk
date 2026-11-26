# Phiếu bàn giao - phiên 14: Phase INT4 (GUI modal khoá + Xuất dữ liệu)

- **Ngày**: 26/11/2026 - **Agent**: agent-tsudisk (Claude Code) - **Nhánh**: `feat/int4-gui-modal-khoa`

## 1. Đã làm

- Core `Integrity/GuiGateSchedule.cs`: lịch kiểm GUI (6 giờ, quay lại sau >= 30 phút ở nền), không kiểm chồng, khoá một
  chiều trong phiên, đang ghi đĩa thì giữ quyết định tới `Idle()`. `UpdateAction.For(kênh, url)` (GitHub chỉ mở https,
  không thì trang Release).
- Core `Integrity/ServerLock.cs` (`ServerLockStore`): `ProAccount.UpdateRequired` (mới) ghi `versions/server-lock.json`
  khi `/api/me/entitlements` trả `426`; gắn đúng phiên bản đang chạy, hết hạn 24 giờ; `VersionGate(serverLock:)` đọc
  trước khi tải bản kê. `RefreshStatus.UpdateRequired` (giữ phiên + JWS đệm). CLI: `ProHook` ghi dấu,
  `CliGate.CheckServerLock` chạy ngay sau `ProHook.Run` => lệnh bị khoá luôn, mã 6.
- `DataExport.StandardSources/DefaultLogRoot/GuiSettingsPath`: nguồn xuất dùng chung CLI + GUI.
- GUI: `GuiGate.cs` (chỉ khi có tệp phiên; HttpClient không theo chuyển hướng 5 giây; nuốt mọi lỗi),
  `UpdateRequiredWindow.xaml(.cs)` (WindowStyle None + Maximized, Esc/Alt+F4/Closing bị chặn, TabNavigation Cycle,
  Activate + focus nút chính, tương phản cao = màu hệ thống đè token ở cửa sổ). Cập nhật ngay = mở url / sao chép lệnh
  rồi ĐÓNG app (ADR-0012 "Ghi chú triển khai"). Xuất dữ liệu = `OpenFolderDialog` + `DataExport`.
- `--chup-man-hinh` chụp thêm `tsudisk-gui-khoa_{vi,en}_{sang,toi}.png` (CI kiểm đủ 4). `--du-lieu-mau --xem-khoa-mau`
  mở sẵn modal mẫu (E2E G11). Test `GuiGateTests` khoá danh sách cờ của App (thêm cờ => phải xem lại).
- E2E `run-e2e-gui.ps1` ca G11: tên UIA = tiêu đề, đúng hai nút, Maximized, IsModal, focus, Esc, Alt+F4, WM_CLOSE, Tab.

## 2. Đã kiểm

- `dotnet build TsuDisk.slnx -c Release` sạch; test 572/572.
- 4 ảnh modal đã xem (Sáng/Tối, vi/en).
- UIA trên máy dev (chế độ mẫu, chỉ đọc): tên, hai nút, Maximized, IsModal, focus, Esc, Tab, WM_CLOSE đạt. Alt+F4
  KHÔNG thử trên máy dev (SendKeys có thể đóng nhầm cửa sổ khác) - để CI.

## 3. Lưu ý

- `powershell.exe` gọi từ WSL trên máy dev hiện không nạp được `Microsoft.PowerShell.Utility` (Add-Type, New-Object,
  Start-Sleep lỗi CouldNotAutoloadMatchingModule). Kịch bản thử tạm dùng `[Type]::new()`, `[Threading.Thread]::Sleep`.
- Đường khoá thật (đã đăng nhập + bản kê có bản mới hoặc 426) vẫn chưa thử với tài khoản thật (INT7).
