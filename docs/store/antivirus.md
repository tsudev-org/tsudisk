# Sổ antivirus / báo nhầm - TsuDisk

> Chuẩn: `.standards/docs/DISTRIBUTION_TRUST.md` mục 3, 3.1. Mỗi bản phát hành được quét trên VirusTotal trong
> workflow `release.yml` (job dùng chung `tsudev-org/.github`); có engine gắn cờ thì Release giữ ở nháp và mở hồ sơ
> báo nhầm trong ≤ 48 giờ.

## Nhật ký

| Ngày | Tệp | SHA-256 | Kết quả VirusTotal | Engine gắn cờ | Hồ sơ nộp | Ngày gỡ |
| --- | --- | --- | --- | --- | --- | --- |
| 22/11/2026 | `tsudisk_26.11.2202_windows-x64-portable.exe` | `efed98ad9e6da43bb8415df8edc0d1abe42b1328affd1ffdc6dda38710891ac7` | 0/74 - sạch ([báo cáo](https://www.virustotal.com/gui/file/efed98ad9e6da43bb8415df8edc0d1abe42b1328affd1ffdc6dda38710891ac7)) | - | - | - |
| 22/11/2026 | `tsudisk_26.11.2201_windows-x64-portable.exe` | `5ac68c948e69a9249ad8f38a356e3b21dc26d7d233a38c8a8899b81e545cd494` | 0/74 - sạch ([báo cáo](https://www.virustotal.com/gui/file/5ac68c948e69a9249ad8f38a356e3b21dc26d7d233a38c8a8899b81e545cd494)); phiếu AV1 xác nhận | - | - | - |

## Hồ sơ báo nhầm soạn sẵn (dùng khi có cờ)

- Sản phẩm: TsuDisk - quản lý phân vùng ổ cứng Windows an toàn (CLI, mã nguồn mở Apache 2.0).
- Nguồn gốc: https://github.com/tsudev-org/tsudisk/releases - build bằng GitHub Actions từ mã công khai; kiểm bằng
  `gh attestation verify <tệp> --repo tsudev-org/tsudisk`; `SHA256SUMS.txt` ký Sigstore; SBOM SPDX đính kèm Release.
- Không nén/làm rối: .NET single-file tự chứa (không UPX/packer). Chưa có chữ ký Authenticode (chờ SignPath Foundation).
- Hành vi: chạy hoàn toàn cục bộ; đọc bố cục đĩa qua Windows Storage Management API, gọi công cụ hệ thống (`reagentc`,
  `bcdedit`, `diskpart`, `mbr2gpt`); thao tác ghi đĩa chỉ khi người dùng xác nhận. Mạng: chỉ gọi `tsudev.com` khi người
  dùng tự đăng nhập tài khoản TsuDEV (`tsudisk dang-nhap`).
- Quyền riêng tư: https://disk.tsudev.com/quyen-rieng-tu/ - Bảo mật: `SECURITY.md`.
