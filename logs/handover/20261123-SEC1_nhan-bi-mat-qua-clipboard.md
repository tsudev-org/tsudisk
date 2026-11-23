# PHIẾU YÊU CẦU SEC1 - Nhận token/secret qua clipboard (tsudisk)

- **Mã phiếu**: 20261123-SEC1
- **Thời điểm**: 23/11/2026
- **Người gửi**: agent-tsuswico (phiên tsuswico-6e) theo yêu cầu của chủ dự án
- **Phiếu điều phối (ĐỌC TRƯỚC)**: `~/projects/logs/handover/20261123-SEC1_nhan-bi-mat-qua-clipboard-toan-he.md`
- **Trạng thái**: ĐÓNG 23/11/2026 - agent-tsudisk (phiên tsudisk-78) áp dụng, xem mục 3-4

## 1. Quy tắc áp dụng ngay cho agent của repo này

- KHÔNG đề nghị chủ dự án dán token/secret vào chat, tệp hay lệnh `!`. Lỡ dán vào chat ⇒ coi là đã lộ, đề nghị thay.
- Nhận bí mật: chạy `tsu-nhan-bi-mat ...` bằng `run_in_background`, nhắn "Bấm Copy trên trang <dịch vụ> - không dán vào chat".
  Ưu tiên `--stdout | <lệnh đích>`; cần giữ ⇒ `--luu ~/.config/tsudev/<ten>.env` (chmod 600, ngoài repo).
- KHÔNG in giá trị bí mật (`cat .env`, `sed`…); chỉ in tên biến/độ dài/kết quả kiểm tra.
- Cần người dán vào dashboard ⇒ `tsu-nhan-bi-mat --dua-vao-clipboard <tệp>` rồi `--xoa-lich-su <tệp>`.
- Lịch sử Win+V trên máy đang BẬT ⇒ luôn để công cụ xoá mục khỏi lịch sử.

## 2. Việc cần làm

1. Rà tài liệu/hướng dẫn của repo (README thiết lập, `docs/`, `logs/STATE.md`, phiếu cũ còn mở) - chỗ nào bảo dán token
   vào chat/.env bằng tay ⇒ sửa theo phiếu điều phối mục 2-3.
2. Kiểm `.gitignore` có `.env*` (trừ `.env.example`); gitleaks/push protection theo SECURITY_BASELINE.
3. Liệt kê TÊN các bí mật repo dùng + nơi lưu (GitHub Actions secret / Cloudflare / Vercel / Render / `~/.config/tsudev/`)
   + phạm vi quyền vào mục 3 dưới (không ghi giá trị). Cái nào từng lộ trong chat/log ⇒ báo chủ dự án thay.
4. Ghi AKB của repo "áp dụng SEC1", đổi Trạng thái → ĐÓNG.

## 3. Bí mật của repo (agent điền - CHỈ TÊN)

| Tên | Nơi lưu | Phạm vi | Ghi chú |
| --- | --- | --- | --- |
| `WINGET_TOKEN` | GitHub Actions, Environment `packages` (chỉ thẻ `v*`) + bản giữ `~/.config/tsudev/winget.env` (chmod 600, giá trị THÔ một dòng, KHÔNG có `VAR=`) | PAT classic `public_repo`, tài khoản `nguyentrangtinhsu` - chỉ để mở PR ở microsoft/winget-pkgs | 18/11 lộ -> đã thay (STATE "Sự cố bảo mật"). 23/11 phiên 8 lỡ in tiền tố `ghp_` + 1 ký tự cuối (không đủ dùng, không cần thay). Tệp hiện HỢP LỆ: secret nạp lại 23/11 12:35 UTC, job winget 26.11.2301 + 26.11.2302 xanh |
| `VT_API_KEY` | GitHub Actions repo secret + `~/.config/tsudev/virustotal.env` | VirusTotal API gói miễn phí (quét Release) | Lộ trong transcript 17/11 - chủ dự án chấp nhận rủi ro (STATE) |
| Cloudflare (deploy website) | Không có secret trong repo: `wrangler` đăng nhập OAuth trên máy dev (`~/.config/.wrangler`) | Worker `tsudisk` + tên miền `disk.tsudev.com` | - |
| `GITHUB_TOKEN` | Tự cấp mỗi lượt Actions | theo `permissions:` từng job | - |

## 4. Kết quả rà (agent-tsudisk, 23/11/2026)

- `.gitignore` đã có `.env`, `.env.*` (trừ `*.example`/`*.sample`), `.envrc`; gitleaks + push protection có sẵn (#16).
- Đã sửa `logs/STATE.md` mục "Sự cố bảo mật": bỏ hướng dẫn `cat > tệp` (dán token vào terminal), thay bằng
  `tsu-nhan-bi-mat`.
- **Thay `WINGET_TOKEN` lần sau** (không ghi đĩa, không có tiền tố `VAR=` lọt vào secret):
  `tsu-nhan-bi-mat --tiento ghp_ --stdout | gh secret set WINGET_TOKEN -R tsudev-org/tsudisk --env packages`.
  Nếu cần giữ bản ở `winget.env` bằng `--luu`: KHÔNG dùng `--bien` (tệp thành `GITHUB_TOKEN=ghp_...`, khi nạp bằng
  `gh secret set ... < winget.env` secret sẽ chứa cả `GITHUB_TOKEN=` => job winget 401). Lệnh đề xuất trong tin nhắn
  SEC1 (`--bien GITHUB_TOKEN --luu winget.env`) vì thế KHÔNG dùng cho tsudisk.
- Không tài liệu nào của repo bảo dán token vào chat.
