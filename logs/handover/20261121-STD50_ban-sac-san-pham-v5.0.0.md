# PHIẾU YÊU CẦU - Bản sắc riêng của TsuDisk (tsudev-standards v5.0.0)

- **Mã phiếu**: 20261121-STD50
- **Từ**: phiên tsudev-standards, 21/11/2026 - **Đến**: phiên kế tiếp của `tsudisk`
- **Trạng thái**: MỞ
- **Nguồn**: https://github.com/tsudev-org/tsudev-standards/releases/tag/v5.0.0 - `docs/DESIGN_SYSTEM.md` mục 1.2,
  CHANGELOG 5.0.0 "Hướng dẫn nâng cấp"
- **Bối cảnh**: chủ dự án muốn mỗi sản phẩm có đặc trưng nổi bật riêng (màu, giao diện, bố cục). Trước v5.0.0, 9/11
  sản phẩm dùng cùng một màu xanh. Từ v5.0.0: nền/chữ/trạng thái/tiếp cận là chung; màu nhấn, bố cục, bo góc, hoạ
  tiết, font tiêu đề là riêng, khai ở `.standards/tokens/products/disk.json` - KHÔNG tự khai lại tại repo này.

## Bản sắc đã chốt cho `tsudisk`

| Trường | Giá trị |
| --- | --- |
| `data-product` | `disk` |
| Màu chính Tối / Sáng | `#24B9A8` / `#08786C` (hue OKLCH 183; lý do: xanh ngọc của chữ Disk và thanh dung lượng) |
| Bố cục `layout` | `cong-cu` - vùng thao tác chính (kéo thả/chọn tệp/nút chạy) ngay màn đầu, căn giữa; giới thiệu nằm dưới |
| Bo góc `shape` | `chuan` - 4/6/8px, nút 6px |
| Hoạ tiết `motif` | `khoi-phan-vung` (tĩnh, ở hero + `og-image`, phủ ≤ 15%) |
| Font tiêu đề | Montserrat (tự host woff2 bộ `vietnamese`, tối đa 2 độ đậm) |

Muốn đổi bất kỳ giá trị nào: PR vào tsudev-standards (sửa `hue`/`layout`... + chạy `gen-product-palette.mjs`), cổng
kiểm chống trùng phải xanh. KHÔNG ghi đè tại repo này.

## Việc cần làm - chia hai PR, mỗi PR một phiên

**PR 1 - màu + hình** (`feat(ui): bản sắc disk theo tsudev-standards v5.0.0`):
1. Sync `.standards/` lên v5.0.0 (bước `curl` sync-standards.sh trước, theo `docs/SYNC.md`).
2. Web: nạp `.standards/tokens/tokens.css` rồi `.standards/tokens/products.css`; `<html data-product="disk">` (cạnh
   `data-theme`). Desktop/di động: gộp đè `products/disk.json` lên `color.light`/`color.dark` lúc build.
3. Xoá bảng màu tự khai (`palette.css`, `extensions.*.json`, dải `--color-*` riêng) và mã màu cứng còn sót.
4. Giá trị đã lưu `warm` đọc thành `light` (khối `warm` đã gỡ khỏi token).
5. Nút dùng `var(--radius-control)`; h1-h3 và số liệu lớn dùng `var(--font-display)`.
6. Ảnh chụp trước/sau, Sáng + Tối, đính vào PR. Cổng kiểm + test xanh.

**PR 2 - bố cục** (`feat(ui): bố cục cong-cu + hoạ tiết khoi-phan-vung`):
1. Dựng phần THÂN trang chủ website / màn hình đầu app theo `cong-cu`; header, khu tài khoản, chân trang, bộ chọn
   giao diện giữ theo `ECOSYSTEM_IDENTITY.md` mục 4.
2. Hoạ tiết `khoi-phan-vung` tĩnh ở hero, vẽ bằng `accent`/`glow`; sinh lại `og-image.png` cùng hoạ tiết.
3. Ảnh chụp trước/sau, Sáng + Tối.

## Việc riêng của repo này

- Màu xanh ngọc khớp logo (hue 183). Thanh phân vùng dùng `primary`/`accent`; vùng nguy hiểm (xoá/gộp) vẫn `danger`.

## Kết quả xử lý

- 
