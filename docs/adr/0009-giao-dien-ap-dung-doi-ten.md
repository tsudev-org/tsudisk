# ADR-0009: Giao diện áp dụng thao tác đầu tiên - đổi chữ cái/nhãn ổ

- **Trạng thái**: Chấp nhận - 23/11/2026 (chủ dự án duyệt "GUI bước 2" trong phiên 8)
- **Phát hành**: 26.11.2302 - trước đó E2E GUI đạt 17/17 trên CI và trên Windows 11 máy dev chạy quyền quản trị
  (ổ ảo VHDX, chủ dự án ủy quyền 23/11/2026).
- **Liên quan**: ADR-0008 (GUI bản đầu chỉ đọc, mục "Việc tiếp theo" 2), ADR-0002 (mọi thao tác là một `Plan`,
  bất biến, hoàn tác), `RenamePlanner` (rủi ro Thấp: không đổi hình học phân vùng). Bổ sung, không thay ADR-0008.

## Bối cảnh

ADR-0008 cố ý để GUI không chạm `PlanRunner`. Bước tiếp theo là cho GUI áp dụng thao tác, bắt đầu bằng thao tác ít rủi
ro nhất: đổi chữ cái và/hoặc nhãn ổ dữ liệu - không dời, không đổi kích thước phân vùng nào, hoàn tác được trọn vẹn.
Rủi ro mới nằm ở giao diện: bấm nhầm, giao diện cũ hơn bố cục thật, chạy hai đường mã khác nhau cho CLI và GUI.

## Quyết định

1. **Một đường chạy chung** `Core/Execution/PlanExecution.Run`: tạo thư mục lần chạy, ghi `plan.json` TRƯỚC khi chạm
   đĩa, giao cho `PlanRunner` (vân tay bố cục, kiểm lại trước mỗi bước ghi, hoàn tác, `SafetyGuard.CheckActual`). CLI
   (`ShowAndRun`) chuyển sang dùng nó; GUI chỉ khác cách hỏi xác nhận và cách hiện tiến độ. Nhật ký vẫn ở
   `%ProgramData%\TsuDisk\nhat-ky`, `tsudisk nhat-ky` đọc được lần chạy từ GUI.
2. **Chỉ một thao tác được áp dụng từ GUI**: `WindowsPlanExecutor.AllowedOperations = { rename-volume }`; kế hoạch
   khác ném lỗi trước khi chạy. Mở rộng ổ vẫn chỉ xem trước + lệnh CLI.
3. **Xác nhận hai lần**: (1) bấm "Áp dụng cho X: ..." (chỉ hiện khi có kế hoạch hợp lệ); (2) hộp thoại liệt kê các
   bước, buộc gõ lại chữ cái ổ (cùng luật với CLI: `RenameForm.ConfirmationMatches`). Enter không bao giờ áp dụng;
   Esc/Hủy không đổi gì. Đổi chữ cái/nhãn trong ô nhập thì kế hoạch cũ bị xoá - không áp dụng được kế hoạch cũ.
4. **Quyền quản trị**: GUI vẫn `asInvoker`. Không phải quản trị => nút "Mở lại với quyền quản trị" (ShellExecute
   `runas`, UAC từ chối thì ở lại) và lệnh CLI tương đương; không có đường ghi nào khi không phải quản trị.
5. **Không có đường ghi ở chế độ dữ liệu mẫu/chụp màn hình**: `IPlanExecutor` là `null`, nút áp dụng không hiện.
6. **Trong lúc ghi**: mọi nút lập kế hoạch/làm mới/áp dụng bị khoá, đóng cửa sổ bị chặn kèm thông báo. Xong (thành
   công hay hoàn tác) thì đọc lại bố cục thật; kết quả hiện trạng thái từng bước (từ nhật ký), thư mục nhật ký và gợi ý
   `RecoveryAdvisor` khi hoàn tác/cần xử lý.
7. **Logic thuần trong Core, có test** (`RenameForm`: ổ đổi được, danh sách chữ cái trống, yêu cầu giữ nguyên trường
   không đổi, lệnh CLI tương đương, xác nhận; `PlanExecution`: ghi `plan.json`, báo tiến độ, hoàn tác khi tiêm lỗi).

## Hệ quả

- (+) GUI làm được việc thật đầu tiên mà không có logic an toàn riêng: mọi bất biến vẫn ở Core, dùng chung CLI.
- (+) E2E VHDX của CLI (`doi-ten`: chữ cái + nhãn, xoá nhãn, từ chối ổ hệ thống) chạy đúng đường `PlanExecution` mà GUI dùng.
- (+) *Cập nhật 23/11/2026*: E2E lái GUI `tests/e2e/run-e2e-gui.ps1` (CI Windows): GUI chạy `--chi-o-ao` (chỉ liệt kê
  ổ VHD/VHDX, kế hoạch mang `VirtualDiskOnly`), `--dia N`, `--thu-muc-nhat-ky`; điều khiển chọn theo AutomationId
  (`x:Name`), kết quả máy đọc qua `ItemStatus` của `RunResultText`. Ca: chỉ thấy ổ ảo; gõ sai/Hủy không đổi gì và không
  tạo lần chạy; gõ đúng => đổi chữ cái + nhãn, nhật ký Succeeded, dữ liệu khớp SHA-256, bản đồ đọc lại bố cục.
- (-) "Mở lại với quyền quản trị" mở tiến trình mới (mất lựa chọn đang xem, cài đặt giao diện vẫn giữ).

## Việc tiếp theo

1. ~~E2E lái GUI (UI Automation) đổi tên trên ổ ảo VHDX~~ - `tests/e2e/run-e2e-gui.ps1`.
2. Mở thêm thao tác cho GUI theo thứ tự rủi ro (tạo phân vùng trong vùng trống, rồi mở rộng) - mỗi thao tác một ADR
   bổ sung + thêm vào `AllowedOperations`.
