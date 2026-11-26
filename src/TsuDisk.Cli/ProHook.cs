using System.Globalization;
using System.Runtime.Versioning;
using TsuDisk.Core.Pro;
using TsuDisk.Windows;

namespace TsuDisk.Cli;

/// <summary>
/// Tai khoan TsuDEV va quyen loi Pro trong CLI (ADR-0007; cai dat lay tu tsuswico ADR-0002). Ban mien phi KHONG can
/// dang nhap va du moi tinh nang; chua dang nhap thi khong co luot goi mang nao, khong tep nao duoc tao (hang C).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ProHook
{
    /// <summary>Lenh in ket qua cho may doc hoac chay tu dong thi khong hien nhac Pro.</summary>
    public static bool IsInteractive(CliArgs cli) =>
        !cli.Has("json") && !cli.Has("dong-y") && !cli.Has("xac-nhan")
        && !Console.IsInputRedirected && !Console.IsOutputRedirected
        && cli.Command is not ("phien-ban" or "tro-giup" or "dang-nhap" or "dang-xuat" or "tai-khoan");

    internal static HttpClient NewHttpClient(TimeSpan timeout)
    {
#pragma warning disable CA2000 // HttpClient so huu handler (disposeHandler mac dinh = true)
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = timeout };
#pragma warning restore CA2000
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TsuDisk/" + Commands.ToolVersion);
        return http;
    }

    internal static ProAccount CreateAccount(HttpClient http)
    {
        var dir = ProContract.DefaultStateDirectory();
        return new ProAccount(
            new ProAccountStore(dir, new DpapiTokenProtector()),
            new InstallationIdStore(dir),
            new ProHttp(http, ProEndpoints.Production, ProContract.ClientId,
                        ProContract.AppHeaderValue(Commands.ToolVersion, ProContract.DetectChannel(Environment.ProcessPath))),
            ProEndpoints.Production,
            ProContract.ClientId,
            () => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Nhac gia han / bao het ky (PRODUCT_POLICY 7.6 - chi nguoi da mua Pro, ngoai le co chu dich cua muc 3.8) o dau
    /// lenh tuong tac. CHI IN, khong cho phim, khong doi ma thoat. Thong bao "nhieu thiet bi" (7.3) da BO tu bo quy uoc
    /// v7.0.0; chua dang nhap thi khong in gi (AUTH 14.4: khong nhac dang nhap). Moi su co (mang, tep, DPAPI) bi nuot: Pro KHONG BAO GIO lam hong lenh dia.
    /// </summary>
    public static void Run(CliArgs cli)
    {
        if (!IsInteractive(cli)) return;
        try
        {
            using var http = NewHttpClient(TimeSpan.FromSeconds(5));
            var account = CreateAccount(http);
            if (!account.IsSignedIn) return;   // hang C: khong mang, khong tep
            var source = account.ForRunAsync(interactive: true, CancellationToken.None).GetAwaiter().GetResult();
            var dir = ProContract.DefaultStateDirectory();
            var now = DateTimeOffset.UtcNow;
            var state = new LocalStateStore(dir);
            state.RemoveLegacyKeys();
            new ProRenewalPrompt(source, state, ProContract.ClientId, Console.Out, readLine: null, _ => { }).Run(now, true);
        }
#pragma warning disable CA1031 // thong bao Pro la phu - khong bao gio chan lenh dia
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary><c>tsudisk dang-nhap | dang-xuat | tai-khoan</c>.</summary>
    public static int AccountCommand(CliArgs cli)
    {
        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            if (cancel.IsCancellationRequested) return;
            e.Cancel = true;
            cancel.Cancel();
        };
        using var http = NewHttpClient(TimeSpan.FromSeconds(20));
        var account = CreateAccount(http);
        try
        {
            return cli.Command switch
            {
                "dang-nhap" => Login(account, cancel.Token),
                "dang-xuat" => Logout(account, cancel.Token),
                _ => Status(account, cancel.Token),
            };
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            Console.Error.WriteLine("\nĐã hủy theo yêu cầu.");
            return ExitCodes.Failed;
        }
    }

    private static int Login(ProAccount account, CancellationToken cancel)
    {
        Console.WriteLine("Đăng nhập tài khoản TsuDEV (tsudev.com) bằng trình duyệt của máy...");
        var outcome = account.LoginAsync(
            SystemBrowser.Open,
            url =>
            {
                Console.WriteLine("Nếu trình duyệt không tự mở, hãy dán liên kết này vào trình duyệt:");
                Console.WriteLine("  " + url.AbsoluteUri);
                Console.WriteLine("Đang chờ đăng nhập (tối đa 5 phút, Ctrl+C để hủy)...");
            },
            ProAccount.LoginTimeout, cancel).GetAwaiter().GetResult();

        if (!outcome.IsSuccess)
        {
            Console.Error.WriteLine(LoginFailureText(outcome));
            return ExitCodes.Failed;
        }
        Console.WriteLine("\nĐã đăng nhập.");
        PrintAccount(outcome.Account, outcome.Entitlement, RefreshStatus.Updated);
        return ExitCodes.Ok;
    }

    private static string LoginFailureText(LoginOutcome o) => o.Status switch
    {
        LoginStatus.Timeout => "Hết 5 phút mà chưa đăng nhập xong. Chạy lại: tsudisk dang-nhap",
        LoginStatus.Denied => $"tsudev.com không cho đăng nhập ({o.Detail}). Bạn đã bấm Từ chối?",
        LoginStatus.StateMismatch => "Phản hồi đăng nhập không khớp phiên này - đã bỏ qua để an toàn. Chạy lại: tsudisk dang-nhap",
        LoginStatus.WrongIssuer => "Phản hồi đăng nhập không đến từ tsudev.com - đã bỏ qua.",
        LoginStatus.TokenRejected => $"tsudev.com từ chối mã đăng nhập ({o.Detail}). Chạy lại: tsudisk dang-nhap",
        LoginStatus.IdTokenInvalid => $"Danh tính trả về không kiểm được chữ ký ({o.Detail}) - không lưu gì.",
        LoginStatus.NoRefreshToken => "tsudev.com không cấp quyền giữ đăng nhập (offline_access) - không lưu gì.",
        LoginStatus.Offline => $"Không kết nối được tsudev.com ({o.Detail}). Kiểm tra mạng rồi chạy lại.",
        LoginStatus.NoLoopback => $"Không mở được cổng nhận phản hồi trên 127.0.0.1 lẫn [::1] ({o.Detail}).",
        _ => o.Status.ToString(),
    };

    private static int Logout(ProAccount account, CancellationToken cancel)
    {
        var wasSignedIn = account.IsSignedIn;
        var revoked = account.LogoutAsync(cancel).GetAwaiter().GetResult();
        if (!wasSignedIn)
        {
            Console.WriteLine("Chưa đăng nhập - không có gì để đăng xuất. Đã dọn dữ liệu tài khoản cũ (nếu có) trên máy này.");
            return ExitCodes.Ok;
        }
        Console.WriteLine("Đã đăng xuất và xóa dữ liệu tài khoản trên máy này: " + ProContract.DefaultStateDirectory());
        if (revoked != true)
            Console.WriteLine("Không báo được cho tsudev.com (mạng?). Phiên trên máy chủ tự hết hạn; có thể đăng xuất từ xa ở tsudev.com > Thiết bị và phiên.");
        Console.WriteLine("Nhật ký và bản sao lưu của TsuDisk KHÔNG bị xóa.");
        return ExitCodes.Ok;
    }

    private static int Status(ProAccount account, CancellationToken cancel)
    {
        if (!account.IsSignedIn)
        {
            Console.WriteLine("Tài khoản TsuDEV: Chưa đăng nhập");
            Console.WriteLine("Bản miễn phí dùng đủ mọi chức năng, không gửi dữ liệu nào lên mạng. Đăng nhập (không bắt buộc): tsudisk dang-nhap");
            Console.WriteLine("Vì sao nên đăng nhập? " + WhySignInUrl);
            return ExitCodes.Ok;
        }
        var r = account.RefreshAsync(cancel).GetAwaiter().GetResult();
        if (r.Status == RefreshStatus.SessionExpired)
        {
            Console.WriteLine("Tài khoản TsuDEV: Phiên đã hết hạn - Đăng nhập lại: tsudisk dang-nhap");
            return ExitCodes.Ok;
        }
        PrintAccount(account.Account, r.Entitlement, r.Status);
        return ExitCodes.Ok;
    }

    /// <summary>AUTH_AND_ACCOUNT 14.4.2 diem 1: giai thich vi sao nen dang nhap tai cho nguoi dung tu tim toi.</summary>
    internal const string WhySignInUrl = "https://tsudev.com/privacy#dang-nhap";

    private static string Date(DateTimeOffset d) => d.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static string DateTime(DateTimeOffset d) => d.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void PrintAccount(AccountInfo? info, Entitlement? ent, RefreshStatus status)
    {
        if (info is not null)
            Console.WriteLine($"Tài khoản TsuDEV: {info.Subject}   (đăng nhập lúc {DateTime(info.SignedInAt)})");
        if (status is RefreshStatus.Offline or RefreshStatus.Busy or RefreshStatus.Rejected)
            Console.WriteLine("Không lấy được quyền lợi mới - đang dùng bản lưu trên máy (nếu còn hạn).");
        if (ent is null)
        {
            Console.WriteLine("Gói: Miễn phí (chưa có thông tin quyền lợi hợp lệ)");
            return;
        }
        var isPro = ent.IsProAt(DateTimeOffset.UtcNow);
        Console.WriteLine(isPro
            ? "Gói: TsuDEV Pro - còn hạn đến " + Date(DateTimeOffset.FromUnixTimeSeconds(ent.ProUntil))
            : "Gói: Miễn phí");
        if (BadgeLabel(ent.Badge) is { } badge) Console.WriteLine("Huy hiệu: " + badge);
        Console.WriteLine("Thiết bị dùng tài khoản trong 30 ngày: " + ent.Devices30d.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("Thông tin quyền lợi lưu trên máy đến: " + DateTime(ent.ExpiresAt));
        if (!isPro)
        {
            // PRODUCT_POLICY 3.7.3: cau dinh vi -> quyen loi dang chay that -> dieu khoan 7.5 + chinh sach thanh toan ->
            // lien ket mua (o xac nhan khong chon san nam tren trang mua cua tsudev.com - chi tsudev.com nhan tien).
            Console.WriteLine();
            Console.WriteLine(ProRenewal.Positioning);
            foreach (var b in ProRenewal.ActiveBenefits) Console.WriteLine("  - " + b);
            Console.WriteLine(ProRenewal.PurchaseTerms);
            Console.WriteLine("Chính sách thanh toán, hoàn tiền và khiếu nại: " + ProRenewal.PaymentPolicyUrl);
            Console.WriteLine("Điều khoản: " + ProRenewal.TermsUrl);
            Console.WriteLine("Ủng hộ tác giả bằng TsuDEV Pro (không bắt buộc): " + ProContract.ProUrl(ProContract.ClientId));
        }
    }

    /// <summary>Nhan chu theo <c>badge</c> cua JWS (PRODUCT_POLICY 4.1); khong tu ve huy hieu.</summary>
    private static string? BadgeLabel(string badge) => badge switch
    {
        "pro" => "[Pro]",
        "donor" => "[Ủng hộ]",
        "both" => "[Pro] [Ủng hộ]",
        _ => null,
    };
}
