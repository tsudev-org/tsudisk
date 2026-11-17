# PHIẾU BÀN GIAO - Phiên 5: VirusTotal trong phát hành, job winget, ADR-0006

- **Mã phiếu**: 20261118-02
- **Từ**: agent-tsudisk (phiên 5, 18/11/2026) - **Đến**: phiên agent-tsudisk kế tiếp + 👤 chủ dự án (mục 2)
- **Trạng thái**: MỞ
- **Thay thế**: phiếu 20261118-01 (mọi việc còn lại chép sang đây).

## 1. Đã xong

| PR / thẻ | Nội dung |
| --- | --- |
| #35 (đã merge) | `release.yml`: Release tạo ở NHÁP -> job `virustotal` (workflow dùng chung `tsudev-org/.github` ghim `8a393eb`, quét `tsudisk_*.exe`) -> `cong-bo` xuất bản; bị gắn cờ/lỗi -> giữ nháp + `bao-loi` mở Issue. Thẻ `-beta.N`/`-rc.N`: Pre-release, `VersionSuffix` vào exe, không sinh manifest winget. VersionPrefix 26.11.1802 + CHANGELOG |
| `v26.11.1802-beta.1` | Kiểm luồng trên: build, publish, virustotal (bỏ qua - chưa có khóa), cong-bo đều xanh; Release là Pre-release, không còn nháp |
| Environment `packages` | Đã tạo, chỉ cho thẻ `v*` (chính sách deployment). CHƯA có secret |
| #36 (MỞ, CI xanh) | `packaging/tools/winget-submit.mjs` (nộp qua GitHub REST API: fork nếu chưa có, đồng bộ upstream, một commit, PR New package/New version, bỏ qua khi đã có PR; `--dry-run`), job `winget` (needs `cong-bo`, Environment `packages`, không chạy thẻ thử). Manifest 1.12.0 + `PublisherSupportUrl`, `PrivacyUrl`, `Moniker` - đã kiểm bằng JSON schema 1.12.0 |
| PR phiếu này | ADR-0006 (chuyển MBR -> GPT) ở trạng thái **Đề xuất**, ROADMAP trỏ đúng số ADR |

## 2. Việc cần chủ dự án - ĐÃ XONG 18/11/2026

Đã nạp `VT_API_KEY` (repo) + `WINGET_TOKEN` mới (env `packages`, token cũ bị lộ đã xóa - xem STATE mục Sự cố); 2 cảnh
báo CodeQL đóng "Won't fix", luồng #36 đã resolve. Khóa VirusTotal không đổi được (gói miễn phí) - chấp nhận rủi ro.
Giữ lại để tham khảo:

1. **Nạp secret** (agent không được phép tự chạy; KHÔNG `cat` tệp):
   `gh secret set VT_API_KEY -R tsudev-org/tsudisk < ~/.config/tsudev/virustotal.env`
   `gh secret set WINGET_TOKEN -R tsudev-org/tsudisk --env packages < ~/.config/tsudev/winget.env`
   (`VT_API_KEY` phải là secret repo: job gọi workflow dùng chung không gắn được Environment.)
2. **#36 bị chặn bởi 2 luồng CodeQL `js/file-access-to-http`** (dòng 32, 38 của `winget-submit.mjs`): cảnh báo đúng
   về mặt kỹ thuật nhưng là chủ đích - script đọc manifest do repo sinh và gửi lên GitHub API. Agent không được phép
   tự đóng cảnh báo/luồng. Chủ dự án: đóng 2 cảnh báo (code scanning alert 3, 4 - "Won't fix") và resolve luồng, rồi
   merge #36. Cảnh báo thứ ba (`js/http-to-file-access`) đã sửa thật bằng cách bỏ ghi `GITHUB_OUTPUT`.
3. Lưu ý token winget thuộc tài khoản `nguyentrangtinhsu`, tài khoản này CHƯA có fork `winget-pkgs` (phiếu 01 ghi
   nhầm); script tự fork lần đầu. Các gói tsudev khác nộp từ fork của `tsudev-tsudev`.

## 3. Việc tiếp theo cho agent (sau mục 2)

1. Gắn thẻ `v26.11.1802` trên `main` (sau khi #36 merge). Kiểm: VirusTotal ghi bảng vào Release notes, Release xuất
   bản, job `winget` mở PR `New package: tsudev.TsuDisk version 26.11.1802` vào microsoft/winget-pkgs. Rồi kiểm
   `sha256sum -c`, `gh attestation verify`, `cosign verify-blob` như phiếu 01; deploy website (`npm run deploy`).
2. Theo dõi PR winget-pkgs: sửa theo bot/người kiểm duyệt (nhãn `Validation-*`). Khi merge: thêm
   `winget install --id tsudev.TsuDisk -e` vào khối "Cài bằng lệnh" (README + website), ghi số PR vào STATE.
3. ADR-0006: chờ chủ dự án duyệt + kiểm Hyper-V Gen 1 -> Gen 2 theo mục "Kiểm thử bắt buộc" trong ADR. Không viết
   mã ghi trước khi duyệt.
4. Giao diện WPF (lớn, chưa bắt đầu): đề xuất bản đầu CHỈ ĐỌC (bản đồ đĩa + phân tích + xem trước kế hoạch, không
   nút áp dụng) để kiểm giao diện trên Windows trước khi nối `PlanRunner`. Sau đó MSIX (phiếu P2).
5. Nợ thương hiệu, ký SignPath: giữ nguyên như STATE.

## 4. Cảnh báo

- Dependabot alert #1 (cao): `picomatch` <= 4.0.7 ReDoS trong `website/package-lock.json` (công cụ build, không
  chạy lúc người dùng truy cập); chưa có bản vá upstream - xem lại khi có.
- Release giờ luôn tạo ở NHÁP trước: nếu `cong-bo` không chạy (VirusTotal lỗi/hết giờ), Scoop và trang Tải về không
  thấy bản mới cho tới khi chạy lại job `virustotal` hoặc xuất bản tay sau khi kiểm.
- Các cảnh báo của phiếu 20261118-01 mục 3 vẫn đúng (Windows xóa trắng đầu phân vùng mới; exe một tệp tự giải nén
  thư viện native; runner Windows có lúc xếp hàng lâu).
