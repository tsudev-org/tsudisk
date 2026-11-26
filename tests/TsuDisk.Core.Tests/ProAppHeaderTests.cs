using TsuDisk.Core.Pro;

namespace TsuDisk.Core.Tests;

/// <summary>
/// <c>X-Tsu-App</c> (APP_INTEGRITY 2.1, phieu SEC3 muc 2.3): khuon giong cong cua tsudev.com, kenh suy tu noi dat exe,
/// va header di kem luot lay quyen loi.
/// </summary>
public class ProAppHeaderTests
{
    [Theory]
    [InlineData("26.11.2401", "github", "tsudisk/26.11.2401/github")]
    [InlineData("26.11.2401-beta.1", "winget", "tsudisk/26.11.2401-beta.1/winget")]
    [InlineData("26.11.2401", "scoop", "tsudisk/26.11.2401/scoop")]
    public void Gia_tri_dung_khuon(string version, string channel, string expected)
        => Assert.Equal(expected, ProContract.AppHeaderValue(version, channel));

    [Theory]
    [InlineData("0", "github")]                 // ban tu build khong co VersionPrefix day du
    [InlineData("26.11", "github")]
    [InlineData("26.11.2401", "GitHub")]        // kenh chu thuong
    [InlineData("26.11.2401 ", "github")]
    [InlineData("26.11.2401", "git/hub")]
    public void Sai_khuon_thi_khong_gui(string version, string channel)
        => Assert.Null(ProContract.AppHeaderValue(version, channel));

    [Theory]
    [InlineData(@"C:\Users\a\scoop\apps\tsudisk\current\tsudisk.exe", "scoop")]
    [InlineData(@"C:\Users\a\AppData\Local\Microsoft\WinGet\Packages\TsuDEV.TsuDisk_x\tsudisk.exe", "winget")]
    [InlineData(@"C:\Users\a\AppData\Local\Microsoft\WinGet\Links\tsudisk.exe", "winget")]
    [InlineData(@"D:\Tools\tsudisk_26.11.2401_windows-x64-portable.exe", "github")]
    [InlineData(null, "github")]
    public void Suy_kenh_tu_noi_dat_exe(string? path, string expected)
        => Assert.Equal(expected, ProContract.DetectChannel(path));

    [Fact]
    public async Task Header_di_kem_luot_lay_quyen_loi()
    {
        using var rig = new ProRig(appHeader: "tsudisk/26.11.2401/github");
        await rig.LoginAsync();
        Assert.NotEmpty(rig.Idp.AppHeaders);
        Assert.All(rig.Idp.AppHeaders, h => Assert.Equal("tsudisk/26.11.2401/github", h));
    }

    [Fact]
    public async Task Khong_co_gia_tri_thi_khong_gui_header()
    {
        using var rig = new ProRig();
        await rig.LoginAsync();
        Assert.Empty(rig.Idp.AppHeaders);
    }
}
