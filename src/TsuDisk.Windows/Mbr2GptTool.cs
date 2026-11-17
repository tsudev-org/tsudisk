using System.Runtime.Versioning;
using System.Text;
using TsuDisk.Core.Conversion;
using TsuDisk.Core.Model;

namespace TsuDisk.Windows;

/// <summary>
/// Chay mbr2gpt.exe /validate (CHI DOC) qua <see cref="SystemTools"/> (duong dan tuyet doi System32) va lay phan
/// %windir%\setupact.log ma lan chay nay ghi them - mbr2gpt ghi ly do tu choi vao do, khong ra console.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Mbr2GptTool
{
    public static bool IsAvailable => File.Exists(Path.Combine(SystemTools.System32, Mbr2GptCheck.ToolName));

    private static string SetupActLog => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "setupact.log");

    public static Mbr2GptReport Validate(DiskLayout layout)
    {
        var before = ReadLog();
        var result = SystemTools.Run(Mbr2GptCheck.ToolName, Mbr2GptCheck.ValidateArguments(layout.Disk.Number), TimeSpan.FromMinutes(5));
        var after = ReadLog();
        return Mbr2GptCheck.Interpret(layout, result.ExitCode, result.Output, Mbr2GptCheck.NewLogLines(before, after));
    }

    /// <summary>Doc tep log dang mo boi tien trinh khac; UTF-16 (co hoac khong BOM) hay ANSI deu duoc.</summary>
    private static string ReadLog()
    {
        try
        {
            using var stream = new FileStream(SetupActLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            var looksUtf16 = bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0;
            return looksUtf16 ? Encoding.Unicode.GetString(bytes) : Encoding.Latin1.GetString(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
