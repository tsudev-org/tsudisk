# ADR-0006: Chuyển đĩa Windows từ MBR sang GPT (lệnh `chuyen-gpt`)

- **Trạng thái**: Đề xuất - 18/11/2026. Chưa viết mã ghi. Chỉ chuyển sang "Chấp nhận" sau khi chủ dự án duyệt và
  kiểm thật trên máy ảo Hyper-V (mục "Kiểm thử bắt buộc").
- **Liên quan**: ADR-0002 (mô hình kế hoạch an toàn); bản 26.11.1705 (`kiem-tra-mbr2gpt`, chỉ đọc).

## Bối cảnh

Máy cài Windows ở chế độ Legacy BIOS (đĩa MBR) không bật được Secure Boot/TPM-boot của Windows 11 và kẹt giới hạn 4
phân vùng chính. Microsoft có công cụ chính thức `mbr2gpt.exe` chuyển tại chỗ, không mất dữ liệu. 26.11.1705 đã bọc
`mbr2gpt /validate` (chỉ đọc). Bước `/convert` khác hẳn các thao tác trước: ghi lại cấu trúc đầu và cuối đĩa, tạo
phân vùng EFI, sửa BCD - và máy **chỉ khởi động lại được nếu người dùng đổi firmware sang UEFI**. Không có hoàn tác.

CI hiện chỉ có ổ ảo VHDX gắn vào runner Windows - không có Windows nằm trên đĩa MBR, nên `/convert` không thể kiểm ở CI.

## Quyết định (đề xuất)

1. **Không tự viết bộ chuyển.** TsuDisk chỉ gọi `mbr2gpt.exe` của System32 (đường dẫn tuyệt đối, như 26.11.1705). Đối
   số được phép: `/validate` và `/convert`, luôn kèm `/disk:N /allowFullOS`; không bao giờ truyền `/logs` ra ổ khác hay
   tham số nào khác.
2. Bước mới `StepKind.ConvertMbrToGpt`, CHỈ hợp lệ khi `Plan.Operation` là `convert-gpt`; mỗi kế hoạch đúng một đĩa,
   và đĩa đó phải là đĩa chứa Windows đang chạy (mbr2gpt chỉ hỗ trợ đĩa hệ thống).
3. `SafetyGuard` chặn khi: đĩa không phải MBR; BitLocker đang bật trên ổ nào của đĩa mà chưa tạm dừng; đĩa báo lỗi
   sức khỏe; firmware hiện tại đã là UEFI (đĩa MBR khởi động UEFI là cấu hình lạ - dừng, không đoán); Windows chạy từ
   VHD gắn khởi động (native boot).
4. Chuỗi bước: sức khỏe -> sao lưu thô 1 MiB đầu + 1 MiB cuối đĩa và bảng phân vùng (thư mục sao lưu trên ổ khác
   nếu có, nếu không thì trên C: kèm cảnh báo) -> `mbr2gpt /validate` chạy lại ngay trước khi ghi (mã 0 mới đi tiếp)
   -> `mbr2gpt /convert` -> đối chiếu (đĩa là GPT, có đúng một phân vùng EFI, mọi phân vùng cũ còn nguyên offset và độ
   dài, chữ cái giữ nguyên). **Không có bước bù tự động**: lỗi sau `/convert` => trạng thái "cần xử lý" kèm
   `setupact.log` và hướng dẫn (bản sao lưu thô chỉ khôi phục được bằng công cụ ngoài, khởi động từ USB).
5. Xác nhận hai lớp: gõ đúng `CHUYEN GPT DIA <N>`, VÀ xác nhận đã đọc hướng dẫn đổi firmware sang UEFI của hãng máy
   (`--da-doc-huong-dan-uefi`). `--dong-y` không đủ.
6. Sau khi chuyển: in hướng dẫn vào firmware (BIOS setup) chọn UEFI, tắt CSM/Legacy, rồi bật Secure Boot nếu muốn;
   nhắc bật lại BitLocker. TsuDisk KHÔNG tự khởi động lại máy.

## Kiểm thử bắt buộc trước khi merge mã ghi

- Test đơn vị: đối số chỉ thuộc danh sách cho phép; guard từ chối từng điều kiện ở mục 3; runner dừng khi `/validate`
  lần hai trả khác 0; đối chiếu bắt được phân vùng lệch offset.
- **Hyper-V (thủ công, chủ dự án duyệt)**: VM Generation 1 cài Windows 10 22H2 và Windows 11 (bỏ qua kiểm TPM) trên
  đĩa MBR -> chạy `tsudisk chuyen-gpt` -> tắt VM -> gắn VHDX đó vào VM Generation 2 (UEFI, Secure Boot tắt) -> khởi
  động được vào Windows, `tsudisk danh-sach` báo GPT, dữ liệu mẫu trên ổ dữ liệu còn nguyên (so SHA-256). Thêm một ca
  có BitLocker bật (phải bị từ chối) và một ca 4 phân vùng chính (phải bị từ chối ở `/validate`).
- Ghi kết quả (bản Windows, mã thoát, ảnh `setupact.log`) vào `logs/` trước khi merge.

## Hệ quả

- Không có E2E tự động cho bước ghi - chấp nhận vì ta không tự viết logic chuyển; rủi ro nằm ở guard và hướng dẫn,
  cả hai test được bằng test đơn vị.
- Người dùng không đổi được firmware sang UEFI (máy quá cũ) sẽ có đĩa GPT không khởi động được: guard không phát hiện
  được điều này từ trong Windows, nên kế hoạch in rõ cảnh báo và cách kiểm firmware trước.
