# PHIẾU BÀN GIAO - phiên 6 (22/11/2026): phát hành 26.11.2201, bản sắc v5.0.0, Pro nhắc gia hạn

- **Mã phiếu**: 20261122-01
- **Từ**: agent-tsudisk (phiên `tsudisk-59`) - **Đến**: phiên kế tiếp của `tsudisk`
- **Trạng thái**: MỞ (mục 3 là việc tiếp theo)
- **AKB**: `~/agent-kb/tsudisk/sessions/SES-20261122-2147-ccd7-*`

## 1. Đã xong

| Việc | Kết quả |
| --- | --- |
| #48 `chore(release): 26.11.2201` | 26.11.1802 chưa từng phát hành chính thức (chỉ beta.1) ⇒ gộp vào 26.11.2201 cùng sửa bộ kiểm GPT (#44). Thẻ `v26.11.2201`: build, VirusTotal (**0/74**, bảng trong Release notes), `cong-bo`, `winget` đều xanh. Đã kiểm: `sha256sum -c`, `gh attestation verify` (exe), `sigstore verify identity` SHA256SUMS - cả ba OK |
| PR winget-pkgs **#486465** | `New package: tsudev.TsuDisk version 26.11.2201`, mở bởi `nguyentrangtinhsu`, nhãn **`Needs-CLA`** - chờ chủ dự án ký CLA (mục 2) |
| #49 `feat(ui): bản sắc disk` | Phiếu STD50 **PR 1 xong**: sync `.standards` v5.0.0; website nạp `.standards/tokens/tokens.css` + `products.css`, `data-product="disk"`; xoá `tokens.css` (v2.0.0 cũ) + `palette.css`; bo góc theo token, h1-h3 Montserrat tự host (600/700, OFL). Ảnh trước/sau `docs/ui/ban-sac-v5/` |
| #50 `feat(pro)` | Phiếu PRO2 mục 2.1 + phần làm-ngay của PRO1: `TsuDisk.Core.Pro` (port tsuswico), CLI `ProHook` chỉ in, ADR-0007, test 165 -> 204 |
| Phiếu gửi tsudev | `~/projects/tsudev/logs/handover/20261122-DK1_client-oidc-tsudisk.md` - INSERT client OIDC `tsudisk` (👤 chủ dự án chạy SQL) |
| Website | deploy sau #50 (thẻ `deploy-2026.11.22.N`) |

## 2. Việc cần chủ dự án (👤)

1. **Ký CLA Microsoft cho PR winget #486465** bằng tài khoản `nguyentrangtinhsu` (là cam kết pháp lý cá nhân, agent
   không ký thay): comment đúng một dòng `@microsoft-github-policy-service agree` tại
   https://github.com/microsoft/winget-pkgs/pull/486465 . Ký một lần là đủ cho mọi PR sau.
2. Chạy INSERT ở phiếu tsudev `20261122-DK1` khi muốn mở đăng nhập TsuDEV cho TsuDisk (không gấp).

## 3. Việc tiếp theo cho agent (theo thứ tự)

1. Theo dõi PR winget #486465 sau khi có CLA: sửa theo bot (`Validation-*`). Merge xong ⇒ thêm
   `winget install --id tsudev.TsuDisk -e` vào khối "Cài bằng lệnh" README + website, ghi STATE.
   Lưu ý PACKAGING v1.2 mục 8.1 (từ v4.4.0): lệnh "Cài bằng lệnh" nên sinh lúc build từ danh mục
   (`catalog.json`), không chép tay - đối chiếu khi sửa khối này.
2. Phiếu STD50 **PR 2**: bố cục `cong-cu` (vùng thao tác chính - ô lệnh tải/chạy - ngay màn đầu, căn giữa; giới thiệu
   ở dưới) + hoạ tiết tĩnh `khoi-phan-vung` (thanh chia phân vùng, `accent`/`glow`, phủ ≤ 15%) ở hero, sinh lại
   `og-image.png` cùng hoạ tiết (`npm run brand`). Hiện hero có khối logo đầy đủ lệch trái phía trên tiêu đề - xem lại
   khi dựng bố cục. Ảnh trước/sau Sáng + Tối.
3. Pro: khi client `tsudisk` có trên tsudev.com ⇒ port đăng nhập loopback PKCE từ tsuswico (ADR-0007 "Việc còn chờ"),
   THREAT_MODEL (giả mạo quyền lợi, dùng lại JWS). Đồng bộ chờ TsuSync .NET (PRO-D3).
4. Giữ nguyên: ADR-0006 (MBR -> GPT) chờ chủ dự án duyệt; WPF bản đầu chỉ đọc; SignPath; nợ thương hiệu.

## 4. Cảnh báo

- Nhánh PR tụt sau `main` thì `gh pr merge` báo cần `--admin`: KHÔNG dùng `--admin`; gọi
  `gh api -X PUT repos/tsudev-org/tsudisk/pulls/<n>/update-branch` rồi chờ CI.
- Website import thẳng `../../../.standards/tokens/*.css` (Vite `server.fs.allow: ['..']` đã có). Sync bộ quy ước là
  đổi màu website - chạy `npm run build` + `check:a11y` sau mỗi lần sync.
- `ProHook` nuốt mọi lỗi I/O/định dạng: Pro không bao giờ được làm hỏng lệnh đĩa - giữ nguyên tắc này khi thêm đăng nhập.
