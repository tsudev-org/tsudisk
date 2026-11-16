# ADR-0003: Cho phép thu nhỏ ổ dữ liệu - chỉ trong thao tác nới rộng WinRE

- **Trạng thái**: Chấp nhận - 16/11/2026
- **Liên quan**: ADR-0002 (mô hình kế hoạch an toàn)

## Bối cảnh

Microsoft KB5034441: Windows Update cài bản vá WinRE thất bại với lỗi 0x80070643 khi phân vùng Recovery còn dưới 250 MB
trống. Bố cục phổ biến nhất là `[C:][Recovery]` không có vùng trống phía sau, nên cách duy nhất là lấy chỗ từ cuối C:.
ADR-0002 chỉ cho phép mở rộng, không bao giờ thu nhỏ.

## Quyết định

1. Thêm `StepKind.ShrinkPartition`, **chỉ hợp lệ khi `Plan.Operation == "grow-winre"`** - `SafetyGuard` từ chối ở mọi
   thao tác khác (có test).
2. Trần: thu nhỏ tối đa 4 GiB; ổ phải còn trống ít nhất min(2 GiB, 25% dung lượng) sau khi nhường.
3. Trước khi thu nhỏ, đọc `GetSupportedSize` (SizeMin): Windows phải xác nhận phần cuối ổ dời được; không thì dừng
   trước khi xóa gì.
4. Bước bù "nới lại ổ về cỡ cũ" đăng ký ngay sau khi thu nhỏ; chỉ bỏ khi Recovery mới đã tạo xong.
5. Có vùng trống sau Recovery thì KHÔNG thu nhỏ gì - nới Recovery tại chỗ.

## Hệ quả

- Thu nhỏ NTFS trực tuyến dùng chính API của Windows (`Resize`), không dời dữ liệu bằng mã của TsuDisk.
- E2E VHDX kiểm: ổ bị thu nhỏ đúng phần thiếu, Recovery mới nằm khít chỗ cũ, ổ khác và dữ liệu nguyên vẹn, GPT toàn vẹn.
