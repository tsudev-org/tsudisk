# PHIẾU BÀN GIAO - Phiên 4: kiem-tra-mbr2gpt, xóa phân vùng, chuỗi cung ứng phát hành, winget

- **Mã phiếu**: 20261118-01
- **Từ**: agent-tsudisk (phiên 4, 17-18/11/2026) - **Đến**: phiên agent-tsudisk kế tiếp
- **Trạng thái**: MỞ
- **Thay thế**: phiếu 20261117-03 (mọi việc còn lại chép sang đây). Phiếu 20261118-P0 ĐÓNG (#29).
- **Ủy quyền**: chủ dự án cho phép tự làm liên tục (nhánh + PR + CI xanh rồi merge, deploy website); 100% tiếng Việt.

## 0. Đọc trước

`AGENTS.md` -> `logs/STATE.md` -> phiếu này -> `docs/adr/0005-*` -> `~/projects/logs/handover/20261117-P2_*` hàng 8.

## 1. Đã xong

| Bản / PR | Nội dung |
| --- | --- |
| #26, scoop-bucket#19 | Phiếu V1: tên tệp `tsudisk_{ver}_windows-x64-portable.exe` (Scoop autoupdate đổi cùng lúc) |
| 26.11.1705 (#27) | `kiem-tra-mbr2gpt` chỉ đọc: `mbr2gpt /validate` + `setupact.log`; E2E ca 13 trên mbr2gpt thật |
| #29 | Bộ quy ước v3.7.0 + khối "Phiên kế tiếp" ở STATE (phiếu P0) |
| #30 | Release: SBOM SPDX, `SHA256SUMS.txt.sigstore.json`, attestation exe + SBOM, kiểm thông tin exe ở CI, thẻ `deploy-*` |
| 26.11.1801 (#31) | `xoa-phan-vung` / `ke-hoach-xoa` (ADR-0005); E2E ca 14: ổ có tệp mở bị từ chối, xóa thật, đọc thô xác nhận boot sector còn nguyên |
| #32 | `packaging/package.json` + manifest winget đính kèm Release |

- 26.11.1801 đã kiểm: `sha256sum -c`, `gh attestation verify` (exe + SBOM), chữ ký Sigstore (`sigstore verify identity`),
  SBOM SPDX-2.3 265 gói, manifest winget trong zip. Website deploy, thẻ `deploy-2026.11.18.1`, trang chủ + Tải về 200.

## 2. Việc tiếp theo (mỗi mục một PR)

**Bí mật đã có (chủ dự án tạo 18/11/2026)** - mỗi tệp là MỘT dòng giá trị thô (không có dạng `TEN=giá trị`), quyền 600:

| Tệp | Nội dung | Nạp thành secret repo |
| --- | --- | --- |
| `~/.config/tsudev/winget.env` | PAT GitHub `public_repo` (tài khoản có fork `winget-pkgs`) | `WINGET_TOKEN` |
| `~/.config/tsudev/virustotal.env` | API key VirusTotal | `VT_API_KEY` |

Cách nạp (KHÔNG `cat`/`echo`/`sed` tệp - phiên 4 đã lỡ in giá trị ra transcript khi định che bằng `sed 's/=.*/'`,
vì tệp không có dấu `=`; chủ dự án sẽ thu hồi sau phase): 
`gh secret set WINGET_TOKEN -R tsudev-org/tsudisk < ~/.config/tsudev/winget.env` và
`gh secret set VT_API_KEY -R tsudev-org/tsudisk < ~/.config/tsudev/virustotal.env`; kiểm bằng `gh secret list` (chỉ hiện tên).
Cân nhắc đặt trong Environment `packages` có người duyệt (PACKAGING.md mục 7) thay vì secret repo trần.

1. **VirusTotal** (DISTRIBUTION_TRUST mục 3, một PR): trong job `publish` của `release.yml`, tạo Release ở **nháp**
   (`--draft`), gửi exe lên VirusTotal API v3 (`POST /api/v3/files`, header `x-apikey` lấy từ `secrets.VT_API_KEY`,
   không in ra log), chờ phân tích xong (`GET /api/v3/analyses/{id}`, giới hạn gói miễn phí 4 yêu cầu/phút - hỏi lại
   mỗi 20-30 giây), ghi link `https://www.virustotal.com/gui/file/<sha256>` vào Release notes. `malicious` = 0 thì
   `gh release edit --draft=false`; có engine gắn cờ thì giữ nháp + mở Issue, KHÔNG xuất bản. Thiếu secret thì bỏ qua
   bước và xuất bản như cũ (để fork/PR không gãy). Thử trên thẻ beta (`v26.11.DDNN-beta.1`, Pre-release) trước.
2. **winget** (một PR, sau mục 1): job mới `winget` (`needs: publish`, chỉ khi có `WINGET_TOKEN`, không chạy cho thẻ
   beta) trên `windows-latest`: tải `tsudisk_{ver}_winget-manifest.zip` của Release, `winget validate` thư mục manifest,
   rồi nộp bằng `wingetcreate submit --token` (tải `wingetcreate.exe` từ Release chính thức microsoft/winget-create theo
   phiên bản cố định + kiểm SHA-256 ghim trong workflow) hoặc `komac` (cũng ghim). Lần đầu là gói MỚI `tsudev.TsuDisk`:
   PR vào microsoft/winget-pkgs cần người kiểm duyệt của Microsoft - theo dõi PR, sửa theo bình luận bot. Khi được
   merge: thêm `winget install --id tsudev.TsuDisk -e` vào khối "Cài bằng lệnh" (website + README) và `channels`.
   Nộp thủ công ngay bây giờ cho bản 26.11.1801 cũng được (manifest đã đính kèm Release) - ghi số PR winget-pkgs vào STATE.
3. **Chuyển MBR -> GPT**: ADR-0006 (số 0005 đã dùng cho xóa phân vùng). CI VHDX không kiểm được bước chuyển -> cần chủ
   dự án duyệt kiểm trên máy ảo Hyper-V Gen 1. Không merge khi chưa kiểm thật.
4. **Giao diện WPF** (lớn): mọi thao tác = một `Plan` qua `PlanRunner`; planner đủ cho bản đầu. Sau đó MSIX (phiếu P2).
5. Nợ thương hiệu và ký SignPath giữ nguyên như STATE.

## 3. Cảnh báo

- **Đo thật (E2E 18/11/2026)**: tạo phân vùng bằng diskpart HOẶC Storage API (`New-Partition`) làm Windows ghi 0 lên đầu
  phân vùng mới (boot sector). Hệ quả: (1) không bao giờ "tạo lại để khôi phục" ổ đã xóa - cứu bằng TestDisk; (2) cơ chế
  "vùng trống hiện ra hệ tệp cũ" của `tao-phan-vung` (26.11.1703) gần như không xảy ra - giữ làm lớp phòng thủ, kế hoạch
  tạo giờ nhắc cứu ổ vừa xóa nhầm TRƯỚC khi tạo.

- `EnableCompressionInSingleFile` + `IncludeNativeLibrariesForSelfExtract`: exe tự giải nén thư viện native vào thư mục
  tạm lúc chạy - DISTRIBUTION_TRUST mục 3 khuyên tránh mẫu này (heuristic AV). Chưa đổi vì bỏ đi thì không còn "một
  tệp"; cân nhắc khi có bộ cài/MSIX.
- `xoa-phan-vung`: backend khóa `FSCTL_LOCK_VOLUME` rồi xóa; nếu Storage API từ chối khi đang giữ khóa thì nhả khóa và
  thử lại đúng một lần (volume đã gỡ gắn kết). Không bao giờ `delete partition override` cho ổ dữ liệu.
- Runner Windows của org có lúc xếp hàng 15-30 phút khi nhiều phiên cùng chạy CI - không phải treo.
- `npm run deploy` giờ gắn thẻ `deploy-YYYY.MM.DD.N` và đẩy lên origin; cây làm việc phải sạch.
