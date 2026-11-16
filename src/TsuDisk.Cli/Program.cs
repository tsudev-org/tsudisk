using System.Globalization;
using System.Reflection;
using System.Text;
using TsuDisk.Cli;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Serialization;
using TsuDisk.Windows;

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("vi-VN");

try
{
    var cli = CliArgs.Parse(args);
    return cli.Command switch
    {
        "danh-sach" => Commands.List(cli),
        "phan-tich" => Commands.Analyze(cli),
        "ke-hoach" => Commands.Extend(cli, planOnly: true),
        "mo-rong" => Commands.Extend(cli, planOnly: false),
        "nhat-ky" => Commands.Journal(cli),
        "winre" => Commands.WinRe(),
        "phien-ban" => Commands.Version(),
        _ => Commands.Help(),
    };
}
catch (UsageException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCodes.Usage;
}
catch (DiskOperationException ex)
{
    Console.Error.WriteLine($"Lỗi: {ex.Message}");
    return ExitCodes.Failed;
}

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Failed = 1;
    public const int Usage = 2;
    public const int NeedsAdmin = 3;
    public const int RolledBack = 4;
    public const int NeedsAttention = 5;
}

internal static class Commands
{
    private static readonly WindowsDiskBackend Backend = new();

    public static string ToolVersion =>
        typeof(Commands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";

    private static string DefaultLogRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TsuDisk", "nhat-ky");

    public static int List(CliArgs cli)
    {
        var layouts = SelectDisks(cli).Select(d => Backend.ReadLayout(d.Number)).ToList();
        if (cli.Has("json"))
        {
            Console.WriteLine(TsuDiskJson.Serialize(layouts));
            return ExitCodes.Ok;
        }
        layouts.ForEach(Render.Layout);
        return ExitCodes.Ok;
    }

    public static int Analyze(CliArgs cli)
    {
        foreach (var disk in SelectDisks(cli))
        {
            var layout = Backend.ReadLayout(disk.Number);
            var advice = LayoutAnalyzer.Analyze(layout);
            if (cli.Has("json"))
            {
                Console.WriteLine(TsuDiskJson.Serialize(advice));
                continue;
            }
            Render.Layout(layout);
            Console.WriteLine($"Phân tích vùng trống đĩa {disk.Number} (tổng {Units.Format(layout.UnallocatedTotal)}):");
            Render.Advice(advice);
        }
        return ExitCodes.Ok;
    }

    public static int Extend(CliArgs cli, bool planOnly)
    {
        if (cli.Positionals.Count != 1)
            throw new UsageException("Cần đúng một ổ đích, ví dụ: tsudisk mo-rong C:");
        var letter = CliArgs.ParseDriveLetter(cli.Positionals[0]);
        var request = BuildRequest(cli, letter);

        var layout = Backend.ListDisks().Select(d => Backend.ReadLayout(d.Number)).FirstOrDefault(l => l.FindByLetter(letter) is not null)
            ?? throw new DiskOperationException($"Không có ổ {letter}: trên đĩa nào.");
        var result = ExtendPlanner.Plan(layout, ReadWinReSafely(), request);
        if (!result.Success)
        {
            if (cli.Has("json"))
                Console.WriteLine(TsuDiskJson.Serialize(result.Refusal!));
            else
                Render.Refusal(result.Refusal!);
            return ExitCodes.Failed;
        }
        var plan = result.Plan!;
        if (cli.Has("json"))
            Console.WriteLine(TsuDiskJson.Serialize(plan));
        else
            Render.Plan(plan);
        if (planOnly)
            return ExitCodes.Ok;

        if (!SystemTools.IsAdministrator())
        {
            Console.Error.WriteLine("Cần quyền quản trị: mở Terminal (Admin) rồi chạy lại lệnh này.");
            return ExitCodes.NeedsAdmin;
        }
        var dryRun = cli.Has("chay-thu");
        if (!dryRun && !cli.Has("dong-y") && !Confirm(letter))
        {
            Console.WriteLine("Đã hủy - không có thay đổi nào.");
            return ExitCodes.Ok;
        }

        var root = cli.Value("thu-muc-nhat-ky") ?? DefaultLogRoot;
        var runDirectory = Path.Combine(root, $"{DateTime.Now:yyyyMMdd-HHmmss}-{plan.Id}{(dryRun ? "-chay-thu" : "")}");
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "plan.json"), TsuDiskJson.Serialize(plan), Encoding.UTF8);
        var store = new ProgressJournalStore(new FileJournalStore(runDirectory));

        var journal = new PlanRunner(Backend, store).Run(plan, new RunOptions(dryRun, runDirectory, ToolVersion));
        Console.WriteLine();
        Render.Journal(journal);
        Console.WriteLine($"\nNhật ký và bản sao lưu: {runDirectory}");
        if (journal.State is RunState.RolledBack or RunState.NeedsAttention)
        {
            foreach (var advice in RecoveryAdvisor.Advise(journal))
                Console.WriteLine($"  -> {advice}");
        }
        else if (journal.State == RunState.Succeeded)
        {
            Render.Layout(Backend.ReadLayout(plan.DiskNumber));
        }
        return journal.State switch
        {
            RunState.Succeeded or RunState.DryRunCompleted => ExitCodes.Ok,
            RunState.RolledBack => ExitCodes.RolledBack,
            RunState.NeedsAttention => ExitCodes.NeedsAttention,
            _ => ExitCodes.Failed,
        };
    }

    public static int Journal(CliArgs cli)
    {
        var path = cli.Positionals.FirstOrDefault() ?? LatestJournal();
        if (path is null || !File.Exists(path))
        {
            Console.WriteLine("Chưa có nhật ký nào.");
            return ExitCodes.Ok;
        }
        var journal = TsuDiskJson.DeserializeJournal(File.ReadAllText(path))
            ?? throw new DiskOperationException($"Tệp nhật ký hỏng: {path}");
        Render.Journal(journal);
        Console.WriteLine("\nViệc cần làm:");
        foreach (var advice in RecoveryAdvisor.Advise(journal))
            Console.WriteLine($"  -> {advice}");
        return ExitCodes.Ok;
    }

    public static int WinRe()
    {
        if (!SystemTools.IsAdministrator())
        {
            Console.Error.WriteLine("Đọc trạng thái WinRE cần quyền quản trị: mở Terminal (Admin) rồi chạy lại.");
            return ExitCodes.NeedsAdmin;
        }
        var status = Backend.ReadWinRe();
        Console.WriteLine(status.Enabled
            ? $"WinRE đang BẬT, nằm trên đĩa {status.DiskNumber} phân vùng {status.PartitionNumber}."
            : "WinRE đang TẮT.");
        return ExitCodes.Ok;
    }

    public static int Version()
    {
        Console.WriteLine($"TsuDisk {ToolVersion} - https://disk.tsudev.com - Một sản phẩm của tsudev (https://tsudev.com)");
        return ExitCodes.Ok;
    }

    public static int Help()
    {
        Console.WriteLine($"""
            TsuDisk {ToolVersion} - quản lý phân vùng ổ cứng Windows an toàn. https://disk.tsudev.com

            Cách dùng:
              tsudisk danh-sach [--dia N] [--json]        Liệt kê đĩa, phân vùng, vùng trống (không cần quyền quản trị)
              tsudisk phan-tich [--dia N] [--json]        Mỗi vùng trống gộp được vào ổ nào, vì sao
              tsudisk ke-hoach <X:> [tùy chọn] [--json]   Lập và in kế hoạch mở rộng, KHÔNG thay đổi gì
              tsudisk mo-rong <X:> [tùy chọn]             Lập kế hoạch, hỏi xác nhận, rồi thực hiện (cần quyền quản trị)
              tsudisk nhat-ky [tệp journal.json]          Xem lần chạy gần nhất và việc cần làm nếu bị đứt giữa chừng
              tsudisk winre                               Trạng thái Windows Recovery Environment
              tsudisk phien-ban | tro-giup

            Tùy chọn cho ke-hoach / mo-rong:
              --chay-thu                 Chỉ chạy các bước không ghi đĩa (quét, sao lưu) để kiểm tra
              --dong-y                   Không hỏi xác nhận (dùng trong kịch bản tự động)
              --recovery-mb N            Kích thước Recovery mới (512-4096, mặc định 1024)
              --recovery tao-lai-rong    Cho phép xóa và tạo lại RỖNG một Recovery không chứa WinRE đang dùng
              --chi-o-ao                 Chỉ cho phép thao tác trên ổ ảo VHD/VHDX (kiểm thử)
              --thu-muc-nhat-ky DIR      Nơi lưu nhật ký và bản sao lưu (mặc định %ProgramData%\TsuDisk\nhat-ky)

            Bí danh tiếng Anh: list, analyze, plan, extend, journal, version, help; --dry-run, --yes, --disk, --virtual-only, --log-dir.
            Mã thoát: 0 xong, 1 từ chối/lỗi, 2 sai cú pháp, 3 thiếu quyền quản trị, 4 thất bại đã hoàn tác, 5 cần xử lý.
            """);
        return ExitCodes.Ok;
    }

    private static ExtendRequest BuildRequest(CliArgs cli, char letter)
    {
        var request = new ExtendRequest(letter) { VirtualDiskOnly = cli.Has("chi-o-ao") };
        if (cli.IntValue("recovery-mb") is { } mb)
            request = request with { RecoverySize = mb * Units.MiB };
        return cli.Value("recovery") switch
        {
            null or "winre" => request,
            "tao-lai-rong" or "recreate-empty" => request with { RecoveryPolicy = RecoveryPolicy.RecreateEmpty },
            var other => throw new UsageException($"--recovery chỉ nhận winre hoặc tao-lai-rong, không nhận {other}."),
        };
    }

    private static IEnumerable<DiskInfo> SelectDisks(CliArgs cli) =>
        cli.IntValue("dia") is { } n ? Backend.ListDisks().Where(d => d.Number == n) : Backend.ListDisks();

    /// <summary>reagentc can quyen quan tri; lenh chi doc van chay duoc khi khong co (coi WinRE la khong ro = tat).</summary>
    private static WinReStatus ReadWinReSafely()
    {
        if (!SystemTools.IsAdministrator())
        {
            Console.Error.WriteLine("(Chưa có quyền quản trị nên không đọc được WinRE - kế hoạch coi WinRE là không rõ.)");
            return WinReStatus.Disabled;
        }
        try
        {
            return Backend.ReadWinRe();
        }
        catch (DiskOperationException)
        {
            Console.Error.WriteLine("(Không đọc được trạng thái WinRE khi chưa có quyền quản trị - kế hoạch coi WinRE là không rõ.)");
            return WinReStatus.Disabled;
        }
    }

    private static bool Confirm(char letter)
    {
        Console.Write($"Gõ chữ cái ổ đích ({letter}) rồi Enter để thực hiện, Enter trống để hủy: ");
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, letter.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, $"{letter}:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? LatestJournal()
    {
        if (!Directory.Exists(DefaultLogRoot))
            return null;
        return Directory.EnumerateDirectories(DefaultLogRoot).OrderDescending()
            .Select(d => Path.Combine(d, "journal.json")).FirstOrDefault(File.Exists);
    }
}

/// <summary>In tien do tung buoc ra man hinh, dong thoi chuyen nhat ky xuong kho that.</summary>
internal sealed class ProgressJournalStore(IJournalStore inner) : IJournalStore
{
    private readonly Dictionary<int, StepStatus> _last = [];

    public void Save(RunJournal journal)
    {
        inner.Save(journal);
        foreach (var step in journal.Steps)
        {
            if (_last.TryGetValue(step.Index, out var previous) && previous == step.Status || step.Status == StepStatus.Pending)
                continue;
            _last[step.Index] = step.Status;
            Console.WriteLine($"  [{Render.StatusText(step.Status),-10}] {step.Index}. {step.Title}{(step.Message is null || step.Status == StepStatus.Running ? "" : $" - {step.Message}")}");
        }
    }
}
