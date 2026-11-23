using System.Globalization;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Presentation;

/// <summary>
/// Logic thuan cua o "Tao phan vung trong vung trong" (ADR-0010) - test duoc tren Linux. Giao dien chi rang buoc; moi
/// kiem tra an toan van nam o <see cref="CreatePartitionPlanner"/> + <see cref="Safety.SafetyGuard"/> + PlanRunner.
/// </summary>
public static class CreateForm
{
    /// <summary>
    /// Vung trong dung duoc (cung so voi <c>--vung</c> cua CLI) nam trong doan dang chon; null = doan khong phai vung trong
    /// hoac qua nho sau khi can le 1 MiB.
    /// </summary>
    public static UsableExtent? ExtentFor(DiskLayout layout, Segment? segment)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (segment is not { IsFree: true })
            return null;
        return CreatePartitionPlanner.UsableExtents(layout)
            .FirstOrDefault(e => e.Offset >= segment.Offset && e.End <= segment.End);
    }

    /// <summary>Chu cai co the chon: D-Z chua ai dung, chu cai dau tien = mac dinh (giong CLI khi bo <c>--chu-cai</c>).</summary>
    public static IReadOnlyList<char> LetterChoices(DiskLayout layout, IReadOnlySet<char> inUse)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(inUse);
        return [.. Enumerable.Range('D', 'Z' - 'D' + 1).Select(c => (char)c)
            .Where(c => !inUse.Contains(c) && layout.FindByLetter(c) is null && VolumeNaming.IsAssignableLetter(c))];
    }

    /// <summary>
    /// Doc o kich thuoc (MB, so nguyen). Trong = chiem het vung trong (null). Sai dinh dang => false; con kich thuoc qua
    /// nho/qua lon de <see cref="CreatePartitionPlanner"/> tu choi kem ly do.
    /// </summary>
    public static bool TryParseSizeMb(string? text, out long? bytes)
    {
        bytes = null;
        var t = (text ?? "").Trim();
        if (t.Length == 0)
            return true;
        if (!long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var mb) || mb <= 0 || mb > long.MaxValue / Units.MiB)
            return false;
        bytes = mb * Units.MiB;
        return true;
    }

    /// <summary>Yeu cau gui CreatePartitionPlanner.</summary>
    /// <param name="virtualDiskOnly">Giao dien chay voi <c>--chi-o-ao</c> (E2E): planner tu choi moi o khong phai VHD/VHDX.</param>
    public static CreatePartitionRequest Request(UsableExtent extent, long? size, string fileSystem, string label, char? letter, bool virtualDiskOnly = false)
    {
        ArgumentNullException.ThrowIfNull(extent);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(label);
        return new CreatePartitionRequest
        {
            ExtentIndex = extent.Index,
            Size = size,
            FileSystem = fileSystem,
            Label = label,
            Letter = letter is { } l ? char.ToUpperInvariant(l) : null,
            VirtualDiskOnly = virtualDiskOnly,
        };
    }

    /// <summary>Lenh CLI tuong duong (de sao chep / chay trong Terminal (Admin)).</summary>
    public static string Command(int diskNumber, CreatePartitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parts = new List<string> { "tsudisk", "tao-phan-vung", "--dia", diskNumber.ToString(CultureInfo.InvariantCulture) };
        if (request.ExtentIndex is { } k)
            parts.AddRange(["--vung", k.ToString(CultureInfo.InvariantCulture)]);
        if (request.Size is { } size)
            parts.AddRange(["--kich-thuoc-mb", (size / Units.MiB).ToString(CultureInfo.InvariantCulture)]);
        parts.AddRange(["--he-tep", request.FileSystem]);
        if (request.Label.Length > 0)
            parts.AddRange(["--nhan", Quote(request.Label)]);
        if (request.Letter is { } l)
            parts.AddRange(["--chu-cai", l.ToString()]);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Xac nhan lan hai: nguoi dung go lai SO DIA se bi ghi (khong phai chu cai moi - chu cai moi chua ton tai va khong noi
    /// gi ve o dang bi dong vao). Bo khoang trang hai dau.
    /// </summary>
    public static bool ConfirmationMatches(string? typed, int diskNumber) =>
        string.Equals(typed?.Trim(), diskNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string Quote(string value) =>
        value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '`' or '$' or ';' or '&' or '|')
            ? value
            : $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
