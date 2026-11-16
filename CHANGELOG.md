# Nhật ký thay đổi

Mới nhất ở trên. Phiên bản theo khuôn `YY.M.DDNN` (`.standards/docs/VERSIONING.md` mục 2).

## [26.11.1601] - 16/11/2026

Bản đầu tiên.

- Lệnh `danh-sach`, `phan-tich`: xem đĩa, phân vùng, vùng trống; với mỗi vùng trống, chỉ ra ổ nào gộp được và vì sao.
- Lệnh `ke-hoach`, `mo-rong`: mở rộng ổ NTFS/ReFS bằng vùng trống liền kề; tự dời phân vùng Recovery (WinRE) khi nó
  chắn giữa ổ và vùng trống, theo quy trình Microsoft KB5028997, không cần khởi động lại.
- Bất biến an toàn kiểm trên mô phỏng trước khi chạy và trên bố cục thật sau khi chạy; dấu vân tay bố cục; tự hoàn
  tác khi lỗi; nhật ký `journal.json` ghi nguyên tử; lệnh `nhat-ky` chỉ việc cần làm khi bị đứt giữa chừng.
- Chạy thử (`--chay-thu`), chế độ chỉ-ổ-ảo (`--chi-o-ao`), đầu ra JSON, mã thoát cho kịch bản tự động.
