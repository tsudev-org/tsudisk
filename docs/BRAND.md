# Nhận diện TsuDisk

Theo `.standards/docs/BRAND_ASSETS.md` mục 15 (logo sản phẩm riêng). Logo do chủ dự án cung cấp ngày 17/11/2026.

## Ảnh gốc duy nhất

| Tệp | Kích thước | SHA-256 |
| --- | --- | --- |
| `assets/brand/source/logo-tsudisk.png` | 717x693, nền đặc `#0C0F16` | `933d93add9ee961863ef2586355a2c6527e0075871508e8ae16723a7febf1fa9` |
| `assets/brand/source/logo-tsudev.png` (dấu hiệu hệ sinh thái, chân trang) | 1254x1254 | `2ad933bde3f5bc81325fd21896b036df883787b12723020e544b2b8d582577ba` |

Mọi bản khác SINH từ hai tệp trên bằng `node website/scripts/brand-assets.mjs` theo `assets/brand/brand.json`.
`MUST NOT` sửa tay tệp trong `website/public/brand/`, `website/public/favicon*`, `android-chrome-*`, `apple-touch-icon.png`,
`og-image.png`, `assets/brand/app/`. CI chạy `--check` và đỏ nếu tệp đã commit lệch ảnh gốc.

## Biểu tượng và logo đầy đủ

- **Biểu tượng**: vùng thanh ổ đĩa + mũi tên + khung nét đứt, cắt từ ảnh gốc theo `iconCrop` (bỏ chữ "TsuDisk" - chữ
  không đọc được ở 16-48px). Từ 24/11/2026 (phiếu `20261124-LG2`, BRAND_ASSETS 15.4) là **SVG xoá nền**
  `assets/brand/source/mark.svg` (nền Tối), `mark-light.svg` (nền Sáng), `mark-adaptive.svg` (theo hệ thống) - sinh bởi
  tsudev `packages/brand/build-vector-marks.js`. Đầu trang website dùng hai SVG theo `data-theme`; `favicon.svg` = bản
  adaptive. Icon `tsudisk.exe`, PWA, apple-touch, `favicon.ico` dự phòng: ô vuông nền `#0C0F16` bo góc 22% dựng TỪ SVG
  (`tileScale` 0.86).
- **Logo đầy đủ** (có chữ): đã gỡ 24/11/2026 - không trang nào dùng (Clean Codebase). Cần lại thì dựng từ `mark.svg` + chữ
  thật, không cắt ảnh.
- **Ảnh chia sẻ** `og-image.png` 1200x630: biểu tượng + tên + mô tả + tên miền.

## Định dạng

| Dùng ở | Định dạng | Lý do |
| --- | --- | --- |
| Giao diện website | SVG trong suốt (hai bản Tối/Sáng) | Sắc ở mọi cỡ và mật độ điểm ảnh, 7 kB, không ô nền tối |
| Tab trình duyệt | `favicon.svg` (tự đổi theo hệ thống) + `favicon.ico` 7 lớp + PNG 16/32/96 dự phòng | Trình duyệt hiện đại lấy SVG; ICO khai `sizes="32x32"` để không giành chỗ SVG |
| iOS, Android, PWA | PNG nền đặc / maskable | Nền tảng yêu cầu PNG |
| Mạng xã hội | `og-image.png` | Open Graph không hỗ trợ AVIF ổn định |
| Ứng dụng Windows | `app.ico` 7 lớp | Icon exe, lối tắt |

## Đổi logo về sau

1. Thay `assets/brand/source/logo-tsudisk.png` (cạnh ngắn từ 1024px hoặc SVG càng tốt).
2. Đo lại vùng biểu tượng và sửa `iconCrop` trong `brand.json`.
3. `node website/scripts/brand-assets.mjs`, cập nhật SHA-256 ở bảng trên, commit mọi tệp sinh ra.
4. Báo phiên repo `tsudev` URL `https://disk.tsudev.com/brand/logo-tsudisk.png` cho danh bạ tsudev.com/apps.

Nợ đã biết: (1) ở 16px các nét đứt mảnh của biểu tượng hòa vào nhau - cần một biến thể đơn giản hóa riêng cho 16px do
người thiết kế vẽ (thêm `source/logo-tsudisk-16.png` + nhánh trong dây chuyền). (2) Ảnh gốc 717x693 nhỏ hơn mức 1024px khuyến nghị - bản 512/1024 của icon là phóng to (vùng biểu tượng gốc
392x221). Khi có bản gốc lớn hơn hoặc SVG thì thay theo các bước trên.
