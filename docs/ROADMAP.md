# Lộ trình TsuDisk

> Đối chiếu với các dự án quản lý đĩa chuyên nghiệp đang có, để TsuDisk đủ tính năng mà vẫn giữ nguyên tắc "an toàn
> trước hết" (ADR-0002). Mức rủi ro: **Thấp** = chỉ đổi siêu dữ liệu/mở rộng; **Vừa** = có thể mất dữ liệu nếu sai
> phân vùng; **Cao** = di chuyển/ghi đè dữ liệu.

## 1. Dự án tham khảo và điều TsuDisk học được

| Dự án | Loại | Điều học được |
| --- | --- | --- |
| GParted / libparted | Mã nguồn mở (GPL) | Hàng đợi thao tác, xem trước rồi "Apply All"; kiểm tra hệ thống tệp trước khi đổi kích thước |
| KDE Partition Manager / kpmcore | Mã nguồn mở (GPL) | Backend tách rời (sfdisk), báo cáo chi tiết từng thao tác, hoàn tác hàng đợi trước khi áp dụng |
| gdisk / sgdisk | Mã nguồn mở (GPL) | Sao lưu/khôi phục header + bảng GPT (`--backup`/`--load-backup`), kiểm tra tính toàn vẹn GPT |
| TestDisk | Mã nguồn mở (GPL) | Quét tìm phân vùng bị mất, dựng lại bảng phân vùng |
| smartmontools, CrystalDiskInfo | Mã nguồn mở | Đọc S.M.A.R.T., cảnh báo sức khỏe ổ trước thao tác nặng |
| Clonezilla / partclone | Mã nguồn mở (GPL) | Sao chép theo khối chỉ phần đã dùng, kiểm checksum |
| Windows Disk Management, diskpart, `mbr2gpt` | Microsoft | Storage Management API, chuyển MBR sang GPT không mất dữ liệu (`/validate` trước) |
| Microsoft KB5028997 / KB5034441 | Tài liệu Microsoft | Quy trình chính thức dời/mở rộng phân vùng WinRE |
| MiniTool, AOMEI, EaseUS, DiskGenius | Thương mại | Bộ tính năng người dùng kỳ vọng: gộp/chia, dời, nhân bản, chuyển OS, xóa an toàn, chuyển MBR/GPT |

TsuDisk **không sao chép mã** của dự án GPL nào (Apache-2.0 không tương thích ngược); chỉ học hành vi đã công bố.

## 2. Giai đoạn

### Giai đoạn 1 - Nền tảng an toàn (26.11.1601, ĐÃ XONG)

- [x] Mô hình bố cục, phân tích vùng trống, gợi ý ổ hấp thu (`phan-tich`)
- [x] Mở rộng ổ bằng vùng trống liền kề, dời Recovery/WinRE theo KB5028997 (`ke-hoach`, `mo-rong`)
- [x] Mô phỏng + bất biến + dấu vân tay + kiểm lại trước từng bước + hoàn tác + nhật ký nguyên tử + `nhat-ky`
- [x] CLI tiếng Việt + bí danh tiếng Anh, JSON, mã thoát; E2E trên VHDX trong CI Windows
- [x] Website disk.tsudev.com

### Giai đoạn 2 - Giao diện và thao tác rủi ro thấp

Đã xong ở 26.11.1602: kiểm tra sức khỏe ổ (chặn ghi khi hỏng), kiểm tra toàn vẹn GPT đọc thô, sao lưu sector GPT thô.
Đã xong ở 26.11.1702: đổi chữ cái, nhãn ổ (`doi-ten`).

| Tính năng | Rủi ro | Ghi chú |
| --- | --- | --- |
| Giao diện WPF: bản đồ đĩa trực quan, hàng đợi thao tác, xem trước, áp dụng | - | Dùng chung `PlanRunner`; mọi thao tác GUI = một `Plan` |
| ~~Sao lưu bảng GPT thô (kiểu `sgdisk --backup`)~~ ĐÃ XONG; khôi phục tự động | Cao | Khôi phục chỉ khi có ADR riêng, xác nhận hai lần |
| ~~Mở rộng riêng phân vùng WinRE (lỗi 0x80070643 của KB5034441)~~ ĐÃ XONG ở 26.11.1603 | Vừa | `mo-rong-winre`, thu nhỏ có trần 4 GB |
| ~~Sức khỏe ổ: S.M.A.R.T./`MSFT_StorageReliabilityCounter`~~ ĐÃ XONG | Thấp | Chặn ghi khi ổ báo lỗi |
| ~~Đổi chữ cái ổ, nhãn ổ~~ ĐÃ XONG ở 26.11.1702 | Thấp | `doi-ten`: Storage API `AddAccessPath`/`RemoveAccessPath`, `SetFileSystemLabel` |
| Thu nhỏ ổ NTFS (`GetSupportedSize` min) | Vừa | Kiểm hệ thống tệp, chống phân mảnh không dời được |
| Tạo/định dạng phân vùng dữ liệu trong vùng trống | Vừa | Chỉ trong vùng trống, không chạm phân vùng có sẵn |
| Xóa phân vùng dữ liệu | Vừa | Xác nhận gõ tên nhãn + chữ cái; sao lưu bảng trước |
| Kiểm tra `mbr2gpt /validate` và chuyển MBR sang GPT | Vừa | Bọc công cụ chính thức, chỉ đĩa hệ thống |

### Giai đoạn 3 - Phân phối

- Bộ cài + bản di động, ký số SignPath, winget (`tsudev.TsuDisk`), Scoop (`tsudev-org/scoop-bucket`)
- Giao diện tiếng Anh; xuất báo cáo bố cục HTML
- Gói vào danh bạ tsudev.com/apps (phiếu gửi repo `tsudev`)

### Giai đoạn 4 - Thao tác rủi ro cao (mỗi mục một ADR riêng, mặc định TẮT)

| Tính năng | Điều kiện tối thiểu trước khi làm |
| --- | --- |
| Dời phân vùng (gộp vùng trống không liền kề) | Sao chép theo khối có nhật ký từng đoạn để tiếp tục sau mất điện; kiểm checksum; chỉ ổ không phải hệ thống đang chạy |
| Nhân bản đĩa/phân vùng, chuyển OS sang ổ mới | VSS snapshot; kiểm checksum; không ghi lên ổ nguồn |
| Xóa an toàn (secure erase, ghi đè) | Hai lớp xác nhận; cấm trên đĩa hệ thống |
| Khôi phục phân vùng bị mất (kiểu TestDisk) | Chỉ đọc mặc định; ghi bảng mới chỉ khi người dùng duyệt |
