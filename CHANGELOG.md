# Nhật ký thay đổi

Mới nhất ở trên. Phiên bản theo khuôn `YY.M.DDNN` (`.standards/docs/VERSIONING.md` mục 2).

## [26.11.1801] - 18/11/2026

- Lệnh `ke-hoach-xoa`, `xoa-phan-vung <X:>` (bí danh `plan-delete`, `delete`): xóa một ổ dữ liệu để lấy vùng trống
  (ADR-0005). Xác nhận bằng cách gõ lại đúng `X: nhãn` (chạy tự động: `--xac-nhan "X: nhãn"`; `--dong-y` không đủ).
  Không bao giờ xóa ổ Windows/khởi động, EFI, MSR, Recovery, ổ động. Ngay trước khi xóa, TsuDisk kiểm lại chữ cái, nhãn,
  kích thước và khóa độc quyền ổ: còn chương trình nào mở tệp trên ổ thì dừng, không ép. Xóa không ghi đè dữ liệu; kế
  hoạch in sẵn cách cứu bằng TestDisk (sector bắt đầu, độ dài) nếu xóa nhầm.
- `ke-hoach-tao-phan-vung` nhắc: Windows xóa trắng phần đầu vùng trống khi tạo phân vùng - ổ vừa xóa nhầm phải cứu trước.
- Phát hành: `SHA256SUMS.txt` ký Sigstore (`SHA256SUMS.txt.sigstore.json`), SBOM SPDX đính kèm, thông tin phiên bản
  trong `tsudisk.exe` đầy đủ (nhà phát hành tsudev, mô tả, phiên bản tệp `26.11.1801.0`).
- Bộ quy ước tsudev v3.7.0.

## [26.11.1705] - 17/11/2026

- Lệnh `kiem-tra-mbr2gpt [--dia N] [--json]` (bí danh `check-mbr2gpt`): kiểm tra CHỈ ĐỌC xem đĩa Windows MBR có chuyển
  được sang GPT không - chạy `mbr2gpt /validate` (công cụ chính thức của Microsoft), đọc lý do trong
  `%windir%\setupact.log`, giải thích mã thoát và lỗi thường gặp bằng tiếng Việt (BitLocker, quá 3 phân vùng chính, ổ
  logic, thiếu chỗ cho EFI, đĩa không có Windows). TsuDisk tự kiểm thêm bố cục theo điều kiện Microsoft công bố. Mặc
  định kiểm đĩa đang chứa Windows. TsuDisk chưa tự chuyển; khi đạt, in các bước chuyển thủ công an toàn.
- Tệp phát hành đổi tên theo quy ước chung của hệ tsudev: `tsudisk_<phiên bản>_windows-x64-portable.exe`.

## [26.11.1704] - 17/11/2026

- Lệnh `ke-hoach-thu-nho`, `thu-nho`: thu nhỏ ổ NTFS để nhường vùng trống ngay sau ổ (rồi dùng `tao-phan-vung`).
  Windows tự dời dữ liệu ở cuối ổ; TsuDisk giữ ổ còn đủ chỗ trống (ổ dữ liệu min(2 GB, 25%), ổ Windows 10 GB), hỏi
  trước Windows cho thu nhỏ tới đâu và gợi ý cách gỡ khi tệp không dời được (ngủ đông, pagefile, điểm khôi phục).
  Lỗi giữa chừng thì nới ổ về cỡ cũ. Quyết định: `docs/adr/0004-thu-nho-o-tong-quat.md`.

## [26.11.1703] - 17/11/2026

- Lệnh `ke-hoach-tao-phan-vung`, `tao-phan-vung`: tạo và định dạng nhanh một ổ dữ liệu mới (NTFS, exFAT, FAT32) trong
  vùng trống, gán nhãn (nhận tiếng Việt có dấu với NTFS/exFAT) và chữ cái. Phải chỉ rõ đĩa (`--dia`); đĩa có nhiều vùng
  trống thì chọn bằng `--vung`. Ổ mới nằm trọn trong vùng trống, không chạm phân vùng có sẵn; chỉ định dạng phân vùng
  vừa tạo và còn RAW - nếu vùng trống còn dấu hệ tệp của một phân vùng đã xóa trước đây, TsuDisk không định dạng đè mà
  gỡ phân vùng vừa tạo để giữ dữ liệu cũ. Lỗi giữa chừng thì xóa đúng phân vùng vừa tạo.

## [26.11.1702] - 17/11/2026

- Lệnh `ke-hoach-doi-ten`, `doi-ten`: đổi chữ cái và/hoặc nhãn ổ dữ liệu (`--chu-cai F --nhan "Dữ liệu"`, `--nhan=` để
  xóa nhãn). Từ chối chữ cái đang dùng ở bất kỳ đâu (ổ khác, ổ mạng, subst) và gợi ý chữ cái còn trống; không bao giờ
  đổi chữ cái ổ Windows hay phân vùng EFI/Recovery; kiểm độ dài và ký tự nhãn theo hệ thống tệp. Đổi nhãn trước, chữ
  cái sau; lỗi giữa chừng thì tự gán lại chữ cái và nhãn cũ. Vị trí, kích thước, dữ liệu của mọi phân vùng giữ nguyên.

## [26.11.1701] - 17/11/2026

- Logo riêng của TsuDisk: `tsudisk.exe` có icon ứng dụng; website dùng biểu tượng mới ở đầu trang, favicon, icon
  PWA, ảnh chia sẻ, kèm logo đầy đủ ở trang chủ (AVIF/WebP, PNG dự phòng).

## [26.11.1603] - 16/11/2026

- Lệnh `ke-hoach-winre`, `mo-rong-winre`: sửa lỗi cập nhật Windows 0x80070643 (KB5034441) khi phân vùng Recovery
  thiếu 250 MB trống. Có vùng trống sau Recovery thì nới tại chỗ; không có thì nhường đúng phần thiếu từ cuối ổ đứng
  trước (tối đa 4 GB, ổ phải còn đủ trống), tạo Recovery lớn hơn và bật lại WinRE. Lỗi giữa chừng tự nới ổ về cỡ cũ và
  tạo lại Recovery cũ.

## [26.11.1602] - 16/11/2026

- Lệnh `suc-khoe`: trạng thái ổ theo Windows, nhiệt độ, độ mòn, giờ chạy, lỗi đọc/ghi không sửa được (S.M.A.R.T.).
- Mọi kế hoạch bắt đầu bằng bước kiểm tra sức khỏe: ổ có dấu hiệu hỏng thì không thay đổi bảng phân vùng.
- Lệnh `kiem-tra-gpt`: kiểm tra toàn vẹn bảng GPT đọc trực tiếp từ đĩa (protective MBR, CRC32 hai header và hai bảng
  entry, header dự phòng, chồng lấn, căn lề) và đối chiếu với những gì Windows báo.
- Lệnh `sao-luu`: sao lưu sector GPT thô đầu và cuối đĩa + bố cục + SHA256SUMS. Mỗi lần `mo-rong` cũng tự sao lưu như
  vậy và dừng nếu GPT không toàn vẹn.

## [26.11.1601] - 16/11/2026

Bản đầu tiên.

- Lệnh `danh-sach`, `phan-tich`: xem đĩa, phân vùng, vùng trống; với mỗi vùng trống, chỉ ra ổ nào gộp được và vì sao.
- Lệnh `ke-hoach`, `mo-rong`: mở rộng ổ NTFS/ReFS bằng vùng trống liền kề; tự dời phân vùng Recovery (WinRE) khi nó
  chắn giữa ổ và vùng trống, theo quy trình Microsoft KB5028997, không cần khởi động lại.
- Bất biến an toàn kiểm trên mô phỏng trước khi chạy và trên bố cục thật sau khi chạy; dấu vân tay bố cục; tự hoàn
  tác khi lỗi; nhật ký `journal.json` ghi nguyên tử; lệnh `nhat-ky` chỉ việc cần làm khi bị đứt giữa chừng.
- Chạy thử (`--chay-thu`), chế độ chỉ-ổ-ảo (`--chi-o-ao`), đầu ra JSON, mã thoát cho kịch bản tự động.
