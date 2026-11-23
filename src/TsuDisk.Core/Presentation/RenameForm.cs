using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Presentation;

/// <summary>
/// Logic thuan cua o "Doi chu cai/nhan" trong giao dien (ADR-0009) - test duoc tren Linux. Giao dien chi rang buoc;
/// moi kiem tra an toan van nam o <see cref="RenamePlanner"/> + <see cref="Safety.SafetyGuard"/> + PlanRunner.
/// </summary>
public static class RenameForm
{
    /// <summary>O doi ten duoc tu giao dien: phan vung du lieu dang co chu cai (giong dieu kien cua RenamePlanner).</summary>
    public static bool CanRename(PartitionInfo? partition) =>
        partition is { Kind: PartitionKind.BasicData, DriveLetter: not null, FileSystem: not null };

    /// <summary>
    /// Chu cai co the chon: chu cai hien tai cua o (= giu nguyen, dung dau) + moi chu C-Z chua ai dung. Chu cai dang
    /// dung o noi khac (dia khac, o mang, subst) KHONG hien - RenamePlanner van tu choi neu danh sach cu.
    /// </summary>
    public static IReadOnlyList<char> LetterChoices(char current, IReadOnlySet<char> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        var free = Enumerable.Range('C', 'Z' - 'C' + 1).Select(c => (char)c)
            .Where(c => c != current && !inUse.Contains(c) && VolumeNaming.IsAssignableLetter(c));
        return [current, .. free];
    }

    /// <summary>Yeu cau gui RenamePlanner: chu cai/nhan khong doi => null (giu nguyen).</summary>
    /// <param name="virtualDiskOnly">Giao dien chay voi <c>--chi-o-ao</c> (E2E): RenamePlanner tu choi moi o khong phai VHD/VHDX.</param>
    public static RenameRequest Request(PartitionInfo partition, char newLetter, string newLabel, char? systemDrive, bool virtualDiskOnly = false)
    {
        ArgumentNullException.ThrowIfNull(partition);
        ArgumentNullException.ThrowIfNull(newLabel);
        var current = partition.DriveLetter ?? throw new ArgumentException("Phân vùng không có chữ cái ổ.", nameof(partition));
        var letter = char.ToUpperInvariant(newLetter);
        return new RenameRequest(current,
            letter == current ? null : letter,
            newLabel == (partition.Label ?? "") ? null : newLabel)
        {
            SystemDrive = systemDrive is { } s ? char.ToUpperInvariant(s) : null,
            VirtualDiskOnly = virtualDiskOnly,
        };
    }

    /// <summary>Lenh CLI tuong duong (de sao chep / chay trong Terminal (Admin)).</summary>
    public static string Command(RenameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parts = new List<string> { "tsudisk", "doi-ten", $"{request.TargetLetter}:" };
        if (request.NewLetter is { } l)
            parts.AddRange(["--chu-cai", l.ToString()]);
        if (request.NewLabel is { } label)
            parts.AddRange(["--nhan", Quote(label)]);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Xac nhan lan hai: nguoi dung go lai chu cai o (giong CLI: "E" hoac "E:", khong phan biet hoa thuong, bo khoang
    /// trang hai dau).
    /// </summary>
    public static bool ConfirmationMatches(string? typed, char letter)
    {
        var t = typed?.Trim() ?? "";
        var word = letter.ToString();
        return string.Equals(t, word, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, $"{word}:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Dat trong ngoac kep kieu PowerShell/cmd; dau ngoac kep trong nhan da bi VolumeNaming chan voi FAT.</summary>
    private static string Quote(string value) =>
        value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '`' or '$' or ';' or '&' or '|')
            ? value
            : $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
