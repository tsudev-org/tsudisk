# PHIẾU PHỐI HỢP - DD1 dọn ổ đĩa (26/11/2026)

- **Mã phiếu**: 20261126-DD1 - **Từ**: agent-tsudisk (phiên `tsudisk-4c`) - **Đến**: phiên `tsucwico-2e` + 👤
- **Trạng thái**: MỞ (chờ 👤 mục 3)

## 1. Chia việc (để tránh xoá trùng hoặc xung đột)

| Vùng | Phiên phụ trách |
| --- | --- |
| Toàn máy: `~/.cache`, cargo (cargo-sweep), npm/pip, apt, journal, `%TEMP%` Windows | `tsucwico-2e` - báo cáo `~/projects/tsucwico/logs/handover/20261126-DD1_*.md` |
| Bản build tái tạo được trong `~/projects/tsudisk` | `tsudisk-4c` |
| `~/.nuget`, SDK dotnet | Không ai dọn khi chưa báo tsudisk (repo dùng NuGet lock file) |

## 2. Số đo (26/11/2026 22:15)

- Ổ C: 326 GB, còn **9,2 GB (98%)**.
- `ext4.vhdx` của Ubuntu (đặt trên C): **186 GB**; bên trong `df /` mới dùng 154 GB, nên khoảng 32 GB có thể thu hồi ngay
  bằng cách co VHDX. Dọn rác trong Ubuntu KHÔNG trả dung lượng cho ổ C nếu không co VHDX (AKB
  `LES-20261126-2217-2a2b`).
- Bản build tái tạo được trong tsudisk (`git clean -ndX`): khoảng 1,1 GB, gồm `website/node_modules` 524 MB,
  `src/TsuDisk.Gui/bin` 312 MB, `src/TsuDisk.Cli/bin` 204 MB, `artifacts` 43 MB, cùng `obj`, `dist`, `.astro`, `.wrangler`.
  Không có tệp bí mật nào trong danh sách. Auto mode **chặn** `git clean -fdX`, nên agent chưa xoá.

## 3. Việc 👤 chủ dự án

1. (Tuỳ chọn) Xoá bản build của tsudisk: `cd ~/projects/tsudisk && git clean -fdX`. Lệnh này chỉ xoá các tệp đã gitignore,
   tất cả đều tái tạo được bằng `dotnet build` và `npm ci`.
2. Sau khi `tsucwico-2e` dọn xong: `sudo fstrim -av` trong Ubuntu.
3. Bật VHDX thưa để ổ C tự được trả dung lượng về sau. Đóng mọi phiên Claude/VS Code trong WSL, rồi chạy trong PowerShell:
   `wsl --shutdown` và `wsl --manage Ubuntu --set-sparse true`. Nếu muốn co ngay một lần (PowerShell Admin, sau
   `wsl --shutdown`): `Optimize-VHD -Path "C:\Users\devng\AppData\Local\wsl\{c3304ba3-a024-4a5c-a690-5ac0f41f96d4}\ext4.vhdx" -Mode Full`
   (cần Hyper-V), hoặc dùng `diskpart` với lệnh `compact vdisk`.
4. `C:\Users\devng\.wslconfig` đang có quyền 777. Tệp không chứa bí mật (chỉ có memory/swap/processors), nên không cần xử lý gấp.

## 3b. Số đo bổ sung của `tsuguard-a6` (22:20 26/11, chỉ đọc)

- VHDX 200 GB (bên trong dùng 154 GB). `%LOCALAPPDATA%\Temp` 14 GB, trong đó có khoảng 12 GB là 6 tệp `swap.vhdx` WSL bị
  bỏ lại (tệp đang dùng thì bị khoá). `SoftwareDistribution\Download` 1,2 GB, Thùng rác 345 MB.
- Trong WSL: thư mục build trong `~/projects` 62 GB (`tsuscan/target` 11 GB, `tsulink/target` 11 GB, `ubnd-app` 6 GB,
  `tsucwico/target` 3,8 GB), `~/.gradle` 9,3 GB, `~/.npm` 2,6 GB. Các mục này thuộc phần của `tsucwico-2e`.
- ⛔ **BẢO MẬT**: phần mềm độc hại **Infatica** (tác vụ `\infatica`, `infatica_agent.exe`) VẪN đang chạy. KHÔNG phiên nào
  được xoá các thư mục Infatica/lrpjava/Precipitator như "rác". `tsuguard-a6` đang chờ 👤 quyết cách ngăn chặn và việc
  giữ bằng chứng pháp y.

## 4. Kết quả

- 26/11/2026 - Mở phiếu và chia việc với `tsucwico-2e` qua SendMessage.
- 26/11/2026 - Chủ dự án ủy quyền: `git clean -fdX` trong tsudisk đã chạy (giải phóng khoảng 1,1 GB; repo còn 27 MB).
  Kiểm chứng: `dotnet restore --locked-mode` và `dotnet test` đạt 391/391. Số đo lúc đó: Ubuntu 154 -> 123 GB đã dùng
  (phiên `tsucwico-2e` đang dọn), C còn 22 GB (94%). Mục 3.1 xong; mục 3.2-3.3 (fstrim + co VHDX) vẫn chờ 👤 làm sau cùng.
  Đây là bước thu hồi lớn nhất: khoảng 75 GB.
- 26/11/2026 - `tsucwico-2e` báo đã dọn xong toàn máy (C: 9,2 -> 22 GB). Báo cáo:
  `~/projects/tsucwico/logs/handover/20261126-DD1_don-o-dia-toan-may.md`, AKB `LES-20261126-2231-b6f6`. Phần agent
  đã xong; chỉ còn mục 3.2-3.3 cho 👤.
