using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Presentation;

/// <summary>Chu mo ta ke hoach dung chung cho CLI va GUI - mot nguon, khong chep hai noi.</summary>
public static class PlanText
{
    public static string Headline(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Operation switch
        {
            WinReGrowPlanner.Operation => $"KẾ HOẠCH {plan.Id}: nới rộng phân vùng Recovery (WinRE) trên đĩa {plan.DiskNumber}, thêm {Units.Format(plan.GainBytes)}",
            RenamePlanner.Operation => $"KẾ HOẠCH {plan.Id}: đổi chữ cái/nhãn {plan.TargetLetter}: trên đĩa {plan.DiskNumber}",
            ShrinkPlanner.Operation => $"KẾ HOẠCH {plan.Id}: thu nhỏ {plan.TargetLetter}: trên đĩa {plan.DiskNumber}, nhường {Units.Format(plan.GainBytes)} vùng trống",
            CreatePartitionPlanner.Operation => $"KẾ HOẠCH {plan.Id}: tạo ổ {plan.TargetLetter}: {Units.Format(plan.GainBytes)} trong vùng trống của đĩa {plan.DiskNumber}",
            _ => $"KẾ HOẠCH {plan.Id}: mở rộng {plan.TargetLetter}: trên đĩa {plan.DiskNumber}, tăng {Units.Format(plan.GainBytes)}",
        };
    }

    /// <summary>Hai dong bat bien an toan da kiem tren mo phong.</summary>
    public static IReadOnlyList<string> Invariants(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return
        [
            "Bất biến an toàn đã kiểm (trên mô phỏng): mọi phân vùng khác giữ nguyên vị trí, kích thước, loại;",
            plan.Operation switch
            {
                WinReGrowPlanner.Operation => "không chạm EFI/MSR; không chồng lấn; offset căn lề 1 MiB; chỉ thu nhỏ phần trống ở cuối ổ, tối đa 4 GB.",
                RenamePlanner.Operation => "ổ được đổi cũng giữ nguyên vị trí, kích thước, loại; chữ cái C-Z không trùng; không đổi chữ cái ổ hệ thống.",
                ShrinkPlanner.Operation => "không chạm EFI/MSR; vị trí đầu ổ giữ nguyên, điểm cuối mới căn lề 1 MiB; ổ còn đủ chỗ trống sau khi thu nhỏ.",
                CreatePartitionPlanner.Operation => "phân vùng mới nằm trọn trong vùng trống, căn lề 1 MiB; chỉ định dạng phân vùng vừa tạo và còn RAW.",
                _ => "không chạm EFI/MSR; không chồng lấn; offset căn lề 1 MiB.",
            },
        ];
    }
}
