# PHIẾU BÀN GIAO - phiên 6 đợt 2 (22/11/2026): đăng nhập TsuDEV, bố cục cong-cu, phát hành 26.11.2202

- **Mã phiếu**: 20261122-02 (nối tiếp 20261122-01)
- **Từ**: agent-tsudisk (phiên `tsudisk-59`) - **Đến**: phiên kế tiếp của `tsudisk`
- **Trạng thái**: ĐÓNG 23/11/2026 - việc còn lại chép sang phiếu 20261123-01

## 1. Đã xong (chủ dự án ủy quyền trực tiếp trong phiên)

| Việc | Kết quả |
| --- | --- |
| CLA Microsoft | Ký bằng tài khoản `nguyentrangtinhsu` (token winget, chỉ đọc từ tệp, đã `shred` tệp header tạm); nhãn `Needs-CLA` gỡ |
| Client OIDC `tsudisk` | INSERT vào CSDL production (lấy `DATABASE_URL` qua Render API, tệp tạm đã `shred`); đo: 303 interaction, redirect lạ 400. Phiếu tsudev 20261122-DK1 ĐÓNG (tệp nằm trong `~/projects/tsudev/logs/handover/`, chưa commit ở repo tsudev) |
| #52 đăng nhập | `tai-khoan`/`dang-nhap`/`dang-xuat` (port tsuswico: PKCE loopback, DPAPI, mã cài đặt 128 bit, trình duyệt quyền thường), `ProHook` kiểm `IsSignedIn` trước mọi lượt mạng; PRIVACY/README cập nhật; `docs/THREAT_MODEL.md`; test 204 -> 285 |
| #53 STD50 PR 2 | Bàn thao tác 3 bước ngay màn đầu, hoạ tiết `khoi-phan-vung` ở hero + og-image (`brand.json` `ogMotif`). Phiếu STD50 ĐÓNG |
| #54 phát hành 26.11.2202 | VirusTotal 0/74; `sha256sum -c`, attestation, Sigstore OK; exe tải về chạy `phien-ban` đúng |
| winget | Job mở **#486490** (2202). Đã đóng #486465 (2201) kèm lời giải thích để người duyệt chỉ xét một PR New-Package |
| Website | `deploy-2026.11.22.2`: đo 200 trang chủ/tải về/quyền riêng tư/og-image, có bàn thao tác + 26.11.2202 + mục "Khi bạn đăng nhập" |

## 2. Việc cần chủ dự án (👤)

1. Thử đăng nhập thật một lần: `tsudisk dang-nhap` (Windows, PowerShell thường) -> đăng nhập tsudev.com trong trình
   duyệt -> `tsudisk tai-khoan` phải in gói + số thiết bị; `tsudisk dang-xuat` xoá `%LOCALAPPDATA%\tsudev\tsudisk`.
   Ghi kết quả vào `docs/THREAT_MODEL.md` mục 4 (ô cuối).
2. Duyệt (hoặc bác) ADR-0006 MBR -> GPT và phạm vi đồng bộ Pro (ADR-0007 "Việc còn chờ").
3. Thu hồi token đã cấp (winget, Render, Cloudflare...) khi xong đợt.

## 3. Việc tiếp theo cho agent

1. Theo dõi winget-pkgs #486490 (nhãn `Validation-*`, yêu cầu người duyệt); merge => khối "Cài bằng lệnh" theo
   PACKAGING 8.1. Bản phát hành sau: job winget mở PR cập nhật (không còn New-Package).
2. Khi PRO-D3 (.NET `TsuSync`) có ở tsusync và chủ dự án duyệt phạm vi: đồng bộ theo PRO2 mục 2.2.
3. Giai đoạn 2: WPF bản đầu chỉ đọc (gộp `products/disk.json` vào token lúc build - DESIGN_SYSTEM 1.2.1); ADR-0006 khi
   được duyệt (kiểm Hyper-V Gen 1 -> Gen 2 trước).

## 4. Cảnh báo

- `ProHook` nuốt mọi ngoại lệ (CA1031 tắt có chủ đích): Pro không bao giờ được làm hỏng lệnh đĩa.
- Backend tsudev.com (Render free) ngủ: lần gọi đầu có thể 503 - không phải lỗi client.
- `DocumentationConsistencyTests` lấy khối ví dụ trang chủ theo chuỗi `tsudisk ke-hoach C:` đầu tiên - bàn thao tác
  dùng `ke-hoach X:` để không đụng; đổi trang chủ thì giữ điều này.
