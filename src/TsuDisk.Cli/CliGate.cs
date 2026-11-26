using System.Runtime.Versioning;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Pro;

namespace TsuDisk.Cli;

/// <summary>
/// Cong phien ban truoc moi lenh (ADR-0012 muc 7, 9). Quyet dinh nam o <see cref="CommandGate"/> (Core, co test bang
/// moi lenh); lop nay chi noi day: trang thai dang nhap (tep, khong mang), HttpClient khong theo chuyen huong, dem.
/// Chay ca khi <c>--json</c>/<c>--dong-y</c>/chuyen huong I/O - neu khong thi do la cua bo qua cong.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class CliGate
{
    /// <returns><c>null</c> = cho chay lenh; so = ma thoat (da in thong bao khoa).</returns>
    public static int? Check(CliArgs cli)
    {
        if (CommandGate.IsAllowedWhenLocked(cli.Command))
            return null;   // khong dung den tep tai khoan lan mang

        var channel = ProContract.DetectChannel(Environment.ProcessPath);
        GateDecision? decision;
        HttpClient? http = null;
        try
        {
            decision = CommandGate.EvaluateAsync(
                cli.Command,
                IsSignedIn,
                () =>
                {
                    http = ProHook.NewHttpClient(VersionGate.FetchTimeout);
                    return new VersionGate(new HttpVersionSource(http), new VersionManifestCache(VersionsDirectory),
                        () => DateTimeOffset.UtcNow, serverLock: ServerLock);
                },
                channel, Commands.ToolVersion, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            http?.Dispose();
        }

        return CommandGate.MustBlock(cli.Command, decision) ? PrintLocked(cli, decision!, channel) : null;
    }

    /// <summary>Thu muc dem ban ke + dau 426 (<c>%LOCALAPPDATA%\tsudev\tsudisk\versions</c>), GUI dung chung.</summary>
    internal static string VersionsDirectory => Path.Combine(ProContract.DefaultStateDirectory(), "versions");

    internal static ServerLockStore ServerLock => new(VersionsDirectory);

    /// <summary>
    /// Sau luot lam moi quyen loi (ProHook): may chu vua tra 426 ⇒ dau da ghi ⇒ khoa lenh nay luon (ADR-0012 muc 8).
    /// Doc tep, khong mang.
    /// </summary>
    public static int? CheckServerLock(CliArgs cli)
    {
        if (CommandGate.IsAllowedWhenLocked(cli.Command) || AppVersion.TryParse(Commands.ToolVersion) is not { } current)
            return null;
        var decision = ServerLock.Load(current, DateTimeOffset.UtcNow);
        return CommandGate.MustBlock(cli.Command, decision)
            ? PrintLocked(cli, decision!, ProContract.DetectChannel(Environment.ProcessPath))
            : null;
    }

    private static int PrintLocked(CliArgs cli, GateDecision decision, string channel)
    {
        if (cli.Has("json"))
        {
            Console.WriteLine(CommandGate.LockedJson(decision, channel));
        }
        else
        {
            foreach (var line in CommandGate.LockedLines(decision, channel))
                Console.Error.WriteLine(line);
        }
        return ExitCodes.UpdateRequired;
    }

    /// <summary>Doc tep phien (khong mang). Loi doc/DPAPI ⇒ coi la chua dang nhap: cong KHONG BAO GIO lam hong lenh dia.</summary>
    private static bool IsSignedIn()
    {
        try
        {
            using var http = ProHook.NewHttpClient(TimeSpan.FromSeconds(1));   // khong gui gi - ProAccount can de khoi tao
            return ProHook.CreateAccount(http).IsSignedIn;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}
