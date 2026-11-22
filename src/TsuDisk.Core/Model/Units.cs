using System.Globalization;

namespace TsuDisk.Core.Model;

/// <summary>Hang so kich thuoc va phep can le. Moi kich thuoc trong TsuDisk tinh bang byte (long).</summary>
public static class Units
{
    public const long KiB = 1024;
    public const long MiB = 1024 * KiB;
    public const long GiB = 1024 * MiB;

    /// <summary>Can le phan vung chuan cua Windows tu Vista: 1 MiB.</summary>
    public const long PartitionAlignment = MiB;

    /// <summary>Khoang trong nho hon muc nay coi nhu khong ton tai (vung dem can le, header GPT).</summary>
    public const long MinUsefulGap = MiB;

    public static long AlignDown(long value, long alignment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return value / alignment * alignment;
    }

    public static long AlignUp(long value, long alignment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        return checked((value + alignment - 1) / alignment * alignment);
    }

    public static bool IsAligned(long value, long alignment) => value % alignment == 0;

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Dinh dang kich thuoc kieu Windows (he 1024) voi dau phay thap phan tieng Viet: "265,11 GB".</summary>
    public static string Format(long bytes) => FormatIn(bytes, Vi);

    /// <summary>Nhu <see cref="Format(long)"/> nhung dau thap phan theo ngon ngu dang hien thi (giao dien en: "63.9 GB").</summary>
    public static string FormatIn(long bytes, IFormatProvider provider)
    {
        var sign = bytes < 0 ? "-" : "";
        double value = Math.Abs((double)bytes);
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }
        var text = value.ToString(index == 0 ? "0" : "0.##", provider);
        return $"{sign}{text} {units[index]}";
    }
}
