using System.Globalization;
using TsuDisk.Core.Model;

namespace TsuDisk.Core.Conversion;

/// <summary>Ket luan kiem tra chuyen MBR -> GPT bang mbr2gpt /validate.</summary>
public enum Mbr2GptVerdict
{
    /// <summary>mbr2gpt /validate tra 0: dia chuyen duoc.</summary>
    Pass,
    /// <summary>mbr2gpt /validate tu choi (ma khac 0).</summary>
    Fail,
    /// <summary>Khong can kiem: dia da la GPT hoac chua khoi tao.</summary>
    NotApplicable,
    /// <summary>Windows nay khong co mbr2gpt.exe (can Windows 10 1703 tro len).</summary>
    ToolMissing,
}

/// <summary>Mot nhan xet. <see cref="Blocking"/> = dieu kien mbr2gpt chac chan tu choi.</summary>
public sealed record Mbr2GptFinding(bool Blocking, string Message);

public sealed record Mbr2GptReport(
    int DiskNumber,
    Mbr2GptVerdict Verdict,
    int? ToolExitCode,
    IReadOnlyList<Mbr2GptFinding> Findings,
    IReadOnlyList<string> ToolLog);

/// <summary>
/// Kiem tra CHI DOC dieu kien chuyen dia MBR sang GPT bang cong cu chinh thuc mbr2gpt cua Microsoft. TsuDisk khong
/// tu chuyen (can ADR-0005): lop nay chi dung doi so /validate, tu kiem bo cuc theo dieu kien Microsoft cong bo va
/// dien giai ma thoat + setupact.log sang tieng Viet. Tach khoi Windows de test duoc tren moi nen tang.
/// </summary>
public static class Mbr2GptCheck
{
    public const string ToolName = "mbr2gpt.exe";

    /// <summary>GPT can 16 KiB bang phan vung + 2 sector (MBR bao ve + header) o dau dia.</summary>
    public static long RequiredLeadingBytes(int sectorSize) => 16 * 1024 + 2L * Math.Max(sectorSize, 512);

    /// <summary>... va 16 KiB + 1 sector (header du phong) o cuoi dia.</summary>
    public static long RequiredTrailingBytes(int sectorSize) => 16 * 1024 + 1L * Math.Max(sectorSize, 512);

    public const int MaxPrimaryPartitions = 3;

    /// <summary>Doi so duy nhat TsuDisk duoc phep truyen cho mbr2gpt: luon /validate, khong bao gio /convert.</summary>
    public static IReadOnlyList<string> ValidateArguments(int diskNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diskNumber);
        return ["/validate", string.Create(CultureInfo.InvariantCulture, $"/disk:{diskNumber}"), "/allowFullOS"];
    }

    /// <summary>Ly do khong can chay mbr2gpt, hoac null neu dia la MBR.</summary>
    public static string? NotApplicableReason(DiskInfo disk) => disk.Style switch
    {
        PartitionStyle.Gpt => $"Đĩa {disk.Number} đã là GPT - không cần chuyển.",
        PartitionStyle.Mbr => null,
        _ => $"Đĩa {disk.Number} chưa khởi tạo (RAW) - khởi tạo thẳng thành GPT, không cần mbr2gpt.",
    };

    /// <summary>TsuDisk tu kiem bo cuc theo dieu kien Microsoft cong bo cho mbr2gpt (khong thay mbr2gpt).</summary>
    public static IReadOnlyList<Mbr2GptFinding> PreCheck(DiskLayout layout)
    {
        var disk = layout.Disk;
        var findings = new List<Mbr2GptFinding>();
        if (disk.Style != PartitionStyle.Mbr)
            return findings;

        var ordered = layout.Ordered;
        var extended = ordered.Where(p => p.Kind == PartitionKind.MbrExtended).ToList();
        if (extended.Count > 0)
            findings.Add(new(true, "Đĩa có phân vùng mở rộng (Extended) chứa ổ logic - mbr2gpt chỉ nhận phân vùng chính."));
        var primary = ordered.Count(p => p.Kind != PartitionKind.MbrExtended);
        if (extended.Count == 0 && primary > MaxPrimaryPartitions)
            findings.Add(new(true, $"Đĩa có {primary} phân vùng chính, mbr2gpt chỉ nhận tối đa {MaxPrimaryPartitions} (cần thêm chỗ cho phân vùng EFI)."));
        if (!ordered.Any(p => p.IsSystem))
            findings.Add(new(true, "Không có phân vùng hệ thống (active) trên đĩa này - mbr2gpt chỉ chuyển đĩa khởi động Windows."));
        if (!ordered.Any(p => p.IsBoot))
            findings.Add(new(true, "Không có phân vùng Windows (boot) trên đĩa này - mbr2gpt chỉ chuyển đĩa đang chứa Windows."));

        if (ordered.Count > 0)
        {
            var lead = RequiredLeadingBytes(disk.LogicalSectorSize);
            if (ordered[0].Offset < lead)
                findings.Add(new(true, $"Phân vùng đầu tiên bắt đầu ở byte {ordered[0].Offset}, cần trống ít nhất {lead} byte đầu đĩa cho bảng GPT."));
            var trail = RequiredTrailingBytes(disk.LogicalSectorSize);
            var tail = disk.Size - ordered.Max(p => p.End);
            if (tail < trail)
                findings.Add(new(true, $"Chỉ còn {tail} byte trống ở cuối đĩa, cần ít nhất {trail} byte cho bảng GPT dự phòng - thu nhỏ phân vùng cuối (thu-nho) một chút."));
        }
        return findings;
    }

    /// <summary>Y nghia ma thoat mbr2gpt theo tai lieu Microsoft.</summary>
    public static string DescribeExitCode(int exitCode) => exitCode switch
    {
        0 => "mbr2gpt xác nhận đĩa chuyển được.",
        1 => "Người dùng hủy.",
        2 => "Lỗi nội bộ của mbr2gpt.",
        3 => "mbr2gpt không khởi tạo được (thường do thiếu quyền quản trị hoặc chạy trong Windows thiếu /allowFullOS).",
        4 => "Tham số dòng lệnh không hợp lệ.",
        5 => "Không đọc được hình học/bố cục đĩa (đĩa không phải MBR, đĩa động, hoặc đĩa không có Windows).",
        6 => "Có ổ đang mã hóa (BitLocker): tạm ngưng BitLocker trước khi chuyển.",
        7 => "Bố cục đĩa không đáp ứng yêu cầu của mbr2gpt.",
        8 => "Lỗi khi tạo phân vùng EFI.",
        9 => "Lỗi khi cài tệp khởi động.",
        10 => "Lỗi khi áp bố cục GPT.",
        100 => "Đã chuyển nhưng một số mục cấu hình khởi động (BCD) không khôi phục được.",
        _ => string.Create(CultureInfo.InvariantCulture, $"mbr2gpt trả mã {exitCode} không có trong tài liệu."),
    };

    private static readonly (string[] Keys, string Hint)[] LogHints =
    [
        (["cannot find os partition"], "mbr2gpt không tìm thấy Windows trên đĩa này: chỉ đĩa hệ thống (đang chứa Windows) mới chuyển được."),
        (["encrypt", "bitlocker"], "Có ổ mã hóa BitLocker: chạy `manage-bde -protectors -disable C:` để tạm ngưng rồi kiểm lại."),
        (["extended partition", "logical drive", "logical partition"], "Đĩa có ổ logic trong phân vùng mở rộng - mbr2gpt không chuyển được loại này."),
        (["too many partitions", "maximum number of partitions"], "Quá nhiều phân vùng chính (tối đa 3)."),
        (["room for the efi", "space for the efi"], "Không đủ chỗ cho phân vùng EFI: thu nhỏ ổ Windows (thu-nho) để có vài trăm MB trống."),
        (["dynamic"], "Đĩa động (dynamic) - mbr2gpt chỉ nhận đĩa cơ bản (basic)."),
        (["active partition", "system partition"], "Không xác định được phân vùng hệ thống (active) hoặc cấu hình khởi động (BCD) trên đĩa này."),
    ];

    /// <summary>Gop ma thoat, dau ra console va dong setupact.log moi thanh bao cao.</summary>
    public static Mbr2GptReport Interpret(DiskLayout layout, int exitCode, string consoleOutput, IReadOnlyList<string> logLines)
    {
        var findings = new List<Mbr2GptFinding>();
        var pass = exitCode == 0;
        // Khi mbr2gpt bao dat, tu kiem cua TsuDisk chi la luu y (cong cu chinh thuc la nguon su that).
        findings.AddRange(PreCheck(layout).Select(f => f with { Blocking = f.Blocking && !pass }));
        findings.Add(new(!pass, DescribeExitCode(exitCode)));
        if (pass)
            return new Mbr2GptReport(layout.Disk.Number, Mbr2GptVerdict.Pass, exitCode, findings, logLines);
        // Goi y theo tu khoa chi khi khong dat: tranh doc nham dong log binh thuong thanh loi.
        var text = (consoleOutput + "\n" + string.Join('\n', logLines)).ToLowerInvariant();
        foreach (var (keys, hint) in LogHints)
        {
            if (keys.Any(k => text.Contains(k, StringComparison.Ordinal)) && findings.All(f => f.Message != hint))
                findings.Add(new(false, hint));
        }
        return new Mbr2GptReport(layout.Disk.Number, Mbr2GptVerdict.Fail, exitCode, findings, logLines);
    }

    /// <summary>
    /// Phan setupact.log do lan chay nay ghi them: phan sau noi dung cu neu tep chi bi noi them, nguoc lai (tep bi
    /// xoay vong) lay toan bo noi dung moi. Chi giu dong lien quan MBR2GPT, toi da <paramref name="maxLines"/> dong cuoi.
    /// </summary>
    public static IReadOnlyList<string> NewLogLines(string before, string after, int maxLines = 40)
    {
        var added = after.StartsWith(before, StringComparison.Ordinal) ? after[before.Length..] : after;
        var lines = added.Split('\n')
            .Select(l => l.TrimEnd('\r', '\0'))
            .Where(l => l.Length > 0 && l.Contains("MBR2GPT", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return lines.Count <= maxLines ? lines : lines[^maxLines..];
    }
}
