# ADR-0005: Xóa phân vùng dữ liệu (lệnh `xoa-phan-vung`)

- **Trạng thái**: Chấp nhận - 18/11/2026
- **Liên quan**: ADR-0002 (mô hình kế hoạch an toàn). Không đổi luật "chỉ xóa Recovery" của các thao tác cũ.
  (Chuyển MBR -> GPT, dự kiến ở phiếu 20261117-03 là ADR-0005, sẽ mang số ADR-0006.)

## Bối cảnh

Người dùng cần gỡ một ổ dữ liệu để gộp vùng trống vào ổ khác (`mo-rong`) hoặc tạo lại ổ (`tao-phan-vung`) - việc
Disk Management làm bằng "Delete Volume". Đây là thao tác có thể mất dữ liệu nếu chọn nhầm ổ, nên rủi ro nằm ở chỗ
**chọn đúng ổ** và **ổ không còn ai đang dùng**, không phải ở cơ chế (Storage API `MSFT_Partition.DeleteObject` chỉ
xóa mục trong bảng phân vùng, không ghi đè dữ liệu).

## Quyết định

1. Bước mới `StepKind.DeleteDataPartition`, CHỈ hợp lệ khi `Plan.Operation` là `delete-partition`. `DeletePartition`
   cũ giữ nguyên luật chỉ-Recovery cho mọi thao tác khác. Mỗi kế hoạch xóa đúng MỘT phân vùng.
2. Chỉ ổ dữ liệu cơ bản (GPT basic data / MBR dữ liệu) **có chữ cái** - chữ cái là cách người dùng gọi tên ổ, và là
   danh tính thứ hai cạnh offset. Cấm: ổ hệ thống/khởi động, EFI, MSR, Recovery, ổ động, Storage Spaces, Extended.
3. Xác nhận bằng cách gõ đúng `<chữ cái>: <nhãn>` (vd `E: Dữ liệu`; ổ không nhãn gõ `E:`). `--dong-y` KHÔNG đủ: chạy
   tự động phải truyền `--xac-nhan "E: Dữ liệu"` khớp từng ký tự.
4. Ngay trước khi xóa, `PlanRunner` đọc lại và kiểm offset, số phân vùng, kích thước, loại, chữ cái VÀ nhãn
   (`LayoutFingerprint` không gồm chữ cái/nhãn). Backend khóa độc quyền volume (`FSCTL_LOCK_VOLUME`) rồi gỡ gắn kết:
   còn chương trình nào mở tệp trên ổ (kể cả pagefile) thì dừng với thông báo "ổ đang được dùng" - không bao giờ
   ép gỡ, không dùng `delete partition override`.
5. Thư mục nhật ký/sao lưu không được nằm trên ổ sắp xóa (CLI kiểm), và tsudisk.exe không chạy từ ổ đó.
6. Chuỗi bước: sức khỏe -> sao lưu bảng phân vùng/GPT thô -> xóa -> đối chiếu. **Không có bước bù tự động.** Đo trên
   E2E 18/11/2026: xóa KHÔNG đụng dữ liệu (boot sector NTFS còn nguyên), nhưng tạo lại phân vùng bằng diskpart hoặc
   Storage API (`New-Partition`) đều xóa trắng đầu phân vùng mới (boot sector thành 0) - "tạo lại để khôi phục" làm mất
   luôn khả năng cứu dễ. Đối chiếu lỗi sau khi xóa => trạng thái "cần xử lý" kèm hướng dẫn TestDisk.
7. Kế hoạch in sẵn cách cứu khi xóa nhầm: KHÔNG tạo phân vùng mới vào vùng đó; dùng TestDisk ghi lại mục bảng phân
   vùng, kèm số đĩa, sector bắt đầu, độ dài, hệ tệp (sao lưu GPT thô ở bước 2 cũng giữ mục cũ).

## Hệ quả

- Thêm một nhánh `LayoutSimulator`, một luật `SafetyGuard` (có test kế hoạch sửa tay), test tiêm lỗi cho runner, E2E
  trên ổ ảo (ổ có tệp mở bị từ chối, xóa thật, đọc thô xác nhận boot sector còn nguyên).
- Hệ quả phụ cho `tao-phan-vung` (ADR không đổi luật, chỉ thêm cảnh báo): tạo phân vùng trong vùng trống làm Windows
  xóa trắng phần đầu vùng đó - kế hoạch tạo nhắc cứu ổ vừa xóa nhầm bằng TestDisk TRƯỚC khi tạo.
- Không hỗ trợ xóa ổ logic trong Extended (MBR) và ổ không chữ cái - dùng Disk Management.
