using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Pro;
using TsuDisk.Windows;

namespace TsuDisk.Gui;

/// <summary>
/// Tai khoan TsuDEV trong GUI (AUTH_AND_ACCOUNT v5.0.0 muc 2.1, phieu RS1). Mo cua so KHONG goi mang: trang thai doc tu
/// tep phien cuc bo. Chi khi nguoi dung bam nut moi co mang (dang nhap PKCE loopback, thu hoi khi dang xuat) - cung duong
/// voi <c>tsudisk dang-nhap</c>. Che do chup man hinh: trang thai mau "chua dang nhap", khong doc tep nao.
/// </summary>
// Window khong co Dispose: HttpClient + CancellationTokenSource duoc giai phong o su kien Closed (moi duong dong deu qua do).
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Giai phong o Closed")]
internal sealed partial class AccountWindow : Window
{
    private readonly HttpClient? _http;
    private readonly ProAccount? _account;
    private CancellationTokenSource? _signIn;
    private bool _busy;

    public AccountWindow(bool sample = false)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        if (!sample)
        {
            _http = ProAccountFactory.NewHttpClient(TimeSpan.FromSeconds(20), App.Version);
            _account = ProAccountFactory.Create(_http, App.Version, r =>
                new ServerLockStore(Path.Combine(ProContract.DefaultStateDirectory(), "versions"))
                    .TrySave(r, App.Version, DateTimeOffset.UtcNow));
        }
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Closing += (_, _) => _signIn?.Cancel();
        Closed += (_, _) =>
        {
            Loc.Instance.LanguageChanged -= OnLanguageChanged;
            _signIn?.Dispose();
            _http?.Dispose();
        };
        Render();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private bool SignedIn => SafeRead(() => _account?.IsSignedIn ?? false);

    private void Render()
    {
        var info = SignedIn ? SafeRead(() => _account?.Account) : null;
        StateText.Text = info is null
            ? Loc.Instance["AccountSignedOut"]
            : Loc.Instance.Format("AccountSignedIn", info.Subject,
                info.SignedInAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture));
        var waiting = _signIn is not null;
        SignInButton.Visibility = info is null && !waiting ? Visibility.Visible : Visibility.Collapsed;
        CancelSignInButton.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
        SignOutButton.Visibility = info is null ? Visibility.Collapsed : Visibility.Visible;
        SignOutButton.IsEnabled = !_busy;
        SignInButton.IsEnabled = !_busy;
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        if (_account is null || _signIn is not null)
            return;
        _signIn = new CancellationTokenSource();
        Show(Loc.Instance["AccountWaiting"]);
        Render();
        LoginOutcome? outcome = null;
        try
        {
            outcome = await _account.LoginAsync(
                SystemBrowser.Open,
                url => Dispatcher.Invoke(() =>
                {
                    ReopenLink.NavigateUri = url;
                    ReopenText.Visibility = Visibility.Visible;
                }),
                ProAccount.LoginTimeout, _signIn.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            outcome = null;
        }
        finally
        {
            _signIn.Dispose();
            _signIn = null;
            ReopenText.Visibility = Visibility.Collapsed;
        }
        if (!IsLoaded)
            return;
        Show(outcome is null
            ? Loc.Instance["AccountSignInCancelled"]
            : outcome.IsSuccess
                ? Loc.Instance["AccountSignInDone"]
                : Loc.Instance.Format(FailureKey(outcome.Status), outcome.Detail ?? ""));
        Render();
    }

    /// <summary>Moi <see cref="LoginStatus"/> (tru Success) co chuoi <c>LoginFail_*</c> o ca hai tep resx (ResxParityTests).</summary>
    private static string FailureKey(LoginStatus status) => "LoginFail_" + status;

    private void OnCancelSignIn(object sender, RoutedEventArgs e) => _signIn?.Cancel();

    private async void OnSignOut(object sender, RoutedEventArgs e)
    {
        if (_account is null || _busy)
            return;
        _busy = true;
        Render();
        bool? revoked;
        try
        {
            revoked = await _account.LogoutAsync(CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _busy = false;
        }
        Show(Loc.Instance["AccountSignOutDone"] + (revoked == true ? "" : " " + Loc.Instance["AccountSignOutOffline"]));
        Render();
    }

    private void OnOpenAccount(object sender, RoutedEventArgs e) => OpenPortal(ProContract.AccountPageUrl, null);

    private void OnDeleteAccount(object sender, RoutedEventArgs e) => OpenPortal(ProContract.DeleteAccountUrl, "AccountDeleteOpened");

    private void OnReopen(object sender, RequestNavigateEventArgs e)
    {
        if (!SystemBrowser.Open(e.Uri))
            Show(Loc.Instance.Format("AccountBrowserFailed", e.Uri.AbsoluteUri));
        e.Handled = true;
    }

    /// <summary>Trinh duyet he thong o quyen THUONG (SystemBrowser) - GUI co the dang chay quyen quan tri.</summary>
    private void OpenPortal(string url, string? doneKey)
    {
        if (!SystemBrowser.Open(new Uri(url)))
            Show(Loc.Instance.Format("AccountBrowserFailed", url));
        else if (doneKey is not null)
            Show(Loc.Instance[doneKey]);
    }

    private void Show(string message)
    {
        MessageText.Text = message;
        MessageText.Visibility = Visibility.Visible;
    }

    /// <summary>Tep phien hong/khong doc duoc (DPAPI, quyen) ⇒ coi nhu chua dang nhap, KHONG lam sap GUI.</summary>
    private static T? SafeRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return default;
        }
    }

    /// <summary>Che do chup: co dinh kich thuoc, dat ngoai man hinh (khong dung tep phien).</summary>
    internal void PrepareForCapture()
    {
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
    }
}
