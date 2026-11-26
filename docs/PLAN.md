# PLAN.md - kế hoạch theo Phase (tsudisk)

> Nguồn sự thật cho chu trình Phase (`~/.claude/CLAUDE.md`, AGENT_PROTOCOL 4.1-4.3). **Mỗi ngữ cảnh làm MỘT Phase.**
> Phase xong khi đủ checklist + tiêu chí nghiệm thu. Sau đó cập nhật `logs/STATE.md`, commit, AKB, rồi DỪNG.
> Lệnh kiểm chung: `dotnet test tests/TsuDisk.Core.Tests` (Linux), CI `ci.yml` (Windows build + E2E VHDX + E2E GUI).
> Thiết kế: [ADR-0012](adr/0012-cong-phien-ban-ban-ke-ky-ed25519.md).
> Quyết định chủ dự án 26/11/2026: chỉ kiểm khi đã đăng nhập; CLI bị khoá thì chặn mọi lệnh trừ
> `phien-ban`/`tro-giup`/`dang-xuat`/`xuat-du-lieu`.

**Trạng thái kế hoạch**: ĐÃ DUYỆT 26/11/2026. Phase INT3 xong; kế tiếp: Phase INT4.

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

- [ ] Nạp secret `TSU_VERSIONS_TOKEN` (Environment `packages`) từ `~/.config/tsudev/keys/tsu-versions-2026.env` qua
      `set -a; . <tệp>` + `gh secret set` (không in). Nếu hook chặn thì 👤 nạp bằng `tsu-nhan-bi-mat`.
- [ ] `release.yml`: job `nang-latest` sau `cong-bo` (chỉ bản chính thức). Thử lại khi gặp 409 `download_unverified`;
      lỗi thì mở Issue.
- [ ] `.github/workflows/kenh-phat-hanh.yml`: chạy theo lịch 6 giờ + dispatch; nâng `winget`/`scoop` chỉ khi kênh đã có
      bản (ADR-0012 mục 12). Action ghim SHA, quyền tối thiểu, `persist-credentials: false`.
- [ ] `packaging/tools/raise-latest.mjs` dùng chung cho hai workflow, có test `node --test`: URL đúng, coi 409
      `version_not_newer` là xong, không in token.

**Nghiệm thu**: actionlint/zizmor (nếu có trong `security.yml`) sạch. Chạy `workflow_dispatch` thật với bản 26.11.2601:
GET bản kê thấy `products.tsudisk.channels.github.latest = 26.11.2601`, và `scoop` nếu bucket đã có. PR đã gộp.

## Phase INT6 - DPoP cho máy khách desktop

- [ ] Phiếu trao đổi với repo tsudev: chốt hợp đồng DPoP desktop gồm `token_endpoint`, `cnf.jkt` trong JWS quyền lợi,
      `DPoP-Nonce`, thời điểm bắt buộc ở `/api/me/entitlements`. Chờ phía tsudev trả lời. Phase này BỊ CHẶN tới khi có
      hợp đồng.
- [ ] `TsuDisk.Windows/DpopKey`: CNG P-256, ưu tiên TPM (Platform Crypto Provider), nếu không có thì dùng Software KSP;
      khoá không xuất được.
- [ ] `ProHttp` gửi proof (`htm`, `htu`, `iat`, `jti`, `ath`), thử lại một lần khi nhận `use_dpop_nonce`.
- [ ] ADR-0012 bổ sung, hoặc ADR-0013 nếu hợp đồng khác giả định.

**Nghiệm thu**: test proof khớp RFC 9449 (vector tự kiểm bằng khoá thử). Đăng nhập thật trên Windows qua (👤 bấm trình
duyệt). JWS quyền lợi có `cnf.jkt` khớp khoá. PR đã gộp.

## Phase INT7 - Phát hành bản có cổng + thử thật

- [ ] Phát hành bản mới (CHANGELOG, VirusTotal, SHA256SUMS + attestation, website deploy, Scoop, winget).
- [ ] Thử thật trên Windows: cài bản N có cổng và đăng nhập. Phát hành N+1, `nang-latest` chạy. Bản N hiện modal (GUI)
      và thoát mã 6 (CLI). Xuất dữ liệu vẫn chạy.
- [ ] Cập nhật bảng phiếu INT1 (dòng tsudisk) + phiếu gốc.

**Nghiệm thu**: APP_INTEGRITY mục 8 đánh dấu đủ cho tsudisk (desktop OPEN: cổng + test, không SDK bên thứ ba, gia cố
5.2 .NET: `DebugType=none` cho bản phát hành - KHÔNG trim/AOT theo AGENTS B.5, ghi lệch chuẩn).

## Phase QR4-UI1 - Đăng nhập QR + tương phản biểu tượng + cổng mã chết

- [ ] Đọc phiếu `20261124-QR1` + các phiếu gốc (`CROSS_DEVICE_LOGIN.md`); lập checklist chi tiết vào đây khi bắt đầu.
- [ ] UI1: biểu tượng trên mọi nền ≥ 3:1 ở Sáng/Tối (GUI + website), có test chặn tái phát.
- [ ] Cổng Clean Codebase (mã chết/phụ thuộc thừa) + ngân sách tốc độ website trong CI.

**Nghiệm thu**: theo phiếu QR1. CI xanh. Ghi kết quả vào phiếu QR1 + phiếu gốc.

## Phase UI2-B - Hiện đại hoá website (phần B)

- [ ] Hoạ tiết nền thẻ theo thể loại (phiếu `20261125-UI2` mục 3). Không tự đổi hex/font chung khi bộ quy ước chưa có.
- [ ] Rà bằng Playwright: 3 khổ × Sáng/Tối; LCP/CLS/INP không xấu đi so với deploy-2026.11.26.1.

**Nghiệm thu**: ảnh trước/sau, số đo; deploy website; phiếu UI2 ĐÓNG.
