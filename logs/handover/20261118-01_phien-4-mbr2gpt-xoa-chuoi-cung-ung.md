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

1. **winget**: 👤 chủ dự án tạo PAT `public_repo` (tài khoản có fork `winget-pkgs`) -> secret `WINGET_TOKEN`. Agent:
   thêm bước nộp (`wingetcreate submit` hoặc `komac`, ghim SHA-256 của công cụ) dùng zip manifest đã đính kèm; khi
   winget-pkgs nhận thì thêm `winget install --id tsudev.TsuDisk -e` vào khối "Cài bằng lệnh" (website + README).
2. **VirusTotal** (DISTRIBUTION_TRUST mục 3, SHOULD): cần secret `VT_API_KEY` từ chủ dự án; Release ở nháp nếu bị gắn cờ.
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
