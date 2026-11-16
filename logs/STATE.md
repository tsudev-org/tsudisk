# STATE.md - Trạng thái project

> Agent đọc file này ở đầu mỗi phiên và cập nhật ở cuối mỗi phiên.
> Quy trình: `.standards/docs/AGENT_PROTOCOL.md`.

## Hàng đợi task (làm từ trên xuống)

- [ ] 👤 Chủ dự án: xem Settings > Actions của org `tsudev-org` - cổng "Bảo vệ bản quyền" startup_failure từ ~16:03Z
      16/11 ở scoop-bucket, linux-packages. Sau đó merge `tsudev-org/scoop-bucket#15` (gói Scoop `tsudisk`, đã cài thử
      thật trên CI) và gửi phiếu cho repo `tsudev` gỡ ngoại lệ tạm cổng C4 cho id `disk` + chạy `pkg:catalog`.
- [ ] Theo dõi phiếu tsudev 20261117-D2 (logo TsuDisk cho tsudev.com/apps).
- [ ] Giai đoạn 2 còn lại (`docs/ROADMAP.md`): giao diện WPF; đổi chữ cái/nhãn ổ; thu nhỏ ổ tổng quát; tạo/định dạng
      phân vùng trong vùng trống; `mbr2gpt`.
- [ ] Giai đoạn 3: bộ cài, winget `tsudev.TsuDisk` (cần PAT `public_repo` làm secret `WINGET_TOKEN`), ký SignPath (D5).
- [ ] Nợ thương hiệu: biến thể 16px đơn giản hóa; ảnh gốc lớn hơn/SVG (`docs/BRAND.md`).

## Đang thực hiện

| Task | Agent | Bắt đầu |
| --- | --- | --- |

## Đã hoàn thành (mới nhất trên cùng)

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

## Sự cố bảo mật

> Ghi theo `.standards/docs/SECURITY_BASELINE.md` mục 9. Để trống nếu chưa có.

- (chưa có)
