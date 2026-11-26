# ADR-0012: Cổng phiên bản - đọc bản kê `versions.json` ký Ed25519, khoá GUI/CLI, nâng `latest` theo kênh, DPoP

- **Trạng thái**: Chấp nhận - 26/11/2026 (chủ dự án duyệt PLAN.md + ADR trong phiên 12).
- **Quyết định chủ dự án đã chốt trong phiên 12**: (Q1) chỉ kiểm bản kê **khi đã đăng nhập**; (Q2) CLI bị khoá thì
  **chặn mọi lệnh trừ** `phien-ban`, `tro-giup`, `dang-xuat`, `xuat-du-lieu`.
- **Liên quan**: `.standards/docs/APP_INTEGRITY.md` (v1.0.0) mục 1-2, 3 (desktop), 6, 8; `AUTH_AND_ACCOUNT.md` 14.1;
  `PRODUCT_POLICY.md` 7.4; ADR-0007 (Pro, `ProHttp`, `X-Tsu-App`); phiếu `logs/handover/20261125-INT1_*.md`;
  phía máy chủ: repo tsudev `docs/release-signing.md` (INT1-a, INT1-b đã chạy production 26/11/2026).

## Bối cảnh

APP_INTEGRITY I2 yêu cầu: khi có bản mới trên kênh của người dùng, bản cũ bị khoá ngay (modal chỉ có **Cập nhật** và
**Xuất dữ liệu**), và máy chủ trả `426` cho bản cũ. tsudev.com đã có:

- `GET /.well-known/tsudev/versions.json` (hiện `products: {}`) và `.sig`: JWS compact tách nội dung (RFC 7515 phụ lục F)
  `<b64url(header)>..<b64url(chữ ký)>`, header `{"alg":"EdDSA","kid":"tsu-versions-2026"}`. Chữ ký Ed25519 tính trên
  `b64url(header) + "." + b64url(các byte versions.json tải về)`.
- `GET /.well-known/tsudev/versions-jwks.json`: khoá công khai `tsu-versions-2026` (OKP/Ed25519).
- `POST /api/versions/raise/<sản phẩm>/<kênh>` với header `x-versions-token`. Máy chủ GET thật `url`, nên không nâng được
  nếu chưa tải được bản đó.
- Cổng `X-Tsu-App` ở `/ent/*` và `/oidc/me`: phiên bản cũ hơn `latest` của đúng kênh thì trả `426 update_required`.
  tsudisk đã gửi header này từ 26.11.2601 (#80).

TsuDisk là hạng C, và PRIVACY.md cam kết chưa đăng nhập thì không kết nối mạng. .NET 10 chưa có Ed25519 trong BCL.

## Quyết định

1. **Chỉ kiểm khi đã đăng nhập (Q1).** Chưa đăng nhập thì không có lượt gọi mạng nào và không bị khoá. Cách này khớp
   "chế độ ngoại tuyến hoàn toàn" của APP_INTEGRITY 2.2 và AUTH 14.1. PRIVACY.md chỉ thêm một dòng: khi đã đăng nhập,
   TsuDisk GET bản kê (không gửi gì ngoài `User-Agent` và `X-Tsu-App`).
2. **Kiểm chữ ký bằng `BouncyCastle.Cryptography`** (MIT, thuần managed, không cần thư viện native, chạy được với
   single-file vì repo không bật trim/AOT). Chỉ dùng hàm kiểm (`Ed25519.Verify`), ghim phiên bản trong
   `packages.lock.json`. Phương án bị loại:
   - NSec: cần libsodium native nên phức tạp hơn khi đóng gói single-file.
   - Tự viết Ed25519: tự viết mật mã là rủi ro.
   - Chờ BCL: chưa có thời hạn.
3. **Khoá công khai ghim lúc dựng.** Một bảng hằng `kid -> 32 byte` trong `TsuDisk.Core/Integrity/VersionKeys.cs`
   (`tsu-versions-2026` lấy từ JWKS production, có test đối chiếu tệp JWKS mẫu). App KHÔNG tải JWKS lúc chạy. Không có
   biến môi trường hay tham số nào để đổi khoá hoặc URL (nếu có thì đó là cửa bỏ qua cổng). Khi xoay khoá: phát hành
   bản ghim cả khoá cũ lẫn khoá mới trước, sau đó tsudev.com mới đổi khoá.
4. **Kiểm bản kê** (theo đúng thứ tự, sai bước nào thì bỏ bản kê ở bước đó):
   1. `.sig` gồm đúng 3 phần, phần giữa rỗng.
   2. Header chỉ nhận `alg == "EdDSA"` và `kid` có trong bảng; chữ ký dài 64 byte.
   3. Ed25519 hợp lệ trên đúng các byte tải về. Chỉ parse JSON SAU khi chữ ký đạt.
   4. `v == 1`; `expires_at - issued_at <= 7 ngày`; `now < expires_at`; `issued_at >= issued_at` của bản đệm
      (chống phát lại bản kê cũ).

   Giới hạn kích thước: bản kê 256 KiB, `.sig` 1 KiB. Chỉ GET qua https tới host cố định `tsudev.com`, không theo
   chuyển hướng (giống `ProHttp`), thời hạn 5 giây.
5. **Đệm**: lưu byte bản kê + `.sig` đã đạt vào `%LOCALAPPDATA%\tsudev\tsudisk\versions\` (ghi nguyên tử). Lỗi mạng
   thì dùng bản đệm nếu chữ ký còn hạn, nếu không thì không chặn. Bản đệm được kiểm lại đầy đủ mỗi lần đọc.
6. **So phiên bản** theo `VERSIONING.md` mục 2: `YY.M.DDNN`; bản thử (`-beta.N`) nhỏ hơn bản chính cùng số. Chỉ so
   `products.tsudisk.channels.<kênh của mình>`. Kênh lấy từ `ProContract.DetectChannel` (github/scoop/winget). Bản tự
   dựng (phiên bản không đúng khuôn) thì không kiểm và không khoá. Mã nguồn công khai (OPEN, Kerckhoffs): người tự dựng
   bản vốn đã gỡ được cổng. Bảo vệ thật nằm ở máy chủ (`426`, I1).
7. **Thời điểm kiểm**: CLI kiểm một lần khi chạy lệnh tương tác, nếu đã đăng nhập. GUI kiểm khi mở, mỗi 6 giờ, và khi
   cửa sổ được kích hoạt lại sau ≥ 30 phút. Lệnh `--json`, chạy tự động hoặc chuyển hướng I/O vẫn kiểm, nhưng báo khoá
   bằng JSON `{"error":"update_required","latest":...,"url":...}` và mã thoát riêng.
8. **`426` từ máy chủ** (`ProHttp`) cũng đưa app vào trạng thái khoá, giống bản kê có bản mới.
9. **Khoá CLI (Q2)**:
   - Mọi lệnh trừ `phien-ban`, `tro-giup`, `dang-xuat`, `xuat-du-lieu` đều in nguyên văn APP_INTEGRITY 2.2 (vi), rồi
     in hướng dẫn cập nhật theo kênh: `scoop update tsudisk`, `winget upgrade tsudev.TsuDisk`, hoặc `url` của bản kê.
     Thoát mã **6** (`UpdateRequired`).
   - Cổng chạy TRƯỚC khi lệnh bắt đầu, không bao giờ ngắt một thao tác đĩa đang chạy. `PlanExecution` không đổi.
10. **Khoá GUI**: `UpdateRequiredWindow` toàn màn, không đóng được (chặn Esc, Alt+F4, nút X), focus không thoát khỏi
    cửa sổ, có tên UI Automation, theo Sáng/Tối và tương phản cao. Nguyên văn vi/en, chỉ hai nút:
    - **Cập nhật ngay**: GitHub thì mở `url`; Scoop/winget thì hiện lệnh cập nhật kèm nút sao chép.
    - **Xuất dữ liệu**.

    Đang có thao tác đĩa chạy dở thì chờ thao tác xong rồi mới hiện modal.
11. **Xuất dữ liệu** (I6, `LOCAL_SYNC` mục 9): lệnh `tsudisk xuat-du-lieu <thư mục>` và nút trong GUI dùng chung hàm
    Core. Hàm ghi một tệp `.zip` gồm nhật ký `%ProgramData%\TsuDisk\nhat-ky`, bản sao lưu GPT (nếu có), cài đặt GUI, và
    `manifest.json` liệt kê tệp + SHA-256. Hàm KHÔNG xuất refresh token hay khoá DPoP: đó là bí mật của thiết bị, không
    phải dữ liệu người dùng.
12. **Nâng `latest` theo kênh** (APP_INTEGRITY 2.1, "chốt chặn"):
    - `release.yml`: thêm job `nang-latest` sau `cong-bo`, chỉ cho bản chính thức, chạy trong Environment `versions`,
      secret `TSU_VERSIONS_TOKEN`. (Bổ sung 26/11/2026: `versions` chỉ giữ `TSU_VERSIONS_TOKEN`, cho nhánh `main` + tag
      `v*` vì `kenh-phat-hanh.yml` chạy từ `main`; `WINGET_TOKEN` vẫn ở `packages` chỉ cho tag `v*` - không nới phạm vi.) Job POST kênh `github` với `url` = `.../releases/tag/v$VERSION`. Lỗi `409
      download_unverified` thì thử lại tối đa 3 lần, cách nhau 2 phút; job lỗi thì mở Issue, KHÔNG làm đỏ việc xuất bản.
    - Workflow mới `kenh-phat-hanh.yml` (chạy theo lịch mỗi 6 giờ + `workflow_dispatch`, quyền `contents: read`):
      - Kênh `winget`: chỉ nâng khi thư mục `manifests/t/tsudev/TsuDisk/<bản>` đã có trên `microsoft/winget-pkgs` (đã
        merge). `url` = trang thư mục đó.
      - Kênh `scoop`: chỉ nâng khi `bucket/tsudisk.json` ở `tsudev-org/scoop-bucket` có `version` bằng bản mới.
        `url` = URL tải exe trong manifest (chứa phiên bản).
      - Nâng rồi thì bỏ qua (máy chủ trả `409 version_not_newer`, coi là xong).
13. **DPoP** (RFC 9449, PRODUCT_POLICY 7.4) làm ở Phase riêng, sau khi tsudev.com chốt hợp đồng DPoP cho desktop
    (INT1-c hiện chỉ làm Android):
    - Khoá P-256 không xuất được, đặt trong CNG Microsoft Platform Crypto Provider (TPM) nếu có, nếu không thì dùng
      Microsoft Software KSP theo người dùng.
    - Gửi `DPoP` proof khi đổi token và khi gọi `/api/me/entitlements`; hỗ trợ `DPoP-Nonce`; không giả vờ "đã chứng thực".
    - Lý do làm trước khi máy chủ bắt buộc DPoP ở `/api/me/entitlements`: nếu không, Pro của tsudisk ngừng chạy.

## Hệ quả

- (+) Đạt APP_INTEGRITY mục 8 cho desktop OPEN, mà không phá cam kết ngoại tuyến của người chưa đăng nhập.
- (+) Lộ `TSU_VERSIONS_TOKEN` chỉ cho phép nâng `latest` lên một bản tải được thật. Kẻ có token không khoá được người
  dùng vào một bản không tồn tại, vì máy chủ GET thật `url`.
- (-) Người chưa đăng nhập không được nhắc cập nhật trong app. Họ vẫn được Scoop/winget nhắc qua kênh của họ.
- (-) Thêm phụ thuộc BouncyCastle (~6 MB vào exe single-file).
- (-) Kênh winget chậm vài ngày, vì chờ moderator merge. Trong thời gian đó, người dùng winget vẫn chạy bản cũ mà không bị
  khoá. Đây là đúng chủ đích của chốt chặn.
- Rủi ro lớn nhất: nâng `latest` khi chưa có bản để tải, làm người dùng bị khoá mà không có lối ra. Giảm bằng ba lớp:
  máy chủ GET thật `url`; workflow chỉ nâng sau khi kênh đã có bản; GUI/CLI luôn còn **Xuất dữ liệu**.

## Ghi chú triển khai (Phase INT4, 26/11/2026)

- **Cập nhật ngay** trên GUI làm hành động theo kênh rồi **đóng TsuDisk** (giống In-App Update `IMMEDIATE`): GitHub mở
  `url`; Scoop/winget tự sao chép lệnh vào bộ nhớ tạm và hộp thoại hiện nguyên lệnh (thay cho "nút sao chép" riêng, để
  modal giữ đúng hai nút). Lý do: Scoop/winget và bản tải tay đều cần tệp exe đang chạy được giải phóng; modal không
  đóng được nên đây là lối ra duy nhất ngoài Task Manager.
- Mục 8 (`426`): `ProAccount.UpdateRequired` ghi dấu `versions/server-lock.json` (`ServerLockStore`, gắn đúng phiên bản
  đang chạy, hết hạn sau 24 giờ); `VersionGate` của CLI và GUI đọc dấu này trước khi tải bản kê.
- Ảnh chụp/E2E dùng `--du-lieu-mau --xem-khoa-mau` (chỉ HIỆN modal với phiên bản mẫu); không có cờ hay biến môi trường
  nào bỏ qua cổng.

## Ghi chú triển khai (Phase INT6, 27/11/2026) - thay chi tiết của mục 13

Làm theo hợp đồng ĐANG CHẠY ở tsudev.com (ADR-0004 mục 6 repo tsudev; phiếu trao đổi `20261127-DP1`), không chờ INT1-c:
máy chủ đã nhận DPoP tuỳ chọn và gắn `cnf.jkt`, nên gửi sớm không phá gì và bản đã cài sẵn sàng khi DPoP thành bắt buộc.

- Chỉ `GET /api/me/entitlements` mang `DPoP`. **Không** gửi ở `/oidc/token` (khác mục 13): máy chủ chưa gắn token với
  khoá; gửi sớm có thể nhận token `DPoP`-bound mà `/api/me/entitlements` (đọc `Bearer`) chưa kiểm.
- Đã gửi DPoP thì JWS PHẢI có `cnf.jkt` đúng thumbprint khoá; thiếu/khác ⇒ bỏ JWS, giữ bản đệm.
- Thử lại đúng một lần: `use_dpop_nonce` kèm nonce mới; hoặc `invalid_dpop_proof` khi header `Date` cho thấy đồng hồ máy
  lệch > 30 giây (ký lại theo giờ máy chủ).
- Khoá CNG `tsudev.tsudisk.dpop.v1`, phạm vi người dùng, `ExportPolicy = None`, TPM trước, Software KSP sau. Tạo trong
  lượt lấy quyền lợi đầu tiên sau đăng nhập; xoá khi `dang-xuat` và khi đổi tài khoản. Không tạo được khoá ở cả hai nơi
  ⇒ gọi không DPoP (`PRODUCT_POLICY` 7.4: `SHOULD`).
- Đã thử trên Windows thật (27/11/2026, khoá tên riêng, xoá sau khi thử): khoá nằm ở Platform Crypto Provider, chữ ký
  P1363 64 byte kiểm được, mở lại đúng khoá, xuất khoá riêng bị từ chối.

## Việc còn chờ

- 👤 Duyệt ADR này và `docs/PLAN.md`.
- ~~tsudev.com trả lời phiếu `20261127-DP1`~~ Đã trả lời 27/11/2026: DPoP bắt buộc cho desktop chưa có lịch (sẽ báo
  trước ít nhất một bản phát hành; thiếu/sai ⇒ `401 invalid_dpop_proof`); không định bật `DPoP-Nonce`; **không** gửi DPoP
  ở `/oidc/token` (máy chủ sẽ cấp token gắn khoá mà `/api/me/entitlements` chỉ nhận `Bearer`); không cần đăng ký `jkt`.
- ~~Đăng nhập thật trên Windows~~ Đạt 27/11/2026: JWS thật có `cnf.jkt` khớp khoá của máy.
- 👤 Chọn ngày "khoá ngay" toàn hệ (INT1-f phía tsudev).
