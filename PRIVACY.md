# Quyền riêng tư

TsuDisk là phần mềm **ngoại tuyến**: chạy trọn vẹn trên máy của bạn, không cần Internet, không cần tài khoản. Đăng
nhập tài khoản TsuDEV là tùy chọn (mục "Khi bạn đăng nhập").

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

**Khi chưa đăng nhập: không gì cả.** Không đo lường, không theo dõi, không tự cập nhật; TsuDisk không kết nối mạng lần
nào. Bạn tự kiểm chứng được: mọi lệnh gọi ra ngoài tiến trình nằm trong `src/TsuDisk.Windows/` (Storage API và các công
cụ hệ thống như `reagentc`, `bcdedit`, `diskpart`, `mbr2gpt`), mã mạng duy nhất nằm trong `src/TsuDisk.Core/Pro/` và
`src/TsuDisk.Core/Integrity/`, và chỉ chạy khi đã đăng nhập.

## Khi bạn đăng nhập (tùy chọn)

`tsudisk dang-nhap` mở trình duyệt tới tsudev.com để bạn đăng nhập tài khoản TsuDEV, nhằm nhận quyền lợi TsuDEV Pro
(huy hiệu). TsuDisk không bao giờ tự nhắc đăng nhập; chỉ khi bạn gõ lệnh này.
Sau đó TsuDisk chỉ trao đổi với tsudev.com:

- Mã đăng nhập (lưu trên máy, mã hóa bằng Windows DPAPI theo tài khoản Windows của bạn) và **mã cài đặt** ngẫu nhiên
  (không suy ra từ phần cứng) để tsudev.com đếm số thiết bị của tài khoản nhằm chống lạm dụng (không dùng để mời mua).
- Nhận về thông tin quyền lợi đã ký (gói Pro, hạn dùng, huy hiệu, số thiết bị) - làm mới tối đa mỗi ngày một lần.
- Tải bản kê phiên bản đã ký `tsudev.com/.well-known/tsudev/versions.json` (mỗi lần chạy lệnh, mở ứng dụng) để biết
  đã có bản mới chưa; không gửi gì ngoài `User-Agent` và phiên bản kèm kênh cài đặt. Giao diện tải lúc mở, mỗi 6 giờ
  và khi quay lại sau ít nhất 30 phút. Có bản mới thì TsuDisk yêu cầu cập nhật trước khi chạy tiếp (vẫn xuất được dữ
  liệu bằng `tsudisk xuat-du-lieu` hoặc nút **Xuất dữ liệu** của giao diện - tệp `.zip` chỉ lưu trên máy bạn).

TsuDisk **không** gửi thông tin đĩa, phân vùng, nhật ký hay tệp nào của bạn lên mạng, kể cả khi đã đăng nhập. Dữ liệu
tài khoản trên máy nằm ở `%LOCALAPPDATA%\tsudev\tsudisk`; `tsudisk dang-xuat` thu hồi phiên và xóa thư mục đó.

## Website disk.tsudev.com

Trang tĩnh, không cookie, không công cụ phân tích. Lựa chọn giao diện Sáng/Tối được lưu trong trình duyệt của bạn
(`localStorage`, khóa `tsudev-theme`). Máy chủ (Cloudflare) có thể ghi nhật ký truy cập kỹ thuật như mọi trang web.
Chính sách chung của hệ sinh thái: <https://tsudev.com/privacy>.
