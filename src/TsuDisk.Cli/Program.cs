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
        "ke-hoach-winre" => Commands.GrowWinRe(cli, planOnly: true),
        "mo-rong-winre" => Commands.GrowWinRe(cli, planOnly: false),
        "ke-hoach-doi-ten" => Commands.Rename(cli, planOnly: true),
        "doi-ten" => Commands.Rename(cli, planOnly: false),
        "ke-hoach-tao-phan-vung" => Commands.CreatePartition(cli, planOnly: true),
        "tao-phan-vung" => Commands.CreatePartition(cli, planOnly: false),
        "ke-hoach-thu-nho" => Commands.Shrink(cli, planOnly: true),
        "thu-nho" => Commands.Shrink(cli, planOnly: false),
        "ke-hoach-xoa" => Commands.DeletePartition(cli, planOnly: true),
        "xoa-phan-vung" => Commands.DeletePartition(cli, planOnly: false),
        "nhat-ky" => Commands.Journal(cli),
        "winre" => Commands.WinRe(),
        "suc-khoe" => Commands.Health(cli),
        "kiem-tra-gpt" => Commands.VerifyGpt(cli),
        "kiem-tra-mbr2gpt" => Commands.CheckMbr2Gpt(cli),
        "sao-luu" => Commands.Backup(cli),
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

internal static partial class Commands
{
    internal static readonly WindowsDiskBackend Backend = new();

    public static string ToolVersion =>
        typeof(Commands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0";

    internal static string DefaultLogRoot =>
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
        return ShowAndRun(cli, ExtendPlanner.Plan(layout, ReadWinReSafely(), request), planOnly, letter.ToString());
    }

    public static int GrowWinRe(CliArgs cli, bool planOnly)
    {
        var winRe = ReadWinReSafely();
        var diskNumber = cli.IntValue("dia") ?? winRe.DiskNumber ?? 0;
        var request = new WinReGrowRequest
        {
            VirtualDiskOnly = cli.Has("chi-o-ao"),
            TargetSize = cli.IntValue("recovery-mb") is { } mb ? mb * Units.MiB : null,
            RecoveryPolicy = cli.Value("recovery") switch
            {
                null or "winre" => RecoveryPolicy.RelocateActiveWinReOnly,
                "tao-lai-rong" or "recreate-empty" => RecoveryPolicy.RecreateEmpty,
                var other => throw new UsageException($"--recovery chỉ nhận winre hoặc tao-lai-rong, không nhận {other}."),
            },
        };
        return ShowAndRun(cli, WinReGrowPlanner.Plan(Backend.ReadLayout(diskNumber), winRe, request), planOnly, "WINRE");
    }

    public static int Rename(CliArgs cli, bool planOnly)
    {
        if (cli.Positionals.Count != 1)
            throw new UsageException("Cần đúng một ổ, ví dụ: tsudisk doi-ten E: --chu-cai F --nhan \"Dữ liệu\"");
        var letter = CliArgs.ParseDriveLetter(cli.Positionals[0]);
        var request = new RenameRequest(letter,
            cli.Value("chu-cai") is { } next ? CliArgs.ParseDriveLetter(next) : null,
            cli.Value("nhan"))
        {
            VirtualDiskOnly = cli.Has("chi-o-ao"),
            SystemDrive = Environment.GetEnvironmentVariable("SystemDrive") is [var sys, ..] ? sys : null,
        };
        var layout = Backend.ListDisks().Select(d => Backend.ReadLayout(d.Number)).FirstOrDefault(l => l.FindByLetter(letter) is not null)
            ?? throw new DiskOperationException($"Không có ổ {letter}: trên đĩa nào.");
        return ShowAndRun(cli, RenamePlanner.Plan(layout, Backend.DriveLettersInUse(), request), planOnly, letter.ToString());
    }

    public static int Shrink(CliArgs cli, bool planOnly)
    {
        if (cli.Positionals.Count != 1)
            throw new UsageException("Cần đúng một ổ, ví dụ: tsudisk thu-nho D: --giam-mb 20480");
        var letter = CliArgs.ParseDriveLetter(cli.Positionals[0]);
        var reduceMb = cli.IntValue("giam-mb") ?? throw new UsageException("Cần --giam-mb N: số MB muốn nhường ra (vùng trống nằm ngay sau ổ).");
        var layout = Backend.ListDisks().Select(d => Backend.ReadLayout(d.Number)).FirstOrDefault(l => l.FindByLetter(letter) is not null)
            ?? throw new DiskOperationException($"Không có ổ {letter}: trên đĩa nào.");
        var target = layout.FindByLetter(letter)!;
        var request = new ShrinkRequest(letter, reduceMb * Units.MiB)
        {
            VirtualDiskOnly = cli.Has("chi-o-ao"),
            SystemDrive = Environment.GetEnvironmentVariable("SystemDrive") is [var sys, ..] ? sys : null,
            WindowsMinSize = target.FileSystem == "NTFS" ? WindowsDiskBackend.TryGetMinPartitionSize(layout.Disk.Number, target.PartitionNumber) : null,
        };
        return ShowAndRun(cli, ShrinkPlanner.Plan(layout, request), planOnly, letter.ToString());
    }

    public static int CreatePartition(CliArgs cli, bool planOnly)
    {
        var diskNumber = cli.IntValue("dia")
            ?? throw new UsageException("Cần chỉ rõ đĩa bằng --dia N (xem số đĩa bằng tsudisk danh-sach) - TsuDisk không tự đoán đĩa để tạo phân vùng.");
        var request = new CreatePartitionRequest
        {
            ExtentIndex = cli.IntValue("vung"),
            Size = cli.IntValue("kich-thuoc-mb") is { } mb ? mb * Units.MiB : null,
            FileSystem = cli.Value("he-tep") ?? "NTFS",
            Label = cli.Value("nhan") ?? "",
            Letter = cli.Value("chu-cai") is { } letter ? CliArgs.ParseDriveLetter(letter) : null,
            VirtualDiskOnly = cli.Has("chi-o-ao"),
        };
        return ShowAndRun(cli, CreatePartitionPlanner.Plan(Backend.ReadLayout(diskNumber), Backend.DriveLettersInUse(), request), planOnly, "TAO");
    }

    /// <summary>
    /// xoa-phan-vung (ADR-0005): xac nhan bang "X: nhan" go lai dung tung ky tu; chay tu dong phai co --xac-nhan khop,
    /// --dong-y khong du. Thu muc nhat ky va tsudisk.exe khong duoc nam tren o sap xoa.
    /// </summary>
    public static int DeletePartition(CliArgs cli, bool planOnly)
    {
        if (cli.Positionals.Count != 1)
            throw new UsageException("Cần đúng một ổ, ví dụ: tsudisk xoa-phan-vung E:");
        var letter = CliArgs.ParseDriveLetter(cli.Positionals[0]);
        var layout = Backend.ListDisks().Select(d => Backend.ReadLayout(d.Number)).FirstOrDefault(l => l.FindByLetter(letter) is not null)
            ?? throw new DiskOperationException($"Không có ổ {letter}: trên đĩa nào.");
        var target = layout.FindByLetter(letter)!;
        var result = DeletePartitionPlanner.Plan(layout, new DeletePartitionRequest(letter) { VirtualDiskOnly = cli.Has("chi-o-ao") });
        if (result.Success && !planOnly)
        {
            var logRoot = Path.GetFullPath(cli.Value("thu-muc-nhat-ky") ?? DefaultLogRoot);
            if (char.ToUpperInvariant(logRoot[0]) == letter)
                throw new UsageException($"Thư mục nhật ký/sao lưu {logRoot} nằm trên chính ổ {letter}: sắp xóa - chọn --thu-muc-nhat-ky ở ổ khác.");
            if (Environment.ProcessPath is [var exeDrive, ':', ..] && char.ToUpperInvariant(exeDrive) == letter)
                throw new UsageException($"tsudisk.exe đang chạy từ ổ {letter}: - chép nó sang ổ khác rồi chạy lại.");
        }
        return ShowAndRun(cli, result, planOnly, DeletePartitionPlanner.ConfirmationText(target), () => ConfirmDelete(cli, target));
    }

    private static bool ConfirmDelete(CliArgs cli, PartitionInfo target)
    {
        var expected = DeletePartitionPlanner.ConfirmationText(target);
        if (cli.Value("xac-nhan") is { } given)
        {
            if (DeletePartitionPlanner.ConfirmationMatches(target, given))
                return true;
            throw new UsageException($"--xac-nhan \"{given}\" không khớp \"{expected}\" - không xóa gì.");
        }
        if (cli.Has("dong-y"))
            throw new UsageException($"Xóa ổ không nhận --dong-y: truyền --xac-nhan \"{expected}\" để chạy tự động.");
        Console.InputEncoding = Encoding.UTF8;
        Console.Write($"XÓA ổ {target.DriveLetter}: - gõ chính xác \"{expected}\" rồi Enter để thực hiện, Enter trống để hủy: ");
        return DeletePartitionPlanner.ConfirmationMatches(target, Console.ReadLine());
    }

    /// <summary>Luong chung: in ke hoach (hoac ly do tu choi), xac nhan bang mot tu khoa, chay, bao cao, ma thoat.</summary>
    private static int ShowAndRun(CliArgs cli, PlanResult result, bool planOnly, string confirmWord, Func<bool>? confirm = null)
    {
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
        if (!dryRun && !(confirm is not null ? confirm() : cli.Has("dong-y") || Confirm(confirmWord)))
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
        Console.WriteLine($"TsuDisk {ToolVersion} - https://disk.tsudev.com - Một sản phẩm của TsuDEV (https://tsudev.com)");
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
              tsudisk ke-hoach-winre [tùy chọn]           Kế hoạch nới rộng Recovery cho WinRE (lỗi cập nhật 0x80070643, KB5034441)
              tsudisk mo-rong-winre [tùy chọn]            Thực hiện nới rộng Recovery; gõ WINRE để xác nhận (cần quyền quản trị)
              tsudisk ke-hoach-doi-ten <X:> [--chu-cai Y] [--nhan TÊN]  Kế hoạch đổi chữ cái và/hoặc nhãn ổ dữ liệu, KHÔNG thay đổi gì
              tsudisk doi-ten <X:> [--chu-cai Y] [--nhan TÊN]  Thực hiện đổi (cần quyền quản trị); --nhan "" để xóa nhãn
              tsudisk ke-hoach-thu-nho <X:> --giam-mb N   Kế hoạch thu nhỏ ổ NTFS, nhường N MB vùng trống ra sau ổ, KHÔNG thay đổi gì
              tsudisk thu-nho <X:> --giam-mb N            Thực hiện thu nhỏ (cần quyền quản trị); gõ lại chữ cái ổ để xác nhận
              tsudisk ke-hoach-xoa <X:>                   Kế hoạch xóa một ổ dữ liệu, in cách khôi phục, KHÔNG thay đổi gì
              tsudisk xoa-phan-vung <X:> [--xac-nhan "X: nhãn"]  Thực hiện (cần quyền quản trị); gõ lại "X: nhãn" để xác nhận
              tsudisk ke-hoach-tao-phan-vung --dia N [--vung K] [--kich-thuoc-mb M] [--he-tep NTFS|exFAT|FAT32] [--nhan TÊN] [--chu-cai Y]
                                                          Kế hoạch tạo + định dạng phân vùng dữ liệu trong vùng trống, KHÔNG thay đổi gì
              tsudisk tao-phan-vung --dia N [như trên]    Thực hiện (cần quyền quản trị); gõ TAO để xác nhận
              tsudisk nhat-ky [tệp journal.json]          Xem lần chạy gần nhất và việc cần làm nếu bị đứt giữa chừng
              tsudisk winre                               Trạng thái Windows Recovery Environment
              tsudisk suc-khoe [--dia N] [--json]         Sức khỏe ổ: trạng thái, nhiệt độ, độ mòn, lỗi đọc/ghi (S.M.A.R.T.)
              tsudisk kiem-tra-gpt [--dia N] [--json]     Kiểm tra toàn vẹn bảng GPT trên đĩa (CRC, header dự phòng) - quản trị
              tsudisk kiem-tra-mbr2gpt [--dia N] [--json] Chuyển đĩa MBR sang GPT được không (mbr2gpt /validate, chỉ đọc) - quản trị
              tsudisk sao-luu [--dia N] [--thu-muc-nhat-ky DIR]  Sao lưu sector GPT thô + bố cục + SHA-256 - quản trị
              tsudisk phien-ban | tro-giup

            Tùy chọn cho ke-hoach / mo-rong / doi-ten / thu-nho / tao-phan-vung:
              --chay-thu                 Chỉ chạy các bước không ghi đĩa (quét, sao lưu) để kiểm tra
              --dong-y                   Không hỏi xác nhận (dùng trong kịch bản tự động)
              --recovery-mb N            Kích thước Recovery mới (512-4096; mo-rong mặc định 1024, mo-rong-winre mặc định đủ 250 MB trống)
              --recovery tao-lai-rong    Cho phép xóa và tạo lại RỖNG một Recovery không chứa WinRE đang dùng
              --chi-o-ao                 Chỉ cho phép thao tác trên ổ ảo VHD/VHDX (kiểm thử)
              --thu-muc-nhat-ky DIR      Nơi lưu nhật ký và bản sao lưu (mặc định %ProgramData%\TsuDisk\nhat-ky)

            Bí danh tiếng Anh: list, analyze, plan, extend, plan-winre, grow-winre, plan-rename, rename, plan-shrink, shrink, plan-delete, delete, plan-create, create, journal, health, verify-gpt, check-mbr2gpt, backup, version, help; --dry-run, --yes, --disk, --virtual-only, --log-dir, --letter, --label, --extent, --size-mb, --fs, --shrink-mb, --confirm.
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

    internal static IEnumerable<DiskInfo> SelectDisks(CliArgs cli) =>
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

    private static bool Confirm(string word)
    {
        Console.Write($"Gõ {word} rồi Enter để thực hiện, Enter trống để hủy: ");
        var answer = Console.ReadLine()?.Trim();
        return string.Equals(answer, word, StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, $"{word}:", StringComparison.OrdinalIgnoreCase);
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
