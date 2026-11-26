# Phiếu bàn giao - phiên 13: Phase INT3 (CLI cổng phiên bản + `xuat-du-lieu`)

- **Ngày**: 26/11/2026 - **Agent**: agent-tsudisk (Claude Code) - **Nhánh**: `feat/int3-cli-cong-phien-ban`

## 1. Đã làm

- `src/TsuDisk.Core/Integrity/CommandGate.cs`: `AllowedWhenLocked` (4 lệnh Q2), `KnownCommands` (sổ đăng ký mọi lệnh),
  `EvaluateAsync` (lệnh được phép => không đọc gì; chưa đăng nhập => KHÔNG gọi `createGate`), `MustBlock` (mặc định
  chặn, chỉ `UpdateRequired` mới chặn), `UpdateHint` theo kênh, `LockedLines` (nguyên văn APP_INTEGRITY 2.2 vi),
  `LockedJson` (`{"error":"update_required",...}`).
- `src/TsuDisk.Cli/CliGate.cs`: nối cổng (HttpClient không theo chuyển hướng, timeout 5 giây, đệm
  `%LOCALAPPDATA%\tsudev\tsudisk\versions`). `Program.cs` gọi `CliGate.Check` TRƯỚC `ProHook.Run` và trước khi chọn lệnh.
  `ExitCodes.UpdateRequired = 6`. `CliArgs` ném lỗi nếu lệnh chưa có trong `KnownCommands`.
- Lệnh `xuat-du-lieu <thư mục> [--thu-muc-nhat-ky DIR] [--json]` (bí danh `export`): nguồn = thư mục nhật ký (gồm bản
  sao lưu GPT) + `%LOCALAPPDATA%\TsuDisk\giao-dien.json`; KHÔNG đưa thư mục trạng thái Pro.
- Test `CommandGateTests` (bảng mọi lệnh x khoá; chưa đăng nhập 0 lượt mạng/0 tệp; đối chiếu văn bản `CliArgs.cs` và
  `Program.cs`). 477 -> 543.
- E2E `run-e2e.ps1` ca 15: xuất lên ổ ảo, giải nén, `manifest.json` đúng định dạng, SHA-256 khớp zip và tệp gốc, không
  có `*.dpapi`, thiếu đối số => exit 2.
- PRIVACY.md (dòng GET bản kê), README (lệnh + mã thoát 6), CHANGELOG mục "Chưa phát hành".

## 2. Chưa làm / lưu ý cho INT4

- `426` từ `ProHttp` (`EntitlementFetch.UpdateRequired`) chưa được `ProAccount` đưa ra ngoài => CLI/GUI chưa khoá theo
  `426` (ADR-0012 mục 8). Đề xuất: `RefreshResult` thêm `UpdateRequired`, `CliGate` đọc cờ đệm lần trước.
- Đường khoá thật (đã đăng nhập + bản kê có bản mới) chưa thử trên Windows: CI không có tài khoản; production
  `products: {}` nên hiện không ai bị khoá.
- Hook Zero Secret Exposure chặn nhầm `sed` có chữ `CancellationToken.None` trong biểu thức - dùng Edit thay thế.
