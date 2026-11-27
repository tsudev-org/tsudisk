# ADR-0013: `quet-rac` - quét rác CHỈ BÁO CÁO, chính sách từ chối thay vì đoán

- **Trạng thái**: Chấp nhận - 27/11/2026

## Bối cảnh

Đợt rà quét máy ngày 27/11/2026 (ổ C còn 25 GB/326 GB) cho thấy việc "dọn rác" dễ xóa nhầm hơn là dọn thiếu:
- 190 GB trên C là `ext4.vhdx` (ổ ảo WSL). Dọn rác bên trong Ubuntu KHÔNG trả dung lượng cho C nếu không co VHDX
  (AKB `LES-20261126-2217-2a2b`); xóa tệp này là mất cả hệ điều hành Linux.
- Trong `%TEMP%` có tệp `swap.vhdx` bị bỏ lại lẫn tệp tạm hợp lệ; có thư mục bằng chứng của phần mềm độc hại (Infatica,
  phiếu `20261126-DD1`) mà KHÔNG được xóa như rác; có tệp token thô, kho khóa ký `.jks`, `Windows\Installer` (2,4 GB, xóa
  là hỏng gỡ cài đặt), Thùng rác (dữ liệu người dùng vừa xóa).
- Công cụ dọn rác phổ biến xóa theo tên/đuôi (`*.tmp`) hoặc theo thư mục, không biết ngữ cảnh.

## Quyết định

1. **Chỉ báo cáo.** `tsudisk quet-rac [--thu-muc DIR]` (mặc định `%TEMP%`) liệt kê và tính dung lượng; KHÔNG có đường xóa
   nào trong Core hay CLI. Test `Cleanup_code_has_no_delete_path` quét mã nguồn tìm `File.Delete`, `Directory.Delete`,
   `.Delete(`, `Remove-Item`... Thêm đường xóa sau này `MUST` có ADR riêng + xác nhận gõ lại (như `xoa-phan-vung`).
2. **Chính sách thuần** `JunkPolicy.Assess` (Core, không chạm đĩa) trả một trong bốn kết luận, theo thứ tự kiểm:
   `Protected` (danh sách bảo vệ, thắng mọi điều kiện khác) -> `Unknown` (không nằm dưới vùng rác đã biết: TsuDisk không
   đoán) -> `TooRecent` (< 3 ngày hoặc đồng hồ lệch tương lai: có thể đang dùng) -> `Junk`.
3. **Danh sách bảo vệ** (gồm: thư mục người dùng, `$Recycle.Bin`, `Windows`/`Installer`/`Program Files`, `.git`/`.ssh`/
   `.gnupg`/`.secrets`, `wsl`, đuôi khóa/đĩa ảo `.pem .key .pfx .jks .vhdx ...`, tên chứa `token/secret/credential/
   .env/id_rsa...`, và bằng chứng sự cố `infatica/lrpjava/precipitator`). Mơ hồ thì bảo vệ (dương tính giả an toàn hơn
   âm tính giả).
4. **Vùng rác đã biết** là danh sách trắng ngắn: `%TEMP%`, `Windows\Temp`, `SoftwareDistribution\Download`, `CrashDumps`,
   `D3DSCache`, `npm-cache`, `INetCache`, `~/.cache`, `~/.npm/_cacache`. Gốc rác không tính chính nó, chỉ tính mục bên trong.
5. Thùng rác, Prefetch, `Windows\Installer`, `ext4.vhdx`: không bao giờ là `Junk`. Co VHDX là việc của chủ dự án (phiếu DD1
   mục 3), không phải của TsuDisk.

## Hệ quả

- Báo cáo nói rõ lý do từng mục; người dùng tự xóa bằng công cụ của Windows. Không có chế độ "tự dọn".
- Thêm vùng rác hoặc nới danh sách bảo vệ `MUST` kèm ca test trong `JunkPolicyTests` (mỗi ca là một bài học thật).
- Không `--json` ở bản này (chưa có người dùng máy đọc); thêm sau thì qua `TsuDiskJson` như các lệnh khác.
