# PHIẾU BÀN GIAO - phiên 11 (26/11/2026): SEC3, LG4, SYN1/PRO3, SEC2, UI2-A, phát hành 26.11.2601

- **Mã phiếu**: 20261126-01 (thay 20261124-01 - phiếu đó ĐÓNG) - **Từ**: agent-tsudisk - **Đến**: phiên kế + 👤
- **Trạng thái**: MỞ (mục 2, 3)

## 1. Đã xong
- #79 NuGet lock file + `--locked-mode` (SEC3). CodeQL #3, #4 dismiss `won't fix` có bằng chứng.
- #80 chuẩn v8.0.0; gỡ `ProNotice`; nội dung 7.5/7.6/3.7.3; `X-Tsu-App`; `/security/` (`docs/security/trust.json`);
  security.txt; theme-color, font dự phòng + preload, lề điện thoại, bảng bằng chứng. Test 391.
- Website deploy-2026.11.26.1: LCP ~1,0-1,5 s, CLS 0, Observatory A+ 150.
- #81 phát hành 26.11.2601: release xanh trừ job winget; VT 0/74; SHA-256 + attestation OK.

## 2. Việc 👤 chủ dự án
1. **Gấp**: thu hồi PAT winget (sự cố 26/11 trong STATE), tạo token classic `public_repo` mới, rồi trong terminal:
   `tsu-nhan-bi-mat --tiento ghp_ --stdout | gh secret set WINGET_TOKEN -R tsudev-org/tsudisk --env packages`
   và cập nhật `~/.config/tsudev/winget.env` bằng cùng công cụ. Xem github.com/settings/security-log.
2. `~/.config/tsudev/cloudflare.env`: token bị Cloudflare từ chối (9109) - deploy đang dùng phiên OAuth của wrangler.
3. Xoá 77 nhánh cũ (đều có PR đã gộp/đóng), nếu đồng ý:
   `git fetch --prune && git branch -r | grep -v -e main -e HEAD | sed 's#origin/##' | xargs -r git push origin --delete`
4. winget-pkgs #487246/#487347 (503 LicenseUrl) và các PR cũ: như phiếu 20261124-01 mục 2.
5. Scorecard BranchProtection/CodeReview: quyết định của chủ dự án (để mở).

## 3. Việc tiếp theo của agent (hàng đợi STATE)
1. Sau khi có token mới: nộp winget 26.11.2601 - tải `*_winget-manifest.zip` của release, giải nén, chạy
   `WINGET_TOKEN=$(< ~/.config/tsudev/winget.env) node packaging/tools/winget-submit.mjs --root <thư mục> --version 26.11.2601`
   (chạy từ main - bản ở thẻ chưa có sửa 422 nên KHÔNG chạy lại job cũ).
2. Scoop bucket `tsudisk` -> 26.11.2601 (tay như #33). Đo lại Scorecard sau lượt chấm mới, cập nhật trust.json.
3. INT1 (cổng phiên bản, cần Opus + ADR), QR4/UI1, UI2-B.
4. Cập nhật bảng phiếu gốc ở `~/projects/logs/handover/` (SEC3, LG4, SYN1, PRO3, SEC2, UI2) - dòng tsudisk.

## 4. Kết quả theo phiếu
| Phiếu | tsudisk |
| --- | --- |
| SEC3 | Cảnh báo 9 -> 7 (CodeQL 2 -> 0; Pinned chờ lượt Scorecard mới; còn tư thế). Secret 0, Dependabot 0, PR 0 |
| LG4 | Xong (#80): không bề mặt mời đăng nhập/Pro; test ca 17 ở mức CLI |
| SYN1 | Xong: không đường đám mây; không cần Tsu Link/.tsubak |
| PRO3 | Xong phần CLI/README/website; Q2 giao diện Pro chưa làm (không quảng bá) |
| SEC2 | Xong E1-E7, E10; E8 chưa có (ghi lý do) |
| UI2 | A xong; B một phần (nền Sáng theo token) |
| INT1, QR1 | Chưa làm |

## 5. Bẫy
- Lock file: phải xoá `obj` khi thử locked-mode (LES-20261126-1310-f349).
- `~/.config/tsudev/*.env` có tệp chứa token trần: không in dưới mọi hình thức.
