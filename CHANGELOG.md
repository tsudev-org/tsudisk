# Nhật ký thay đổi

Mới nhất ở trên. Phiên bản theo khuôn `YY.M.DDNN` (`.standards/docs/VERSIONING.md` mục 2).

## [Chưa phát hành]

- Giao diện có nút **Tài khoản**: xem trạng thái đăng nhập (không kết nối mạng khi mở), **Đăng nhập trên trình duyệt**,
  **Mở trang tài khoản**, **Đăng xuất**, **Xóa tài khoản**. Đăng nhập (kể cả bằng mã QR từ điện thoại) diễn ra trên
  tsudev.com; TsuDisk không có ô nhập mật khẩu nào.
- Lệnh mới `tsudisk xoa-tai-khoan`: mở trang xóa tài khoản TsuDEV trên tsudev.com. `tai-khoan` in thêm trang tài khoản.
- Liên kết tới tsudev.com trong giao diện có dấu hiệu TsuDEV đứng trước.
- Bản đồ phân vùng: vùng trống có viền rõ hơn (đạt tương phản 3:1 ở cả chế độ Sáng và Tối).

## [26.11.2702] - 27/11/2026

- Bản bảo trì, không đổi tính năng: kiểm thật đường cập nhật bắt buộc của 26.11.2701 (bản 26.11.2701 đã đăng nhập sẽ
  được nhắc cập nhật lên bản này; dữ liệu vẫn xuất được bằng `xuat-du-lieu` hoặc nút **Xuất dữ liệu**).

## [26.11.2701] - 27/11/2026

- Khi đã đăng nhập: TsuDisk tạo một khoá riêng của thiết bị (trong TPM nếu máy có, khoá không xuất được) và ký mỗi
  lượt lấy quyền lợi (DPoP, RFC 9449), để quyền lợi trả về gắn với đúng máy này. `dang-xuat` xoá khoá.
- Lệnh mới `tsudisk xuat-du-lieu <thư mục>`: xuất nhật ký, bản sao lưu GPT và cài đặt giao diện ra một tệp `.zip` kèm
  `manifest.json` liệt kê SHA-256 từng tệp. Không bao giờ xuất mã đăng nhập hay khoá của thiết bị.
- Khi đã đăng nhập: trước mỗi lệnh, TsuDisk kiểm bản kê phiên bản đã ký của tsudev.com (chữ ký Ed25519). Nếu đã có bản
  mới trên kênh cài đặt của bạn, lệnh dừng với hướng dẫn cập nhật (`scoop update tsudisk`, `winget upgrade
  tsudev.TsuDisk` hoặc liên kết tải) và mã thoát 6; `phien-ban`, `tro-giup`, `dang-xuat`, `xuat-du-lieu` vẫn chạy.
  Chưa đăng nhập thì không kiểm, không kết nối mạng. Mạng lỗi không bao giờ chặn lệnh.
- Giao diện (`tsudisk-gui`): khi đã đăng nhập, kiểm bản kê lúc mở, mỗi 6 giờ và khi quay lại sau ít nhất 30 phút. Có
  bản mới thì hiện màn hình "Cần cập nhật ứng dụng" toàn màn, chỉ có hai nút: **Cập nhật ngay** (mở trang tải, hoặc
  sao chép lệnh `scoop`/`winget`, rồi đóng TsuDisk để cập nhật) và **Xuất dữ liệu** (chọn thư mục, tạo tệp `.zip` như
  lệnh `xuat-du-lieu`). Đang ghi đĩa thì chờ thao tác xong mới hiện, không bao giờ ngắt giữa chừng.
- Bản phát hành không còn nhúng ký hiệu gỡ lỗi trong hai tệp exe.
- Máy chủ tsudev.com từ chối bản cũ (`426`) cũng khoá CLI và giao diện như khi có bản mới (tự hết sau 24 giờ hoặc khi
  đã cập nhật).

## [26.11.2601] - 26/11/2026

- Không làm phiền: bỏ thông báo "tài khoản dùng trên nhiều thiết bị, nâng cấp Pro để đồng bộ"; TsuDisk không bao giờ
  tự nhắc đăng nhập hay mời mua Pro. Chỉ người đã mua Pro mới thấy nhắc gia hạn khi còn 7 ngày.
- Nội dung TsuDEV Pro theo chính sách mới: Pro là gói tự nguyện để ủng hộ tác giả, quyền lợi là huy hiệu; không còn
  đồng bộ đám mây, dữ liệu chỉ ở máy của bạn. `tsudisk tai-khoan` hiện thêm "Vì sao nên đăng nhập?".
- Khi đã đăng nhập, mỗi lượt gọi tsudev.com gửi kèm phiên bản và kênh cài đặt (`X-Tsu-App`) để máy chủ báo khi bản
  đang dùng đã cũ.
- Website: trang Bảo mật và tin cậy (`/security/`) với điểm SSL Labs, Mozilla Observatory, OpenSSF Scorecard,
  Best Practices, VirusTotal kèm liên kết tự kiểm; `security.txt` có địa chỉ security@tsudev.com.
- Bản dựng: ghim phiên bản gói NuGet bằng lock file.

## [26.11.2401] - 24/11/2026

- Giao diện cửa sổ: mở rộng ổ dữ liệu NTFS/ReFS bằng vùng trống liền kề ngay trong ứng dụng (chọn ổ, "Lập kế hoạch mở
  rộng", "Mở rộng X: ..."). Cần quyền quản trị; xác nhận hai lần: bấm nút rồi gõ lại chữ cái ổ. Dùng chung đường chạy
  với `tsudisk mo-rong`: không dời dữ liệu, kiểm lại đĩa trước mỗi bước ghi, tự hoàn tác khi lỗi; nếu phân vùng Recovery
  chứa WinRE nằm chắn thì dời Recovery theo KB5028997. Tạo lại Recovery rỗng (`--recovery tao-lai-rong`) vẫn chỉ ở
  lệnh (ADR-0011).
- Kiểm thử: mở rộng ổ trong giao diện được kiểm tự động trên ổ ảo VHDX (từ chối khi không liền kề hoặc Recovery không
  chứa WinRE, hủy, gõ sai chữ cái, gõ đúng, dữ liệu nguyên vẹn).

## [26.11.2303] - 23/11/2026

- Giao diện cửa sổ: tạo phân vùng dữ liệu mới trong vùng trống (chọn khối trống trên bản đồ, nhập kích thước MB hoặc để
  trống để dùng hết, chọn NTFS/exFAT/FAT32, nhãn, chữ cái). Cần quyền quản trị; xác nhận hai lần: bấm "Áp dụng" rồi gõ
  lại số đĩa. Dùng chung đường chạy với `tsudisk tao-phan-vung`: phân vùng có sẵn giữ nguyên, lỗi giữa chừng chỉ xoá
  phân vùng vừa tạo (ADR-0010).
- Kiểm thử: thao tác tạo phân vùng trong giao diện được kiểm tự động trên ổ ảo VHDX (hủy, gõ sai số đĩa, gõ đúng, dữ
  liệu ổ cũ nguyên vẹn).

## [26.11.2302] - 23/11/2026

- Giao diện cửa sổ: đổi chữ cái và/hoặc nhãn ổ dữ liệu ngay trong ứng dụng (cần quyền quản trị; có nút "Mở lại với
  quyền quản trị"). Phải xác nhận hai lần: bấm "Áp dụng" rồi gõ lại chữ cái ổ. Dùng chung đường chạy với lệnh
  `tsudisk doi-ten` (kiểm lại đĩa trước mỗi bước ghi, tự hoàn tác khi lỗi, nhật ký trong `%ProgramData%\TsuDisk\nhat-ky`).
  Mở rộng ổ trong giao diện vẫn chỉ xem trước kèm lệnh.
- Kiểm thử: thao tác đổi tên trong giao diện được kiểm tự động trên ổ ảo VHDX ở mỗi thay đổi mã (bấm qua giao diện
  bằng UI Automation: hủy, gõ sai, gõ đúng, dữ liệu nguyên vẹn).

## [26.11.2301] - 23/11/2026

- Giao diện cửa sổ `tsudisk-gui.exe` (bản xem trước, CHỈ ĐỌC), tệp tải riêng
  `tsudisk-gui_26.11.2301_windows-x64-portable.exe`: bản đồ đĩa, bảng phân vùng, phân tích vùng trống, xem trước kế
  hoạch mở rộng kèm lệnh để chạy bằng `tsudisk`. Không có nút áp dụng, không cần quyền quản trị. Tiếng Việt/tiếng Anh
  và Sáng/Tối theo Windows, đổi được trong ứng dụng. Được quét VirusTotal, có SHA-256 và chứng thực nguồn gốc build như
  `tsudisk.exe`.
- Kích thước trong giao diện tiếng Anh dùng dấu chấm thập phân (`63.9 GB`).

## [26.11.2202] - 22/11/2026

- Lệnh `tai-khoan`, `dang-nhap`, `dang-xuat` (bí danh `account`, `login`, `logout`): đăng nhập tài khoản TsuDEV bằng
  trình duyệt của máy (tùy chọn) để nhận quyền lợi TsuDEV Pro và nhắc gia hạn. Không đăng nhập thì TsuDisk vẫn đủ tính
  năng và không kết nối mạng lần nào. Đã đăng nhập cũng không gửi thông tin đĩa, nhật ký hay tệp nào lên mạng (xem
  `PRIVACY.md`).
- TsuDEV Pro (ADR-0007): khi gói Pro sắp hết hạn (còn ≤ 7 ngày), mỗi lần chạy lệnh TsuDisk in lời nhắc gia hạn kèm
  liên kết; hết hạn thì báo một lần. Không chặn lệnh, không in khi chạy `--json`/tự động.
- Website: màu xanh ngọc riêng của TsuDisk (bộ quy ước tsudev v5.0.0), tiêu đề dùng phông Montserrat; trang chủ đưa
  ba bước bắt đầu (tải, xem đĩa, xem kế hoạch) lên ngay màn đầu.

## [26.11.2201] - 22/11/2026

- Sửa: bộ kiểm GPT (`kiem-tra-gpt`, sao lưu GPT trước khi ghi) ném ngoại lệ thay vì báo lỗi khi header GPT trên đĩa
  khai bảng entry vô lý (số entry/kích thước entry tràn số, kích thước không phải 128 × 2^n, bảng > 1 MiB) hoặc LBA ngoài
  vùng đã đọc. Nay báo "bảng entry vô lý" và dừng như mọi GPT hỏng khác. Lỗi chỉ làm lệnh dừng sớm (trước mọi bước
  ghi), không gây mất dữ liệu. Tìm ra bằng test fuzz mới `GptFuzzTests` (đột biến trường header/entry rồi tính lại CRC).
- Tài liệu: `CONTRIBUTING.md` (tiếng Anh), tóm tắt tiếng Anh trong README và SECURITY, hồ sơ OpenSSF Best Practices
  `docs/best-practices.json`.
- Phát hành: mỗi bản được quét trên VirusTotal trước khi công bố; link báo cáo nằm trong ghi chú phát hành. Có phần
  mềm diệt virus nào gắn cờ thì bản phát hành giữ ở dạng nháp cho tới khi gỡ báo nhầm.
- Bản thử nghiệm (`-beta.N`, `-rc.N`) được đánh dấu Pre-release, không đẩy sang kho gói.

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
