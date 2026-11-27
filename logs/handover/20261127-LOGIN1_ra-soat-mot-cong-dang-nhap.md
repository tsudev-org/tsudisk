# PHIẾU RÀ SOÁT MỘT CỔNG ĐĂNG NHẬP - tsudisk

> Mẫu theo `AUTH_AND_ACCOUNT.md` mục 2.1. KHÔNG dán giá trị bí mật, token hay dữ liệu tài khoản vào phiếu.

- **Trạng thái**: ĐÓNG (khi PR Phase QR4-UI1 gộp với CI xanh) - **Bộ quy ước**: v9.0.0 - **Phiên**: phiên 18 (agent-tsudisk), 27/11/2026
- **Bề mặt**: desktop (GUI WPF) | CLI | website sản phẩm `disk.tsudev.com` - **Có tài khoản TsuDEV**: có (tuỳ chọn, hạng C)

## 1. Kết quả quét

`.standards/scripts/check-central-login.sh`: 0 nghi vấn; `--strict`: đạt (mã 0). Bổ sung test
`tests/TsuDisk.Core.Tests/CentralLoginTests.cs` cho phần script không thấy (XAML/WPF, cờ CLI, discovery).

## 2. Rà tay (đánh dấu khi đã kiểm bằng cách chạy sản phẩm)

- [x] Website: không trang/hộp thoại đăng nhập, đăng ký, quên mật khẩu; không ô mật khẩu/OTP tài khoản (trang tĩnh
      Astro, chỉ `theme.js`; `check-dist` + `check-central-login`).
- [x] Không nút, SDK, OAuth client riêng của Google/Apple/GitHub; không WebView hứng đăng nhập (test
      `Giao_dien_khong_co_o_mat_khau_hay_WebView`, `Ma_nguon_khong_nhan_mat_khau_tai_khoan`).
- [x] App/CLI: chỉ nút/lệnh mở trình duyệt (PKCE S256 + `state` qua `127.0.0.1`/`[::1]`, RFC 8252 - có từ ADR-0007)
      và nút mở trang tài khoản. CLI `dang-nhap`; GUI cửa sổ **Tài khoản** (mới, Phase QR4-UI1): Đăng nhập trên trình
      duyệt / Mở trang tài khoản / Đăng xuất / Xóa tài khoản, không ô nhập (test + E2E GUI G12).
- [x] `client_id` `tsudisk` + `redirect_uri` loopback đã đăng ký tại tsudev.com (22/11/2026, phiếu tsudev DK1).
      URL: điểm cuối OIDC GHIM theo ADR-0007 (discovery giả mạo không đổi được điểm cuối token/JWKS), đi qua
      `/authorize` của issuer `https://tsudev.com/oidc`, KHÔNG ghi cứng trang đăng nhập; test
      `Diem_cuoi_ghim_khop_discovery_cua_issuer` đối chiếu discovery thật (fixture 27/11/2026). Cổng đổi chỗ
      (`id.tsudev.com`) không ảnh hưởng vì issuer giữ nguyên và `/authorize` tự chuyển tới trang đăng nhập hiện hành.
- [x] Token ở kho bí mật hệ điều hành (DPAPI CurrentUser) + DPoP khoá CNG/TPM (INT6); có đăng xuất (thu hồi + xoá
      tệp) và nút/lệnh xóa tài khoản (`xoa-tai-khoan`, nút **Xóa tài khoản**) mở trang xoá của cổng
      `https://tsudev.com/settings/security#xoa-tai-khoan` (đo 27/11: 200; `/tai-khoan` mục 13.1 chưa có - 404).
- [x] Máy chủ sản phẩm không nhận, không lưu mật khẩu tài khoản (tsudisk không có máy chủ; website tĩnh).
- [x] `/privacy` (PRIVACY.md -> disk.tsudev.com/quyen-rieng-tu) khớp dữ liệu thật sự gửi đi: đã thêm đăng nhập từ GUI,
      mọi bước đăng nhập trên tsudev.com, xoá tài khoản. Hồ sơ cửa hàng: winget/Scoop không có mục dữ liệu riêng.

## 3. Vi phạm và cách sửa

| Vi phạm (tệp:dòng) | Cách sửa | Di trú dữ liệu | PR | Trạng thái |
| --- | --- | --- | --- | --- |
| Thiếu nút/lệnh "Xóa tài khoản" trong app (mục 2.1) | `xoa-tai-khoan` + nút GUI mở trang xoá của cổng | Không | Phase QR4-UI1 | Đã sửa |
| GUI chưa có mục Tài khoản (QR4-DESKTOP, phiếu QR1) | `AccountWindow` đúng hai loại nút + Đăng xuất/Xóa | Không | Phase QR4-UI1 | Đã sửa |
| Liên kết tsudev.com trong GUI không có dấu hiệu TsuDEV (BRAND_ASSETS 12.3) | Ảnh `logo-tsudev-32.png` từ dây chuyền thương hiệu trước liên kết | Không | Phase QR4-UI1 | Đã sửa |

## 4. Ngoại lệ đã khai trong `.central-login-allow`

| Luật + đường dẫn | Lý do |
| --- | --- |
| (không có) | Script không báo nghi vấn nào |

Đóng phiếu khi mục 3 hết dòng mở, `check-central-login.sh --strict` đạt và đã đưa vào CI (`standards.yml`).
