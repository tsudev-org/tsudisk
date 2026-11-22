# PHIẾU YÊU CẦU AV1 - Antivirus / báo nhầm (tsudisk)

- **Mã phiếu**: 20261122-AV1
- **Thời điểm**: 22/11/2026
- **Người gửi**: agent-tsuswico (phiên tsuswico-6e) theo uỷ quyền của chủ dự án
- **Phiếu điều phối**: `~/projects/logs/handover/20261122-AV1_bao-nham-antivirus-toan-he.md` (bảng kết quả cả hệ + thư soạn sẵn từng hãng)
- **Chuẩn**: `.standards/docs/DISTRIBUTION_TRUST.md` mục 3, 3.1
- **Trạng thái**: ĐÓNG 22/11/2026

## 1. Kết quả VirusTotal 22/11/2026 - bản Latest `v26.11.2201`

| Tệp | Kết quả | Engine |
| --- | --- | --- |
| `tsudisk_26.11.2201_windows-x64-portable.exe` | 0+0/74 | - |

## 2. Việc cần làm

1. Đang sạch - ghi một dòng "22/11/2026 sạch" vào `docs/store/antivirus.md` (tạo nếu chưa có).
2. Giữ job VirusTotal trong workflow phát hành (`tsudev-org/.github/.github/workflows/virustotal.yml`); cờ mới ⇒ nộp trong ≤ 48 giờ theo phiếu điều phối.

## 3. Khép phiếu

Đổi Trạng thái → ĐÓNG khi mọi cờ đã gỡ (hoặc 👤 chấp nhận) và sổ đã ghi.

## Kết quả xử lý

- 22/11/2026 (tsudisk-59): sổ `docs/store/antivirus.md` đã tạo, ghi 26.11.2201 và 26.11.2202 (cả hai 0/74, có SHA-256 + link báo cáo) kèm hồ sơ báo nhầm soạn sẵn. Job VirusTotal giữ nguyên trong `release.yml`. ĐÓNG.
