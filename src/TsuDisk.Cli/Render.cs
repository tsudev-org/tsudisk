using System.Globalization;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Cli;

/// <summary>Trinh bay ket qua ra man hinh. Tach rieng de lenh khong lan logic voi dinh dang.</summary>
internal static class Render
{
    public static void Layout(DiskLayout layout)
    {
        var d = layout.Disk;
        Console.WriteLine($"Đĩa {d.Number}: {d.FriendlyName} - {Units.Format(d.Size)} - {d.Style.ToString().ToUpperInvariant()} - {d.BusType}" +
            $"{(d.IsSystem ? " - đĩa hệ thống" : "")}{(d.IsVirtual ? " - ổ ảo" : "")}{(d.IsReadOnly ? " - CHỈ ĐỌC" : "")}");
        Console.WriteLine($"  {"#",-3} {"Ổ",-3} {"Loại",-38} {"Vị trí",12} {"Dung lượng",11} {"Trống",11} {"Hệ tệp",-6}");
        foreach (var segment in layout.Segments())
        {
            if (segment.Partition is not { } p)
            {
                Console.WriteLine($"  {"-",-3} {"",-3} {"(Vùng trống - Unallocated)",-38} {Units.Format(segment.Offset),12} {Units.Format(segment.Size),11}");
                continue;
            }
            var letter = p.DriveLetter is { } l ? $"{l}:" : "";
            var free = p.FreeBytes is { } f ? Units.Format(f) : "";
            Console.WriteLine($"  {p.PartitionNumber,-3} {letter,-3} {PartitionTypes.Describe(p.Kind),-38} {Units.Format(p.Offset),12} {Units.Format(p.Size),11} {free,11} {p.FileSystem ?? "",-6}");
        }
        Console.WriteLine();
    }

    public static void Advice(IReadOnlyList<FreeSpaceAdvice> advice)
    {
        if (advice.Count == 0)
        {
            Console.WriteLine("  Không có vùng trống nào (từ 1 MB trở lên).");
            return;
        }
        foreach (var item in advice)
            Console.WriteLine($"  - {item.Advice}");
        Console.WriteLine();
    }

    public static void Plan(Plan plan)
    {
        Console.WriteLine(plan.Operation switch
        {
            WinReGrowPlanner.Operation => $"KẾ HOẠCH {plan.Id}: nới rộng phân vùng Recovery (WinRE) trên đĩa {plan.DiskNumber}, thêm {Units.Format(plan.GainBytes)}",
            RenamePlanner.Operation => $"KẾ HOẠCH {plan.Id}: đổi chữ cái/nhãn {plan.TargetLetter}: trên đĩa {plan.DiskNumber}",
            _ => $"KẾ HOẠCH {plan.Id}: mở rộng {plan.TargetLetter}: trên đĩa {plan.DiskNumber}, tăng {Units.Format(plan.GainBytes)}",
        });
        foreach (var note in plan.Notes)
            Console.WriteLine($"  * {note}");
        Console.WriteLine();
        foreach (var step in plan.Steps)
            Console.WriteLine($"  {step.Index}. {(step.ChangesDisk ? "[GHI]" : "     ")} {step.Title}");
        Console.WriteLine();
        Console.WriteLine("  Bất biến an toàn đã kiểm (trên mô phỏng): mọi phân vùng khác giữ nguyên vị trí, kích thước, loại;");
        Console.WriteLine(plan.Operation switch
        {
            WinReGrowPlanner.Operation => "  không chạm EFI/MSR; không chồng lấn; offset căn lề 1 MiB; chỉ thu nhỏ phần trống ở cuối ổ, tối đa 4 GB.",
            RenamePlanner.Operation => "  ổ được đổi cũng giữ nguyên vị trí, kích thước, loại; chữ cái C-Z không trùng; không đổi chữ cái ổ hệ thống.",
            _ => "  không chạm EFI/MSR; không chồng lấn; offset căn lề 1 MiB.",
        });
        Console.WriteLine();
    }

    public static void Refusal(Refusal refusal)
    {
        Console.Error.WriteLine($"TỪ CHỐI ({refusal.Code}): {refusal.Message}");
        foreach (var hint in refusal.Hints)
            Console.Error.WriteLine($"  - {hint}");
    }

    public static void Journal(RunJournal journal)
    {
        Console.WriteLine($"Lần chạy {journal.RunId} - kế hoạch {journal.Plan.Id} - trạng thái: {StateText(journal.State)}");
        foreach (var step in journal.Steps)
        {
            var when = step.FinishedAt?.ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
            Console.WriteLine($"  {step.Index}. [{StatusText(step.Status),-10}] {step.Title} {when}");
            if (!string.IsNullOrWhiteSpace(step.Message))
                Console.WriteLine($"       {step.Message}");
        }
        foreach (var message in journal.Messages)
            Console.WriteLine($"  > {message}");
    }

    public static string StatusText(StepStatus status) => status switch
    {
        StepStatus.Pending => "chờ",
        StepStatus.Running => "đang chạy",
        StepStatus.Done => "xong",
        StepStatus.Skipped => "bỏ qua",
        StepStatus.Failed => "LỖI",
        StepStatus.RolledBack => "đã hoàn tác",
        _ => status.ToString(),
    };

    public static string StateText(RunState state) => state switch
    {
        RunState.Running => "ĐANG CHẠY hoặc BỊ ĐỨT GIỮA CHỪNG",
        RunState.Succeeded => "THÀNH CÔNG",
        RunState.RolledBack => "THẤT BẠI - đã hoàn tác về trạng thái an toàn",
        RunState.NeedsAttention => "THẤT BẠI - CẦN XỬ LÝ (xem gợi ý)",
        RunState.DryRunCompleted => "CHẠY THỬ XONG - không thay đổi đĩa",
        RunState.Aborted => "DỪNG trước khi thay đổi đĩa",
        _ => state.ToString(),
    };
}
