# ADR-0008: Giao diện WPF - bản đầu chỉ đọc

- **Trạng thái**: Chấp nhận - 23/11/2026
- **Liên quan**: `docs/ROADMAP.md` giai đoạn 2; ADR-0001 (WPF), ADR-0002 (mọi thao tác là một `Plan`); bộ quy ước
  `DESIGN_SYSTEM.md` mục 1.2.1, 5, 7; `PRODUCT_POLICY.md` mục 1; `DATA_TABLE.md`; `ECOSYSTEM_IDENTITY.md` mục 2.3.
  Phiếu bàn giao 20261118-02 mục 3.4 (đề xuất bản đầu chỉ đọc).

## Bối cảnh

TsuDisk mới có CLI. Người dùng phổ thông cần thấy bản đồ đĩa kiểu Disk Management trước khi tin một công cụ đụng vào
phân vùng. Một GUI có nút "Áp dụng" là bề mặt rủi ro mới (bấm nhầm, trạng thái giao diện lệch bố cục thật). Bộ kiểm
thử E2E hiện chỉ lái được CLI.

## Quyết định

1. **Bản đầu CHỈ ĐỌC.** `tsudisk-gui.exe` đọc bố cục (cùng `WindowsDiskBackend.ListDisks/ReadLayout` như
   `danh-sach`), chạy `LayoutAnalyzer` và `ExtendPlanner` trên mô hình. **Không có nút áp dụng, không tham chiếu tới
   `PlanRunner`.** Kế hoạch hiện kèm lệnh CLI tương ứng (`tsudisk mo-rong X:`) và nút sao chép.
2. **Project riêng `src/TsuDisk.Gui`** (`net10.0-windows`, WPF, `asInvoker`), tên tệp `tsudisk-gui.exe`: không trùng
   `tsudisk.exe` khi đặt chung thư mục (Windows không phân biệt hoa thường).
3. **Logic hiển thị test được nằm trong Core** (`TsuDisk.Core/Presentation`): gộp token, chọn ngôn ngữ/giao diện, cửa
   sổ phân trang, tỉ lệ khối bản đồ, chữ mô tả kế hoạch (dùng chung với CLI). WPF chỉ gắn dữ liệu.
4. **Token đọc lúc build**: nhúng `.standards/tokens/design-tokens.json` + `products/disk.json`, gộp đè vai trò nhấn,
   bộ bo góc theo `shape` (DESIGN_SYSTEM mục 7). Không mã màu nào viết trong XAML.
5. **Ngôn ngữ và giao diện theo hệ thống** (PRODUCT_POLICY mục 1): chuỗi trong `Resources/Strings.resx` (en, mặc định
   khi ngôn ngữ hệ thống chưa hỗ trợ) + `Strings.vi.resx`; giao diện "Theo hệ thống" đọc `AppsUseLightTheme` và đổi
   ngay khi Windows đổi. Lựa chọn của người dùng (ngôn ngữ, giao diện, số bản ghi mỗi bảng theo khóa
   `tsudev.pagesize.<mã-bảng>`) lưu ở `%LOCALAPPDATA%\TsuDisk\giao-dien.json`, KHÔNG ở `%LOCALAPPDATA%\tsudev\tsudisk`
   (thư mục đó bị `dang-xuat` xoá).
6. **Chế độ chụp** `--chup-man-hinh <thư-mục> [--du-lieu-mau]`: vẽ vi/en × Sáng/Tối ra PNG rồi thoát; CI Windows chạy
   cả dữ liệu mẫu lẫn ổ thật của runner, nên GUI được kiểm trên Windows thật ở mọi PR.
7. **Chưa phát hành** `tsudisk-gui.exe` ở bản này: CI build + chụp + tải artifact. Đưa vào Release (VirusTotal,
   SHA256SUMS, attestation, Scoop/winget) ở PR riêng sau khi chủ dự án xem giao diện.
   - *Cập nhật 23/11/2026*: chủ dự án đã duyệt ảnh; từ 26.11.2301 Release có
     `tsudisk-gui_{ver}_windows-x64-portable.exe` (VirusTotal, SHA256SUMS, attestation). Scoop thêm `bin`/`shortcuts`
     ở kho `scoop-bucket`. winget: gói `tsudev.TsuDisk` vẫn chỉ có CLI (gói đầu đang chờ duyệt) - xét sau khi merge.

## Hệ quả

- (+) Thấy được bố cục, vùng trống và kế hoạch mà không có đường nào dẫn tới thao tác ghi.
- (+) Một nguồn chữ cho kế hoạch (`PlanText`), một nguồn token; test trên Linux: 285 -> 328.
- (-) Lời phân tích, tiêu đề bước, lời từ chối do Core sinh vẫn chỉ có tiếng Việt; giao diện tiếng Anh ghi chú điều
  này. Dịch Core là việc của giai đoạn 3 ("Giao diện tiếng Anh").
- (-) Thanh cuộn, menu chuột phải dùng kiểu mặc định của Windows (chưa theo token ở chế độ Tối).
- (-) Font tiêu đề Montserrat chỉ dùng được khi máy đã cài (repo chỉ có `.woff2` cho web, WPF cần TTF/OTF); thiếu thì
  về Inter/Segoe UI theo danh sách token.
- Danh sách "Phân tích vùng trống" không có bộ chọn số bản ghi: mỗi mục ứng với một vùng trống ≥ 1 MiB giữa các phân
  vùng, trên thực tế vài mục; bảng phân vùng thì có đủ chân vùng bản ghi.

## Việc tiếp theo

1. ~~Phát hành `tsudisk-gui.exe` cùng CLI (mục 7)~~ - 26.11.2301.
2. Nối `PlanRunner` cho thao tác rủi ro Thấp trước (`doi-ten`), xác nhận hai lần, cần ADR bổ sung + E2E lái GUI.
3. Màn Pro/Ủng hộ + huy hiệu, hộp thoại nhắc gia hạn (PRO2 mục 2.1 "GUI dùng hộp thoại").
