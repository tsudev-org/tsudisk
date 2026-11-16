using TsuDisk.Cli;
using System.Globalization;
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
