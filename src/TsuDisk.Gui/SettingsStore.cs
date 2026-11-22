using System.IO;
using System.Text;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

/// <summary>
/// Tep cai dat giao dien <c>%LOCALAPPDATA%\TsuDisk\giao-dien.json</c>. Khong dat trong <c>%LOCALAPPDATA%\tsudev\tsudisk</c>
/// vi <c>tsudisk dang-xuat</c> xoa ca thu muc do. Doc/ghi loi => dung mac dinh, khong bao loi (tien nghi hien thi).
/// </summary>
internal static class SettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TsuDisk", "giao-dien.json");

    public static bool Disabled { get; set; }

    public static GuiSettings Load()
    {
        if (Disabled)
            return GuiSettings.Default;
        try
        {
            return File.Exists(FilePath) ? GuiSettings.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) : GuiSettings.Default;
        }
        catch (IOException)
        {
            return GuiSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return GuiSettings.Default;
        }
    }

    public static void Save(GuiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Disabled)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, settings.ToJson(), Encoding.UTF8);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
