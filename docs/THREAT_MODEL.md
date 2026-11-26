# Mô hình đe doạ - Tài khoản TsuDEV và quyền lợi Pro trong TsuDisk

- **Phạm vi**: `tsudisk dang-nhap` / `tai-khoan` / `dang-xuat`, quyền lợi Pro, nhắc gia hạn (PRODUCT_POLICY 7.6) và
  thông báo nhiều thiết bị (7.3) - ADR-0007. Chưa gồm đồng bộ (viết bổ sung khi có `TsuSync` .NET).
- **Cập nhật**: 22/11/2026 (phiên tsudisk-59). Cài đặt và mô hình lấy từ tsuswico `docs/THREAT_MODEL.md` (cùng mã);
  checklist nguồn: `.standards/docs/PRODUCT_POLICY.md` mục 7, `AUTH_AND_ACCOUNT.md` mục 7, 14.3.
- **Mã**: `src/TsuDisk.Core/Pro/` (logic, test trên Linux), `src/TsuDisk.Windows/ProAdapters.cs` (DPAPI, mở trình
  duyệt), `src/TsuDisk.Cli/ProHook.cs` (nối CLI).

## 1. Tài sản và ranh giới tin cậy

| Tài sản | Ở đâu | Giá trị với kẻ tấn công |
| --- | --- | --- |
| Refresh token (30 ngày, xoay vòng) | `%LOCALAPPDATA%\tsudev\tsudisk\` (DPAPI CurrentUser + entropy riêng) | Lấy JWS quyền lợi của nạn nhân, đếm thêm thiết bị vào tài khoản |
| Access token (15 phút) | Chỉ trong bộ nhớ | Như trên, ngắn hạn |
| Mã cài đặt (128 bit) | `installation-id` | Làm sai số `devices_30d` của tài khoản đó |
| JWS quyền lợi đệm (≤ 7 ngày) | `entitlement.jws` + `jwks.json` | Không có - JWS có chữ ký, chỉ ẩn/hiện lời nhắc |
| Bố cục đĩa, nhật ký, sao lưu GPT (D1) | `%ProgramData%\TsuDisk\nhat-ky` | **Không** đi qua đường nào trong phạm vi này |

Ranh giới: (A) tiến trình `tsudisk.exe` (có thể chạy quản trị) ↔ người dùng/tiến trình khác cùng máy; (B) máy ↔
`tsudev.com` qua TLS; (C) cổng loopback - **mọi tiến trình trên máy** đều gọi tới được.

## 2. Đe doạ và biện pháp

| # | Đe doạ (STRIDE) | Biện pháp | Kiểm chứng |
| --- | --- | --- | --- |
| T1 | **Giả mạo JWS quyền lợi** (S/T): sửa `entitlement.jws` để thành Pro, ký bằng khoá tự sinh, `alg: none`/`HS256` dùng khoá công khai làm bí mật, đổi `aud` lấy JWS của app khác | `EntitlementVerifier`: chỉ `ES256`, `typ` = `tsu-ent+jwt`, khoá theo `kid` từ JWKS, chữ ký P1363 64 byte, `iss`, `aud` = `tsudisk`, `exp`, `exp - iat ≤ 7 ngày`, từ chối `crit`. Kiểm lại MỖI lần đọc đệm. Gate thật ở máy chủ (TsuDEV Sync trả 402) - gate trong app chỉ ẩn/hiện | `EntitlementTests` (P2) |
| T2 | **Thay JWKS đệm** (T): ghi `jwks.json` bằng khoá của kẻ tấn công rồi ký JWS giả | Ai ghi được `jwks.json` là đã chạy mã dưới tài khoản người dùng ⇒ ngoài phạm vi (A). Hệ quả tối đa: app hiện mình là Pro trên máy đó; không lấy được gì từ máy chủ. JWKS chỉ ghi đè khi JWS mới kiểm được bằng nó | `ProLoginTests.JWS_quyen_loi_sai_chu_ky_*` |
| T3 | **Dùng lại / trộm refresh token** (I/E): đọc tệp token, chép sang máy khác | DPAPI CurrentUser: tệp chép sang tài khoản/máy khác không giải được ⇒ coi như chưa đăng nhập. Xoay vòng mỗi lần dùng + phát hiện dùng lại ở tsudev.com (thu hồi cả cây phiên). **Giới hạn đã biết**: tiến trình độc chạy CÙNG tài khoản Windows giải được DPAPI - không cơ chế phía khách nào chặn được việc đó; thiệt hại giới hạn ở quyền lợi (không có dữ liệu đĩa trên máy chủ) | `Token_tep_tu_tai_khoan_khac_*`, `Refresh_xoay_vong_*` |
| T4 | **Tự gây phát hiện dùng lại** (D): hai cửa sổ tsudisk refresh cùng lúc ⇒ lần hai dùng token đã xoay ⇒ tsudev.com thu hồi mọi phiên của người dùng | Khoá tệp `session.lock` (FileShare.None) quanh refresh; không lấy được trong 10 s ⇒ dùng đệm, không refresh. Token mới ghi NGAY sau khi nhận | `Hai_tien_trinh_khong_refresh_song_song` |
| T5 | **Chặn mã ủy quyền trên loopback** (I): tiến trình khác nghe trộm/đọc URL callback | PKCE S256 (verifier 256 bit chỉ ở trong bộ nhớ) - mã vô dụng nếu thiếu verifier; mã dùng một lần, sống 60 s | `PKCE_S256_dung_vector_RFC_7636_*`, IdP giả kiểm verifier |
| T6 | **Tiêm phản hồi giả vào cổng loopback** (S): tiến trình khác gọi `/callback?code=<mã của kẻ tấn công>` để đăng nhập nạn nhân vào tài khoản lạ (login CSRF) | `state` 256 bit so thời gian hằng số TRƯỚC mọi thứ; lặp tham số ⇒ sai dạng; `nonce` trong id_token buộc vào đúng lượt; `iss` (RFC 9207) phải là `https://tsudev.com/oidc`. Đường khác `/callback` ⇒ 404 và chờ tiếp; kết nối treo bị cắt sau 10 s; đầu yêu cầu ≤ 8 KB | `State_sai_*`, `Thieu_state_*`, `Tham_so_iss_khac_*`, `Nonce_sai_*`, `Callback_lap_tham_so_*`, `Duong_khac_callback_*` |
| T7 | **Lẫn IdP / giả mạo discovery** (S) | Endpoint ghi cứng (không đọc discovery lúc chạy), chỉ `https://tsudev.com`, `HttpClient` KHÔNG theo chuyển hướng. id_token: chỉ ES256, `iss`, `aud` (mảng nhiều phần tử ⇒ `azp`), `exp`/`iat` lệch ≤ 5 phút | `IdTokenValidatorTests`, `Diem_cuoi_production_*` |
| T8 | **Trình duyệt chạy quyền quản trị** (E): tsudisk.exe chạy quản trị (lệnh ghi đĩa), ShellExecute URL sẽ mở trình duyệt nâng quyền | Nhờ explorer của desktop (không nâng quyền) gọi `ShellExecute` qua COM `ShellWindows` ⇒ `IShellBrowser` ⇒ `IShellView` ⇒ `IShellDispatch2`; thất bại ⇒ in liên kết, KHÔNG lùi về ShellExecute nâng quyền; chỉ nhận URL `https`. Không WebView. (`explorer.exe <url>` - cách đầu tiên - đo SAI trên Windows thật: mở thư mục Documents) | Cùng mã đã đo ở tsuswico 20/11/2026 Win 11: Edge Elevated = 0 |
| T9 | **Liệt kê / đánh dấu thiết bị** (I): dùng mã cài đặt để theo dấu máy | Mã ngẫu nhiên 128 bit, KHÔNG từ MAC/serial/mã sản phẩm dù app đọc được; chỉ sinh khi đăng nhập; chỉ gửi trong `X-Tsu-Installation` tới `/api/me/entitlements`; máy chủ lưu HMAC, JWS chỉ trả số đếm; `dang-xuat` xoá mã | `InstallationIdTests`, `Dang_nhap_thanh_cong_*` (header đúng mã) |
| T10 | **Rò mã cài đặt / token qua nhật ký** (I) | TsuDisk không ghi gì về tài khoản vào nhật ký đĩa. URL ủy quyền in ra màn hình chỉ chứa `state`/`nonce`/challenge (không bí mật sau khi dùng) | Rà mã `ProHook.cs` |
| T11 | **Gọi mạng ngầm khi chưa đăng nhập** (I, vi phạm hạng C) | Mọi đường trừ `dang-nhap` kiểm `HasSession` trước khi chạm `ProHttp`; đọc không tạo tệp; `--json`/`--dong-y`/`--xac-nhan`/vào-ra chuyển hướng không bao giờ gọi tsudev.com | `ProOfflineGuardTests` (bộ xử lý HTTP ném lỗi + rà nguồn `ProHook.cs`) |
| T12 | **Làm hỏng lệnh đĩa / kịch bản tự động** (D): tsudev.com chậm/sập, tệp hỏng, DPAPI lỗi | Nhắc Pro chạy ở ĐẦU lệnh, trước mọi bước ghi: timeout 5 s, mọi ngoại lệ nuốt, lệnh chạy tiếp; không đổi mã thoát; CHỈ IN, không bao giờ chờ phím | `Ngoai_tuyen_*`, `ProRenewalTests` |
| T13 | **Lùi đồng hồ máy để kéo dài Pro đệm** (T) | Chỉ ảnh hưởng ẩn/hiện trong app trên máy đó; JWS vẫn bị giới hạn bởi chữ ký + máy chủ gate 402 cho mọi thứ có giá trị | Chấp nhận |

## 3. Rủi ro còn lại (chấp nhận, ghi rõ)

1. Mã độc chạy cùng tài khoản Windows đọc được refresh token (T3) - giới hạn của mọi app desktop; thiệt hại: quyền lợi
   + đếm thiết bị, không có dữ liệu người dùng phía máy chủ.
2. Chạy `tsudisk` bằng tài khoản quản trị khác (UAC "over-the-shoulder"): phiên nằm trong `%LOCALAPPDATA%` của tài khoản
   đó - đúng hành vi DPAPI CurrentUser.
3. Mở trình duyệt ở quyền thường cần explorer.exe làm shell; không có thì người dùng tự dán liên kết đã in.

## 4. Phiếu rà soát bảo mật (trước khi phát hành)

- [x] Không thêm gói NuGet (DPAPI qua `crypt32`, HTTP/JSON/ECDsa của BCL)
- [x] Client công khai: không secret nào trong exe; client `tsudisk` chỉ nhận redirect loopback `/callback` (đo 22/11/2026:
      redirect lạ ⇒ 400)
- [x] Mọi kết nối mới liệt kê trong `PRIVACY.md`, nêu rõ chỉ sau khi đăng nhập
- [x] Test hạng C + IdP giả, không test nào gọi `tsudev.com` (285 test)
- [x] `tai-khoan` khi chưa đăng nhập chạy thật trên Windows 11 (22/11/2026): không gọi mạng, không tạo tệp
- [ ] Đăng nhập thật với tài khoản TsuDEV trên Windows (cần chủ dự án bấm đăng nhập trong trình duyệt) - phiên sau
