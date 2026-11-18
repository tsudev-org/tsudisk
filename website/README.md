# Website disk.tsudev.com

Trang sản phẩm của TsuDisk: [Astro](https://astro.build) dựng trang tĩnh; JavaScript phía trình duyệt chỉ có
`public/theme.js` (bộ chọn giao diện Sáng/Tối/Hệ thống ở chân trang, khóa `tsudev-theme` như tsudev.com, nạp chặn
trong `<head>`, CSP `script-src 'self'`). Khung dùng chung với swico.tsudev.com và files.tsudev.com.

## Kiến trúc phục vụ và chuẩn hóa URL

```
yêu cầu ──> Cloudflare (HTTPS, HSTS preload) ──> khớp tệp trong dist/? ── có ──> trả tệp + public/_headers
                                                          │
                                                          └─ không ──> worker/index.mjs
                                                                         ├─ khác GET/HEAD      -> 405 + Allow
                                                                         ├─ URL > 2048 ký tự   -> 414
                                                                         ├─ có dạng chuẩn khác -> 301 về dạng chuẩn
                                                                         └─ đã chuẩn           -> 404 thật (trang 404)
```

- **Tệp tĩnh không qua Worker**: không tốn lượt Worker, kẻ tấn công không làm cạn hạn mức miễn phí bằng cách gọi
  trang thật. Worker chỉ xử lý đường dẫn hỏng.
- **Dạng chuẩn**: `https://disk.tsudev.com`, đường dẫn chữ thường, một `/` giữa các đoạn, kết thúc bằng `/` (trừ tệp
  có đuôi), không `index.html`, không `.html`, bỏ tham số theo dõi (`utm_*`, `fbclid`, `gclid`...). Trang nào cũng có
  `<link rel="canonical">` trỏ đúng dạng này.
- **Chống open redirect**: đích chuyển hướng luôn là origin cố định + đường dẫn bắt đầu bằng đúng một `/`.
- **Không vòng lặp**: test kiểm mọi đích chuyển hướng là điểm dừng.
- `workers_dev` và `preview_urls` tắt: không có bản sao ở `*.workers.dev`.
- Logic thuần ở `worker/chuan-hoa-url.mjs`, test `npm run test:worker` (không phụ thuộc thư viện).

## Header bảo mật

`public/_headers` (khối `/*`) và `HEADER_BAO_MAT` trong Worker **phải khớp từng dòng** - test tự kiểm:
HSTS 1 năm + preload; CSP `default-src 'none'` chỉ mở `'self'` cho script/style/ảnh/font/manifest, không
`'unsafe-inline'`, `frame-ancestors 'none'`, `form-action 'none'`, `base-uri 'none'`; COOP/CORP/COEP; `nosniff`;
`X-Frame-Options: DENY`; Permissions-Policy tắt mọi cảm biến; `Origin-Agent-Cluster`. `/.well-known/security.txt`
theo RFC 9116.

## Một nguồn nội dung

| Trang | Nguồn |
| --- | --- |
| `/huong-dan/` | mục `Vì sao có TsuDisk`, `Cách dùng`, `Đánh số phiên bản` của `README.md` |
| `/an-toan/` | mục `Cơ chế an toàn`, `Kiểm thử` của `README.md` |
| `/huong-dan/thu-cong/` | `docs/HUONG-DAN-MO-RONG-O-C.md` |
| `/quyen-rieng-tu/`, `/dieu-khoan/` | `PRIVACY.md`, `docs/DIEU-KHOAN.md` |
| `/nhat-ky/`, `/ban-quyen/` | `CHANGELOG.md`, `LICENSE`, `NOTICE`, `THIRD-PARTY-NOTICES.md` |
| `/tai-ve/` | GitHub Releases (phiên bản, liên kết, SHA-256 từ `SHA256SUMS.txt`) lúc build |

Đổi tên một mục `##` của README thì sửa danh sách `MUC` ở trang tương ứng (build báo lỗi nếu lệch).

## Tài sản thương hiệu

Sinh bằng `scripts/brand-assets.mjs` từ MỘT ảnh gốc `assets/brand/source/logo-tsudev.png` (bản gốc chính thức của
TsuDEV, SHA-256 ghi trong `assets/brand/brand.json`). Không sửa tay; CI chạy `--check`.

## Lệnh

```bash
cd website
npm ci
npm run dev           # http://localhost:4321
npm run test:worker   # test chuẩn hóa URL + header
npm run build         # dựng dist/ + check-dist (title, og, canonical, mô tả, CSP, em-dash, liên kết gãy)
npm run check:a11y    # axe-core WCAG 2.2 AA, Sáng + Tối (lần đầu: npx playwright install chromium)
npm run check:theme   # bộ chọn giao diện trong Chromium với CSP production
npm run deploy        # test:worker + build + wrangler deploy (cần `npx wrangler login` tài khoản tsudev)
```

Sau mỗi lần phát hành bản mới, chạy lại `npm run deploy` để trang Tải về cập nhật phiên bản và SHA-256.
