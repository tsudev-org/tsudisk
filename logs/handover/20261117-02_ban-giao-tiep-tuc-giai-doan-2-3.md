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
| tsudev.com/apps | Có thẻ + lệnh Scoop (tsudev#291, #295). Logo riêng: tsudev#294 đã được chủ dự án DUYỆT, chờ phiên tsudev merge + deploy |
| Test | 89 đơn vị; E2E VHDX 32/32 (máy dev + CI Windows) |

## 2. Việc tiếp theo, theo thứ tự

### 2.1. 👤 + agent: chính sách `pull_request_target` cấp org (HẠN 02/12/2026)

Bối cảnh: GitHub chặn thử (brownout) `pull_request_target` trên repo công khai 16:00-18:00 UTC 16/11/2026, làm cổng
"Bảo vệ bản quyền" (`.github/workflows/bao-ve-ban-quyen.yml` ở scoop-bucket, linux-packages, homebrew-tap, tsudev...)
`startup_failure` và chặn mọi PR. Từ 02/12/2026 GitHub chặn vĩnh viễn trừ khi org có chính sách sự kiện Actions cho phép
(nguồn: phiên `tsudev-27`, đang soạn hướng dẫn cho chủ dự án - hỏi phiên tsudev lấy hướng dẫn trước, đừng làm lại).

Việc:
1. **Chủ dự án (agent KHÔNG làm được - token không có `admin:org`, và đây là quyết định bảo mật cấp org)**: vào
   `https://github.com/organizations/tsudev-org/settings/actions`, tạo chính sách cho phép sự kiện `pull_request_target`
   cho các repo dùng cổng (tối thiểu: scoop-bucket, linux-packages, homebrew-tap, tsudev). Đối chiếu tên mục và cách
   bật với thông báo chính thức trên GitHub Changelog trước khi bấm - không đoán.
2. **Agent kiểm sau khi bật**: mở một PR thử vô hại (vd. sửa chính tả README) ở `tsudev-org/scoop-bucket`, xác nhận
   check "Bảo vệ bản quyền" ra `success`/`failure` thật chứ không `startup_failure`:
   `gh run list -R tsudev-org/scoop-bucket --workflow 'Bảo vệ bản quyền' --limit 3`. Đóng PR thử sau đó.
3. Nếu chủ dự án KHÔNG muốn bật: đề xuất (phiếu cho repo tsudev, vì cổng là quy ước toàn hệ) chuyển cổng sang mẫu
   `pull_request` + `workflow_run`. Vùng `.github/workflows/` được bảo vệ - cần `nguyentrangtinhsu` duyệt.

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
