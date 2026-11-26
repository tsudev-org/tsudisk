using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TsuDisk.Core.Integrity;

/// <summary>Mot nguon xuat: tep hoac thu muc (de quy) vao thu muc <see cref="ArchiveFolder"/> trong zip.</summary>
public sealed record ExportSource(string ArchiveFolder, string Path);

public sealed record ExportedFile(string Path, long Size, string Sha256);

/// <param name="Skipped">Tep khong doc duoc (dang bi khoa, khong co quyen) hoac bi loai vi la bi mat/lien ket.</param>
public sealed record ExportResult(string ArchivePath, IReadOnlyList<ExportedFile> Files, IReadOnlyList<string> Skipped);

/// <summary>
/// Xuat du lieu nguoi dung ra mot tep zip (APP_INTEGRITY I6, LOCAL_SYNC muc 9; ADR-0012 muc 11) - dung chung cho lenh
/// <c>xuat-du-lieu</c> va nut tren modal khoa. Kem <c>manifest.json</c> liet ke tung tep + SHA-256.
/// An toan:
/// - KHONG theo lien ket tuong trung/junction (reparse point) - khong keo tep ngoai nguon vao zip.
/// - KHONG xuat bi mat thiet bi (<c>*.dpapi</c> = refresh token, khoa DPoP; <c>session.lock</c>; tep tam <c>*.tmp</c>)
///   ke ca khi ben goi lo dua thu muc chua chung.
/// - Khong ghi de tep co san (<see cref="FileMode.CreateNew"/>); loi giua chung ⇒ xoa tep zip do dang.
/// </summary>
public static partial class DataExport
{
    public const string ManifestEntry = "manifest.json";
    public const string Format = "tsudisk-export/1";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex FolderShape();

    /// <summary>Tep bi mat thiet bi hoac tep tam - khong bao gio vao zip.</summary>
    public static bool IsExcluded(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.EndsWith(".dpapi", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, Pro.ProAccountStore.LockFile, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary><c>%ProgramData%\TsuDisk\nhat-ky</c> - nhat ky lan chay + ban sao luu GPT (CLI va GUI ghi chung).</summary>
    public static string DefaultLogRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TsuDisk", "nhat-ky");

    /// <summary>Cai dat giao dien <c>%LOCALAPPDATA%\TsuDisk\giao-dien.json</c> (trung <c>TsuDisk.Gui.SettingsStore</c>).</summary>
    public static string GuiSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TsuDisk", "giao-dien.json");

    /// <summary>
    /// Nguon chung cho lenh <c>xuat-du-lieu</c> va nut tren modal khoa GUI (ADR-0012 muc 11). KHONG co thu muc trang thai
    /// Pro (<c>%LOCALAPPDATA%\tsudev\tsudisk</c>: refresh token, dem ban ke) - do la bi mat/du lieu thiet bi.
    /// </summary>
    public static IReadOnlyList<ExportSource> StandardSources(string? logRoot = null) =>
    [
        new("nhat-ky", Path.GetFullPath(logRoot ?? DefaultLogRoot)),
        new("cai-dat", GuiSettingsPath),
    ];

    public static ExportResult Export(IEnumerable<ExportSource> sources, string destinationDirectory, string appVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var list = sources.ToList();
        foreach (var s in list)
            if (!FolderShape().IsMatch(s.ArchiveFolder))
                throw new ArgumentException("Tên thư mục trong tệp xuất không hợp lệ: " + s.ArchiveFolder, nameof(sources));

        Directory.CreateDirectory(destinationDirectory);
        var (archivePath, stream) = CreateArchiveFile(destinationDirectory, now);
        var files = new List<ExportedFile>();
        var skipped = new List<string>();
        try
        {
            using (stream)
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ManifestEntry };
                foreach (var source in list)
                {
                    var full = Path.GetFullPath(source.Path);
                    if (File.Exists(full))
                        AddFile(zip, new FileInfo(full), source.ArchiveFolder + "/" + Path.GetFileName(full), archivePath, names, files, skipped);
                    else if (Directory.Exists(full) && !IsReparse(new DirectoryInfo(full)))
                        AddDirectory(zip, new DirectoryInfo(full), source.ArchiveFolder, archivePath, names, files, skipped);
                }
                WriteManifest(zip, appVersion, now, files, skipped);
            }
            return new ExportResult(archivePath, files, skipped);
        }
        catch
        {
            try { File.Delete(archivePath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private static (string, FileStream) CreateArchiveFile(string dir, DateTimeOffset now)
    {
        var stem = "tsudisk-xuat-du-lieu-" + now.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (var i = 1; i <= 100; i++)
        {
            var path = Path.Combine(dir, i == 1 ? stem + ".zip" : $"{stem}-{i}.zip");
            try { return (path, new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)); }
            catch (IOException) when (File.Exists(path)) { }
        }
        throw new IOException("Không tạo được tệp xuất mới trong " + dir);
    }

    private static void AddDirectory(ZipArchive zip, DirectoryInfo dir, string prefix, string archivePath,
        HashSet<string> names, List<ExportedFile> files, List<string> skipped)
    {
        IEnumerable<FileSystemInfo> entries;
        try { entries = dir.EnumerateFileSystemInfos().OrderBy(e => e.Name, StringComparer.Ordinal).ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            skipped.Add(prefix + "/");
            return;
        }
        foreach (var e in entries)
        {
            var name = prefix + "/" + e.Name;
            if (IsReparse(e)) { skipped.Add(name); continue; }
            if (e is DirectoryInfo d) AddDirectory(zip, d, name, archivePath, names, files, skipped);
            else if (e is FileInfo f) AddFile(zip, f, name, archivePath, names, files, skipped);
        }
    }

    private static void AddFile(ZipArchive zip, FileInfo file, string name, string archivePath,
        HashSet<string> names, List<ExportedFile> files, List<string> skipped)
    {
        if (string.Equals(file.FullName, archivePath, StringComparison.OrdinalIgnoreCase)) return;   // chinh tep zip dang ghi
        if (IsReparse(file) || IsExcluded(file.Name) || !names.Add(name)) { skipped.Add(name); return; }

        FileStream input;
        try
        {
            // Nhat ky co the dang duoc ghi: cho chia se doc/ghi/xoa, chi doc.
            input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            skipped.Add(name);
            return;
        }
        using (input)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            using (var output = entry.Open())
            {
                var buffer = new byte[81920];
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, n);
                    output.Write(buffer, 0, n);
                    size += n;
                }
            }
            files.Add(new ExportedFile(name, size, Convert.ToHexStringLower(hash.GetHashAndReset())));
        }
    }

    private static void WriteManifest(ZipArchive zip, string appVersion, DateTimeOffset now, List<ExportedFile> files, List<string> skipped)
    {
        var arr = new JsonArray();
        foreach (var f in files)
            arr.Add((JsonNode)new JsonObject { ["path"] = f.Path, ["size"] = f.Size, ["sha256"] = f.Sha256 });
        var sk = new JsonArray();
        foreach (var s in skipped) sk.Add((JsonNode?)JsonValue.Create(s));
        var root = new JsonObject
        {
            ["format"] = Format,
            ["app"] = VersionKeys.Product,
            ["version"] = appVersion,
            ["created_at"] = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["files"] = arr,
            ["skipped"] = sk,
        };
        var entry = zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
        using var w = entry.Open();
        using var writer = new Utf8JsonWriter(w, new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        root.WriteTo(writer);
    }

    private static bool IsReparse(FileSystemInfo e) => e.Attributes.HasFlag(FileAttributes.ReparsePoint) || e.LinkTarget is not null;
}
