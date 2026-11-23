# ADR-0011: Giao diện áp dụng thao tác thứ ba - mở rộng ổ bằng vùng trống liền kề

- **Trạng thái**: Chấp nhận - 23/11/2026 (chủ dự án duyệt "GUI bước 4" trong phiên 10)
- **Phát hành**: 26.11.2401 - trước đó E2E GUI đạt 58/58 trên CI Windows (ổ ảo VHDX, ca G1-G10, #74).
- **Liên quan**: ADR-0009 (đường chạy chung `PlanExecution`, xác nhận hai lần, `AllowedOperations`), ADR-0010 (mẫu ADR
  bổ sung + ca E2E GUI), ADR-0002 (kế hoạch, bất biến, hoàn tác), ADR-0003 (WinRE/Recovery), `ExtendPlanner` (KB5028997).
  Bổ sung, không thay ADR-0009/0010.

## Bối cảnh

ADR-0010 "Việc tiếp theo" 1: thao tác kế tiếp theo rủi ro là mở rộng ổ. Khác hai thao tác trước, mở rộng **đổi kích
thước một phân vùng đang có dữ liệu** và ở mẫu [Đích][Recovery][Trống] còn **xoá rồi tạo lại Recovery** kèm tắt/bật
WinRE. `ExtendPlanner` đã có từ 16/11/2026 (chạy thật trên ổ C: máy dev, có ca tái hiện từng byte), CLI `mo-rong` dùng
nó; GUI từ 26.11.2301 đã lập và xem trước kế hoạch này nhưng chỉ in lệnh. Rủi ro riêng của giao diện: bấm nhầm ổ, áp
dụng kế hoạch lập khi chưa đọc được WinRE, và một nút giao diện dễ bấm hơn một lệnh gõ tay.

`ExtendPlanner` **không bao giờ dời dữ liệu**: chỉ nhận hai mẫu liền kề, mọi bố cục khác bị từ chối kèm lý do. Đây là
giới hạn có chủ đích và giữ nguyên ở GUI.

## Quyết định

1. **Cùng đường chạy**: `WindowsPlanExecutor.AllowedOperations = { rename-volume, create-partition, extend }`
   (`ExtendPlanner.Operation`). Chạy qua `PlanExecution.Run` - vân tay bố cục, kiểm lại trước mỗi bước ghi, hoàn tác,
   `SafetyGuard`, nhật ký `%ProgramData%\TsuDisk\nhat-ky`, giống `tsudisk mo-rong`.
2. **Không có tuỳ chọn riêng trên giao diện** (`ExtendForm.Request`): đúng yêu cầu mặc định của `tsudisk mo-rong X:` -
   Recovery chỉ được dời khi đang chứa WinRE đang dùng, Recovery mới 1 GiB. `--recovery tao-lai-rong` (xoá Recovery
   không chứa WinRE - có thể là phân vùng khôi phục của hãng) và `--recovery-mb` **chỉ có ở CLI**; GUI hiện lời từ chối
   `RecoveryNotWinRe` kèm gợi ý.
3. **Chỉ áp dụng khi đọc được WinRE** (`ExtendForm.CanApply`): không đọc được (reagentc lỗi) thì planner coi WinRE là tắt
   - đủ để xem trước, không đủ chắc để ghi đĩa từ giao diện. Khi đó chỉ hiện lệnh CLI kèm thông báo riêng.
4. **Xác nhận hai lần, lần hai gõ lại CHỮ CÁI ổ được mở rộng** (`ExtendForm.ConfirmationMatches`, giống từ CLI
   `mo-rong` hỏi). Hộp thoại có hai cảnh báo: mở rộng trực tiếp (vị trí bắt đầu và dữ liệu giữ nguyên) và dời Recovery
   (xoá + tạo lại Recovery, tắt/bật WinRE, ảnh WinRE lưu vào nhật ký, không tắt máy khi đang chạy).
5. **Chế độ `--chi-o-ao`** truyền xuống `ExtendRequest.VirtualDiskOnly` (trước đây GUI lập kế hoạch mở rộng không mang
   cờ này vì chỉ xem trước).
6. **E2E lái GUI** (`tests/e2e/run-e2e-gui.ps1` ca G7-G10, VHDX): mã từ chối đọc qua UIA `ItemStatus` của
   `RefusalTextBlock`. G7 ổ bị chặn bởi phân vùng khác -> `NotAdjacent`, không có nút áp dụng; G8 [NTFS][Recovery không
   chứa WinRE][Trống] -> `RecoveryNotWinRe`, không có nút; G9 [NTFS][Trống], gõ chữ cái khác / `MO RONG` -> nút tắt, Hủy ->
   không đổi, không có lần chạy mới; G10 gõ đúng -> nhật ký `extend` Succeeded, ổ lớn thêm ≥ 16 MB cùng offset, NTFS lớn
   theo, SHA-256 tệp canh gác khớp, phân vùng khác giữ nguyên, ghi được 32 MB vào ổ.

## Hệ quả

- (+) Thao tác ghi thứ ba trên GUI vẫn không có logic an toàn riêng; `ExtendForm` thuần, có test trên Linux (yêu cầu
  mặc định, cờ chỉ-ổ-ảo, từ chối Recovery không chứa WinRE, nhận biết dời Recovery, điều kiện áp dụng, lệnh, xác nhận,
  chạy chung + hoàn tác khi tiêm lỗi lúc đổi kích thước sau khi đã xoá Recovery).
- (-) Mẫu dời Recovery có WinRE thật **không** có ca E2E GUI (VHDX không mang WinRE đang dùng của máy); nó được che bởi
  test đơn vị + lần chạy thật CLI 16/11/2026 + E2E CLI. Ca GUI chỉ kiểm mở rộng trực tiếp và lời từ chối.
- (-) Người cần tạo lại Recovery rỗng phải dùng CLI - chủ ý: đó là thao tác xoá dữ liệu có thể không thuộc người dùng.

## Việc tiếp theo

1. Thao tác kế tiếp theo rủi ro (thu nhỏ, xoá phân vùng) - mỗi thao tác một ADR bổ sung + ca E2E GUI; màn Pro/Ủng hộ.
