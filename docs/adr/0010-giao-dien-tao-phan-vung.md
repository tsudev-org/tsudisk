# ADR-0010: Giao diện áp dụng thao tác thứ hai - tạo phân vùng trong vùng trống

- **Trạng thái**: Chấp nhận - 23/11/2026 (chủ dự án duyệt "GUI bước 3" trong phiên 9)
- **Phát hành**: 26.11.2303 - trước đó E2E GUI đạt 35/35 trên CI Windows (ổ ảo VHDX, ca G1-G6, #70).
- **Liên quan**: ADR-0009 (đường chạy chung `PlanExecution`, xác nhận hai lần, `AllowedOperations`), ADR-0002 (mọi thao
  tác là một `Plan`, bất biến, hoàn tác), `CreatePartitionPlanner` (rủi ro Vừa: ghi bảng phân vùng + định dạng, nhưng chỉ
  trong vùng trống). Bổ sung, không thay ADR-0009.

## Bối cảnh

ADR-0009 mục "Việc tiếp theo" 2: mở thao tác cho GUI theo thứ tự rủi ro. Sau đổi tên (không đổi hình học), thao tác kế
tiếp là tạo phân vùng trong vùng trống: có ghi bảng phân vùng và định dạng, nhưng không dời/thu/xoá phân vùng nào có
sẵn; lỗi giữa chừng thì `PlanRunner` chỉ xoá đúng phân vùng vừa tạo (đã có test tiêm lỗi + E2E CLI ca `tao-phan-vung`).
Rủi ro riêng của giao diện: chọn nhầm đĩa (vùng trống không có chữ cái để gõ lại), nhập kích thước sai, ghi đè chỗ của
một ổ vừa xoá nhầm (Windows xoá trắng đầu vùng trống khi tạo).

## Quyết định

1. **Cùng đường chạy**: `WindowsPlanExecutor.AllowedOperations = { rename-volume, create-partition }`. GUI lập kế hoạch
   bằng `CreatePartitionPlanner.Plan` (cùng bất biến với `tsudisk tao-phan-vung`), chạy qua `PlanExecution.Run`.
2. **Chỉ hiện khi chọn một vùng trống dùng được**: `CreateForm.ExtentFor` ánh xạ khối trống đang chọn sang
   `UsableExtent` (cùng số `--vung` với CLI, đã căn lề 1 MiB, ≥ 64 MB). Ô nhập: kích thước MB (trống = dùng hết), hệ tệp
   (NTFS/exFAT/FAT32), nhãn, chữ cái (D-Z còn trống, mặc định chữ đầu - giống CLI). Kích thước sai định dạng bị từ chối
   ngay trên giao diện; mọi giới hạn khác (tối thiểu, FAT32 ≤ 32 GB, MBR đủ 4 phân vùng, ổ động...) do planner từ chối
   kèm lý do.
3. **Xác nhận hai lần, lần hai gõ lại SỐ ĐĨA** (`CreateForm.ConfirmationMatches`): chữ cái mới chưa tồn tại nên không
   xác nhận được gì về chỗ sắp ghi; số đĩa thì khớp danh sách đĩa trên màn hình và `--dia` của CLI. Hộp thoại có cảnh
   báo riêng (phân vùng có sẵn giữ nguyên; nếu vùng trống là chỗ của ổ vừa xoá nhầm thì Hủy và cứu bằng TestDisk trước).
4. **Lệnh CLI tương đương** luôn hiện (`tsudisk tao-phan-vung --dia N --vung K ...`) - chế độ dữ liệu mẫu và khi không
   phải quản trị thì đó là đường duy nhất (giữ ADR-0009 mục 4-5).
5. **Đổi bất kỳ ô nhập nào xoá kế hoạch cũ**; nút áp dụng dùng chung (AutomationId `ApplyPlanButton`, trước là
   `ApplyRenameButton`).
6. **E2E lái GUI** (`tests/e2e/run-e2e-gui.ps1` ca G5-G6): khối bản đồ có `ItemStatus` = vai trò (`Free`/`Data`...) để
   chọn vùng trống ở mọi ngôn ngữ; gõ `TAO`/số đĩa khác => nút tắt, Hủy => không có phân vùng/lần chạy mới; gõ đúng =>
   exFAT 200 MB nhãn "GUI TAO" trong vùng trống, phân vùng cũ giữ nguyên offset/kích thước, SHA-256 tệp canh gác khớp,
   ghi được tệp lên ổ mới, bản đồ đọc lại có ổ mới.

## Hệ quả

- (+) Thao tác ghi thứ hai trên GUI vẫn không có logic an toàn riêng: bất biến, hoàn tác, nhật ký ở Core dùng chung CLI.
- (+) `CreateForm` thuần, có test trên Linux (ánh xạ vùng trống = `--vung`, chữ cái, kích thước, lệnh, xác nhận, chạy
  chung + hoàn tác khi tiêm lỗi định dạng).
- (-) Chưa có ô "đơn vị GB" - nhập MB (khớp `--kich-thuoc-mb`); để trống là cách thường dùng (dùng hết vùng trống).
- (-) E2E đổi AutomationId nút áp dụng: script cũ (`ApplyRenameButton`) không chạy với bản mới.

## Việc tiếp theo

1. Thao tác kế tiếp theo rủi ro: mở rộng ổ (dời/đổi kích thước phân vùng - rủi ro Cao hơn) - ADR riêng, kèm ca E2E GUI.
