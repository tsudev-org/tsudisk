# PHIẾU BÀN GIAO - Phiên 1: khởi tạo TsuDisk

- **Mã phiếu**: 20261116-01
- **Từ**: agent-tsudisk - **Đến**: phiên sau của repo tsudisk
- **Thời điểm**: 21:50 16/11/2026
- **Trạng thái**: HOÀN THÀNH
- **Nhánh git**: main (sạch)

## 1. Việc đã làm xong

- Core/Windows/CLI + 57 test đơn vị (bao phủ Core 89% dòng) + E2E VHDX (19/19 trên máy dev và CI Windows).
- Repo công khai, ruleset bảo vệ main, bảo mật GitHub; CI Linux + Windows + website + gitleaks + CodeQL + NuGet.
- Website disk.tsudev.com: Worker chuẩn hóa URL (chỉ chạy khi không khớp tệp), header bảo mật khóa bằng test.
  Đo production 16/11/2026: 301 chữ hoa/tham số theo dõi, 405 POST, 414 URL dài, 404 thật, http->https 301.
- Phát hành v26.11.1601: `tsudisk_26.11.1601_x64-portable.exe`, SHA256SUMS, chứng thực nguồn gốc build.

## 2. Việc dang dở + bước tiếp theo CỤ THỂ

- [ ] Hàng đợi trong `logs/STATE.md` (Dependabot, phiếu tsudev 20261116-D1, Giai đoạn 2 của `docs/ROADMAP.md`).
- [ ] Bắt đầu Giai đoạn 2 bằng tính năng rủi ro thấp nhất: sao lưu bảng GPT thô (`IOCTL_DISK_GET_DRIVE_LAYOUT_EX`).

## 5. Cảnh báo và quyết định quan trọng

- Không bao giờ chạy thử thao tác ghi trên đĩa thật; E2E VHDX cần một lần bấm UAC trên máy dev.
- `html_handling: auto-trailing-slash` của Cloudflare dùng 307 (không đổi được nếu không bật run_worker_first, mà bật
  thì mọi yêu cầu tốn lượt Worker - đã chọn giữ chống cạn hạn mức).
- Không bật `PublishTrimmed`/NativeAOT. Không bật CodeQL default setup (repo có workflow CodeQL riêng).
- Tài khoản wrangler trên máy dev đã đăng nhập; deploy: `cd website && GITHUB_TOKEN=$(gh auth token) npm run deploy`.

## 6. Trạng thái cổng kiểm

- `./scripts/check-standards.sh`: đạt. CI main: xanh.
