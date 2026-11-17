using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Execution;

/// <summary>
/// Doc nhat ky cua mot lan chay bi dut (mat dien, tat may, tien trinh bi giet: trang thai van la Running) hoac can nguoi
/// xu ly, va tra ve cac viec cu the phai lam. Moi tinh huong deu da duoc tinh truoc - khong de nguoi dung tu doan.
/// </summary>
public static class RecoveryAdvisor
{
    public static IReadOnlyList<string> Advise(RunJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.State is RunState.Succeeded or RunState.DryRunCompleted)
            return ["Lần chạy đã hoàn tất, không cần làm gì."];
        if (journal.State == RunState.Aborted)
            return ["Lần chạy dừng trước khi thay đổi đĩa. Lập lại kế hoạch rồi chạy lại."];

        bool Done(StepKind kind) => journal.Steps.Any(s => s.Kind == kind && s.Status == StepStatus.Done);
        bool Planned(StepKind kind) => journal.Steps.Any(s => s.Kind == kind);
        bool Unfinished(StepKind kind) => journal.Steps.Any(s => s.Kind == kind && s.Status is StepStatus.Running or StepStatus.Failed);

        var advice = new List<string>();
        var deleted = Done(StepKind.DeletePartition) || Unfinished(StepKind.DeletePartition);
        var extended = Done(StepKind.ExtendPartition);
        var recreated = Done(StepKind.CreateRecovery);
        var target = journal.Plan.TargetLetter;
        var oldRecovery = journal.Plan.Steps.FirstOrDefault(s => s.Kind == StepKind.DeletePartition);
        var newRecovery = journal.Plan.Steps.FirstOrDefault(s => s.Kind == StepKind.CreateRecovery);

        if (deleted && !extended && oldRecovery is not null)
        {
            advice.Add($"Phân vùng Recovery cũ có thể đã bị xóa nhưng {target}: chưa mở rộng. Mở Disk Management: nếu vùng trống " +
                $"ngay sau {target}: thì chạy lại `tsudisk mo-rong {target}:` (kế hoạch mới sẽ tạo Recovery đúng chỗ).");
        }
        if (extended && !recreated && newRecovery is not null)
        {
            advice.Add($"{target}: đã mở rộng nhưng chưa có Recovery mới. Chạy lại `tsudisk mo-rong {target}:` hoặc tạo Recovery " +
                $"thủ công tại offset {newRecovery.Offset} (xem HUONG-DAN mục 5 bước 5).");
        }
        if (Planned(StepKind.DisableWinRe) && !Done(StepKind.EnableWinRe))
        {
            advice.Add("WinRE có thể đang tắt. Chạy `reagentc /enable` trong cửa sổ quản trị rồi `reagentc /info` để kiểm tra " +
                "(WinRE vẫn chạy được từ Winre.wim trên ổ hệ thống).");
        }
        var letterStep = journal.Plan.Steps.FirstOrDefault(s => s.Kind == StepKind.SetDriveLetter);
        if (letterStep is not null && Unfinished(StepKind.SetDriveLetter))
        {
            advice.Add($"Ổ {letterStep.DriveLetter}: có thể đang không có chữ cái (đổi sang {letterStep.NewDriveLetter}: bị đứt). Chạy " +
                $"`tsudisk danh-sach`: nếu phân vùng {letterStep.PartitionNumber} không có chữ cái, mở Disk Management -> " +
                $"Change Drive Letter and Paths -> Add {letterStep.DriveLetter}:. Dữ liệu không bị ảnh hưởng.");
        }
        var createStep = journal.Plan.Steps.FirstOrDefault(s => s.Kind == StepKind.CreatePartition);
        if (createStep is not null && (Done(StepKind.CreatePartition) || Unfinished(StepKind.CreatePartition)) && !Done(StepKind.FormatVolume))
        {
            advice.Add($"Có thể còn một phân vùng mới chưa định dạng (RAW) tại offset {createStep.Offset}. Chạy `tsudisk danh-sach`; " +
                "nếu có, xóa nó trong Disk Management (Delete Volume) rồi lập lại kế hoạch. Phân vùng có sẵn không bị ảnh hưởng.");
        }
        var deleteData = journal.Plan.Steps.FirstOrDefault(s => s.Kind == StepKind.DeleteDataPartition);
        if (deleteData is not null && (Done(StepKind.DeleteDataPartition) || Unfinished(StepKind.DeleteDataPartition)))
        {
            var restore = journal.Plan.Before.FindByOffset(deleteData.Offset!.Value) is { } victim
                ? string.Join(" ; ", DeletePartitionPlanner.ManualRestoreCommands(journal.Plan.Before, victim))
                : "(xem plan.json)";
            advice.Add($"Ổ {deleteData.DriveLetter}: có thể đã bị xóa. Chạy `tsudisk danh-sach`: nếu đúng là đã mất mà bạn đổi ý, " +
                $"KHÔNG ghi gì vào vùng trống đó và chạy diskpart (Admin): {restore} - dữ liệu cũ sẽ hiện lại.");
        }
        if (advice.Count == 0)
            advice.Add("Không phát hiện bước dở dang nào làm đĩa ở trạng thái thiếu. Chạy `tsudisk danh-sach` để xác nhận bố cục.");
        advice.Add("Dữ liệu trên các phân vùng khác KHÔNG bị chạm: TsuDisk chỉ thay đổi phân vùng Recovery và ổ đích của kế hoạch.");
        return advice;
    }
}
