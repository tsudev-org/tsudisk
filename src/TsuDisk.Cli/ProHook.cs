using TsuDisk.Core.Pro;

namespace TsuDisk.Cli;

/// <summary>
/// Nhac gia han / bao het ky TsuDEV Pro (PRODUCT_POLICY 7.6) va thong bao nhieu thiet bi (7.3) o dau moi lan chay
/// lenh tuong tac - ADR-0007. CHI IN, khong cho phim, khong doi ma thoat, khong bao gio lam hong lenh dia.
///
/// Nguon quyen loi: JWS + JWKS da dem trong <see cref="ProContract.DefaultStateDirectory"/>. TsuDisk chua co luong
/// dang nhap (cho client <c>tsudisk</c> tren tsudev.com) ⇒ hien tai khong co tep nao, ham nay khong in, khong ghi gi,
/// khong goi mang (hang C).
/// </summary>
internal static class ProHook
{
    /// <summary>Lenh in ket qua cho may doc hoac chay tu dong thi khong hien.</summary>
    public static bool IsInteractive(CliArgs cli) =>
        !cli.Has("json") && !cli.Has("dong-y") && !cli.Has("xac-nhan")
        && !Console.IsInputRedirected && !Console.IsOutputRedirected
        && cli.Command is not ("phien-ban" or "tro-giup");

    public static void Run(CliArgs cli, string backupFolder)
    {
        if (!IsInteractive(cli)) return;
        try
        {
            var dir = ProContract.DefaultStateDirectory();
            var source = LoadSource(dir);
            if (source is NoEntitlementSource) return;
            var state = new LocalStateStore(dir);
            var now = DateTimeOffset.UtcNow;
            new ProRenewalPrompt(source, state, ProContract.ClientId, Console.Out, readLine: null, _ => { }).Run(now, true);
            new ProNoticePrompt(source, state, ProContract.ClientId, Console.Out, readLine: null, _ => { })
                .Run(now, true, backupFolder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (FormatException) { }
    }

    private static IEntitlementSource LoadSource(string dir)
    {
        var jwsPath = Path.Combine(dir, ProContract.CachedEntitlementFile);
        var jwksPath = Path.Combine(dir, ProContract.CachedJwksFile);
        if (!File.Exists(jwsPath) || !File.Exists(jwksPath)) return NoEntitlementSource.Instance;
        var keys = JwksKeySet.Parse(File.ReadAllText(jwksPath));
        return new CachedEntitlementSource(File.ReadAllText(jwsPath).Trim(), new EntitlementVerifier(keys, ProContract.ClientId));
    }
}
