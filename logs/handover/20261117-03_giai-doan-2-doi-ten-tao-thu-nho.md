# PHIẾU BÀN GIAO - Giai đoạn 2: đổi tên, tạo phân vùng, thu nhỏ ổ đã xong; còn mbr2gpt, xóa phân vùng, WPF

- **Mã phiếu**: 20261117-03
- **Từ**: agent-tsudisk (phiên 3) - **Đến**: phiên agent-tsudisk kế tiếp (repo `tsudev-org/tsudisk`)
- **Thời điểm**: 17/11/2026
- **Trạng thái**: MỞ
- **Nhánh git**: main sạch, khớp origin. Không khóa nào trong `logs/LOCKS.md`. Không tác vụ nền nào.
- **Ủy quyền**: chủ dự án cho phép tự làm liên tục, mỗi thay đổi qua nhánh + PR + CI xanh rồi merge; 100% tiếng Việt.
- **Thay thế**: phiếu 20261117-02 (mọi việc trong đó đã xong hoặc được chép sang đây).

## 0. Đọc trước

`AGENTS.md` -> `logs/STATE.md` -> phiếu này -> `docs/ROADMAP.md`, `docs/adr/0002-*`, `0003-*`, `0004-*`.

## 1. Việc đã làm xong (phiên 3)

| Bản | PR | Nội dung |
| --- | --- | --- |
| 26.11.1702 | #22 | `ke-hoach-doi-ten` / `doi-ten X: --chu-cai Y --nhan TÊN` (`--nhan=` xóa nhãn). `StepKind.SetVolumeLabel`, `SetDriveLetter` |
| 26.11.1703 | #23 | `ke-hoach-tao-phan-vung` / `tao-phan-vung --dia N [--vung K] [--kich-thuoc-mb] [--he-tep] [--nhan] [--chu-cai]`. `StepKind.CreatePartition`, `FormatVolume` |
| 26.11.1704 | #24 | `ke-hoach-thu-nho` / `thu-nho X: --giam-mb N`, ADR-0004 (mở rộng ADR-0003) |

- Mỗi bản: CI 11/11 xanh, tag `vYY.M.DDNN` -> `release.yml`, `sha256sum -c` + `gh attestation verify` đạt, website deploy
  (`cd website && GITHUB_TOKEN=$(gh auth token) npm run deploy`), trang chủ có thẻ tính năng mới (đo 200).
- Test đơn vị 89 -> 137 (`RenameTests`, `CreatePartitionTests`, `ShrinkTests`). E2E VHDX 32 -> 63 kiểm tra; ca 11-12
  dùng ổ ảo THỨ HAI `e2e-tao.vhdx` vì ổ đầu đã hết vùng trống sau ca 5/9.
- tsudev#294 (logo riêng trên tsudev.com) đã MERGED - mục 👤 cũ đã xong.

## 2. Việc tiếp theo, theo thứ tự (mỗi mục một PR, đúng ADR-0002)

1. **`kiem-tra-mbr2gpt` (chỉ đọc)**: bọc `mbr2gpt.exe /validate /disk:N /allowFullOS`, đọc `%windir%\setupact.log`
   (mbr2gpt ghi lý do vào đó), in kết luận tiếng Việt. Không đổi đĩa -> không cần StepKind. E2E: trên VHDX dữ liệu
   kết quả mong đợi là "không đạt" (không có Windows) - kiểm mã thoát + thông điệp, KHÔNG kỳ vọng "đạt".
2. **Chuyển MBR -> GPT**: cần ADR-0005 trước (chỉ đĩa hệ thống, `/validate` đạt, sao lưu, nhắc đổi firmware sang UEFI).
   CI VHDX KHÔNG kiểm được bước chuyển (cần Windows cài trên đĩa MBR) -> cần chủ dự án duyệt cách kiểm (máy ảo Hyper-V
   Gen 1 trên máy dev). Đừng merge bước chuyển khi chưa có kiểm chứng thật.
3. **Xóa phân vùng dữ liệu** (ROADMAP: rủi ro vừa): xác nhận gõ nhãn + chữ cái, sao lưu bảng trước; `SafetyGuard` hiện
   chỉ cho `DeletePartition` với Recovery - nới phải có ADR + test kế hoạch sửa tay.
4. **Giao diện WPF** (lớn): mọi thao tác GUI = một `Plan` qua `PlanRunner`; các planner đã đủ cho bản đầu.
5. Giai đoạn 3 và nợ thương hiệu: giữ nguyên như STATE (winget cần chủ dự án tạo PAT `public_repo` -> secret `WINGET_TOKEN`).

## 3. File liên quan / đang khóa

| Đường dẫn | Lý do | Còn khóa? |
| --- | --- | --- |
| (không có) | | Không |

## 5. Cảnh báo và quyết định quan trọng

- **Danh tính = offset** vẫn là nguyên tắc: bước đổi tên không nằm trong `Plan.TouchedOffsets` (thuộc tính
  `PlanStep.IsMetadataOnly`) nên phân vùng đổi tên vẫn chịu bất biến "giữ nguyên offset/kích thước/loại".
- `LayoutFingerprint` KHÔNG gồm chữ cái/nhãn -> `PlanRunner` tự kiểm lại chữ cái trước khi ghi (`RequireRenameTarget`).
- `tao-phan-vung`: bước bù "xóa phân vùng vừa tạo" đăng ký TRƯỚC lệnh tạo; chỉ xóa khi offset không có trong bố cục cũ,
  đúng loại, đúng cỡ. Chỉ định dạng khi phân vùng mới còn RAW: nếu vùng trống còn boot sector của phân vùng đã xóa
  (Windows "hiện ra" hệ tệp cũ) thì dừng và gỡ phân vùng - không bao giờ định dạng đè.
- `--dia` bắt buộc cho `tao-phan-vung` (không tự đoán đĩa).
- PowerShell 5.1 bỏ đối số rỗng khi gọi exe: trong E2E dùng `--nhan=` để xóa nhãn. Tệp `.ps1` lưu ASCII; chuỗi tiếng
  Việt trong E2E dựng bằng `[char]0x...`.
- `Units.AlignDown/AlignUp` ném lỗi với số âm - E2E đã bắt một lần (`thu-nho --giam-mb 100000` crash). Mọi phép tính
  "cuối - mức giảm" phải chặn trước khi căn lề.
- Nhánh cục bộ `docs/ban-giao-phien-3` là tàn dư của phiên 2 (nội dung đã vào main ở #20), không có trên origin - có
  thể `git branch -D` nếu chủ dự án đồng ý; phiên 3 không xóa vì không phải của mình.
- Các bẫy cũ ở phiếu 20261117-02 mục 3 vẫn đúng (không ghi đĩa thật, UAC cho E2E máy dev, ghim action theo SHA commit,
  scoop-bucket trên máy đang ở nhánh phiên khác).

## 6. Trạng thái cổng kiểm

- `./scripts/check-standards.sh`: đạt. CI main (b36ea4d): xanh. gitleaks: sạch. Bản mới nhất `v26.11.1704`.
