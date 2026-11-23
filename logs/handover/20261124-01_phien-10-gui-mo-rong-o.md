# PHIẾU BÀN GIAO - phiên 10 (23-24/11/2026): GUI mở rộng ổ, phát hành 26.11.2401

- **Mã phiếu**: 20261124-01 (thay phiếu 20261123-03 - phiếu đó ĐÓNG)
- **Từ**: agent-tsudisk (phiên `tsudisk-00`) - **Đến**: phiên kế tiếp + 👤 chủ dự án
- **Trạng thái**: MỞ (mục 2)

## 1. Đã xong
- Dependabot: không có PR mở.
- #74 GUI mở rộng ổ (ADR-0011, chủ dự án duyệt "GUI bước 4"): `ExtendForm` (Core), `AllowedOperations` +extend, chỉ áp
  dụng khi đọc được WinRE, xác nhận lần hai gõ lại chữ cái, cảnh báo riêng khi dời Recovery; `--chi-o-ao` nay truyền vào
  ExtendRequest; `RefusalTextBlock` có ItemStatus = mã từ chối; nút `RefreshButton`/`PlanExtendButton` có AutomationId.
  E2E GUI 58/58 (G7 NotAdjacent, G8 RecoveryNotWinRe, G9 hủy/gõ sai, G10 mở rộng thật + SHA-256). Test 392.
- #75 phát hành **26.11.2401** (thẻ `v26.11.2401`): release.yml xanh (VT, cong-bo, winget); SHA-256 + attestation hai exe
  OK; website deploy-2026.11.24.1 (đo 200, trang Tải về có 2401); Scoop scoop-bucket#33 MERGED (tay).
- #76 MERGED: LicenseUrl winget -> `https://disk.tsudev.com/ban-quyen/`.

## 2. Việc tiếp theo
1. ~~#76~~ - XONG (MERGED 24/11).
2. 👤 winget-pkgs #487246 (2303) + #487347 (2401): `URL-Validation-Error` - validator nhận 503 từ
   github.com/tsudev-org/tsudisk/blob/main/LICENSE (URL hiện trả 200). Tài khoản tác giả `nguyentrangtinhsu` bình luận
   trên PR xin moderator chạy lại (`@wingetbot run` chỉ moderator dùng được); nếu không, PR tự đóng sau ~7 ngày và bản
   sau (đã có #76) nộp lại. Nhãn `Needs-CLA` là nhãn cũ - check `license/cla` đều pass.
3. winget #486490/#487170/#487205 chờ moderator; merge => khối "Cài bằng lệnh" (PACKAGING 8.1).
4. GUI tiếp theo rủi ro (thu nhỏ, xoá) - mỗi bước hỏi chủ dự án duyệt. Đồng bộ Pro: chờ tsusync máy khách HTTP .NET.

## 3. Bẫy
- Máy dev WSL **không có `jq`**: vòng chờ CI dùng `jq` lặp vô hạn / Monitor im lặng. Dùng `gh ... --jq`
  (LES-20261124-0055-dc0e).
- Số phiên bản theo ngày GẮN THẺ: PR phát hành mở 23/11 nhưng gộp 24/11 => đổi 2304 -> 2401 trước khi gắn thẻ.
