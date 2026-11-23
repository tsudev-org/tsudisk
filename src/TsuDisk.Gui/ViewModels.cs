using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;
using TsuDisk.Core.Presentation;

namespace TsuDisk.Gui;

internal abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Raise(name);
        return true;
    }
}

internal sealed class RelayCommand(Action<object?> run, Func<object?, bool>? can = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => can?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => run(parameter);
}

/// <summary>Muc cua o chon: nhan doc lai tu <see cref="Loc"/> moi khi doi ngon ngu (khong dung lai danh sach).</summary>
internal sealed class Choice<T>(T value, Func<string> label) : Observable
{
    public T Value { get; } = value;
    public string Label => label();
    public void Relabel() => Raise(nameof(Label));
    public override string ToString() => Label;
}

internal sealed class DiskItem(DiskLayout layout) : Observable
{
    public DiskLayout Layout { get; } = layout;

    public string Label
    {
        get
        {
            var l = Loc.Instance;
            var d = Layout.Disk;
            var extra = string.Join("", new[]
            {
                d.IsSystem ? $" - {l["SystemDisk"]}" : "",
                d.IsVirtual ? $" - {l["VirtualDisk"]}" : "",
                d.IsReadOnly ? $" - {l["ReadOnlyDisk"]}" : "",
            });
            return l.Format("DiskLabelFormat", d.Number, d.FriendlyName, Units.FormatIn(d.Size, l.Culture),
                d.Style.ToString().ToUpperInvariant()) + extra;
        }
    }

    public void Relabel() => Raise(nameof(Label));
    public override string ToString() => Label;
}

/// <summary>Mot hang cua bang / mot khoi cua ban do - cung mot doi tuong de chon o cho nay sang cho kia.</summary>
internal sealed class SegmentItem(Segment segment, int index, double fraction, MapRole role) : Observable
{
    private static Loc L => Loc.Instance;

    public Segment Segment { get; } = segment;
    public int Index { get; } = index;
    public double Fraction { get; } = fraction;
    public MapRole Role { get; } = role;
    public PartitionInfo? Partition => Segment.Partition;

    public string Number => Partition?.PartitionNumber.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string Drive => Partition?.DriveLetter is { } l ? $"{l}:" : "";
    public string Type => Partition is { } p ? L[$"Kind_{p.Kind}"] : L["FreeSegment"];
    public string Offset => Units.FormatIn(Segment.Offset, L.Culture);
    public string Size => Units.FormatIn(Segment.Size, L.Culture);
    public string Free => Partition?.FreeBytes is { } f ? Units.FormatIn(f, L.Culture) : "";
    public string FileSystem => Partition?.FileSystem ?? "";

    /// <summary>Chu trong khoi ban do: chu cai (neu co) + dung luong.</summary>
    public string MapLabel => Drive.Length > 0 ? $"{Drive} {Size}" : Size;

    /// <summary>Ten cho trinh doc man hinh: loai, o, dung luong.</summary>
    public string AccessibleName => string.Join(", ", new[] { Type, Drive, Size, Partition?.Label ?? "" }.Where(s => s.Length > 0));

    public bool CanPlanExtend => Partition is { Kind: PartitionKind.BasicData, DriveLetter: not null };

    public void Relabel()
    {
        foreach (var name in new[] { nameof(Type), nameof(Offset), nameof(Size), nameof(Free), nameof(MapLabel), nameof(AccessibleName) })
            Raise(name);
    }
}

/// <summary>Chan vung ban ghi (DATA_TABLE): bo chon moc trai, tom tat giua, phan trang phai.</summary>
internal sealed class PagerViewModel : Observable
{
    private PageWindow _window = new(0);
    private readonly Action<int> _sizeChanged;

    public PagerViewModel(int pageSize, Action<int> sizeChanged)
    {
        _window = new PageWindow(0, pageSize);
        _sizeChanged = sizeChanged;
        Go = new RelayCommand(p => Window = _window.WithPage(Convert.ToInt32(p, CultureInfo.InvariantCulture)));
        Previous = new RelayCommand(_ => Window = _window.WithPage(_window.Page - 1), _ => _window.CanGoPrevious);
        Next = new RelayCommand(_ => Window = _window.WithPage(_window.Page + 1), _ => _window.CanGoNext);
        Loc.Instance.LanguageChanged += (_, _) => RaiseAll();
    }

    public event EventHandler? Changed;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "XAML rang buoc theo thuoc tinh cua DataContext.")]
    public IReadOnlyList<int> Sizes => PageWindow.Sizes;
    public ICommand Go { get; }
    public ICommand Previous { get; }
    public ICommand Next { get; }

    public PageWindow Window
    {
        get => _window;
        set
        {
            _window = value;
            RaiseAll();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int PageSize
    {
        get => _window.PageSize;
        set
        {
            if (value == _window.PageSize)
                return;
            Window = _window.WithPageSize(value);
            _sizeChanged(_window.PageSize);
        }
    }

    public bool HasRecords => _window.Total > 0;

    public string Summary => _window.Total == 0 ? ""
        : _window.IsSinglePage ? Loc.Instance.Format("SummarySingle", _window.Total.ToString("N0", Loc.Instance.Culture))
        : Loc.Instance.Format("SummaryRange", _window.FirstIndex + 1, _window.FirstIndex + _window.CountOnPage,
            _window.Total.ToString("N0", Loc.Instance.Culture));

    /// <summary>Cau cho vung aria-live tuong duong (LiveSetting.Polite) sau khi doi moc/trang.</summary>
    public string LiveSummary => _window.Total == 0 ? ""
        : Loc.Instance.Format("LiveSummary", _window.FirstIndex + 1, _window.FirstIndex + _window.CountOnPage, _window.Total);

    public IReadOnlyList<PageCell> Cells => [.. _window.PageCells().Select(p => new PageCell(p, p == _window.Page))];

    private void RaiseAll()
    {
        Raise(nameof(Window));
        Raise(nameof(PageSize));
        Raise(nameof(Summary));
        Raise(nameof(LiveSummary));
        Raise(nameof(Cells));
        Raise(nameof(HasRecords));
        CommandManager.InvalidateRequerySuggested();
    }
}

internal sealed record PageCell(int? Page, bool IsCurrent)
{
    public bool IsEllipsis => Page is null;
    public string Text => Page?.ToString(CultureInfo.InvariantCulture) ?? "...";
    public string AccessibleName => Page is { } p ? Loc.Instance.Format("PageN", p) : "...";
}

internal sealed record StepItem(int Index, string Title, bool Writes);

/// <summary>Mot dong tien do khi ap dung: trang thai buoc lay tu nhat ky (cung nhat ky CLI ghi ra dia).</summary>
internal sealed record RunStepItem(int Index, string Title, string Status, string? Message);

internal enum PlanKind { None, Extend, Rename, Create }

internal sealed class MainViewModel : Observable
{
    public const string PartitionTable = "phan-vung";

    private readonly IDiskSource _source;
    private GuiSettings _settings;
    private IReadOnlyList<DiskLayout> _layouts = [];
    private DiskItem? _selectedDisk;
    private SegmentItem? _selectedSegment;
    private bool _isLoading;
    private string? _error;
    private Plan? _plan;
    private Refusal? _refusal;
    private bool _winReKnown = true;
    private bool _copied;
    private readonly IPlanExecutor? _executor;
    private PlanKind _planKind;
    private RenameRequest? _renameRequest;
    private IReadOnlySet<char> _lettersInUse = new HashSet<char>();
    private IReadOnlyList<char> _renameLetters = [];
    private char _renameLetter;
    private string _renameLabel = "";
    private bool _isBusy;
    private RunJournal? _lastRun;
    private string _lastRunDirectory = "";
    private string? _runError;
    private UsableExtent? _createExtent;
    private CreatePartitionRequest? _createRequest;
    private IReadOnlyList<char> _createLetters = [];
    private char _createLetter;
    private string _createSize = "";
    private string _createFileSystem = "NTFS";
    private string _createLabel = "";

    public MainViewModel(IDiskSource source, GuiSettings settings, IPlanExecutor? executor = null)
    {
        _source = source;
        _settings = settings;
        _executor = executor;
        Pager = new PagerViewModel(settings.PageSize(PartitionTable), size =>
        {
            _settings = _settings.WithPageSize(PartitionTable, size);
            SettingsStore.Save(_settings);
        });
        Pager.Changed += (_, _) => Raise(nameof(PageRows));
        Refresh = new RelayCommand(_ => _ = LoadAsync(), _ => !IsLoading && !IsBusy);
        PlanExtend = new RelayCommand(_ => MakePlan(), _ => SelectedSegment?.CanPlanExtend == true && !IsBusy);
        PlanRename = new RelayCommand(_ => MakeRenamePlan(), _ => CanRenameSelected && !IsBusy);
        PlanCreate = new RelayCommand(_ => MakeCreatePlan(), _ => CanCreateSelected && !IsBusy);
        Apply = new RelayCommand(_ => _ = ApplyAsync(), _ => ShowApply && _executor!.IsAdministrator && !IsBusy);
        RelaunchAsAdmin = new RelayCommand(_ =>
        {
            if (_executor?.RelaunchAsAdministrator() == true)
                System.Windows.Application.Current.Shutdown(0);
        }, _ => !IsBusy);
        CopyCommand = new RelayCommand(_ =>
        {
            System.Windows.Clipboard.SetText(Command);
            Copied = true;
        }, _ => Command.Length > 0);
        var l = Loc.Instance;
        ThemeChoices =
        [
            new(ThemeChoice.System, () => l["ThemeSystem"]),
            new(ThemeChoice.Light, () => l["ThemeLight"]),
            new(ThemeChoice.Dark, () => l["ThemeDark"]),
        ];
        LanguageChoices =
        [
            new(UiLanguage.System, () => l["LanguageSystem"]),
            new("vi", () => "Tiếng Việt"),
            new("en", () => "English"),
        ];
        // Doi ngon ngu chi doi CHU: giu dia, o dang chon va ke hoach dang xem.
        l.LanguageChanged += (_, _) =>
        {
            foreach (var c in ThemeChoices)
                c.Relabel();
            foreach (var c in LanguageChoices)
                c.Relabel();
            foreach (var d in Disks)
                d.Relabel();
            foreach (var r in Rows)
                r.Relabel();
            Raise(nameof(PlanButtonText));
            Raise(nameof(AnalysisTotal));
            Raise(nameof(RefusalText));
            Raise(nameof(PlanNotice));
            Raise(nameof(ModeBadge));
            Raise(nameof(CreateAvailable));
            Raise(nameof(ApplyText));
            RaiseRun();
        };
    }

    public bool IsSample => _source.IsSample;

    /// <summary><c>--dia N</c>: dia chon san khi mo (E2E chon dung o ao vua tao).</summary>
    public int? InitialDisk { get; init; }
    public ObservableCollection<DiskItem> Disks { get; } = [];
    public ObservableCollection<SegmentItem> Rows { get; } = [];
    public IEnumerable<SegmentItem> PageRows => Pager.Window.Slice(Rows);
    public ObservableCollection<string> Advice { get; } = [];
    public PagerViewModel Pager { get; }
    public ICommand Refresh { get; }
    public ICommand PlanExtend { get; }
    public ICommand PlanRename { get; }
    public ICommand PlanCreate { get; }
    public ICommand Apply { get; }
    public ICommand RelaunchAsAdmin { get; }
    public ICommand CopyCommand { get; }

    /// <summary>Cua so chinh gan: hien hop thoai xac nhan lan hai, true = da go dung chu cai/so dia va bam ap dung.</summary>
    public Func<Plan, bool>? ConfirmApply { get; set; }
    public IReadOnlyList<Choice<ThemeChoice>> ThemeChoices { get; }
    public IReadOnlyList<Choice<string>> LanguageChoices { get; }

    public ThemeChoice Theme
    {
        get => _settings.Theme;
        set
        {
            if (value == _settings.Theme)
                return;
            _settings = _settings with { Theme = value };
            SettingsStore.Save(_settings);
            ThemeManager.Apply(value);
            Raise();
        }
    }

    public string Language
    {
        get => _settings.Language;
        set
        {
            if (value is null || value == _settings.Language)
                return;
            _settings = _settings with { Language = value };
            SettingsStore.Save(_settings);
            Raise();
            Loc.Instance.SetLanguage(UiLanguage.Resolve(value, App.SystemLanguages()));
        }
    }

    public bool IsLoading { get => _isLoading; private set { if (Set(ref _isLoading, value)) CommandManager.InvalidateRequerySuggested(); } }
    public string? Error { get => _error; private set { Set(ref _error, value); Raise(nameof(HasError)); } }
    public bool HasError => Error is not null;
    public bool IsEmpty => !IsLoading && !HasError && Disks.Count == 0;

    public DiskItem? SelectedDisk
    {
        get => _selectedDisk;
        set
        {
            if (!Set(ref _selectedDisk, value))
                return;
            ShowDisk();
        }
    }

    public SegmentItem? SelectedSegment
    {
        get => _selectedSegment;
        set
        {
            if (!Set(ref _selectedSegment, value))
                return;
            ClearPlan();
            ResetRenameForm();
            ResetCreateForm();
            if (value is not null && (value.Index < Pager.Window.FirstIndex || value.Index >= Pager.Window.FirstIndex + Pager.Window.PageSize))
                Pager.Window = Pager.Window.WithPage(value.Index / Pager.Window.PageSize + 1);
            Raise(nameof(PlanButtonText));
            Raise(nameof(ShowPlanHint));
            Raise(nameof(CanRenameSelected));
            Raise(nameof(ShowRenameHint));
            Raise(nameof(CanCreateSelected));
            Raise(nameof(ShowCreateHint));
            Raise(nameof(CreateAvailable));
        }
    }

    // ---------- Tao phan vung trong vung trong (ADR-0010) ----------

    public bool CanCreateSelected => _createExtent is not null;
    public bool ShowCreateHint => !CanCreateSelected;
    public string CreateAvailable => _createExtent is { } e ? Loc.Instance.Format("CreateAvailable", Units.FormatIn(e.Size, Loc.Instance.Culture)) : "";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "XAML rang buoc theo thuoc tinh cua DataContext.")]
    public IReadOnlyList<string> CreateFileSystems => CreatePartitionRequest.FileSystems;
    public IReadOnlyList<char> CreateLetters { get => _createLetters; private set => Set(ref _createLetters, value); }
    public char CreateLetter { get => _createLetter; set { if (Set(ref _createLetter, value)) ClearPlanIf(PlanKind.Create); } }
    public string CreateSize { get => _createSize; set { if (Set(ref _createSize, value ?? "")) ClearPlanIf(PlanKind.Create); } }
    public string CreateFileSystem { get => _createFileSystem; set { if (Set(ref _createFileSystem, value ?? "NTFS")) ClearPlanIf(PlanKind.Create); } }
    public string CreateLabel { get => _createLabel; set { if (Set(ref _createLabel, value ?? "")) ClearPlanIf(PlanKind.Create); } }

    // ---------- Doi chu cai / nhan (ADR-0009) ----------

    public bool CanRenameSelected => RenameForm.CanRename(SelectedSegment?.Partition);
    public bool ShowRenameHint => !CanRenameSelected;
    public IReadOnlyList<char> RenameLetters { get => _renameLetters; private set => Set(ref _renameLetters, value); }
    public char RenameLetter { get => _renameLetter; set { if (Set(ref _renameLetter, value)) ClearPlanIf(PlanKind.Rename); } }
    public string RenameLabel { get => _renameLabel; set { if (Set(ref _renameLabel, value ?? "")) ClearPlanIf(PlanKind.Rename); } }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value))
                return;
            Raise(nameof(IsIdle));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsIdle => !IsBusy;

    /// <summary>Nhan tren dau trang: "Chi doc" tru khi dang chay voi quyen quan tri va duoc phep ap dung.</summary>
    public string ModeBadge => _executor?.IsAdministrator == true ? Loc.Instance["AdminBadge"] : Loc.Instance["ReadOnlyBadge"];

    /// <summary>Nut ap dung chi co cho ke hoach doi ten / tao phan vung, o that (khong phai du lieu mau).</summary>
    public bool ShowApply => _planKind is PlanKind.Rename or PlanKind.Create && _plan is not null && _executor is not null;
    public bool NeedsAdmin => ShowApply && !_executor!.IsAdministrator;
    public bool CanApplyNow => ShowApply && _executor!.IsAdministrator;
    public string ApplyText => _plan is not { } p ? ""
        : _planKind == PlanKind.Create ? Loc.Instance.Format("ApplyCreate", p.DiskNumber)
        : Loc.Instance.Format("ApplyRename", p.TargetLetter);

    public string PlanNotice => _planKind switch
    {
        PlanKind.Rename when _executor is null => Loc.Instance["RenameSampleNotice"],
        PlanKind.Rename when !_executor.IsAdministrator => Loc.Instance["RenameNeedsAdmin"],
        PlanKind.Rename => Loc.Instance["RenameApplyNotice"],
        PlanKind.Create when _executor is null => Loc.Instance["CreateSampleNotice"],
        PlanKind.Create when !_executor.IsAdministrator => Loc.Instance["CreateNeedsAdmin"],
        PlanKind.Create => Loc.Instance["CreateApplyNotice"],
        _ => Loc.Instance["ReadOnlyNotice"],
    };

    public bool HasRun => _lastRun is not null || _runError is not null;
    public IReadOnlyList<RunStepItem> RunSteps => _lastRun is { } j
        ? [.. j.Steps.Select(s => new RunStepItem(s.Index, s.Title, Loc.Instance[$"Step_{s.Status}"], s.Message))]
        : [];
    public string RunResult => _lastRun is { } j ? Loc.Instance[$"Run_{j.State}"] : "";

    /// <summary>Trang thai may doc (ten RunState, "Error" khi loi truoc khi chay) - E2E doc qua UIA ItemStatus.</summary>
    public string RunStateCode => _runError is not null ? "Error" : _lastRun?.State.ToString() ?? "";
    public IReadOnlyList<string> RunAdvice => _lastRun is { State: RunState.RolledBack or RunState.NeedsAttention } j
        ? RecoveryAdvisor.Advise(j) : [];
    public string RunDirectory => _lastRunDirectory.Length > 0 ? Loc.Instance.Format("RunDirectory", _lastRunDirectory) : "";
    public string? RunError => _runError;
    public bool HasRunError => _runError is not null;

    public string AnalysisTotal => SelectedDisk is { } d ? Loc.Instance.Format("AnalysisTotal", Units.FormatIn(d.Layout.UnallocatedTotal, Loc.Instance.Culture)) : "";
    public bool HasNoFreeSpace => SelectedDisk is not null && Advice.Count == 0;

    public string PlanButtonText => SelectedSegment?.Partition?.DriveLetter is { } l && SelectedSegment.CanPlanExtend
        ? Loc.Instance.Format("PlanButton", l) : Loc.Instance.Format("PlanButton", "?");

    public bool ShowPlanHint => SelectedSegment?.CanPlanExtend != true;
    public bool HasPlan => _plan is not null;
    public bool HasRefusal => _refusal is not null;
    public string PlanHeadline => _plan is { } p ? PlanText.Headline(p) : "";
    public IReadOnlyList<string> PlanNotes => _plan?.Notes ?? [];
    public IReadOnlyList<StepItem> PlanSteps => _plan is { } p ? [.. p.Steps.Select(s => new StepItem(s.Index, s.Title, s.ChangesDisk))] : [];
    public string PlanInvariants => _plan is { } p ? string.Join(" ", PlanText.Invariants(p)) : "";
    public string RefusalText => _refusal is { } r ? $"{Loc.Instance["PlanRefused"]} ({r.Code}): {r.Message}" : "";
    public IReadOnlyList<string> RefusalHints => _refusal?.Hints ?? [];
    public bool ShowWinReNote => (HasPlan || HasRefusal) && !_winReKnown;
    public string Command => _plan is null ? ""
        : _planKind == PlanKind.Rename && _renameRequest is { } r ? RenameForm.Command(r)
        : _planKind == PlanKind.Create && _createRequest is { } c ? CreateForm.Command(_plan.DiskNumber, c)
        : $"tsudisk mo-rong {_plan.TargetLetter}:";
    public bool Copied { get => _copied; private set => Set(ref _copied, value); }

    public async Task LoadAsync()
    {
        var keep = SelectedDisk?.Layout.Disk.Number ?? InitialDisk;
        IsLoading = true;
        Error = null;
        Raise(nameof(IsEmpty));
        try
        {
            _layouts = await Task.Run(_source.ReadAll).ConfigureAwait(true);
            _lettersInUse = await Task.Run(_source.LettersInUse).ConfigureAwait(true);
            RebuildDisks(keep);
        }
#pragma warning disable CA1031 // Giao dien bao "Khong tai duoc du lieu" + Thu lai cho MOI loi doc (WMI, quyen, COM) - khong sap app.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Error = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            Raise(nameof(IsEmpty));
        }
    }

    private void RebuildDisks(int? keep)
    {
        Disks.Clear();
        foreach (var layout in _layouts)
            Disks.Add(new DiskItem(layout));
        _selectedDisk = null;
        SelectedDisk = Disks.FirstOrDefault(x => x.Layout.Disk.Number == keep) ?? Disks.FirstOrDefault();
        Raise(nameof(IsEmpty));
    }

    private void ShowDisk()
    {
        var previous = SelectedSegment?.Segment.Offset;
        Rows.Clear();
        Advice.Clear();
        if (SelectedDisk is { } disk)
        {
            var blocks = DiskMap.Build(disk.Layout);
            for (var i = 0; i < blocks.Count; i++)
                Rows.Add(new SegmentItem(blocks[i].Segment, i, blocks[i].Fraction, blocks[i].Role));
            foreach (var item in LayoutAnalyzer.Analyze(disk.Layout))
                Advice.Add(item.Advice);
        }
        Pager.Window = Pager.Window.WithTotal(Rows.Count);
        _selectedSegment = null;
        SelectedSegment = Rows.FirstOrDefault(r => r.Segment.Offset == previous && r.CanPlanExtend)
            ?? Rows.FirstOrDefault(r => r.CanPlanExtend);
        Raise(nameof(SelectedSegment));
        Raise(nameof(PageRows));
        Raise(nameof(AnalysisTotal));
        Raise(nameof(HasNoFreeSpace));
    }

    /// <summary>Chi LAP ke hoach (thuan, tren mo hinh) - khong co duong nao tu day toi PlanRunner.</summary>
    private void MakePlan()
    {
        if (SelectedDisk is not { } disk || SelectedSegment?.Partition?.DriveLetter is not { } letter)
            return;
        var (winRe, known) = _source.ReadWinRe();
        _winReKnown = known;
        var result = ExtendPlanner.Plan(disk.Layout, winRe, new ExtendRequest(letter));
        ClearRun();
        _planKind = PlanKind.Extend;
        _renameRequest = null;
        _plan = result.Plan;
        _refusal = result.Refusal;
        Copied = false;
        RaisePlan();
    }

    /// <summary>Lap ke hoach doi ten (thuan). Ap dung la buoc rieng, qua hai lan xac nhan.</summary>
    private void MakeRenamePlan()
    {
        if (SelectedDisk is not { } disk || SelectedSegment?.Partition is not { } partition || !RenameForm.CanRename(partition))
            return;
        ClearRun();
        _winReKnown = true;
        _renameRequest = RenameForm.Request(partition, RenameLetter, RenameLabel, _source.SystemDrive, _source.VirtualOnly);
        var result = RenamePlanner.Plan(disk.Layout, _lettersInUse, _renameRequest);
        _planKind = PlanKind.Rename;
        _plan = result.Plan;
        _refusal = result.Refusal;
        Copied = false;
        RaisePlan();
    }

    /// <summary>Lap ke hoach tao phan vung (thuan). Ap dung la buoc rieng, qua hai lan xac nhan (go lai so dia).</summary>
    private void MakeCreatePlan()
    {
        if (SelectedDisk is not { } disk || _createExtent is not { } extent)
            return;
        ClearRun();
        _winReKnown = true;
        _planKind = PlanKind.Create;
        _createRequest = null;
        _plan = null;
        if (!CreateForm.TryParseSizeMb(CreateSize, out var size))
        {
            _refusal = new Refusal(RefusalCode.InvalidOption, Loc.Instance["CreateSizeInvalid"], []);
        }
        else
        {
            _createRequest = CreateForm.Request(extent, size, CreateFileSystem, CreateLabel,
                CreateLetter == default ? null : CreateLetter, _source.VirtualOnly);
            var result = CreatePartitionPlanner.Plan(disk.Layout, _lettersInUse, _createRequest);
            _plan = result.Plan;
            _refusal = result.Refusal;
        }
        Copied = false;
        RaisePlan();
    }

    private void ResetCreateForm()
    {
        _createExtent = SelectedDisk is { } disk ? CreateForm.ExtentFor(disk.Layout, SelectedSegment?.Segment) : null;
        CreateLetters = _createExtent is not null && SelectedDisk is { } d ? CreateForm.LetterChoices(d.Layout, _lettersInUse) : [];
        _createLetter = CreateLetters.Count > 0 ? CreateLetters[0] : default;
        _createSize = "";
        _createFileSystem = "NTFS";
        _createLabel = "";
        foreach (var name in new[] { nameof(CreateLetter), nameof(CreateSize), nameof(CreateFileSystem), nameof(CreateLabel) })
            Raise(name);
    }

    private void ResetRenameForm()
    {
        var p = SelectedSegment?.Partition;
        if (RenameForm.CanRename(p))
        {
            RenameLetters = RenameForm.LetterChoices(p!.DriveLetter!.Value, _lettersInUse);
            _renameLetter = p.DriveLetter.Value;
            _renameLabel = p.Label ?? "";
        }
        else
        {
            RenameLetters = [];
            _renameLetter = default;
            _renameLabel = "";
        }
        Raise(nameof(RenameLetter));
        Raise(nameof(RenameLabel));
    }

    private async Task ApplyAsync()
    {
        if (_executor is null || _plan is not { } plan || _planKind is not (PlanKind.Rename or PlanKind.Create) || !_executor.IsAdministrator)
            return;
        // Xac nhan lan hai: go lai chu cai o (doi ten) / so dia (tao) trong hop thoai. Khong co hop thoai => KHONG ap dung.
        if (ConfirmApply?.Invoke(plan) != true)
            return;
        IsBusy = true;
        ClearRun();
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        try
        {
            var (journal, dir) = await Task.Run(() => _executor.Run(plan, j => dispatcher.BeginInvoke(() =>
            {
                _lastRun = j;
                RaiseRun();
            }))).ConfigureAwait(true);
            _lastRun = journal;
            _lastRunDirectory = dir;
        }
#pragma warning disable CA1031 // Loi truoc khi PlanRunner bat dau (tao thu muc nhat ky, quyen) => bao tren giao dien, khong sap app.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _runError = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RaiseRun();
        }
        // Doc lai bo cuc that sau khi chay (thanh cong hay hoan tac) de giao dien khop dia.
        ClearPlan();
        await LoadAsync().ConfigureAwait(true);
    }

    private void ClearPlanIf(PlanKind kind)
    {
        if (_planKind == kind)
            ClearPlan();
    }

    private void ClearPlan()
    {
        _plan = null;
        _refusal = null;
        _renameRequest = null;
        _createRequest = null;
        _planKind = PlanKind.None;
        Copied = false;
        RaisePlan();
    }

    private void ClearRun()
    {
        _lastRun = null;
        _lastRunDirectory = "";
        _runError = null;
        RaiseRun();
    }

    private void RaiseRun()
    {
        foreach (var name in new[] { nameof(HasRun), nameof(RunSteps), nameof(RunResult), nameof(RunAdvice), nameof(RunDirectory),
            nameof(RunError), nameof(HasRunError), nameof(RunStateCode) })
            Raise(name);
    }

    private void RaisePlan()
    {
        foreach (var name in new[] { nameof(HasPlan), nameof(HasRefusal), nameof(PlanHeadline), nameof(PlanNotes), nameof(PlanSteps),
            nameof(PlanInvariants), nameof(RefusalText), nameof(RefusalHints), nameof(ShowWinReNote), nameof(Command),
            nameof(ShowApply), nameof(NeedsAdmin), nameof(CanApplyNow), nameof(ApplyText), nameof(PlanNotice) })
            Raise(name);
        CommandManager.InvalidateRequerySuggested();
    }
}
