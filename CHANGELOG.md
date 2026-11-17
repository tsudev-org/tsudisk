# Nhật ký thay đổi

Mới nhất ở trên. Phiên bản theo khuôn `YY.M.DDNN` (`.standards/docs/VERSIONING.md` mục 2).

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
