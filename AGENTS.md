# AGENTS.md - tsudisk

> **ĐỌC FILE NÀY ĐẦU TIÊN trong mọi phiên làm việc mới.**

## Phần A - Quy ước chung của hệ sinh thái (KHÔNG SỬA Ở ĐÂY)

Toàn bộ quy ước chung nằm trong bản sao chỉ-đọc tại
[`.standards/AGENTS.md`](.standards/AGENTS.md). `MUST` đọc file đó trước.

Bản quy ước repo này đang dùng: xem [`.standards-version`](.standards-version).

| Cần gì | Đọc file nào |
| --- | --- |
| Điểm vào, nguyên tắc chung | `.standards/AGENTS.md` |
| Quy trình phiên, khóa file, bàn giao | `.standards/docs/AGENT_PROTOCOL.md` |
| Bảo mật bắt buộc | `.standards/docs/SECURITY_BASELINE.md` |
| Quy tắc `.gitignore` | `.standards/docs/GITIGNORE_POLICY.md` |
| Nhánh, commit, PR, phát hành | `.standards/docs/GIT_WORKFLOW.md` |
| Đăng nhập, đăng ký, xác minh tài khoản | `.standards/docs/AUTH_AND_ACCOUNT.md` |
| Giao diện và token | `.standards/docs/DESIGN_SYSTEM.md` |
| Bảng bản ghi, bộ chọn số bản ghi | `.standards/docs/DATA_TABLE.md` |
| Logo, favicon, icon ứng dụng | `.standards/docs/BRAND_ASSETS.md` |
| tsudev.com, ảnh đại diện, trang hồ sơ | `.standards/docs/ECOSYSTEM_IDENTITY.md` |
| Cấu trúc thư mục | `.standards/docs/PROJECT_STRUCTURE.md` |
| Chọn ngôn ngữ, framework | `.standards/docs/LANGUAGE_SELECTION.md` |
| Hạ tầng 0 đồng | `.standards/docs/FREE_TIER_STACK.md` |
| Trình soạn thảo nội dung | `.standards/docs/RICH_TEXT_EDITOR.md` |
| Tìm kiếm và lọc tiếng Việt | `.standards/docs/SEARCH_AND_FILTER.md` |
| Kiểm thử và chất lượng mã | `.standards/docs/TESTING_QUALITY.md` |
| Khả năng truy cập | `.standards/docs/ACCESSIBILITY.md` |

`MUST NOT` sửa bất kỳ file nào trong `.standards/`. Cần đổi quy ước thì mở đề
xuất tại repo `tsudev-standards` theo `.standards/docs/SYNC.md` mục 1.

## Phần B - Riêng của repo này

> Phần này KHÔNG thuộc bộ quy ước chung.

### B.1. Repo này là gì

- **Loại**: phần mềm desktop (CLI, giai đoạn 2 thêm GUI WPF) + website sản phẩm `disk.tsudev.com`
- **Stack**: C# / .NET 10 (Ports & Adapters), website Astro - chi tiết `docs/ARCHITECTURE.md`
- **Hạng sản phẩm**: C - Ngoại tuyến
- **Mức phân loại dữ liệu cao nhất**: D1
- **Hình trạng**: A (ứng dụng đơn); `src/` chia theo project biên dịch .NET (lý do: `docs/ARCHITECTURE.md` mục 2)
- **Người liên hệ khi có sự cố**: chủ project tsudev
- **Agent phụ trách**: `agent-tsudisk` - trao đổi 100% tiếng Việt

### B.2. Chạy trong 5 phút

```bash
dotnet test tests/TsuDisk.Core.Tests                            # test đơn vị, chạy được trên Linux
dotnet publish src/TsuDisk.Cli -c Release -o artifacts/publish  # ra tsudisk.exe (win-x64, một tệp)
dotnet publish src/TsuDisk.Gui -c Release -o artifacts/gui      # tsudisk-gui.exe (WPF, ADR-0008/0009)
#   tsudisk-gui.exe --du-lieu-mau --chup-man-hinh <thư-mục>     # ảnh vi/en x Sáng/Tối, chạy được trên máy dev
# E2E trên ổ ảo VHDX (Windows, PowerShell Admin):
#   powershell -ExecutionPolicy Bypass -File tests\e2e\run-e2e.ps1 -Exe artifacts\publish\tsudisk.exe
cd website && npm ci && npm run build                           # website
```

### B.3. Ranh giới sở hữu file

| Vùng | Chủ sở hữu |
| --- | --- |
| `src/TsuDisk.Core/` | agent-tsudisk (logic + bất biến an toàn - mọi thay đổi `MUST` kèm test) |
| `src/TsuDisk.Windows/`, `src/TsuDisk.Cli/` | agent-tsudisk |
| `website/` | agent-tsudisk |
| `.standards/` | Chỉ-đọc, đồng bộ từ `tsudev-org/tsudev-standards` |

### B.4. Điểm lệch chuẩn đã biết

| Lệch ở đâu | Vì sao | Kế hoạch |
| --- | --- | --- |
| `src/` không theo cây `components/features/...` | Giải pháp .NET chia theo project biên dịch | Giữ, đã ghi `docs/ARCHITECTURE.md` |
| Chưa ký số `tsudisk.exe` | Chờ SignPath Foundation (D5 của hệ sinh thái) | Thêm bước ký khi D5 duyệt |

### B.5. Việc riêng của repo cần biết trước khi sửa

- **Không bao giờ chạy thử thao tác ghi trên đĩa thật của máy dev.** Dùng `tests/e2e/run-e2e.ps1` (ổ ảo VHDX,
  `--chi-o-ao`). Lệnh chỉ đọc (`danh-sach`, `phan-tich`, `ke-hoach`) chạy thoải mái.
- Thêm thao tác mới: `StepKind` + nhánh `LayoutSimulator` + bất biến `SafetyGuard` + test tiêm lỗi (ADR-0002).
- Không bật `PublishTrimmed`/NativeAOT (`System.Management`).
- Website lấy nội dung từ `README.md`, `PRIVACY.md`, `CHANGELOG.md`, `docs/HUONG-DAN-MO-RONG-O-C.md` lúc build: sửa
  ở nguồn, không sửa trong `website/`.
