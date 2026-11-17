using TsuDisk.Cli;
using System.Globalization;
using TsuDisk.Core.Conversion;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;
using TsuDisk.Core.Serialization;
using TsuDisk.Windows;


/// <summary>Lenh chi doc ve suc khoe o va bang GPT (giai doan 2 - rui ro thap).</summary>
internal static partial class Commands
{
    public static int Health(CliArgs cli)
    {
        var all = SelectDisks(cli).Select(d => (Disk: d, Health: Backend.ReadHealth(d.Number))).ToList();
        if (cli.Has("json"))
        {
            Console.WriteLine(TsuDiskJson.Serialize([.. all.Where(x => x.Health is not null).Select(x => x.Health!)]));
            return ExitCodes.Ok;
        }
        var worst = HealthLevel.Good;
        foreach (var (disk, health) in all)
        {
            var verdict = HealthEvaluator.Evaluate(health);
            if (verdict.Level == HealthLevel.Bad || verdict.Level == HealthLevel.Warning && worst != HealthLevel.Bad)
                worst = verdict.Level;
            Console.WriteLine($"Đĩa {disk.Number}: {disk.FriendlyName} - {Units.Format(disk.Size)} - {LevelText(verdict.Level)}");
            if (health is not null)
            {
                Console.WriteLine($"  Loại: {health.MediaType}; Windows đánh giá: {health.HealthStatus}; vận hành: {string.Join(", ", health.OperationalStatus)}");
                Console.WriteLine($"  Nhiệt độ: {Show(health.TemperatureCelsius, "°C")}; độ mòn: {Show(health.WearPercent, "%")}; " +
                    $"giờ chạy: {Show(health.PowerOnHours, " giờ")}; lỗi đọc/ghi không sửa được: {Show(health.ReadErrorsUncorrected, "")}/{Show(health.WriteErrorsUncorrected, "")}");
            }
            foreach (var reason in verdict.Reasons)
                Console.WriteLine($"  - {reason}");
            Console.WriteLine();
        }
        if (!SystemTools.IsAdministrator())
            Console.WriteLine("(Nhiệt độ, độ mòn, lỗi đọc/ghi chỉ đọc được khi chạy với quyền quản trị.)");
        return worst == HealthLevel.Bad ? ExitCodes.Failed : ExitCodes.Ok;
    }

    /// <summary>
    /// kiem-tra-mbr2gpt: CHI DOC. Mac dinh kiem dia dang chua Windows; TsuDisk khong tu chuyen (cho ADR-0005).
    /// </summary>
    public static int CheckMbr2Gpt(CliArgs cli)
    {
        var disks = cli.IntValue("dia") is { } n
            ? Backend.ListDisks().Where(d => d.Number == n).ToList()
            : Backend.ListDisks().Where(d => d.IsBoot || d.IsSystem).Take(1).ToList();
        if (disks.Count == 0)
            throw new UsageException(cli.IntValue("dia") is { } m ? $"Không có đĩa số {m}." : "Không xác định được đĩa hệ thống: chỉ rõ bằng --dia N.");

        var reports = new List<Mbr2GptReport>();
        foreach (var disk in disks)
        {
            var layout = Backend.ReadLayout(disk.Number);
            if (Mbr2GptCheck.NotApplicableReason(disk) is { } reason)
            {
                reports.Add(new Mbr2GptReport(disk.Number, Mbr2GptVerdict.NotApplicable, null, [new(false, reason)], []));
                continue;
            }
            if (!SystemTools.IsAdministrator())
            {
                Console.Error.WriteLine("mbr2gpt cần quyền quản trị: mở Terminal (Admin) rồi chạy lại. TsuDisk tự kiểm bố cục:");
                foreach (var f in Mbr2GptCheck.PreCheck(layout))
                    Console.Error.WriteLine($"  [{(f.Blocking ? "CHẶN" : "LƯU Ý")}] {f.Message}");
                return ExitCodes.NeedsAdmin;
            }
            reports.Add(Mbr2GptTool.IsAvailable
                ? Mbr2GptTool.Validate(layout)
                : new Mbr2GptReport(disk.Number, Mbr2GptVerdict.ToolMissing, null,
                    [.. Mbr2GptCheck.PreCheck(layout), new(true, "Windows này không có mbr2gpt.exe (cần Windows 10 bản 1703 trở lên).")], []));
        }

        if (cli.Has("json"))
            Console.WriteLine(TsuDiskJson.Serialize(reports));
        else
            reports.ForEach(r => RenderMbr2Gpt(r, disks.First(d => d.Number == r.DiskNumber)));
        return reports.Any(r => r.Verdict is Mbr2GptVerdict.Fail or Mbr2GptVerdict.ToolMissing) ? ExitCodes.Failed : ExitCodes.Ok;
    }

    private static void RenderMbr2Gpt(Mbr2GptReport report, DiskInfo disk)
    {
        var verdict = report.Verdict switch
        {
            Mbr2GptVerdict.Pass => "ĐẠT - chuyển được sang GPT",
            Mbr2GptVerdict.Fail => "KHÔNG ĐẠT - mbr2gpt sẽ từ chối chuyển",
            Mbr2GptVerdict.ToolMissing => "KHÔNG KIỂM ĐƯỢC - thiếu mbr2gpt.exe",
            _ => "KHÔNG CẦN CHUYỂN",
        };
        Console.WriteLine($"Đĩa {disk.Number}: {disk.FriendlyName} - {Units.Format(disk.Size)} - {disk.Style} - {verdict}");
        foreach (var f in report.Findings)
            Console.WriteLine($"  [{(f.Blocking ? "CHẶN" : "LƯU Ý")}] {f.Message}");
        if (report.ToolLog.Count > 0)
        {
            Console.WriteLine(@"  Nhật ký mbr2gpt (%windir%\setupact.log):");
            foreach (var line in report.ToolLog)
                Console.WriteLine($"    {line}");
        }
        if (report.Verdict == Mbr2GptVerdict.Pass)
        {
            Console.WriteLine($"""
                  Bước tiếp theo (TsuDisk chưa tự chuyển - mbr2gpt /convert là công cụ chính thức của Microsoft):
                    1. Sao lưu dữ liệu quan trọng; tạm ngưng BitLocker nếu đang bật.
                    2. Terminal (Admin): mbr2gpt /convert /disk:{disk.Number} /allowFullOS
                    3. Khởi động lại, vào BIOS/UEFI đổi chế độ khởi động từ Legacy/CSM sang UEFI - KHÔNG đổi thì máy
                       không khởi động được. Sau đó mới bật Secure Boot nếu muốn.
                """);
        }
        Console.WriteLine();
    }

    public static int VerifyGpt(CliArgs cli)
    {
        if (!SystemTools.IsAdministrator())
        {
            Console.Error.WriteLine("Đọc trực tiếp bảng GPT cần quyền quản trị: mở Terminal (Admin) rồi chạy lại.");
            return ExitCodes.NeedsAdmin;
        }
        var healthy = true;
        foreach (var disk in SelectDisks(cli))
        {
            if (disk.Style != PartitionStyle.Gpt)
            {
                Console.WriteLine($"Đĩa {disk.Number}: {disk.Style} - không phải GPT, bỏ qua.\n");
                continue;
            }
            var report = GptVerifier.Verify(RawDiskReader.ReadGptRegions(disk), Backend.ReadLayout(disk.Number));
            healthy &= report.Healthy;
            if (cli.Has("json"))
            {
                Console.WriteLine(TsuDiskJson.Serialize(report));
                continue;
            }
            Console.WriteLine($"Đĩa {disk.Number}: {disk.FriendlyName} - GPT {(report.Healthy ? "TOÀN VẸN" : "CÓ LỖI")}");
            if (report.Primary is { } h)
                Console.WriteLine($"  GUID đĩa {h.DiskGuid}; LBA dùng được {h.FirstUsableLba}-{h.LastUsableLba}; header dự phòng ở LBA {h.AlternateLba}");
            foreach (var e in report.Entries)
                Console.WriteLine($"  #{e.Index,-3} {PartitionTypes.Describe(PartitionTypes.FromGpt(e.TypeGuid)),-38} {Units.Format(e.OffsetBytes(disk.LogicalSectorSize)),12} {Units.Format(e.SizeBytes(disk.LogicalSectorSize)),11}  {e.Name}");
            foreach (var f in report.Findings)
                Console.WriteLine($"  [{f.Level switch { GptCheckLevel.Ok => "ĐẠT", GptCheckLevel.Warning => "LƯU Ý", _ => "LỖI" }}] {f.Message}");
            Console.WriteLine();
        }
        return healthy ? ExitCodes.Ok : ExitCodes.Failed;
    }

    public static int Backup(CliArgs cli)
    {
        if (!SystemTools.IsAdministrator())
        {
            Console.Error.WriteLine("Sao lưu bảng phân vùng thô cần quyền quản trị: mở Terminal (Admin) rồi chạy lại.");
            return ExitCodes.NeedsAdmin;
        }
        var root = cli.Value("thu-muc-nhat-ky") ?? DefaultLogRoot;
        var directory = Path.Combine(root, $"{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-sao-luu");
        var ok = true;
        foreach (var disk in SelectDisks(cli))
        {
            var layout = Backend.ReadLayout(disk.Number);
            if (disk.Style != PartitionStyle.Gpt)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"bo-cuc-dia{disk.Number}.json"), TsuDiskJson.Serialize(new DiskLayout[] { layout }));
                Console.WriteLine($"Đĩa {disk.Number}: {disk.Style} - đã lưu bố cục (sao lưu sector thô chỉ hỗ trợ GPT).");
                continue;
            }
            var report = GptBackup.Save(layout, directory);
            ok &= report.Healthy;
            Console.WriteLine($"Đĩa {disk.Number}: đã sao lưu 2 vùng GPT thô + bố cục + kiểm tra - GPT {(report.Healthy ? "toàn vẹn" : "CÓ LỖI, xem kiem-tra-gpt")}.");
        }
        Console.WriteLine($"\nThư mục sao lưu: {directory}");
        Console.WriteLine("Giữ thư mục này ở nơi an toàn (ổ khác/USB). Khôi phục là thao tác chuyên gia - xem hướng dẫn trên disk.tsudev.com.");
        return ok ? ExitCodes.Ok : ExitCodes.Failed;
    }

    private static string LevelText(HealthLevel level) => level switch
    {
        HealthLevel.Good => "KHỎE MẠNH",
        HealthLevel.Warning => "CẢNH BÁO",
        HealthLevel.Bad => "CÓ DẤU HIỆU HỎNG - không thay đổi bảng phân vùng",
        _ => "KHÔNG RÕ",
    };

    private static string Show<T>(T? value, string unit) where T : struct =>
        value is { } v ? string.Create(CultureInfo.InvariantCulture, $"{v}{unit}") : "-";
}
