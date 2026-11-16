using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using TsuDisk.Core.Execution;

namespace TsuDisk.Windows;

/// <summary>Ket qua chay mot cong cu he thong.</summary>
public sealed record ToolResult(int ExitCode, string Output);

/// <summary>
/// Chay cong cu he thong cua Windows. BAO MAT: luon dung duong dan tuyet doi trong System32 (khong tra PATH, khong
/// dung thu muc hien hanh) de mot reagentc.exe/diskpart.exe gia dat canh tsudisk.exe khong bao gio duoc chay voi
/// quyen quan tri. Tham so truyen qua ArgumentList (khong ghep chuoi lenh, khong qua shell).
/// </summary>
[SupportedOSPlatform("windows")]
public static class SystemTools
{
    static SystemTools() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static string System32 =>
        // Tien trinh 32-bit bi WOW64 chuyen huong System32 -> SysWOW64 (khong co reagentc); Sysnative tranh dieu do.
        Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem
            ? Environment.GetFolderPath(Environment.SpecialFolder.System)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative");

    public static string WinReImagePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Recovery", "Winre.wim");

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static ToolResult Run(string exeName, IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var path = Path.Combine(System32, exeName);
        if (!File.Exists(path))
            throw new DiskOperationException($"Không tìm thấy công cụ hệ thống {path}.");

        var oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = oem,
            StandardErrorEncoding = oem,
            WorkingDirectory = System32,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new DiskOperationException($"Không chạy được {exeName}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout ?? TimeSpan.FromMinutes(10)))
        {
            process.Kill(entireProcessTree: true);
            throw new DiskOperationException($"{exeName} quá thời gian chờ.");
        }
        return new ToolResult(process.ExitCode, stdout.Result + stderr.Result);
    }

    public static ToolResult RunChecked(string exeName, params string[] arguments)
    {
        var result = Run(exeName, arguments);
        if (result.ExitCode != 0)
            throw new DiskOperationException($"{exeName} {string.Join(' ', arguments)} thất bại (mã {result.ExitCode}): {Trim(result.Output)}");
        return result;
    }

    private static string Trim(string text) => text.Length <= 600 ? text.Trim() : text[..600].Trim() + "...";
}
