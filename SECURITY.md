# Chính sách bảo mật - TsuDisk

> Áp dụng cho repo `tsudev-org/tsudisk` và website <https://disk.tsudev.com>. Chuẩn kỹ thuật chung của hệ sinh thái:
> [`.standards/docs/SECURITY_BASELINE.md`](.standards/docs/SECURITY_BASELINE.md).

## 1. Phạm vi

TsuDisk chạy với **quyền quản trị** và **ghi bảng phân vùng**. Lỗi logic ở đây có thể làm mất dữ liệu, nên với dự án
này "lỗ hổng" bao gồm cả:

- Bất kỳ cách nào khiến TsuDisk thay đổi một phân vùng **ngoài** những phân vùng kế hoạch đã công bố.
- Bất kỳ cách nào vượt qua bất biến an toàn (`src/TsuDisk.Core/Safety/SafetyGuard.cs`).
- Chiếm quyền thực thi: nạp DLL/tệp chạy giả, chèn tham số vào `diskpart`/`reagentc`/`bcdedit`.
- Nhật ký hoặc bản sao lưu bị ghi vào nơi người dùng thường sửa được rồi bị đọc lại với quyền quản trị.

## 2. Phiên bản được hỗ trợ

| Phiên bản | Nhận cập nhật bảo mật |
| --- | --- |
| Bản mới nhất trên trang Releases | Có |
| Bản cũ hơn | Không - hãy dùng bản mới nhất |

## 3. Báo cáo lỗ hổng

**KHÔNG mở Issue công khai cho lỗ hổng bảo mật.**

1. Ưu tiên: **GitHub Private Vulnerability Reporting** tại
   <https://github.com/tsudev-org/tsudisk/security/advisories/new>.
2. Nếu không dùng được: gửi tới địa chỉ liên hệ công bố tại <https://tsudev.com>, tiêu đề bắt đầu bằng `[SECURITY]`.

Xin ghi: phiên bản, bố cục đĩa tái hiện (đầu ra `tsudisk danh-sach --json`, có thể che tên thiết bị), bước tái hiện,
tác động. **Đừng thử lỗ hổng trên đĩa thật có dữ liệu** - dùng ổ ảo như `tests/e2e/run-e2e.ps1`.

## 4. Cam kết phản hồi

| Mốc | Thời hạn |
| --- | --- |
| Xác nhận đã nhận | 3 ngày làm việc |
| Đánh giá mức độ | 7 ngày |
| Bản vá cho lỗi có thể gây mất dữ liệu | Ưu tiên cao nhất, công bố kèm khuyến cáo |

## 5. Biện pháp đang áp dụng

- Công cụ hệ thống gọi bằng đường dẫn tuyệt đối trong System32, `ArgumentList` tách rời, không qua shell.
- Nhật ký mặc định ở `%ProgramData%\TsuDisk` (ghi cần quyền quản trị).
- Không mã mạng, không tự cập nhật: không có kênh cập nhật để bị chiếm.
- CI: gitleaks (binary ghim checksum), CodeQL `security-extended`, kiểm gói NuGet lỗ hổng, Dependabot, E2E trên ổ ảo.
- Bản phát hành kèm `SHA256SUMS.txt` và chứng thực nguồn gốc build (GitHub Artifact Attestation).
