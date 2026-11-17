# ADR-0004: Thu nhỏ ổ NTFS tổng quát (lệnh `thu-nho`)

- **Trạng thái**: Chấp nhận - 17/11/2026
- **Liên quan**: ADR-0002 (mô hình kế hoạch an toàn), ADR-0003 (thu nhỏ chỉ cho WinRE) - ADR này MỞ RỘNG ADR-0003

## Bối cảnh

Người dùng cần lấy bớt dung lượng một ổ để tạo ổ mới (`tao-phan-vung`, 26.11.1703) - việc Disk Management làm bằng
"Shrink Volume". ADR-0003 chỉ cho thu nhỏ bên trong thao tác `grow-winre`, tối đa 4 GiB. Thu nhỏ NTFS trực tuyến là
thao tác của chính Windows (`MSFT_Partition.Resize`): hệ thống tệp tự dời cluster ở cuối ổ, giao dịch NTFS bảo đảm
gián đoạn không làm hỏng ổ. Rủi ro chính không nằm ở cơ chế mà ở hậu quả: ổ còn quá ít chỗ trống (nhất là ổ Windows),
hoặc người dùng đoán sai mức Windows cho phép.

## Quyết định

1. `StepKind.ShrinkPartition` hợp lệ khi `Plan.Operation` là `grow-winre` (giữ nguyên luật ADR-0003) **hoặc**
   `shrink-volume`. Mọi thao tác khác vẫn bị `SafetyGuard` từ chối.
2. `shrink-volume` chỉ cho ổ dữ liệu **NTFS** (ReFS không thu nhỏ được, FAT/exFAT Windows không hỗ trợ). Offset giữ
   nguyên; điểm cuối mới căn lề 1 MiB để vùng trống sinh ra dùng được ngay.
3. Không có trần cố định, thay bằng **chỗ trống tối thiểu sau khi thu nhỏ**: ổ dữ liệu min(2 GiB, 25% cỡ mới) (cùng
   công thức ADR-0003); ổ Windows/khởi động ít nhất 10 GiB (Windows Update cần chỗ). Không đọc được dung lượng trống thì
   từ chối. Luật này nằm trong cả planner lẫn `SafetyGuard` (có test kế hoạch sửa tay).
4. Lúc lập kế hoạch, CLI đọc `GetSupportedSize` (SizeMin) nếu được và từ chối sớm kèm gợi ý (tắt ngủ đông, tạm tắt
   pagefile, xóa điểm khôi phục) khi Windows không cho thu tới mức yêu cầu. `PlanRunner` vẫn đọc lại SizeMin ngay trước
   khi thu nhỏ (ADR-0003 mục 3).
5. Chuỗi bước: sức khỏe -> quét NTFS (chỉ đọc) -> sao lưu bảng phân vùng/GPT thô -> thu nhỏ -> đối chiếu. Bước bù "nới
   lại về cỡ cũ" như ADR-0003 mục 4.

## Hệ quả

- TsuDisk không tự dời dữ liệu; ổ có tệp không dời được ở cuối sẽ bị từ chối với lý do cụ thể thay vì "cố làm".
- E2E VHDX: thu nhỏ một ổ NTFS, dữ liệu nguyên vẹn, rồi `tao-phan-vung` vào đúng vùng trống vừa sinh ra; thu nhỏ exFAT
  bị từ chối.
