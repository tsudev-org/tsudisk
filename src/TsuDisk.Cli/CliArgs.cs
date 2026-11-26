using System.Globalization;
using TsuDisk.Core.Integrity;

namespace TsuDisk.Cli;

/// <summary>
/// Bo phan tich tham so toi gian, khong phu thuoc thu vien ngoai (it phu thuoc = it be mat tan cong chuoi cung ung).
/// Moi co deu co ten tieng Viet va bi danh tieng Anh.
/// </summary>
internal sealed class CliArgs
{
    private static readonly Dictionary<string, string> CommandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["danh-sach"] = "danh-sach", ["list"] = "danh-sach",
        ["phan-tich"] = "phan-tich", ["analyze"] = "phan-tich",
        ["ke-hoach"] = "ke-hoach", ["plan"] = "ke-hoach",
        ["mo-rong"] = "mo-rong", ["extend"] = "mo-rong",
        ["ke-hoach-winre"] = "ke-hoach-winre", ["plan-winre"] = "ke-hoach-winre",
        ["mo-rong-winre"] = "mo-rong-winre", ["grow-winre"] = "mo-rong-winre",
        ["ke-hoach-doi-ten"] = "ke-hoach-doi-ten", ["plan-rename"] = "ke-hoach-doi-ten",
        ["doi-ten"] = "doi-ten", ["rename"] = "doi-ten",
        ["ke-hoach-tao-phan-vung"] = "ke-hoach-tao-phan-vung", ["plan-create"] = "ke-hoach-tao-phan-vung",
        ["tao-phan-vung"] = "tao-phan-vung", ["create"] = "tao-phan-vung",
        ["ke-hoach-thu-nho"] = "ke-hoach-thu-nho", ["plan-shrink"] = "ke-hoach-thu-nho",
        ["thu-nho"] = "thu-nho", ["shrink"] = "thu-nho",
        ["ke-hoach-xoa"] = "ke-hoach-xoa", ["plan-delete"] = "ke-hoach-xoa",
        ["xoa-phan-vung"] = "xoa-phan-vung", ["delete"] = "xoa-phan-vung",
        ["nhat-ky"] = "nhat-ky", ["journal"] = "nhat-ky",
        ["winre"] = "winre",
        ["suc-khoe"] = "suc-khoe", ["health"] = "suc-khoe",
        ["kiem-tra-gpt"] = "kiem-tra-gpt", ["verify-gpt"] = "kiem-tra-gpt",
        ["kiem-tra-mbr2gpt"] = "kiem-tra-mbr2gpt", ["check-mbr2gpt"] = "kiem-tra-mbr2gpt",
        ["sao-luu"] = "sao-luu", ["backup"] = "sao-luu",
        ["xuat-du-lieu"] = "xuat-du-lieu", ["export"] = "xuat-du-lieu",
        ["dang-nhap"] = "dang-nhap", ["login"] = "dang-nhap",
        ["dang-xuat"] = "dang-xuat", ["logout"] = "dang-xuat",
        ["tai-khoan"] = "tai-khoan", ["account"] = "tai-khoan",
        ["phien-ban"] = "phien-ban", ["version"] = "phien-ban", ["--version"] = "phien-ban", ["-v"] = "phien-ban",
        ["tro-giup"] = "tro-giup", ["help"] = "tro-giup", ["--help"] = "tro-giup", ["-h"] = "tro-giup", ["/?"] = "tro-giup",
    };

    private static readonly Dictionary<string, string> FlagAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["--json"] = "json",
        ["--chay-thu"] = "chay-thu", ["--dry-run"] = "chay-thu",
        ["--dong-y"] = "dong-y", ["--yes"] = "dong-y",
        ["--chi-o-ao"] = "chi-o-ao", ["--virtual-only"] = "chi-o-ao",
        ["--dia"] = "dia", ["--disk"] = "dia",
        ["--recovery-mb"] = "recovery-mb",
        ["--recovery"] = "recovery",
        ["--chu-cai"] = "chu-cai", ["--letter"] = "chu-cai",
        ["--nhan"] = "nhan", ["--label"] = "nhan",
        ["--vung"] = "vung", ["--extent"] = "vung",
        ["--kich-thuoc-mb"] = "kich-thuoc-mb", ["--size-mb"] = "kich-thuoc-mb",
        ["--he-tep"] = "he-tep", ["--fs"] = "he-tep",
        ["--giam-mb"] = "giam-mb", ["--shrink-mb"] = "giam-mb",
        ["--xac-nhan"] = "xac-nhan", ["--confirm"] = "xac-nhan",
        ["--thu-muc-nhat-ky"] = "thu-muc-nhat-ky", ["--log-dir"] = "thu-muc-nhat-ky",
    };

    private static readonly HashSet<string> ValueFlags = ["dia", "recovery-mb", "recovery", "thu-muc-nhat-ky", "chu-cai", "nhan", "vung", "kich-thuoc-mb", "he-tep", "giam-mb", "xac-nhan"];

    public string Command { get; private init; } = "tro-giup";
    public List<string> Positionals { get; } = [];
    private readonly Dictionary<string, string?> _flags = [];

    public bool Has(string flag) => _flags.ContainsKey(flag);

    public string? Value(string flag) => _flags.GetValueOrDefault(flag);

    public int? IntValue(string flag) =>
        Value(flag) is { } text
            ? int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : throw new UsageException($"Giá trị của --{flag} phải là số nguyên dương.")
            : null;

    public static CliArgs Parse(string[] args)
    {
        if (args.Length == 0)
            return new CliArgs();
        if (!CommandAliases.TryGetValue(args[0], out var command))
            throw new UsageException($"Lệnh không hợp lệ: {args[0]}. Chạy `tsudisk tro-giup`.");
        if (!CommandGate.KnownCommands.Contains(command))   // lenh moi PHAI dang ky o Core de cong phien ban biet (ADR-0012)
            throw new InvalidOperationException("Lệnh chưa đăng ký với cổng phiên bản: " + command);

        var result = new CliArgs { Command = command };
        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                result.Positionals.Add(arg);
                continue;
            }
            var (name, inline) = arg.Split('=', 2) is [var n, var v] ? (n, v) : (arg, null);
            if (!FlagAliases.TryGetValue(name, out var flag))
                throw new UsageException($"Cờ không hợp lệ: {name}.");
            if (ValueFlags.Contains(flag))
            {
                var value = inline ?? (i + 1 < args.Length ? args[++i] : throw new UsageException($"{name} cần một giá trị."));
                result._flags[flag] = value;
            }
            else
            {
                result._flags[flag] = null;
            }
        }
        return result;
    }

    /// <summary>Nhan "C", "C:", "c:\" - tra ve chu cai in hoa.</summary>
    public static char ParseDriveLetter(string text)
    {
        var trimmed = text.Trim().TrimEnd('\\', '/').TrimEnd(':');
        if (trimmed.Length != 1 || !char.IsAsciiLetter(trimmed[0]))
            throw new UsageException($"Ổ đích không hợp lệ: {text}. Ví dụ đúng: C:");
        return char.ToUpperInvariant(trimmed[0]);
    }
}

internal sealed class UsageException(string message) : Exception(message);
