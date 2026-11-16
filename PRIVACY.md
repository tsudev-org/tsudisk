# Quyền riêng tư

TsuDisk là phần mềm **ngoại tuyến**: chạy trọn vẹn trên máy của bạn, không cần Internet, không cần tài khoản.

## TsuDisk đọc gì

- Danh sách đĩa, phân vùng và ổ (qua Windows Storage Management API): số đĩa, tên thiết bị, kích thước, kiểu bảng
  phân vùng, vị trí và loại từng phân vùng, chữ cái ổ, hệ thống tệp, nhãn, dung lượng trống.
- Trạng thái Windows Recovery Environment (`reagentc /info`).

TsuDisk **không đọc nội dung tệp** của bạn. Bước quét hệ thống tệp dùng chức năng kiểm tra cấu trúc NTFS của Windows,
không mở tệp nào.

## TsuDisk ghi gì

Chỉ khi bạn chạy `tsudisk mo-rong`, vào thư mục nhật ký của lần chạy (mặc định `%ProgramData%\TsuDisk\nhat-ky`):
kế hoạch, nhật ký từng bước, bố cục phân vùng trước khi đổi, bản sao lưu BCD, và (nếu dời Recovery chứa WinRE) một
bản sao `Winre.wim`. Các tệp này chứa thông tin kỹ thuật của máy (tên thiết bị đĩa, bố cục). Xóa thư mục đó khi không
cần nữa.

## TsuDisk gửi gì đi

**Không gì cả.** Mã nguồn không có lệnh gọi mạng nào; không đo lường, không theo dõi, không tự cập nhật. Bạn tự kiểm
chứng được: mọi lệnh gọi ra ngoài tiến trình nằm trong `src/TsuDisk.Windows/` (Storage API và ba công cụ hệ thống
`reagentc`, `bcdedit`, `diskpart`).

## Website disk.tsudev.com

Trang tĩnh, không cookie, không công cụ phân tích. Lựa chọn giao diện Sáng/Tối được lưu trong trình duyệt của bạn
(`localStorage`, khóa `tsudev-theme`). Máy chủ (Cloudflare) có thể ghi nhật ký truy cập kỹ thuật như mọi trang web.
Chính sách chung của hệ sinh thái: <https://tsudev.com/privacy>.
