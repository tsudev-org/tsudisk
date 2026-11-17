# PHIẾU BÀN GIAO - Tiếp tục TsuDisk: giai đoạn 2 còn lại, giai đoạn 3, chính sách pull_request_target của org

- **Mã phiếu**: 20261117-02
- **Từ**: agent-tsudisk (phiên 1-2) - **Đến**: phiên agent-tsudisk kế tiếp (repo `tsudev-org/tsudisk`)
- **Thời điểm**: 17/11/2026
- **Trạng thái**: MỞ
- **Nhánh git**: main (sạch, khớp origin). Không khóa nào trong `logs/LOCKS.md`. Không tác vụ nền nào của phiên trước.
- **Ủy quyền**: chủ dự án cho phép tự động làm tới khi xong 100% kế hoạch; trao đổi 100% tiếng Việt.

## 0. Đọc trước

`AGENTS.md` -> `logs/STATE.md` -> phiếu này -> `docs/ROADMAP.md`, `docs/adr/0002-*`, `docs/adr/0003-*`, `docs/BRAND.md`.

## 1. Trạng thái hiện tại (đo 17/11/2026)

| Thứ | Trạng thái |
| --- | --- |
| Bản mới nhất | `v26.11.1701` (SHA256SUMS + attestation đã kiểm) |
| Website | https://disk.tsudev.com - deploy từ main sau mỗi merge: `cd website && GITHUB_TOKEN=$(gh auth token) npm run deploy` |
| Scoop | `scoop install tsudev/tsudisk` (scoop-bucket#15 merge; excavator tự nâng phiên bản) |
| tsudev.com/apps | Có thẻ + lệnh Scoop (tsudev#291, #295). Logo riêng: tsudev#294 phải giải xung đột với #295, lượt duyệt cũ bị hủy (dismiss stale) - chờ chủ dự án DUYỆT LẠI, phiên tsudev merge + deploy |
| Test | 89 đơn vị; E2E VHDX 32/32 (máy dev + CI Windows) |

## 2. Việc tiếp theo, theo thứ tự

### 2.1. Chính sách `pull_request_target` cấp org - ĐÃ XONG (17/11/2026), KHÔNG làm lại

Phiên `tsudev-27` báo: Actions event policy id **15243** (active) của org `tsudev-org` tạo lúc 10:15 17/11/2026, cho phép
`pull_request_target` với `.github/workflows/bao-ve-ban-quyen.yml` ở tsudev, scoop-bucket, linux-packages, homebrew-tap.
Chi tiết: `tsudev/logs/handover/20261117-01_phien-57-ket-kiem-thu-he-sinh-thai.md` mục 2.

- tsudisk KHÔNG có workflow `pull_request_target` (đã kiểm `grep` 17/11) - không cần thêm vào policy.
- Nếu sau này thêm workflow như vậy: cần chủ dự án (quyền `admin:org`) thêm repo + đường dẫn workflow vào conditions của
  policy 15243. Agent không tự làm.
- Kiểm tùy chọn: PR thử vô hại ở `tsudev-org/scoop-bucket`, check "Bảo vệ bản quyền" phải ra kết quả thật (không
  `startup_failure`).

### 2.2. Giai đoạn 2 còn lại (`docs/ROADMAP.md`), mỗi mục một PR, đúng mô hình ADR-0002

1. **Đổi chữ cái / nhãn ổ** (rủi ro thấp): `MSFT_Partition.AddAccessPath/RemoveAccessPath`, `MSFT_Volume.SetFileSystemLabel`
   qua `IDiskBackend`; `StepKind` mới + nhánh `LayoutSimulator` + bất biến `SafetyGuard` (cấm đổi chữ cái ổ hệ thống,
   cấm chữ cái đang dùng) + test tiêm lỗi + ca E2E VHDX.
2. **Tạo + định dạng phân vùng dữ liệu trong vùng trống** (rủi ro vừa): chỉ trong vùng trống, không chạm phân vùng sẵn có.
3. **Thu nhỏ ổ NTFS tổng quát** (rủi ro vừa): mở rộng ADR-0003 thành ADR mới, dùng `GetSupportedSize` min.
4. **`mbr2gpt /validate`** rồi chuyển MBR -> GPT (rủi ro vừa, chỉ đĩa hệ thống, bọc công cụ chính thức).
5. **Giao diện WPF** (lớn): bản đồ đĩa trực quan, hàng đợi thao tác; mọi thao tác GUI = một `Plan` qua `PlanRunner`.

### 2.3. Giai đoạn 3

- Bộ cài (Inno Setup như Tsu Swico) + gói `_x64-setup.exe` trong `release.yml`.
- winget `tsudev.TsuDisk` qua `komac`: **cần chủ dự án tạo PAT `public_repo`** đặt secret `WINGET_TOKEN` cho repo tsudisk.
- Ký SignPath khi D5 của hệ sinh thái được duyệt.

### 2.4. Nợ thương hiệu (`docs/BRAND.md`)

- Biến thể biểu tượng 16px đơn giản hóa (cần người thiết kế); ảnh gốc lớn hơn 1024px hoặc SVG.

## 3. Cảnh báo (bẫy đã gặp)

- KHÔNG BAO GIỜ thử thao tác ghi trên đĩa thật; E2E VHDX cần 1 lần UAC trên máy dev (viết `.ps1` riêng rồi
  `Start-Process -Verb RunAs -File`, đừng nhồi lệnh nhiều tầng trích dẫn).
- `brand-assets.mjs` (AVIF effort 9) chạy 5-10 phút: chạy nền; đừng chờ bằng `pgrep -f` (tự khớp vòng chờ).
- Không xóa bằng glob sau `cd` (bị chặn) - `git rm` từng đường dẫn.
- `~/projects/scoop-bucket` trên máy đang ở nhánh của phiên khác - làm trên bản clone riêng ở scratchpad.
- Ghim action theo SHA **commit** (annotated tag phải dereference: `gh api repos/<o>/<r>/git/tags/<sha>`).
- main có ruleset "Bao ve main": luôn nhánh + PR, chờ 11 check (E2E Windows ~2 phút).
- Repo-local git identity đã đặt (sao từ tsuswico) - không đặt global.

## 6. Trạng thái cổng kiểm

- `./scripts/check-standards.sh`: đạt. CI main: xanh. `gitleaks`: sạch.
