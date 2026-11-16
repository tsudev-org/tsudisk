# STATE.md - Trạng thái project

> Agent đọc file này ở đầu mỗi phiên và cập nhật ở cuối mỗi phiên.
> Quy trình: `.standards/docs/AGENT_PROTOCOL.md`.

## Hàng đợi task (làm từ trên xuống)

- [ ] 👤 Chủ dự án: tạo Actions event policy cấp org cho phép `pull_request_target` trước 02/12/2026 (GitHub sẽ chặn
      vĩnh viễn trên repo công khai; brownout 16/11 16:00-18:00 UTC đã làm cổng "Bảo vệ bản quyền" startup_failure).
      Phiên tsudev đang hướng dẫn.
- [ ] Theo dõi phiếu tsudev 20261117-D2 (logo TsuDisk cho tsudev.com/apps): tsudev#294 chờ `nguyentrangtinhsu` duyệt.
- [ ] Giai đoạn 2 còn lại (`docs/ROADMAP.md`): giao diện WPF; đổi chữ cái/nhãn ổ; thu nhỏ ổ tổng quát; tạo/định dạng
      phân vùng trong vùng trống; `mbr2gpt`.
- [ ] Giai đoạn 3: bộ cài, winget `tsudev.TsuDisk` (cần PAT `public_repo` làm secret `WINGET_TOKEN`), ký SignPath (D5).
- [ ] Nợ thương hiệu: biến thể 16px đơn giản hóa; ảnh gốc lớn hơn/SVG (`docs/BRAND.md`).

## Đang thực hiện

| Task | Agent | Bắt đầu |
| --- | --- | --- |

## Đã hoàn thành (mới nhất trên cùng)

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

## Sự cố bảo mật

> Ghi theo `.standards/docs/SECURITY_BASELINE.md` mục 9. Để trống nếu chưa có.

- (chưa có)
