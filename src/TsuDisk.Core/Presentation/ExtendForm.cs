using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Presentation;

/// <summary>
/// Logic thuan cua nut "Mo rong o" trong giao dien (ADR-0011) - test duoc tren Linux. Giao dien KHONG co tuy chon rieng:
/// cung yeu cau mac dinh voi <c>tsudisk mo-rong X:</c> (Recovery chi doi cho khi chua WinRE dang dung, 1 GiB). Tao lai
/// Recovery rong (<c>--recovery tao-lai-rong</c>) va doi kich thuoc Recovery van chi co o CLI.
/// </summary>
public static class ExtendForm
{
    /// <summary>Yeu cau gui ExtendPlanner - giong CLI khi khong truyen tuy chon nao.</summary>
    /// <param name="virtualDiskOnly">Giao dien chay voi <c>--chi-o-ao</c> (E2E): planner tu choi moi o khong phai VHD/VHDX.</param>
    public static ExtendRequest Request(char letter, bool virtualDiskOnly = false) =>
        new(char.ToUpperInvariant(letter)) { VirtualDiskOnly = virtualDiskOnly };

    /// <summary>Lenh CLI tuong duong (de sao chep / chay trong Terminal (Admin)).</summary>
    public static string Command(char letter) => $"tsudisk mo-rong {char.ToUpperInvariant(letter)}:";

    /// <summary>Ke hoach co xoa + tao lai phan vung Recovery (mau [Dich][Recovery][Trong], KB5028997).</summary>
    public static bool RelocatesRecovery(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Steps.Any(s => s.Kind is StepKind.DeletePartition or StepKind.CreateRecovery);
    }

    /// <summary>
    /// Giao dien chi cho ap dung ke hoach mo rong khi da DOC DUOC trang thai WinRE (reagentc, can quyen quan tri): khong doc
    /// duoc thi planner coi WinRE la tat - an toan cho viec lap ke hoach, nhung khong du chac de ghi dia tu giao dien.
    /// </summary>
    public static bool CanApply(Plan plan, bool winReKnown)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Operation == ExtendPlanner.Operation && winReKnown;
    }

    /// <summary>Xac nhan lan hai: go lai chu cai o se duoc mo rong (giong CLI <c>mo-rong</c>: "E" hoac "E:").</summary>
    public static bool ConfirmationMatches(string? typed, char letter) => RenameForm.ConfirmationMatches(typed, letter);
}
