namespace TsuDisk.Core.Integrity;

public enum GateStatus
{
    /// <summary>Dang chay ban moi nhat cua kenh (hoac ban ke khong co muc cho san pham/kenh nay).</summary>
    Ok,

    /// <summary>Co ban moi hon tren kenh cua minh, hoac may chu tra <c>426</c> ⇒ KHOA (APP_INTEGRITY I2).</summary>
    UpdateRequired,

    /// <summary>Khong co ban ke hop le (mang loi + khong co dem con han) ⇒ KHONG chan (APP_INTEGRITY 2.2).</summary>
    Unknown,

    /// <summary>Ban tu dung (phien ban sai khuon) ⇒ cong khong ap dung (ADR-0012 muc 6).</summary>
    NotApplicable,
}

/// <param name="Latest">Ban moi nhat cua kenh; co the null khi <c>426</c> khong noi ban nao.</param>
/// <param name="Problem">Ly do ban ke tai ve bi bo (null neu tai ve dung hoac mang loi).</param>
/// <param name="FromCache">Quyet dinh dua tren ban ke da dem (mang loi hoac ban tai ve bi bo).</param>
public sealed record GateDecision(
    GateStatus Status, AppVersion? Current, AppVersion? Latest, Uri? Url, ManifestCheck? Problem, bool FromCache)
{
    public bool IsLocked => Status == GateStatus.UpdateRequired;
}

/// <summary>
/// Cong phien ban (ADR-0012). Ben goi (CLI/GUI) CHI tao cong khi da dang nhap (quyet dinh chu du an Q1, hang C).
/// Loi mang/ban ke hong KHONG BAO GIO lam khoa nguoi dung; chi ban ke da qua kiem chu ky hoac <c>426</c> moi khoa.
/// </summary>
public sealed class VersionGate
{
    public static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

    private readonly IVersionSource source;
    private readonly VersionManifestCache cache;
    private readonly Func<DateTimeOffset> clock;
    private readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> keys;

    /// <param name="keys">Chi test truyen khoa thu; CLI/GUI luon dung <see cref="VersionKeys.Production"/>.</param>
    public VersionGate(IVersionSource source, VersionManifestCache cache, Func<DateTimeOffset> clock,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? keys = null)
    {
        this.source = source;
        this.cache = cache;
        this.clock = clock;
        this.keys = keys ?? VersionKeys.Production;
    }

    public async Task<GateDecision> CheckAsync(string product, string channel, string currentVersion, CancellationToken cancel)
    {
        if (AppVersion.TryParse(currentVersion) is not { } current)
            return new GateDecision(GateStatus.NotApplicable, null, null, null, null, false);

        var now = clock();
        var cached = cache.Load() is { } doc
            ? VersionManifestVerifier.Verify(doc.Manifest, doc.Signature, now, null, keys)
            : null;

        VersionDocument? fetched = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(FetchTimeout);
            fetched = await source.FetchAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested) { }   // het gio
        catch (HttpRequestException) { }
        catch (IOException) { }

        ManifestCheck? problem = null;
        if (fetched is not null)
        {
            var fresh = VersionManifestVerifier.Verify(fetched.Manifest, fetched.Signature, now, cached?.IssuedAt, keys);
            if (fresh.Manifest is { } m)
            {
                TrySave(fetched);
                return Decide(m, product, channel, current, problem: null, fromCache: false);
            }
            problem = fresh.Status;
        }

        return cached?.Manifest is { } c
            ? Decide(c, product, channel, current, problem, fromCache: true)
            : new GateDecision(GateStatus.Unknown, current, null, null, problem, false);
    }

    /// <summary>Chot chan theo kenh: chi so voi <c>latest</c> cua dung (san pham, kenh).</summary>
    public static GateDecision Decide(VersionManifest manifest, string product, string channel, AppVersion current,
        ManifestCheck? problem = null, bool fromCache = false)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(current);
        if (manifest.Find(product, channel) is { } r && r.Latest > current)
            return new GateDecision(GateStatus.UpdateRequired, current, r.Latest, r.Url, problem, fromCache);
        return new GateDecision(GateStatus.Ok, current, null, null, problem, fromCache);
    }

    /// <summary>May chu tra <c>426 update_required</c> (APP_INTEGRITY 2.2) ⇒ khoa, du ban ke noi gi.</summary>
    public static GateDecision FromServer(Pro.UpdateRequiredResponse response, string currentVersion)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new GateDecision(GateStatus.UpdateRequired, AppVersion.TryParse(currentVersion),
            AppVersion.TryParse(response.Latest), response.Url, null, false);
    }

    private void TrySave(VersionDocument doc)
    {
        try { cache.Save(doc); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
