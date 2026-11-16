# STATE.md - Trạng thái project

> Agent đọc file này ở đầu mỗi phiên và cập nhật ở cuối mỗi phiên.
> Quy trình: `.standards/docs/AGENT_PROTOCOL.md`.

## Hàng đợi task (làm từ trên xuống)

- [ ] Giai đoạn 2 (`docs/ROADMAP.md`): GUI WPF, sao lưu/khôi phục GPT thô, mở rộng WinRE (KB5034441), S.M.A.R.T.
- [ ] Phát hành bản cài + ký SignPath khi D5 duyệt; winget `tsudev.TsuDisk`, Scoop
- [ ] Rà 8 PR Dependabot (#1-#6, #8, #9). Riêng #6 `System.Management` 11.0.0: kiểm có cần .NET 11 không trước khi merge
- [ ] Khi có gói winget/Scoop: gửi phiếu mới cho repo `tsudev` để gỡ ngoại lệ tạm của cổng C4 cho id `disk` và chạy `pkg:catalog`

## Đang thực hiện

| Task | Agent | Bắt đầu |
| --- | --- | --- |

## Đã hoàn thành (mới nhất trên cùng)

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

## Sự cố bảo mật

> Ghi theo `.standards/docs/SECURITY_BASELINE.md` mục 9. Để trống nếu chưa có.

- (chưa có)
