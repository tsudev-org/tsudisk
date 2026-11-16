# Nhật ký thay đổi

Mới nhất ở trên. Phiên bản theo khuôn `YY.M.DDNN` (`.standards/docs/VERSIONING.md` mục 2).

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
