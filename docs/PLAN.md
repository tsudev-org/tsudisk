# PLAN.md - kế hoạch theo Phase (tsudisk)

> Nguồn sự thật cho chu trình Phase (`~/.claude/CLAUDE.md`, AGENT_PROTOCOL 4.1-4.3). **Mỗi ngữ cảnh làm MỘT Phase.**
> Phase xong khi đủ checklist + tiêu chí nghiệm thu. Sau đó cập nhật `logs/STATE.md`, commit, AKB, rồi DỪNG.
> Lệnh kiểm chung: `dotnet test tests/TsuDisk.Core.Tests` (Linux), CI `ci.yml` (Windows build + E2E VHDX + E2E GUI).
> Thiết kế: [ADR-0012](adr/0012-cong-phien-ban-ban-ke-ky-ed25519.md).
> Quyết định chủ dự án 26/11/2026: chỉ kiểm khi đã đăng nhập; CLI bị khoá thì chặn mọi lệnh trừ
> `phien-ban`/`tro-giup`/`dang-xuat`/`xuat-du-lieu`.

**Trạng thái kế hoạch**: ĐÃ DUYỆT 26/11/2026. Phase INT1-INT7 xong; kế tiếp: Phase QR4-UI1.

## Phase INT1 - Lập kế hoạch + ADR cổng phiên bản

- [x] Đọc APP_INTEGRITY v1.0.0, phiếu 20261125-INT1, tsudev `docs/release-signing.md`.
- [x] Đo production: `versions.json` 200 (`products: {}`), `.sig` JWS EdDSA tách nội dung, JWKS `tsu-versions-2026`.
- [x] Hỏi chủ dự án hai mâu thuẫn (hạng C và GET bản kê; phạm vi khoá CLI) và ghi lại quyết định.
- [x] ADR-0012 (Đề xuất) + PLAN.md này.

**Nghiệm thu**: ADR-0012 và PLAN.md đã commit qua PR có CI xanh. Chủ dự án duyệt thì đổi ADR-0012 sang "Chấp nhận".

## Phase INT2 - Core: kiểm bản kê + quyết định khoá (không giao diện)

- [x] Thêm `BouncyCastle.Cryptography` (ghim phiên bản) vào `TsuDisk.Core`, cập nhật `packages.lock.json`, NOTICE và
      THIRD-PARTY-NOTICES. CI `--locked-mode` xanh.
- [x] `src/TsuDisk.Core/Integrity/`:
      - `VersionKeys` (khoá ghim theo `kid`).
      - `DetachedJws` (tách và kiểm `.sig`).
      - `VersionManifest` (parse sau khi chữ ký đạt, giới hạn kích thước).
      - `AppVersion` (so `YY.M.DDNN` + bản thử).
      - `VersionManifestCache` (ghi nguyên tử, đọc lại thì kiểm lại).
      - `VersionGate` (đầu vào: bản kê, kênh, phiên bản, giờ; đầu ra: `Ok` | `UpdateRequired(latest, url)` | `Unknown`).
- [x] `IVersionSource` + bản `HttpVersionSource`: https `tsudev.com` cố định, không theo chuyển hướng, 5 giây, giới hạn
      byte. `ProHttp` nhận `426 update_required` và trả về `UpdateRequired`.
- [x] Hàm `DataExport` trong Core: zip nhật ký + bản sao lưu GPT + cài đặt + `manifest.json` (SHA-256), KHÔNG lấy token
      hay khoá. Có test đường dẫn: chặn zip-slip, chặn liên kết tượng trưng ra ngoài.
- [x] Test (APP_INTEGRITY 6) gồm các ca:
      - Bộ dữ liệu thật: byte `versions.json` + `.sig` production, chụp lại 26/11.
      - Chữ ký sai, đổi 1 byte, `alg` khác, `kid` lạ, `.sig` 2 hoặc 4 phần.
      - Bản kê hết hạn, `expires-issued > 7 ngày`, `issued_at` cũ hơn bản đệm.
      - Kênh khác có bản mới thì không khoá.
      - Bản thử nhỏ hơn bản chính; bản tự dựng không khoá.
      - Lỗi mạng + đệm còn hạn thì dùng đệm; lỗi mạng + không đệm thì không chặn.
      - `426` thì khoá.

**Kết quả 26/11/2026**: test 391 -> 477 (+86); bao phủ `Integrity/` 91,7% dòng; khoá ghim khớp JWKS production
(bộ dữ liệu `tests/TsuDisk.Core.Tests/Fixtures/versions/`); build Release toàn solution sạch.

**Nghiệm thu**: test Core đều xanh (tăng tối thiểu 30 ca); bao phủ `Integrity/` ≥ 90% dòng. Có test đối chiếu khoá ghim
với JWKS production (bộ dữ liệu trong repo). CI xanh. PR đã gộp.

## Phase INT3 - CLI: khoá lệnh + `xuat-du-lieu`

- [x] `Program`: sau khi parse, nếu đã đăng nhập và lệnh không thuộc nhóm được phép thì gọi `VersionGate` (bản kê +
      đệm). Khoá thì in nguyên văn vi (APP_INTEGRITY 2.2) + lệnh cập nhật theo kênh, thoát `ExitCodes.UpdateRequired = 6`.
      `--json` thì in đối tượng lỗi.
- [x] Lệnh `xuat-du-lieu <thư mục>` (Core `DataExport`), thêm vào `tro-giup`, README, `CHANGELOG`.
- [x] Chưa đăng nhập: KHÔNG có lượt gọi mạng nào. Test bằng `IVersionSource` giả đếm số lượt gọi = 0.
- [x] PRIVACY.md: một dòng về GET bản kê khi đã đăng nhập. Website tự lấy nội dung lúc build.
- [x] E2E VHDX: thêm ca chạy `xuat-du-lieu` trên ổ ảo, kiểm zip có `manifest.json` và SHA-256 khớp.

**Kết quả 26/11/2026**: Core `Integrity/CommandGate` (mặc định CHẶN, chỉ 4 lệnh được phép; chưa đăng nhập thì không
tạo cổng) + CLI `CliGate` chạy trước `ProHook.Run` và trước khi chọn lệnh, kể cả `--json`/`--dong-y`. `CliArgs` từ chối
lệnh chưa đăng ký với `CommandGate.KnownCommands`; test đối chiếu văn bản `CliArgs.cs`/`Program.cs`. Test 477 -> 543.
E2E ca 15 (`xuat-du-lieu` lên ổ ảo, manifest + SHA-256 khớp zip và tệp gốc). Còn lại cho INT4: `426` từ `ProHttp` chưa
được `ProAccount` đẩy lên CLI/GUI (ADR-0012 mục 8).

**Nghiệm thu**: test CLI/Core xanh. Không có cách nào để một lệnh ghi đĩa chạy khi trạng thái là `UpdateRequired` (test
bảng mọi lệnh). E2E xanh trên CI. PR đã gộp.

## Phase INT4 - GUI: modal khoá + Xuất dữ liệu

- [x] `UpdateRequiredWindow`:
      - Toàn màn, không đóng được (Esc, Alt+F4, X); focus không ra khỏi cửa sổ; có tên UI Automation.
      - Sáng/Tối/tương phản cao; nguyên văn vi/en; đúng hai nút.
- [x] Lịch kiểm: khi mở, mỗi 6 giờ, khi kích hoạt lại sau ≥ 30 phút. Có thao tác đĩa đang chạy thì chờ xong mới hiện.
- [x] Nút **Cập nhật ngay** theo kênh: GitHub mở `url`; Scoop/winget hiện lệnh + sao chép. Nút **Xuất dữ liệu**: chọn
      thư mục, gọi `DataExport`.
- [x] `--chup-man-hinh` chụp thêm modal vi/en × Sáng/Tối, dùng dữ liệu mẫu chỉ để hiển thị, không đi qua quyết định của
      cổng. Không thêm cờ hay biến môi trường nào bỏ qua cổng.
- [x] Ca E2E GUI G11: modal không đóng được bằng Esc/Alt+F4 (UIA) khi khởi chạy ở chế độ chụp mẫu.

**Nghiệm thu**: E2E GUI xanh trên CI (G1-G11). Đã xem ảnh chụp 4 biến thể. Tương phản chữ ≥ 4,5:1 (test token). PR đã gộp.

Tiến độ (26/11/2026, phiên 14): Core `GuiGateSchedule` + `UpdateAction` + `ServerLockStore` (`426` từ `ProAccount` khoá
CLI và GUI), `DataExport.StandardSources` dùng chung; GUI `GuiGate` + `UpdateRequiredWindow` (tương phản cao: màu hệ
thống); 4 ảnh modal đã xem; UIA thử trên máy dev đạt (tên, hai nút, Maximized, IsModal, focus, Esc, Tab, WM_CLOSE).
Test 543 -> 572. E2E GUI G1-G11 xanh trên CI (PR #88). Cập nhật ngay = hành động theo kênh rồi đóng app (ADR-0012
"Ghi chú triển khai").

## Phase INT5 - Phát hành: nâng `latest` theo kênh

- [x] Nạp secret `TSU_VERSIONS_TOKEN` (Environment `packages`) từ `~/.config/tsudev/keys/tsu-versions-2026.env` qua
      `set -a; . <tệp>` + `gh secret set` (không in). Nếu hook chặn thì 👤 nạp bằng `tsu-nhan-bi-mat`.
      (26/11: agent bị bộ phân loại quyền chặn; 👤 đã nạp bằng `set -a` + `gh secret set` qua stdin.)
- [x] `release.yml`: job `nang-latest` sau `cong-bo` (chỉ bản chính thức). Thử lại khi gặp 409 `download_unverified`;
      lỗi thì mở Issue.
- [x] `.github/workflows/kenh-phat-hanh.yml`: chạy theo lịch 6 giờ + dispatch; nâng `winget`/`scoop` chỉ khi kênh đã có
      bản (ADR-0012 mục 12). Action ghim SHA, quyền tối thiểu, `persist-credentials: false`.
- [x] `packaging/tools/raise-latest.mjs` dùng chung cho hai workflow, có test `node --test`: URL đúng, coi 409
      `version_not_newer` là xong, không in token.

**Nghiệm thu**: actionlint/zizmor (nếu có trong `security.yml`) sạch. Chạy `workflow_dispatch` thật với bản 26.11.2601:
GET bản kê thấy `products.tsudisk.channels.github.latest = 26.11.2601`, và `scoop` nếu bucket đã có. PR đã gộp.

- [x] PR #89 gộp (kèm gia cố CodeQL `js/file-access-to-http`: kiểm khuôn `package.json`, chỉ gửi url `https://github.com`).
- [x] Lần dispatch đầu lỗi: Environment `packages` chỉ cho tag `v*`. Chủ dự án chọn env mới `versions` (chỉ
      `TSU_VERSIONS_TOKEN`, nhánh `main` + tag `v*`) - PR #90 đã gộp; 👤 đã nạp secret (biến nguồn `VERSIONS_RAISE_TOKEN`).
- [x] Nghiệm thu 27/11: run 45864874082 xanh; bản kê `channels.github.latest = 26.11.2601`,
      `channels.scoop.latest = 26.11.2601`; winget bỏ qua đúng (winget-pkgs#489979 chưa gộp).
- [x] Đã xoá `TSU_VERSIONS_TOKEN` khỏi env `packages` (chủ dự án đồng ý 27/11); chỉ còn ở env `versions`.

## Phase INT6 - DPoP cho máy khách desktop

- [x] Phiếu trao đổi với repo tsudev (`20261127-DP1`): làm theo hợp đồng đang chạy (tsudev ADR-0004 mục 6: DPoP tuỳ
      chọn ở `/api/me/entitlements`, `cnf.jkt`). Câu hỏi còn mở (thời điểm bắt buộc, `DPoP-Nonce`, `token_endpoint`)
      không chặn việc phía máy khách.
- [x] `TsuDisk.Windows/DpopKeyStore`: CNG P-256, ưu tiên TPM (Platform Crypto Provider), nếu không có thì dùng Software
      KSP; khoá không xuất được. Đã thử trên Windows thật (TPM).
- [x] `ProHttp` gửi proof (`htm`, `htu`, `iat`, `jti`, `ath`), thử lại một lần khi nhận `use_dpop_nonce` (và khi lệch
      giờ).
- [x] ADR-0012 bổ sung (ghi chú triển khai INT6).

**Nghiệm thu**: test proof khớp RFC 9449 (vector tự kiểm bằng khoá thử). Đăng nhập thật trên Windows qua (👤 bấm trình
duyệt). JWS quyền lợi có `cnf.jkt` khớp khoá. PR đã gộp.

✅ Nghiệm thu 27/11/2026: PR #93 gộp; chủ dự án đăng nhập thật trên Windows, `tai-khoan` hiện "Gắn với khóa thiết bị
của máy này: có".

## Phase INT7 - Phát hành bản có cổng + thử thật

- [x] Phát hành bản mới (CHANGELOG, VirusTotal, SHA256SUMS + attestation, website deploy, Scoop, winget).
      26.11.2701 (#95, run 45897552660 mọi job xanh): VT 0/74 hai exe; SHA256SUMS + attestation hai exe đã kiểm;
      website deploy-2026.11.27.1; Scoop scoop-bucket#35; winget-pkgs#490479; bản kê github + scoop = 26.11.2701.
- [x] Thử thật trên Windows: cài bản N có cổng và đăng nhập. Phát hành N+1, `nang-latest` chạy. Bản N hiện modal (GUI)
      và thoát mã 6 (CLI). Xuất dữ liệu vẫn chạy.
      27/11: N = 26.11.2701 (đã đăng nhập, khoá TPM); trước N+1 `danh-sach` chạy bình thường. N+1 = 26.11.2702 (#96, run
      45900280868 mọi job xanh, bản kê `github.latest = 26.11.2702`). Bản N: `danh-sach` mã 6 + nguyên văn, `--json` ra
      `update_required`; `phien-ban` mã 0; `xuat-du-lieu` ra zip có `manifest.json`. GUI (UIA): cửa sổ "Update required"
      Maximized, IsModal, đúng hai nút Export data / Update now, WM_CLOSE không đóng.
- [x] Cập nhật bảng phiếu INT1 (dòng tsudisk) + phiếu gốc (phiên tsudev ghi vào phiếu gốc 27/11, vào git cùng PR CP3).

Tiến độ (27/11/2026, phiên 17): rà soát cổng trước phát hành không thấy lỗi chặn (lỗi mạng/503/chữ ký sai/đệm hết hạn
⇒ `Unknown`, không chặn; kênh scoop/winget nhận đúng). PR #95 phát hành 26.11.2701 + `DebugType=none`.
Bản kê production từng trả 503 (27/11, backend Render ngủ + đệm Worker chỉ trong isolate; KHÔNG mất khoá) - phiếu tsudev
`20261127-VS1`; đã 200 lại ~04:15. Phía app giữ "mở khi lỗi".

**Nghiệm thu**: APP_INTEGRITY mục 8 đánh dấu đủ cho tsudisk (desktop OPEN: cổng + test, không SDK bên thứ ba, gia cố
5.2 .NET: `DebugType=none` cho bản phát hành - KHÔNG trim/AOT theo AGENTS B.5, ghi lệch chuẩn).

✅ Nghiệm thu 27/11/2026: APP_INTEGRITY mục 8 cho tsudisk - cổng + modal có test (INT2-INT4) và thử thật đạt; nhận `426`;
không SDK bên thứ ba (I5); OPEN 5.2: `DebugType=none` (#95), lệch chuẩn trim/AOT ghi AGENTS B.4. Mục Android: không áp dụng.

## Phase QR4-UI1 - Đăng nhập QR + tương phản biểu tượng + cổng mã chết

> Bộ quy ước v9.0.0 (27/11/2026) thay đổi QR4: app cài đặt KHÔNG có màn đăng nhập riêng - QR, mã khớp, Google/GitHub,
> passkey đều nằm trên cổng `tsudev.com` (AUTH_AND_ACCOUNT v5.0.0 mục 2.1, CROSS_DEVICE_LOGIN v1.2.0). QR4 của tsudisk
> = mục Tài khoản đúng hai loại nút (Đăng nhập trên trình duyệt, Mở trang tài khoản) + Xoá tài khoản, CLI giữ PKCE
> loopback. Phiếu: `logs/handover/20261124-QR1_dang-nhap-qr-ui1-dk2.md`, rà soát `20261127-LOGIN1_ra-soat-mot-cong-dang-nhap.md`.

**A. Bộ quy ước v9.0.0 + rà soát một cổng**
- [x] Đọc phiếu `20261124-QR1` + phiếu gốc QR1 (mục 2 QR4-DESKTOP, STD-SYNC) + CHANGELOG 9.0.0.
- [x] `sync-standards.sh --ref v9.0.0` hai lượt (+ cập nhật `scripts/sync-standards.sh` theo bản trung tâm, lượt ba để
      chép `check-central-login.sh`); `check-standards.sh` đạt; `check-central-login.sh` (thường + `--strict`) đạt.
- [ ] Phiếu `logs/handover/20261127-LOGIN1_ra-soat-mot-cong-dang-nhap.md` theo mẫu `RA_SOAT_DANG_NHAP.md` (rà tay CLI + GUI).
- [ ] CI `standards.yml`: thêm `.standards/scripts/check-central-login.sh --strict` (bước 5 hướng dẫn nâng cấp).

**B. QR4 - mục Tài khoản theo mục 2.1**
- [ ] Core `Pro/AccountLinks`: URL trang tài khoản + trang xoá tài khoản của cổng (đo 27/11: `/settings/security`
      200, neo `#xoa-tai-khoan`), test (https, host `tsudev.com`, không ghi cứng trang đăng nhập).
- [ ] CLI: lệnh `xoa-tai-khoan` (`delete-account`) mở trang xoá của cổng bằng trình duyệt hệ thống (không ô nhập, không
      gọi API xoá); `tai-khoan` in thêm trang tài khoản + cách xoá; trợ giúp, README, CHANGELOG. Cổng khoá giữ nguyên
      danh sách chủ dự án chốt (lệnh mới bị chặn khi khoá - xoá vẫn làm được ở tsudev.com).
- [ ] GUI: nút "Tài khoản" ở đầu trang mở `AccountWindow`: trạng thái từ tệp phiên cục bộ (KHÔNG gọi mạng khi mở),
      nút **Đăng nhập trên trình duyệt** (PKCE loopback như CLI, huỷ được), **Mở trang tài khoản**, **Đăng xuất**,
      **Xoá tài khoản**; không ô nhập nào; vi/en; tương phản cao.
- [ ] BRAND_ASSETS 12.3: dấu hiệu TsuDEV (PNG sinh từ dây chuyền `brand-assets.mjs`) trước liên kết tsudev.com ở chân
      trang GUI, Giới thiệu, Tài khoản.
- [ ] Ảnh chụp `--chup-man-hinh` thêm cửa sổ Tài khoản (vi/en × Sáng/Tối); E2E GUI ca G12: cửa sổ Tài khoản mở được,
      không có ô nhập (UIA Edit), đủ các nút, Esc đóng.

**C. UI1 - biểu tượng ≥ 3:1 trên nền thật mọi trạng thái (DESIGN_SYSTEM mục 1)**
- [ ] Core test: bảng cặp (màu biểu tượng, nền ở mọi trạng thái) của GUI ≥ 3:1 cả Sáng/Tối + test quét XAML GUI:
      mọi `Path`/biểu tượng vẽ bằng vai trò có trong bảng (chặn tái phát).
- [ ] Website `scripts/check-bieu-tuong.mjs` (Playwright): mọi `svg` hiện trên các trang mẫu × Sáng/Tối × thường/
      di chuột/đang chọn ≥ 3:1 so với nền thật phía sau; gắn CI. Sửa chỗ trượt (nếu có).

**D. Clean Codebase + ngân sách tốc độ (PROJECT_STRUCTURE 3.1, LANGUAGE_SELECTION 5.1)**
- [ ] .NET: bật `IDE0051`/`IDE0052` mức lỗi + `EnforceCodeStyleInBuild`; sửa vi phạm; build `-warnaserror` đã chặn.
- [ ] Website: `knip` (ghim phiên bản) `npm run check:knip` trong CI; sửa vi phạm. CI kiểm `git status --porcelain`
      sạch sau build.
- [ ] Website: `scripts/check-toc-do.mjs` - LCP ≤ 2,5 s (CPU chậm 4×, mạng 4G chậm), CLS ≤ 0,1, JS tải ban đầu (gzip)
      ≤ 170 KB trên các trang mẫu; gắn CI.
- [ ] Desktop: ghi ngoại lệ .NET (khởi động/kích thước) ở AGENTS B.4 kèm số đo hiện có.

**Nghiệm thu**: theo phiếu QR1 (ảnh Tài khoản 2 chế độ, `check-central-login --strict` xanh, cổng mã chết + tốc độ chặn
CI). Mọi thay đổi Core kèm test. PR + CI xanh. Ghi kết quả vào phiếu QR1 + phiếu gốc + phiếu LOGIN1.

## Phase UI2-B - Hiện đại hoá website (phần B)

- [ ] Hoạ tiết nền thẻ theo thể loại (phiếu `20261125-UI2` mục 3). Không tự đổi hex/font chung khi bộ quy ước chưa có.
- [ ] Rà bằng Playwright: 3 khổ × Sáng/Tối; LCP/CLS/INP không xấu đi so với deploy-2026.11.26.1.

**Nghiệm thu**: ảnh trước/sau, số đo; deploy website; phiếu UI2 ĐÓNG.
