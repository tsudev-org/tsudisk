using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using TsuDisk.Core.Integrity;
using TsuDisk.Core.Pro;
using TsuDisk.Windows;

namespace TsuDisk.Gui;

/// <summary>
/// Cong phien ban cua GUI (ADR-0012 muc 7 + 10). Quyet dinh nam o Core (<see cref="VersionGate"/>,
/// <see cref="GuiGateSchedule"/> - co test); lop nay chi noi su kien cua so, bo dem gio va modal.
///
/// - CHUA dang nhap (khong co tep phien cua <c>tsudisk dang-nhap</c>) ⇒ khong mang, khong tep (hang C, Q1).
/// - Da dang nhap ⇒ GET ban ke (HttpClient khong theo chuyen huong, 5 giay) + doc dau <c>426</c> do CLI ghi.
/// - Moi loi (mang, tep, DPAPI) bi nuot: cong KHONG BAO GIO lam hong GUI; chi ban ke da ky hoac 426 moi khoa.
/// - KHONG co co hay bien moi truong nao bo qua cong.
/// </summary>
internal sealed class GuiGate
{
    private readonly Window _owner;
    private readonly MainViewModel _model;
    private readonly string? _logRoot;
    private readonly GuiGateSchedule _schedule = new(() => DateTimeOffset.UtcNow);
    private readonly DispatcherTimer _timer;
    private readonly string _channel = ProContract.DetectChannel(Environment.ProcessPath);

    public GuiGate(Window owner, MainViewModel model, string? logRoot)
    {
        _owner = owner;
        _model = model;
        _logRoot = logRoot;
        _timer = new DispatcherTimer(DispatcherPriority.Background, owner.Dispatcher) { Interval = GuiGateSchedule.Period };
        _timer.Tick += (_, _) => _ = CheckAsync();
    }

    /// <summary>Kiem ngay khi mo, roi moi 6 gio va khi quay lai sau &gt;= 30 phut o nen.</summary>
    public void Start()
    {
        _owner.Deactivated += (_, _) => _schedule.Deactivated();
        _owner.Activated += (_, _) =>
        {
            if (_schedule.Activated())
                _ = CheckAsync();
        };
        // Dang ghi dia thi modal cho; thao tac xong (IsBusy -> false) moi hien.
        ((INotifyPropertyChanged)_model).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsBusy) && !_model.IsBusy)
                Present(_schedule.Idle());
        };
        _timer.Start();
        _ = CheckAsync();
    }

    private async Task CheckAsync()
    {
        if (!_schedule.TryBeginCheck())
            return;
        GateDecision? decision = null;
        try
        {
            if (IsSignedIn())
            {
                using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = VersionGate.FetchTimeout };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TsuDisk/" + App.Version);
                var directory = Path.Combine(ProContract.DefaultStateDirectory(), "versions");
                var gate = new VersionGate(new HttpVersionSource(http), new VersionManifestCache(directory),
                    () => DateTimeOffset.UtcNow, serverLock: new ServerLockStore(directory));
                decision = await gate.CheckAsync(VersionKeys.Product, _channel, App.Version, CancellationToken.None).ConfigureAwait(true);
            }
        }
#pragma warning disable CA1031 // cong la phu: moi su co ⇒ khong khoa, GUI chay tiep
        catch (Exception)
#pragma warning restore CA1031
        {
            decision = null;
        }
        Present(_schedule.EndCheck(decision, _model.IsBusy));
    }

    /// <summary>Tep phien ton tai = da dang nhap (chi kiem tep, khong giai ma, khong tao gi).</summary>
    private static bool IsSignedIn()
    {
        try
        {
            return new ProAccountStore(ProContract.DefaultStateDirectory(), new DpapiTokenProtector()).HasSession;
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }

    private void Present(GateDecision? decision)
    {
        if (decision is null)
            return;
        _timer.Stop();
        // BeginInvoke: khong mo vong lap modal ben trong handler PropertyChanged/Activated.
        _owner.Dispatcher.BeginInvoke(() => Show(_owner, decision, UpdateAction.For(_channel, decision.Url), _logRoot));
    }

    public static void Show(Window owner, GateDecision decision, UpdateAction action, string? logRoot)
        => new UpdateRequiredWindow(decision, action, logRoot) { Owner = owner }.ShowDialog();

    /// <summary>
    /// Quyet dinh MAU chi de hien thi (<c>--chup-man-hinh</c>, <c>--du-lieu-mau --xem-khoa-mau</c>): khong di qua cong,
    /// khong mang, khong tep. Ban moi = ban dang chay + 1 o phan cuoi.
    /// </summary>
    public static GateDecision SampleDecision()
    {
        var current = AppVersion.TryParse(App.Version) ?? AppVersion.TryParse("26.11.2601")!;
        var latest = AppVersion.TryParse($"{current.Major}.{current.Minor}.{current.Patch + 1}");
        return new GateDecision(GateStatus.UpdateRequired, current, latest, UpdateAction.ReleasesPage, null, FromCache: false);
    }
}
