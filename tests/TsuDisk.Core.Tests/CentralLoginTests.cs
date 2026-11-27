using System.Text.RegularExpressions;
using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Tests;

/// <summary>
/// Mot cong dang nhap (AUTH_AND_ACCOUNT v5.0.0 muc 2.1, phieu LOGIN1): app chi mo trinh duyet he thong toi cong tsudev.com;
/// khong o mat khau, khong WebView, khong co CLI nhan mat khau. Bo sung cho <c>check-central-login.sh</c> (script chi
/// quet chu, khong biet XAML/WPF).
/// </summary>
public class CentralLoginTests
{
    [Fact]
    public void Trang_tai_khoan_va_trang_xoa_nam_tren_cong_tsudev()
    {
        var account = new Uri(ProContract.AccountPageUrl);
        var delete = new Uri(ProContract.DeleteAccountUrl);
        foreach (var u in new[] { account, delete })
        {
            Assert.Equal(Uri.UriSchemeHttps, u.Scheme);
            Assert.Equal("tsudev.com", u.Host);
            Assert.True(u.IsDefaultPort);
            Assert.DoesNotMatch("(?i)login|dang-nhap|signin", u.AbsolutePath);
        }
        Assert.Equal(account.AbsolutePath, delete.AbsolutePath);
        Assert.Equal("#xoa-tai-khoan", delete.Fragment);
    }

    [Fact]
    public void Dang_nhap_di_qua_authorize_cua_issuer_khong_ghi_cung_trang_dang_nhap()
    {
        var e = ProEndpoints.Production;
        foreach (var u in new[] { e.Authorization, e.Token, e.Revocation, e.Jwks })
            Assert.StartsWith(ProContract.Issuer + "/", u.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?i)login|dang-nhap", e.Authorization.AbsolutePath);
    }

    /// <summary>
    /// Diem cuoi duoc GHIM (ADR-0007: discovery gia mao khong doi duoc diem cuoi token/JWKS), nhung PHAI trung discovery
    /// that cua issuer - fixture chup tu https://tsudev.com/oidc/.well-known/openid-configuration ngay 27/11/2026.
    /// IdP doi diem cuoi ⇒ chup lai fixture ⇒ test do ⇒ sua <see cref="ProEndpoints.Production"/>.
    /// </summary>
    [Fact]
    public void Diem_cuoi_ghim_khop_discovery_cua_issuer()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot(), "tests", "TsuDisk.Core.Tests", "Fixtures", "oidc", "openid-configuration.json")));
        var d = doc.RootElement;
        var e = ProEndpoints.Production;
        Assert.Equal(ProContract.Issuer, d.GetProperty("issuer").GetString());
        Assert.Equal(e.Authorization.AbsoluteUri, d.GetProperty("authorization_endpoint").GetString());
        Assert.Equal(e.Token.AbsoluteUri, d.GetProperty("token_endpoint").GetString());
        Assert.Equal(e.Revocation.AbsoluteUri, d.GetProperty("revocation_endpoint").GetString());
        Assert.Equal(e.Jwks.AbsoluteUri, d.GetProperty("jwks_uri").GetString());
        Assert.Contains("S256", d.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Giao_dien_khong_co_o_mat_khau_hay_WebView()
    {
        var offenders = SourceFiles("*.xaml")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"<PasswordBox\b|WebView2?\b|<WebBrowser\b"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Ma_nguon_khong_nhan_mat_khau_tai_khoan()
    {
        // CLI: khong co --password/--mat-khau; khong goi password grant; khong goi SDK dang nhap cua nha cung cap.
        var pattern = new Regex(@"""--?(password|mat-khau|pass)""|grant_type=password|""password""\s*\]|GoogleSignIn|Microsoft\.Web\.WebView2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offenders = SourceFiles("*.cs").Where(f => pattern.IsMatch(File.ReadAllText(f))).ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Cua_so_tai_khoan_GUI_chi_co_nut_khong_co_o_nhap()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "TsuDisk.Gui", "AccountWindow.xaml"));
        Assert.DoesNotMatch(@"<(TextBox|PasswordBox|ComboBox|RichTextBox)\b", xaml);
        foreach (var name in new[] { "SignInButton", "OpenAccountButton", "SignOutButton", "DeleteAccountButton" })
            Assert.Contains($"x:Name=\"{name}\"", xaml, StringComparison.Ordinal);
    }

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "TsuDisk.slnx"))) return d.FullName;
        throw new InvalidOperationException("Khong tim thay goc repo.");
    }
}
