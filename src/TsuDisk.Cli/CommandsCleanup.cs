using TsuDisk.Cli;
using TsuDisk.Core.Cleanup;
using TsuDisk.Core.Model;

/// <summary>quet-rac (ADR-0013): CHI DOC. Khong co duong xoa nao trong lenh nay.</summary>
internal static partial class Commands
{
    private const int MaxTopLevelEntries = 5000;

    public static int ScanJunk(CliArgs cli)
    {
        var root = cli.Value("thu-muc") ?? Path.GetTempPath();
        if (!Directory.Exists(root))
            throw new UsageException($"Không tìm thấy thư mục: {root}");

        var now = DateTime.UtcNow;
        var items = new List<JunkAssessment>();
        var info = new DirectoryInfo(root);
        foreach (var entry in info.EnumerateFileSystemInfos().Take(MaxTopLevelEntries))
        {
            var isDir = entry is DirectoryInfo;
            var size = isDir ? DirectorySize((DirectoryInfo)entry) : ((FileInfo)entry).Length;
            // Thu muc nam trong chi dinh --thu-muc: xet theo duong dan day du, nen goc rac phai khop (khong khop => "Unknown").
            items.Add(JunkPolicy.Assess(new JunkCandidate(entry.FullName, isDir, size, entry.LastWriteTimeUtc), now));
        }

        var summary = JunkPolicy.Summarize(items);
        Console.WriteLine($"Quét (chỉ đọc): {info.FullName}");
        Console.WriteLine($"  Có thể thu hồi (rác tái tạo được, đủ cũ): {Units.Format(summary.ReclaimableBytes)}");
        Console.WriteLine($"  Được bảo vệ, KHÔNG BAO GIỜ đề xuất xóa:   {Units.Format(summary.ProtectedBytes)}");
        Console.WriteLine($"  Quá mới (có thể đang dùng):              {Units.Format(summary.TooRecentBytes)}");
        Console.WriteLine($"  Ngoài vùng rác đã biết (không đoán):      {Units.Format(summary.UnknownBytes)}");
        Console.WriteLine();
        foreach (var a in items.OrderByDescending(i => i.Candidate.SizeBytes).Take(25))
            Console.WriteLine($"  [{Label(a.Verdict)}] {Units.Format(a.Candidate.SizeBytes),10}  {Path.GetFileName(a.Candidate.Path)} - {a.Reason}");
        if (items.Count > 25)
            Console.WriteLine($"  ... và {items.Count - 25} mục nhỏ hơn.");
        Console.WriteLine();
        Console.WriteLine("TsuDisk KHÔNG xóa gì. Tự xóa các mục [RAC] bằng Dọn dẹp ổ đĩa của Windows (Disk Cleanup) hoặc Cài đặt > Bộ nhớ.");
        return ExitCodes.Ok;
    }

    private static string Label(JunkVerdict v) => v switch
    {
        JunkVerdict.Junk => "RAC ",
        JunkVerdict.Protected => "GIU ",
        JunkVerdict.TooRecent => "MOI ",
        _ => "???",
    };

    /// <summary>Tong kich thuoc de quy, khong theo lien ket (reparse point), bo qua loi quyen/dang bi khoa.</summary>
    private static long DirectorySize(DirectoryInfo dir)
    {
        long total = 0;
        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        try
        {
            foreach (var f in dir.EnumerateFiles("*", opts))
            {
                try { total += f.Length; }
                catch (IOException) { /* dang bi khoa/xoa giua chung: bo qua */ }
            }
        }
        catch (UnauthorizedAccessException) { /* khong du quyen: tinh phan doc duoc */ }
        return total;
    }
}
