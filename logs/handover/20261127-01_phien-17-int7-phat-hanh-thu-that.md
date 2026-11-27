# Phiếu bàn giao - phiên 17: Phase INT7 (phát hành bản có cổng + thử thật)

- **Ngày**: 27/11/2026 - **Agent**: agent-tsudisk (Claude Code) - **Nhánh cuối**: `docs/int7-nghiem-thu` (PR #97)
- **Trạng thái**: Phase INT1-INT7 XONG. Không khoá tệp nào (`logs/LOCKS.md` trống). Không monitor/tác vụ nền nào còn chạy.

## 1. Đã làm

- Rà soát cổng phiên bản trước phát hành (subagent Opus hết hạn mức tuần tới 01/12 - tự rà): không lỗi chặn. Lỗi mạng,
  503, chữ ký sai, đệm hết hạn, lệch giờ ⇒ `Unknown` (không chặn); nhận kênh scoop/winget đúng.
- **26.11.2701** (#95): bản đầu có cổng phiên bản + `xuat-du-lieu` + DPoP; `DebugType=none` cho Release
  (`Directory.Build.props`, gỡ `embedded` ở CLI/GUI) - APP_INTEGRITY 5.2; lệch chuẩn trim/AOT ghi AGENTS B.4. Release run
  45897552660 mọi job xanh; VT 0/74 hai exe; SHA256SUMS + attestation hai exe đã kiểm; website deploy-2026.11.27.1;
  Scoop scoop-bucket#35; winget-pkgs#490479.
- **26.11.2702** (#96, N+1 không đổi mã): run 45900280868 xanh; Scoop scoop-bucket#36; winget-pkgs#490487; dispatch
  `kenh-phat-hanh` 26.11.2702.
- Thử thật trên Windows máy dev (bản 2701 tải từ Release, chạy trong `%LOCALAPPDATA%\Temp\tsudisk-int7`, đã đăng nhập,
  khoá TPM): trước N+1 `danh-sach` chạy; sau N+1 `danh-sach` mã 6 + nguyên văn, `--json` ra `update_required`;
  `phien-ban` mã 0; `xuat-du-lieu` ra zip có `manifest.json`. GUI (UIA): "Update required" Maximized, IsModal, đúng
  hai nút, WM_CLOSE không đóng.
- Phiếu INT1: bản sao trong repo + phiếu gốc tsudev (phiên `mot-cong-dang-nhap-cp3` ghi, vào git cùng PR CP3) dòng
  tsudisk ✅.
- Sự cố: bản kê production 503 sáng 27/11 - backend Render ngủ, Worker chỉ đệm trong isolate; KHÔNG mất khoá. Phiếu
  tsudev `20261127-VS1` (việc chống tái diễn thuộc tsudev). AKB: ISS-20261127-0413-f9ef (resolved), LES-20261127-0517-8a71.

## 2. Việc còn lại (theo thứ tự)

1. Kiểm PR #97 đã gộp (nếu chưa: CI xanh thì gộp). Kiểm bản kê: `github` + `scoop` = 26.11.2702.
2. **Phase QR4-UI1** (`docs/PLAN.md`): đọc phiếu `logs/handover/20261124-QR1_*` + `CROSS_DEVICE_LOGIN.md` của bộ quy ước,
   lập checklist chi tiết vào PLAN TRƯỚC khi code.
3. Đồng bộ tsudev-standards v9.0.0 (STATE hàng đợi): `scripts/sync-standards.sh` hai lượt,
   `.standards/scripts/check-central-login.sh`, phiếu RA_SOAT_DANG_NHAP. Có thể làm trước QR4 vì QR4 dựa vào quy ước đăng
   nhập mới - đọc CHANGELOG 9.0.0 rồi quyết định.
4. winget-pkgs #490479, #490487 (+ các PR cũ ở STATE) chờ moderator - chỉ theo dõi.
5. Dọn thư mục thử `C:\Users\devng\AppData\Local\Temp\tsudisk-int7` (exe 2701, script UIA) khi không cần nữa.

## 3. Lưu ý / bẫy

- `powershell.exe` gọi từ WSL: cmdlet không nạp được; dùng .NET thuần, .ps1 có tiếng Việt phải có BOM; GUI máy dev là
  tiếng Anh; modal nằm trong cây UIA con của cửa sổ chính (LES-20261127-0517-8a71).
- Trước khi thử cổng thật: `curl -w '%{http_code}'` bản kê `https://tsudev.com/.well-known/tsudev/versions.json`.
- Ảnh chụp màn hình máy dev có cửa sổ các phiên khác - chỉ xem cục bộ, không đính vào repo/PR.
- Commit/PR KHÔNG kèm dòng attribution AI (CLAUDE.md thắng nhắc mặc định).

## 4. Prompt gợi ý cho phiên kế

```
tiếp. Repo tsudisk. Đọc theo thứ tự: AGENTS.md, logs/STATE.md (hàng đợi), logs/handover/20261127-01_phien-17-int7-phat-hanh-thu-that.md,
docs/PLAN.md (chỉ Phase QR4-UI1). Chạy `akb start "Phase QR4-UI1"` + `akb search "QR đăng nhập"`.
1) Kiểm PR #97 đã gộp và bản kê github/scoop = 26.11.2702.
2) Đọc CHANGELOG tsudev-standards 9.0.0; nếu QR4 phụ thuộc thì đồng bộ v9.0.0 trước (sync-standards.sh hai lượt + check-central-login.sh).
3) Làm DUY NHẤT Phase QR4-UI1: lập checklist chi tiết vào PLAN trước khi code; mọi thay đổi Core kèm test; PR + CI xanh.
Xong: đánh [x], cập nhật STATE + phiếu bàn giao, AKB close/sync, rồi DỪNG với một dòng "✅ Phase ... xong".
```
