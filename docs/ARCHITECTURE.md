# ARCHITECTURE - TsuDisk

> Bắt buộc theo `.standards/docs/PROJECT_STRUCTURE.md` và `.standards/docs/LANGUAGE_SELECTION.md` mục 6. Quyết định
> lớn có ADR riêng trong [`adr/`](adr/).

## 1. Tech Stack

| Thành phần | Chọn | Mức ở `LANGUAGE_SELECTION.md` | Lý do |
| --- | --- | --- | --- |
| Lõi + CLI | C# / .NET 10 LTS | ⭐ "Ứng dụng Windows" | API lưu trữ của Windows (Storage Management API qua WMI) dùng trực tiếp từ .NET; an toàn bộ nhớ; cùng stack với Tsu Swico, TsuFiles |
| Giao diện (giai đoạn 2) | WPF trên .NET 10 | ⭐ | Bản đồ đĩa trực quan kiểu Disk Management, chỉ cần Windows |
| Website | Astro (trang tĩnh, TypeScript) | ✅ JS thuần cho trang tĩnh / TS | Cùng khung với swico.tsudev.com, files.tsudev.com |
| Hạ tầng web | Cloudflare Workers Static Assets | `FREE_TIER_STACK.md` | 0 đồng; Custom Domain tự tạo DNS + chứng chỉ |

**Phương án đã cân nhắc** (chi tiết [`adr/0001-chon-nen-tang.md`](adr/0001-chon-nen-tang.md)):

- *Rust (Tauri)*: an toàn bộ nhớ ngang C#, nhưng Storage Management API là COM/WMI - hệ sinh thái Rust phải tự bọc
  nhiều hơn; giao diện web trong Tauri không mang lại lợi ích cho công cụ chỉ chạy trên Windows.
- *PowerShell thuần* (như `extend-c.ps1` bản đầu): nhanh, nhưng không kiểm thử đơn vị được logic lập kế hoạch, khó
  phân phối, khó bảo đảm bất biến.
- *C++ + VDS/IOCTL trực tiếp*: VDS đã bị Microsoft đánh dấu lỗi thời từ Windows 8; IOCTL ghi bảng phân vùng trực
  tiếp bỏ qua mọi kiểm tra của hệ thống - rủi ro không cần thiết.

## 2. Mô hình: Ports & Adapters

| Tầng | Thư mục | Nền tảng | Test trên Linux |
| --- | --- | --- | --- |
| Nghiệp vụ thuần | `src/TsuDisk.Core/` | `net10.0` | ✅ test đơn vị + FakeBackend tiêm lỗi |
| Adapter hệ thống | `src/TsuDisk.Windows/` | `net10.0-windows10.0.17763.0` | ❌ - E2E VHDX trên Windows |
| Điểm vào | `src/TsuDisk.Cli/` | `net10.0-windows10.0.17763.0` | ❌ - E2E VHDX trên Windows |

`TsuDisk.Core` gồm:

| Thư mục | Trách nhiệm |
| --- | --- |
| `Model/` | `DiskInfo`, `PartitionInfo`, `DiskLayout` (chia đoạn, tìm vùng trống), bảng GUID/byte loại phân vùng, `WinReStatus` + bộ phân tích `reagentc /info` |
| `Planning/` | `ExtendPlanner` (lập kế hoạch), `LayoutAnalyzer` (vùng trống nào gộp được vào đâu), `Plan`/`PlanStep`/`Refusal` |
| `Safety/` | `LayoutSimulator` (chạy kế hoạch trên mô hình), `SafetyGuard` (bất biến), `LayoutFingerprint` |
| `Execution/` | `IDiskBackend` (cổng ra hệ điều hành), `PlanRunner` (chạy + kiểm từng bước + hoàn tác), `RunJournal`, `RecoveryAdvisor` |
| `Scripts/` | `DiskpartScript` - kịch bản diskpart sinh thuần, test từng dòng |
| `Serialization/` | JSON sinh lúc biên dịch (an toàn khi cắt tỉa) |

## 3. Luật bất di bất dịch

| Luật | Vì sao |
| --- | --- |
| Logic quyết định (chạm phân vùng nào, kích thước bao nhiêu, bước nào, hoàn tác ra sao) **chỉ nằm trong Core** | Test được trên Linux bằng `FakeBackend` có tiêm lỗi |
| Adapter Windows **không quyết định gì**: chỉ đọc, hoặc thực hiện đúng một thao tác Core yêu cầu | Một chỗ duy nhất để rà soát bảo mật |
| Mọi bước ghi đĩa phải có trong `LayoutSimulator` và qua `SafetyGuard.CheckPlan` | Không có bước ghi "ngoài sổ" |
| Danh tính phân vùng là **offset**, không phải số thứ tự | Số thứ tự đổi được (MBR, tạo/xóa) - offset thì không |
| Công cụ hệ thống: đường dẫn tuyệt đối System32, `ArgumentList`, không shell | Chống chiếm quyền thực thi khi chạy với quyền quản trị |
| Không mã mạng trong ứng dụng | Hạng C; không có kênh cập nhật để bị chiếm |
| Không bật `PublishTrimmed`/NativeAOT | `System.Management` dùng COM/reflection - Tsu Swico đã mất dữ liệu WMI âm thầm vì cắt tỉa |

## 4. Luồng `tsudisk mo-rong C:`

```
ListDisks/ReadLayout ──> ExtendPlanner.Plan ──> LayoutSimulator ──> SafetyGuard.CheckPlan ──> in kế hoạch
                                                                                              │ xác nhận
PlanRunner.Run: fingerprint ─> [Scan] ─> [Backup] ─> [DisableWinRe] ─> [Delete Recovery]* ─> [Extend]* ─> [Create Recovery]* ─> [EnableWinRe] ─> [Verify]
                 (* kiểm lại tiền điều kiện trên bố cục đọc mới; mỗi chuyển trạng thái ghi journal.json)
lỗi ở bước k ─> chạy bước bù (ngược) ─> RolledBack | NeedsAttention ─> RecoveryAdvisor
```

Vì sao từng bước như vậy: [`adr/0002-mo-hinh-ke-hoach-an-toan.md`](adr/0002-mo-hinh-ke-hoach-an-toan.md).

## 5. Phân loại dữ liệu

**D1 - Nội bộ kỹ thuật** (`SECURITY_BASELINE.md` mục 2): tên thiết bị đĩa, bố cục phân vùng, chữ cái ổ, nhãn ổ. Không
có dữ liệu cá nhân, không đọc nội dung tệp. Lưu cục bộ ở `%ProgramData%\TsuDisk\nhat-ky`, không truyền đi.

## 6. Hạn mức 0 đồng đang dùng

| Dịch vụ | Dùng cho | Hạn mức miễn phí | Mức dùng dự kiến |
| --- | --- | --- | --- |
| GitHub (repo công khai) | Mã, CI Linux + Windows, Releases | Phút CI không giới hạn cho repo công khai | ~10 phút/PR |
| Cloudflare Workers Static Assets | disk.tsudev.com | Tệp tĩnh không giới hạn yêu cầu | Nhỏ |

## 7. Ràng buộc bên ngoài

| Ràng buộc | Hệ quả |
| --- | --- |
| Ký số qua SignPath Foundation (D5 của hệ sinh thái) | Repo công khai + Apache-2.0 |
| Máy dev là Linux + WSL | Test đơn vị chạy trên Linux; E2E chạy trên Windows của máy dev (UAC) và CI `windows-latest` |
| Ghi đĩa cần quyền quản trị | Manifest `asInvoker`; lệnh ghi tự kiểm quyền và báo rõ, không tự nâng quyền ngầm |
