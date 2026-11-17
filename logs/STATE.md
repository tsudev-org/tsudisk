# STATE.md - Trạng thái project

> Agent đọc file này ở đầu mỗi phiên và cập nhật ở cuối mỗi phiên.
> Quy trình: `.standards/docs/AGENT_PROTOCOL.md`.

> **Phiên kế tiếp**: đọc `logs/handover/20261118-02_phien-5-virustotal-winget.md`. Chờ 👤: nạp `VT_API_KEY` +
> `WINGET_TOKEN` (env `packages`), đóng 2 cảnh báo CodeQL ở #36 rồi merge; sau đó gắn thẻ `v26.11.1802`.

## Hàng đợi task (làm từ trên xuống)

- [ ] Phiếu bàn giao đầy đủ cho phiên kế tiếp: `logs/handover/20261118-02_phien-5-virustotal-winget.md`.
- [ ] 👤 Nạp secret + merge #36 (phiếu 20261118-02 mục 2), rồi phát hành `v26.11.1802` và theo dõi PR winget-pkgs.
- [ ] Giai đoạn 2 còn lại (`docs/ROADMAP.md`): chuyển MBR -> GPT (ADR-0006)
      (ADR-0006 Đề xuất - chờ duyệt + kiểm Hyper-V Gen 1 -> Gen 2); giao diện WPF (bản đầu chỉ đọc).
- [ ] Giai đoạn 3: bộ cài, ký SignPath (D5).
- [ ] Nợ thương hiệu: biến thể 16px đơn giản hóa; ảnh gốc lớn hơn/SVG (`docs/BRAND.md`).

## Đang thực hiện

| Task | Agent | Bắt đầu |
| --- | --- | --- |

## Đã hoàn thành (mới nhất trên cùng)

- 18/11/2026 - Release qua NHÁP -> VirusTotal (workflow dùng chung) -> xuất bản; thẻ beta/rc là Pre-release - #35,
  kiểm bằng `v26.11.1802-beta.1` (xanh). Environment `packages` (chỉ thẻ `v*`). Job winget + `winget-submit.mjs` ở #36
  (mở, CI xanh, chờ đóng cảnh báo CodeQL). ADR-0006 MBR -> GPT ở trạng thái Đề xuất.

- 18/11/2026 - 26.11.1801: `xoa-phan-vung` (ADR-0005) - #31; test 151 -> 163, E2E ca 14. Phát hành theo quy trình mới
  (#30): SBOM SPDX, SHA256SUMS ký Sigstore, attestation exe + SBOM - đã kiểm cả ba. `packaging/package.json` + manifest
  winget (#32). Bộ quy ước v3.7.0 (#29, phiếu P0 đóng). Website deploy, thẻ `deploy-2026.11.18.1`.

- 17/11/2026 - 26.11.1705: `kiem-tra-mbr2gpt` (chỉ đọc, mbr2gpt /validate + setupact.log) - #27; test 150 -> 151, E2E
  ca 13 đạt trên mbr2gpt thật (mã 7 "Wrong boot partition count"). Website đã deploy (đo 200, có thẻ mới).
- 17/11/2026 - Phiếu toàn hệ 20261117-V1: tsudisk đã đọc; tên tệp đổi `tsudisk_{ver}_windows-x64-portable.exe` (#26,
  scoop-bucket#19), bản đầu tiên 26.11.1705. Chờ tsudev-standards v3.7.0 để sync.

- 17/11/2026 - 26.11.1704: `thu-nho` (thu nhỏ NTFS tổng quát, ADR-0004) - #24; E2E 63/63.
- 17/11/2026 - 26.11.1703: `tao-phan-vung` (tạo + định dạng trong vùng trống) - #23.
- 17/11/2026 - 26.11.1702: `doi-ten` (đổi chữ cái + nhãn ổ) - #22. Cả ba bản: SHA256SUMS + attestation đã kiểm,
  website đã deploy. Test đơn vị 89 -> 137.
- 17/11/2026 - tsudev#294 (logo riêng TsuDisk trên tsudev.com) đã MERGED.
- 17/11/2026 - Org `tsudev-org` có Actions event policy 15243 cho `pull_request_target` (phiên tsudev); tsudisk không cần thêm.
- 17/11/2026 - Phiếu tsudev 20261117-D3 ĐÓNG (tsudev#295): tsudev.com/apps có `scoop install tsudev/tsudisk` (đo 200).
- 17/11/2026 - Gói Scoop `tsudisk` merge (scoop-bucket#15); trang Tải về + README có khối cài bằng Scoop.
- 17/11/2026 - 26.11.1701: logo riêng TsuDisk (một ảnh gốc, favicon/PWA/og/icon exe/AVIF+WebP), `docs/BRAND.md`.
- 16/11/2026 - 26.11.1603: `mo-rong-winre` sửa lỗi 0x80070643 (KB5034441), ADR-0003; E2E VHDX 32/32.
- 16/11/2026 - 26.11.1602: `suc-khoe` (chặn ghi khi ổ hỏng), `kiem-tra-gpt`, `sao-luu` GPT thô.
- 16/11/2026 - Gộp 8 PR Dependabot (#13), nội dung website khớp đầu ra thật + test đối chiếu (#12).
- 16/11/2026 - Phiếu tsudev 20261116-D1 ĐÓNG: tsudev#291 thêm thẻ TsuDisk vào tsudev.com/apps (đo 200, có thẻ).
- 16/11/2026 - Repo `tsudev-org/tsudisk` công khai, ruleset "Bao ve main" (PR + 6 check bắt buộc, 0 bypass), secret
  scanning + push protection + private vulnerability reporting + Dependabot. Website https://disk.tsudev.com (Worker
  chuẩn hóa URL #7). Phát hành v26.11.1601 (SHA256SUMS + attestation đã kiểm chứng).
- 16/11/2026 - Khởi tạo repo: Core (lập kế hoạch, mô phỏng, bất biến, chạy + hoàn tác), Windows adapter, CLI,
  57 test đơn vị (bao phủ Core 89% dòng), E2E VHDX 19/19 trên Windows thật, CI Linux + Windows, website.

## Quyết định quan trọng

> Quyết định kiến trúc lớn thì viết ADR riêng theo `docs/templates/ADR.md` và chỉ
> ghi một dòng tham chiếu ở đây.

- 16/11/2026 - C# / .NET 10 + Storage Management API: `docs/adr/0001-chon-nen-tang.md`
- 16/11/2026 - Mô hình kế hoạch an toàn: `docs/adr/0002-mo-hinh-ke-hoach-an-toan.md`
- 16/11/2026 - Thu nhỏ chỉ cho WinRE: `docs/adr/0003-thu-nho-chi-cho-winre.md`
- 17/11/2026 - Thu nhỏ NTFS tổng quát (mở rộng ADR-0003): `docs/adr/0004-thu-nho-o-tong-quat.md`
- 18/11/2026 - Xóa phân vùng dữ liệu, không tự tạo lại (Windows xóa trắng đầu phân vùng mới): `docs/adr/0005-xoa-phan-vung-du-lieu.md`
- 18/11/2026 - (Đề xuất) Chuyển MBR -> GPT bằng mbr2gpt chính thức, không tự viết bộ chuyển: `docs/adr/0006-chuyen-mbr-sang-gpt.md`

## Sự cố bảo mật

> Ghi theo `.standards/docs/SECURITY_BASELINE.md` mục 9. Để trống nếu chưa có.

- (chưa có)
