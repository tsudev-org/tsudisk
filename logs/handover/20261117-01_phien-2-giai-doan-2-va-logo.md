# PHIẾU BÀN GIAO - Phiên 2: giai đoạn 2 (rủi ro thấp/vừa) và logo riêng

- **Mã phiếu**: 20261117-01
- **Từ**: agent-tsudisk - **Đến**: phiên sau của repo tsudisk
- **Thời điểm**: 17/11/2026
- **Trạng thái**: HOÀN THÀNH (còn việc chờ chủ dự án ở STATE.md)
- **Nhánh git**: main (sạch)

## 1. Việc đã làm xong

- 26.11.1602: `suc-khoe`, `kiem-tra-gpt`, `sao-luu`; bước CheckHealth đầu mọi kế hoạch; Backup tự lưu GPT thô và dừng nếu
  GPT hỏng. GPT đĩa thật kiểm: toàn vẹn.
- 26.11.1603: `ke-hoach-winre`/`mo-rong-winre` (KB5034441), ShrinkPartition chỉ trong grow-winre (ADR-0003).
- 26.11.1701: logo riêng - `assets/brand/source/logo-tsudisk.png`, dây chuyền `website/scripts/brand-assets.mjs`
  (iconCrop, fullSizes, ecosystem), icon exe, `docs/BRAND.md`.
- Kiểm website bằng Chromium (9 trang x 2 viewport), sửa 3 chỗ nội dung lệch, test `DocumentationConsistencyTests`.
- 89 test đơn vị; E2E VHDX 32/32 trên máy dev và CI Windows; mọi bản phát hành kiểm SHA-256 + attestation.

## 2. Việc dang dở + bước tiếp theo

- Xem hàng đợi `logs/STATE.md`. Việc tự làm được tiếp theo: lệnh đổi chữ cái/nhãn ổ (Storage API
  `MSFT_Partition.AddAccessPath/RemoveAccessPath`, `MSFT_Volume.SetFileSystemLabel`) - rủi ro thấp, cùng mô hình Plan.

## 5. Cảnh báo

- `brand-assets.mjs` mã hóa AVIF effort 9: chạy 5-10 phút, chạy nền. Không dùng `pgrep -f` để chờ nó (tự khớp chính vòng chờ).
- Không xóa bằng glob sau `cd` (bộ kiểm an toàn chặn) - dùng `git rm` với đường dẫn cụ thể.
- Bản checkout `~/projects/scoop-bucket` đang ở nhánh của phiên khác - làm trên bản clone riêng.

## 6. Trạng thái cổng kiểm

- `./scripts/check-standards.sh`: đạt. CI main: xanh.
