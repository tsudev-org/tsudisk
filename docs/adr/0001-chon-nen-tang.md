# ADR-0001: Nền tảng C# / .NET 10 và Storage Management API

- **Trạng thái**: Chấp nhận - 16/11/2026
- **Người quyết định**: agent-tsudisk, theo ủy quyền của chủ dự án ("chọn phương án tối ưu, chuyên nghiệp nhất")

## Bối cảnh

TsuDisk ra đời từ một lần mở rộng ổ C: thật (16/11/2026) bằng script PowerShell: phân vùng Recovery 917 MB chắn giữa
C: và 60 GB trống. Script chạy tốt nhưng: không test được logic, không tái sử dụng, không có mô hình hoàn tác chung.
Cần một công cụ đóng gói được, kiểm thử được, mở rộng được sang nhiều thao tác phân vùng khác.

## Quyết định

1. **C# / .NET 10 LTS** (mức ⭐ cho ứng dụng Windows ở `LANGUAGE_SELECTION.md`), một tệp tự chứa `tsudisk.exe`.
2. **Windows Storage Management API** (WMI `root/Microsoft/Windows/Storage`: `MSFT_Disk`, `MSFT_Partition`,
   `MSFT_Volume`) cho đọc bố cục, `GetSupportedSize`, `Resize`, `DeleteObject`, `Repair -Scan`. Đây là API mà
   Disk Management và các lệnh `Get-Partition`/`Resize-Partition` dùng - đã được Microsoft kiểm chứng ở quy mô lớn.
3. **diskpart** chỉ cho đúng một việc API trên không làm được: đặt thuộc tính GPT `0x8000000000000001` cho Recovery.
   Kịch bản sinh thuần trong Core, có test từng dòng.
4. **reagentc / bcdedit** cho WinRE và sao lưu BCD - công cụ chính thức, không có API công khai tương đương.

## Hệ quả

- Logic test trên Linux; phần chạm hệ điều hành kiểm bằng E2E trên ổ ảo VHDX (máy dev + CI Windows).
- Không dùng VDS (lỗi thời) hay IOCTL ghi bảng phân vùng trực tiếp (bỏ qua kiểm tra của hệ thống).
- Thao tác cần dời dữ liệu (move partition) KHÔNG nằm trong phạm vi Storage API - nếu làm ở giai đoạn sau, phải là
  một mô-đun riêng, có ADR riêng và cổng an toàn riêng (`docs/ROADMAP.md`).
