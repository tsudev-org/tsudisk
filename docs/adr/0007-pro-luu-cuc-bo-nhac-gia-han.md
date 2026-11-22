# ADR-0007: TsuDEV Pro trong TsuDisk - lưu cục bộ, nhắc gia hạn, chưa có đồng bộ

- **Trạng thái**: Chấp nhận - 22/11/2026 (mục 1-6 đã làm; đồng bộ chờ - mục "Việc còn chờ")
- **Liên quan**: phiếu `logs/handover/20261119-PRO1_*`, `20261120-PRO2_*`; bộ quy ước `PRODUCT_POLICY.md` v1.2.0 mục
  3.3, 7.1-7.6, 8; `AUTH_AND_ACCOUNT.md` mục 14.3. Mẫu cài đặt: tsuswico ADR-0002 (cùng nền .NET, cùng hợp đồng).

## Bối cảnh

Từ 19/11/2026 mọi sản phẩm TsuDEV lưu cục bộ mặc định; đồng bộ/sao lưu đám mây là quyền lợi **TsuDEV Pro** (kỳ 1 năm,
không tự gia hạn, 20/11/2026). TsuDisk là công cụ hạng C (ngoại tuyến, không tài khoản), dữ liệu cao nhất D1.

**Kiểm kê dữ liệu rời máy (PRO1 bước 1, đo trên mã 22/11/2026):** TsuDisk KHÔNG có mã mạng nào - không `HttpClient`,
không socket, không kiểm cập nhật, không đo đếm. Tiến trình con duy nhất là công cụ của Windows (`mbr2gpt`, `reagentc`,
`chkdsk`...) chạy cục bộ. Dữ liệu TsuDisk tạo ra (kế hoạch, nhật ký, bản sao lưu GPT) nằm ở
`%ProgramData%\TsuDisk\nhat-ky` và chỉ ở đó. ⇒ Bản miễn phí đã đạt N2 ("0 byte dữ liệu người dùng rời máy"); không có
đường đồng bộ miễn phí nào phải tắt.

## Quyết định

1. **Bản miễn phí giữ nguyên đủ tính năng.** Mọi thao tác đĩa, kế hoạch, nhật ký, hoàn tác, sao lưu GPT ra tệp là miễn
   phí và ngoại tuyến. Pro không khoá chức năng cục bộ nào.
2. **Phần lõi Pro dùng chung hợp đồng, đặt ở `TsuDisk.Core.Pro`** (lấy từ tsuswico, chỉ đổi định danh):
   - `EntitlementVerifier` + `JwksKeySet`: kiểm JWS quyền lợi bằng ECDsa của .NET BCL - chỉ `ES256`, `typ` =
     `tsu-ent+jwt`, khoá theo `kid`, `iss` = `https://tsudev.com/oidc`, `aud` = `tsudisk`, `exp` chưa qua,
     `exp - iat` ≤ 7 ngày. Không thêm thư viện JWT (mỗi thư viện mở thêm đường `alg=none`/HS256/`jku`).
   - `ProRenewal` (mục 7.6): nhắc khi còn ≤ 7 ngày, mỗi lần mở; báo hết kỳ một lần cho mỗi `pro_until`
     (khoá `tsu.pro_expired.notified_until`), chỉ khi máy này từng thấy kỳ đó còn hạn (`tsu.pro.seen_until`).
     TsuDisk chưa có đồng bộ ⇒ bỏ câu "Bản sao trên đám mây..." đúng như chuẩn cho phép.
   - `ProNotice` (mục 7.3): thông báo nhiều thiết bị, chu kỳ 30 ngày (`tsu.pro_notice.last_shown`).
   - Nội dung vi/en chép nguyên văn; test đối chiếu thẳng với `.standards/docs/PRODUCT_POLICY.md`.
3. **CLI chỉ in, không chờ phím.** Lệnh đĩa không được bị chặn bởi một lời nhắc; CLI in tiêu đề, thân và liên kết
   `https://tsudev.com/pro?app=tsudisk`, rồi chạy lệnh ngay. Không in khi `--json`, `--dong-y`, `--xac-nhan`, khi vào/ra
   bị chuyển hướng, hoặc với `phien-ban`/`tro-giup`. Mỗi lần chạy `tsudisk.exe` là một "lần mở" (tiến trình không ở
   nền). Mọi lỗi đọc tệp quyền lợi bị nuốt: Pro không bao giờ làm hỏng lệnh đĩa.
4. **Nguồn quyền lợi = phiên đăng nhập** trong `%LOCALAPPDATA%\tsudev\tsudisk\` (`ProAccountStore`). Chưa đăng nhập
   ⇒ `ProHook.Run` thoát trước mọi lượt gọi mạng, không tạo tệp/thư mục nào (hạng C không để lại dấu vết). Đã đăng
   nhập: JWS đệm còn mới (< 24 giờ) thì dùng luôn, không thì làm mới (hết giờ 5 giây, lỗi ⇒ dùng bản đệm).
5. **Huy hiệu, màn Pro/Ủng hộ**: CLI `tai-khoan` in nhãn chữ theo `badge` và điều khoản 7.5 nguyên văn trước liên kết
   mua; huy hiệu đồ hoạ đi cùng giao diện WPF (giai đoạn 2).
6. **Đăng nhập** (22/11/2026, client OIDC native `tsudisk` đã đăng ký - phiếu tsudev 20261122-DK1): `tsudisk dang-nhap`
   = Authorization Code + PKCE S256 qua loopback `127.0.0.1`/`[::1]` cổng ngẫu nhiên (RFC 8252), `prompt=consent` để có
   `offline_access`; refresh token lưu DPAPI (CurrentUser, entropy riêng); mã cài đặt 128 bit ngẫu nhiên; trình duyệt
   mở ở quyền thường kể cả khi `tsudisk.exe` chạy quản trị. `dang-xuat` thu hồi token và xoá thư mục tài khoản (không
   đụng nhật ký đĩa). Cài đặt lấy nguyên từ tsuswico (đã kiểm trên Windows 11 thật), kèm bộ test IdP giả.

## Việc còn chờ (không làm khi chưa đủ điều kiện)

- **Tính năng Pro riêng** (đồng bộ cấu hình/kế hoạch/nhật ký giữa các máy qua TsuDEV Sync): chờ thư viện .NET
  `TsuSync` (PRO-D3) và chủ dự án duyệt phạm vi. Khi có: gửi JWS `iat` ≤ 24 giờ, `401 stale` ⇒ lấy JWS mới thử lại một
  lần, `402 pro_required` ⇒ màn Pro, `429/507/5xx` ⇒ "Chưa đồng bộ"; hết kỳ ⇒ tắt đẩy, vẫn cho tải bản sao 90 ngày.

## Hệ quả

- (+) Một hợp đồng, một cài đặt cho hai sản phẩm .NET; sửa lỗi ở tsuswico chép sang được ngay.
- (+) Chưa đăng nhập ⇒ không mã mạng nào chạy; bề mặt tấn công mới chỉ mở khi người dùng chủ động đăng nhập.
- (-) Thêm mã mạng (HttpClient, loopback) vào một công cụ đĩa - giữ cô lập trong `TsuDisk.Core.Pro`, mọi lỗi bị nuốt ở
  `ProHook`, không bao giờ chạy giữa một thao tác ghi đĩa (chỉ ở đầu lệnh).
