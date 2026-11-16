# ADR-0002: Mô hình "Lập kế hoạch - Mô phỏng - Kiểm bất biến - Chạy có nhật ký - Hoàn tác"

- **Trạng thái**: Chấp nhận - 16/11/2026

## Bối cảnh

Công cụ phân vùng gây mất dữ liệu theo ba kiểu chính: (1) chạm nhầm phân vùng (chọn sai số, số thứ tự đổi giữa
chừng), (2) bị gián đoạn giữa chừng không biết đang ở đâu, (3) bố cục bị công cụ khác sửa giữa lúc xem và lúc làm.
GParted và KDE Partition Manager giải (1) bằng hàng đợi thao tác xem trước; `sgdisk` giải phần khôi phục bằng sao lưu
bảng phân vùng. TsuDisk gom các ý đó và thêm kiểm chứng tự động.

## Quyết định

1. **Kế hoạch là dữ liệu bất biến** (`Plan`), in ra và lưu JSON trước khi làm. Người dùng thấy đúng những gì sẽ chạy.
2. **Mô phỏng** (`LayoutSimulator`) ra bố cục dự kiến; **bất biến** (`SafetyGuard`) kiểm trên bố cục đó: phân vùng
   ngoài danh sách chạm giữ nguyên offset/kích thước/loại, chỉ xóa Recovery, chỉ mở rộng ổ dữ liệu, không chạm EFI/MSR,
   không chồng lấn, căn lề 1 MiB.
3. **Dấu vân tay** SHA-256 của phần hình học bố cục; khác lúc lập kế hoạch thì dừng (tương tự optimistic concurrency).
4. **Danh tính = offset.** Trước mỗi bước ghi, đọc lại bố cục và xác minh phân vùng tại offset đó đúng số, đúng loại,
   đúng kích thước như kế hoạch.
5. **Bước bù** đăng ký ngay khi một bước có tác dụng phụ thành công; lỗi thì chạy ngược. Sau khi ổ đã mở rộng, bỏ bước
   bù "tạo lại Recovery cũ" (vị trí đó nay thuộc ổ đích) - trạng thái "ổ lớn hơn, WinRE chạy từ C:" là an toàn.
6. **Nhật ký nguyên tử** sau mỗi chuyển trạng thái; `RecoveryAdvisor` diễn giải nhật ký dở dang thành việc cụ thể.
7. **Đối chiếu cuối** bố cục thật với bố cục dự kiến (sai số 1 MiB cho kích thước do Windows làm tròn).

## Hệ quả

- Mọi thao tác mới (thu nhỏ, tạo, xóa ổ dữ liệu, đổi chữ cái...) `MUST` đi qua cùng đường: thêm `StepKind`, thêm
  nhánh mô phỏng, thêm bất biến, thêm test tiêm lỗi.
- TsuDisk từ chối thay vì "cố làm" khi bố cục ngoài các mẫu đã chứng minh an toàn.
