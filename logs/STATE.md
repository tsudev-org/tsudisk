# STATE.md - Trạng thái project

> Agent đọc file này ở đầu mỗi phiên và cập nhật ở cuối mỗi phiên.
> Quy trình: `.standards/docs/AGENT_PROTOCOL.md`.

> **Phiên kế tiếp**: đọc `logs/handover/20261126-04_phien-14-int4-gui-modal-khoa.md`, `docs/PLAN.md`
> và `logs/handover/20261126-DD1_phoi-hop-don-dia.md`.

## Hàng đợi task (làm từ trên xuống)

- [ ] winget-pkgs#489979 (26.11.2601) chờ moderator. (PAT winget cũ đã xoá - sự cố 26/11 ĐÓNG.)
- [ ] Phase INT5 (nâng `latest` theo kênh) theo `docs/PLAN.md`, rồi INT7, QR4-UI1, UI2-B - mỗi ngữ cảnh một Phase. INT6 (DPoP) bị chặn tới khi tsudev chốt hợp đồng DPoP desktop.
- [ ] 👤 Dọn đĩa: phiếu `logs/handover/20261126-DD1_phoi-hop-don-dia.md` mục 3 (co VHDX, bản build tsudisk).
      ⛔ Infatica đang chạy - phiên tsuguard theo dõi.
- [ ] QR4 + UI1 (phiếu 20261124-QR1): đăng nhập QR, tương phản biểu tượng, cổng mã chết/ngân sách tốc độ.
- [ ] UI2 phần B: hoạ tiết nền thẻ theo thể loại (phần A + nền Sáng mới + font đã xong ở #80).
- [ ] E2E GUI ca G6 (tạo exFAT trên ổ ảo) chập chờn 1 lần ở #81 (chạy lại thì đạt) - nếu lặp lại, lưu nhật ký lần chạy.
- [ ] winget-pkgs #486490 (26.11.2202), #487170 (26.11.2301), #487205 (26.11.2302): chờ moderator; #487246 (26.11.2303),
      #487347 (26.11.2401): URL-Validation-Error 503 LicenseUrl github.com - cần tác giả (`nguyentrangtinhsu`) bình luận
      xin chạy lại; bản sau dùng `disk.tsudev.com/ban-quyen/` (#76). Merge => khối "Cài bằng lệnh" (PACKAGING 8.1).
- [ ] Pro: đăng nhập thật bằng tài khoản TsuDEV trên Windows (cần chủ dự án bấm trong trình duyệt) - THREAT_MODEL mục 4.
      Đồng bộ: chờ .NET `TsuSync` (PRO-D3, repo tsusync) + chủ dự án duyệt phạm vi (ADR-0007 "Việc còn chờ").
- [ ] GUI tiếp (cần chủ dự án duyệt từng bước): thu nhỏ, xoá phân vùng theo rủi ro - mỗi cái ADR bổ sung +
      `AllowedOperations` + ca E2E GUI; màn Pro/Ủng hộ + hộp thoại nhắc gia hạn.
- [ ] Chuyển MBR -> GPT: ADR-0006 Đề xuất - chờ duyệt + kiểm Hyper-V Gen 1 -> Gen 2.
- [ ] Giai đoạn 3: bộ cài, ký SignPath (D5).
- [ ] Nợ thương hiệu: biến thể 16px đơn giản hóa; ảnh gốc lớn hơn/SVG (`docs/BRAND.md`).

## Đang thực hiện

| Task | Agent | Bắt đầu |
| --- | --- | --- |

## Đã hoàn thành (mới nhất trên cùng)

- 26/11/2026 (phiên 14) - Phase INT4: GUI `UpdateRequiredWindow` (toàn màn, không đóng được, hai nút nguyên văn, tương
  phản cao) + `GuiGate` (mở / 6 giờ / quay lại >= 30 phút, chờ thao tác đĩa xong); `426` từ `ProAccount` -> dấu
  `ServerLockStore` khoá CLI + GUI; `DataExport.StandardSources` dùng chung; ảnh modal vi/en x Sáng/Tối; E2E GUI G11.
  Test 543 -> 572.

- 26/11/2026 (phiên 13) - Phase INT3: cổng phiên bản CLI (`Core/Integrity/CommandGate` mặc định chặn, chỉ
  `phien-ban`/`tro-giup`/`dang-xuat`/`xuat-du-lieu`; chưa đăng nhập = 0 lượt mạng; `CliGate` trước mọi lệnh, mã thoát 6,
  `--json`), lệnh `xuat-du-lieu`, PRIVACY/README/CHANGELOG ("Chưa phát hành"), E2E ca 15. Test 477 -> 543.

- 26/11/2026 (phiên 12) - Phase INT2: Core `Integrity/` (kiểm bản kê JWS EdDSA tách nội dung bằng BouncyCastle 2.7.0,
  khoá ghim `tsu-versions-2026`, đệm + chống phát lại, chốt chặn theo kênh, `426` từ `ProHttp`, `DataExport` zip +
  SHA-256, nguyên văn modal vi/en). Test 391 -> 477, bao phủ `Integrity/` 91,7%. Chủ dự án duyệt PLAN + ADR-0012.

- 26/11/2026 (phiên 12) - Phase INT1: `docs/PLAN.md` + ADR-0012 (Đề xuất). Chủ dự án chốt: chỉ kiểm bản kê khi đã
  đăng nhập; CLI khoá mọi lệnh trừ `phien-ban`/`tro-giup`/`dang-xuat`/`xuat-du-lieu`. Sự cố PAT winget 26/11 ĐÓNG.
  Phiếu XK1 ĐÓNG (chấp nhận rủi ro). Phối hợp dọn đĩa: phiếu DD1.

- 26/11/2026 (phiên 11) - SEC3: NuGet lock file (#79), đóng CodeQL #3 #4 có bằng chứng, gitleaks toàn lịch sử sạch.
  Bộ quy ước v8.0.0; LG4 (gỡ `ProNotice`), SYN1/PRO3 (nội dung Pro mới), `X-Tsu-App`, SEC2 `/security/`, UI2 A (CLS
  0,076 -> 0, lề điện thoại) - #80, test 391. Website deploy-2026.11.26.1 (SSL Labs A+, Observatory A+ 150). Phát hành
  **26.11.2601** (#81): VirusTotal 0/74 hai exe, SHA256SUMS + attestation đã kiểm; job winget lỗi 422 (thiếu scope
  `workflow`), script đã sửa; sau khi thay token đã nộp winget-pkgs#489979 và cập nhật Scoop (scoop-bucket#34). Còn 77 nhánh cũ trên remote (lệnh xoá bị chặn quyền, chờ chủ dự án).

- 24/11/2026 (phiên 10) - Không có PR Dependabot. GUI mở rộng ổ (#74, ADR-0011, `ExtendForm`, test 377 -> 392, E2E GUI
  58/58 G1-G10 trên CI). Phát hành **26.11.2401** (#75; đổi từ 2304 vì sang ngày 24/11): release.yml mọi job xanh,
  SHA-256 + attestation hai exe đã kiểm, winget-pkgs#487347; website deploy-2026.11.24.1; Scoop scoop-bucket#33 (tay).
  LicenseUrl winget -> disk.tsudev.com/ban-quyen/ (#76). Đồng bộ Pro: tsusync .NET vẫn chưa có máy khách HTTP.

- 23/11/2026 (phiên 9) - Gộp Dependabot #59, #60. GUI tạo phân vùng trong vùng trống (#70, ADR-0010, test 349 -> 377,
  E2E GUI 35/35 trên CI). Phát hành **26.11.2303** (#71): mọi job release xanh (VT, cong-bo, winget-pkgs#487246);
  website deploy-2026.11.23.3; SHA-256 + attestation hai exe đã kiểm; Scoop scoop-bucket#32 (tay). winget: 3 PR cũ vẫn chờ moderator (license/cla success).

- 23/11/2026 (phiên 8, đợt 3, chủ dự án ủy quyền) - E2E GUI chạy trên Windows 11 máy dev với quyền quản trị (VHDX,
  UAC) 17/17. Phát hành **26.11.2302** (#68): GUI đổi chữ cái/nhãn; VT 0/73 cả hai exe, SHA-256 + attestation đã kiểm;
  winget-pkgs#487205; website deploy-2026.11.23.2 (ảnh GUI mới); Scoop scoop-bucket#31. Phiếu SEC1 áp dụng, ĐÓNG.

- 23/11/2026 (phiên 8, đợt 2) - E2E lái GUI `tests/e2e/run-e2e-gui.ps1` (#66): UI Automation trên VHDX trong CI
  Windows, 17/17 kiểm tra đạt lần đầu (chỉ thấy ổ ảo; gõ sai/Hủy không đổi gì; đổi chữ cái + nhãn Succeeded, SHA-256
  khớp). GUI thêm `--chi-o-ao`/`--dia`/`--thu-muc-nhat-ky`. Test 349. winget 26.11.2301 nộp: winget-pkgs#487170.

- 23/11/2026 (phiên 8) - Phát hành **26.11.2301** kèm `tsudisk-gui.exe` (#62; thử trước bằng `v26.11.2301-beta.1`;
  VT 0/73 cả hai exe, SHA-256 + attestation GUI đã kiểm); job winget 401 (secret cũ). Website deploy-2026.11.23.1
  (trang Tải về có gói + ảnh GUI). Scoop 26.11.2301 + GUI (scoop-bucket#30; Excavator kẹt vì tsudev-contact). GUI đổi
  chữ cái/nhãn qua `PlanExecution` dùng chung CLI (#63, ADR-0009, test 328 -> 348) - chưa phát hành.

- 23/11/2026 (phiên 7) - GUI WPF bản đầu CHỈ ĐỌC `tsudisk-gui.exe` (#57, ADR-0008): bản đồ đĩa, bảng + chân vùng bản
  ghi, phân tích, xem trước kế hoạch mở rộng; vi/en + Sáng/Tối theo hệ thống; token nhúng từ `.standards`; CI Windows
  chụp vi/en × Sáng/Tối (mẫu + ổ thật). Core/Presentation, test 285 -> 328. winget-pkgs#486490 Validation-Completed.
  Đăng nhập thật: chủ dự án chưa thử. TsuSync .NET mới có lớp mã hoá (chưa có máy khách HTTP) + phạm vi chưa duyệt.

- 22/11/2026 (đợt 2, chủ dự án ủy quyền) - CLA Microsoft đã ký (winget-pkgs); client OIDC `tsudisk` chèn vào CSDL
  production (phiếu tsudev DK1 ĐÓNG). Đăng nhập `tai-khoan`/`dang-nhap`/`dang-xuat` + THREAT_MODEL (#52, test 285).
  STD50 PR 2 bố cục `cong-cu` + hoạ tiết (#53, phiếu ĐÓNG). Phát hành 26.11.2202 (#54; VT 0/74, sha256/attestation/
  Sigstore OK), winget-pkgs#486490. Website deploy-2026.11.22.2.

- 22/11/2026 - Phát hành 26.11.2201 (#48; gộp 1802): VirusTotal 0/74, SHA256SUMS + attestation + Sigstore đã kiểm; job
  winget mở microsoft/winget-pkgs#486465 (Needs-CLA). Bản sắc `disk` v5.0.0 PR 1 (#49, bộ quy ước v5.0.0). Pro nhắc gia
  hạn + nhiều thiết bị, ADR-0007 (#50; test 165 -> 204). Website deploy.

- 19-21/11/2026 - OpenSSF Scorecard (#43), sửa bộ kiểm GPT + fuzz (#44), hồ sơ Best Practices 16949 passing +
  CONTRIBUTING (#45, #46), bộ quy ước v4.2.0 (#47).

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
- 22/11/2026 - TsuDEV Pro: lưu cục bộ, CLI chỉ in nhắc gia hạn, đăng nhập/đồng bộ chờ: `docs/adr/0007-pro-luu-cuc-bo-nhac-gia-han.md`
- 23/11/2026 - Giao diện WPF bản đầu chỉ đọc, `tsudisk-gui.exe` riêng (phát hành từ 26.11.2301): `docs/adr/0008-giao-dien-wpf-chi-doc.md`
- 23/11/2026 - GUI áp dụng đổi chữ cái/nhãn qua đường chạy chung CLI, xác nhận hai lần: `docs/adr/0009-giao-dien-ap-dung-doi-ten.md`
- 23/11/2026 - GUI tạo phân vùng trong vùng trống, xác nhận lần hai bằng số đĩa: `docs/adr/0010-giao-dien-tao-phan-vung.md`
- 23/11/2026 - GUI mở rộng ổ, không tuỳ chọn riêng, chỉ áp dụng khi đọc được WinRE: `docs/adr/0011-giao-dien-mo-rong-o.md`
- 26/11/2026 - (Đề xuất) Cổng phiên bản: bản kê ký Ed25519 (BouncyCastle), chỉ kiểm khi đã đăng nhập, khoá CLI/GUI, nâng `latest` theo kênh, DPoP: `docs/adr/0012-cong-phien-ban-ban-ke-ky-ed25519.md`

## Sự cố bảo mật

> Ghi theo `.standards/docs/SECURITY_BASELINE.md` mục 9. Để trống nếu chưa có.

- 26/11/2026 - **Lộ PAT winget** (classic, `public_repo`, tài khoản `nguyentrangtinhsu`): agent chạy
  `cut -d= -f1 ~/.config/tsudev/winget.env` để xem tên biến, nhưng tệp chứa token trần nên cả giá trị hiện trong phiên
  Claude (transcript cục bộ + phiên mô hình). Không vào git, không công khai; D1. Agent ngừng dùng token. Chủ dự án
  tạo token mới, nạp qua `tsu-nhan-bi-mat` 18:14 26/11 (`~/.config/tsudev/winget.env` + secret `WINGET_TOKEN`; kiểm:
  scope `public_repo`, hết hạn 24/02/2027). Chủ dự án xác nhận đã xoá token cũ (26/11/2026, phiên 12). **ĐÓNG**. Phòng ngừa: tệp
  trong `~/.config/tsudev/` coi như chỉ chứa bí mật - KHÔNG `cat/cut/head/grep` in nội dung; chỉ `< tệp` vào lệnh nhận.
- 18/11/2026 - **Lộ PAT winget** (classic, `public_repo`, tài khoản `nguyentrangtinhsu`): khi nạp `WINGET_TOKEN`, token
  được dán vào terminal và hiện đầy đủ trong ảnh chụp gửi vào phiên Claude, đồng thời có thể đã vào `~/.bash_history`.
  Lộ khoảng vài phút trước khi thay. Đã làm: tạo token mới (hết hạn 17/12/2026), ghi lại `~/.config/tsudev/winget.env`,
  nạp lại secret (Environment `packages`), xóa dòng `ghp_` khỏi `~/.bash_history`. Không nằm trong git (không cần
  filter-repo). Token không chạm dữ liệu người dùng (D1). Token CŨ (hết hạn 17/12/2026 18:36 UTC) đã bị xóa - đối
  chiếu danh sách github.com/settings/tokens 18/11/2026: chỉ còn token mới (hết hạn 18/12/2026) và một token
  `winget tsudev` khác chưa từng dùng. Chủ dự án đã xem security-log: không có thao tác lạ trong lúc lộ. ĐÓNG.
  Phòng ngừa (cập nhật SEC1 23/11/2026): nhận token bằng `tsu-nhan-bi-mat` (clipboard, xoá lịch sử Win+V), ví dụ
  `tsu-nhan-bi-mat --tiento ghp_ --stdout | gh secret set WINGET_TOKEN -R tsudev-org/tsudisk --env packages`; KHÔNG dán
  token vào chat/terminal/tệp. Phiếu `logs/handover/20261123-SEC1_nhan-bi-mat-qua-clipboard.md`.
- 17/11/2026 (ghi bổ sung) - Phiên 4 in giá trị `~/.config/tsudev/{winget,virustotal}.env` ra transcript (phiếu
  20261118-01 mục 2). PAT winget cũ đã thay ở sự cố trên. Khóa VirusTotal: tài khoản miễn phí, chủ dự án
  không đổi được khóa - CHẤP NHẬN rủi ro (khóa chỉ lộ trong transcript cục bộ/phiên Claude, không công khai; gói miễn
  phí không tính tiền, hậu quả tệ nhất là người khác dùng hết hạn mức 4 lượt/phút, 500 lượt/ngày). Theo dõi: job
  `virustotal` báo lỗi hạn mức (HTTP 429/`QuotaExceededError`) bất thường => nghi khóa bị dùng, tạo tài khoản mới.

