using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TsuDisk.Core.Pro;

/// <summary>Bo token tu <c>/oidc/token</c>.</summary>
public sealed record TokenSet(string AccessToken, string? RefreshToken, string? IdToken);

/// <summary>Ket qua goi <c>/oidc/token</c>: <see cref="Tokens"/> hoac ma loi OAuth (<c>invalid_grant</c>...).</summary>
public sealed record TokenResult(TokenSet? Tokens, string? Error, HttpStatusCode Status)
{
    public bool IsOk => Tokens is not null;
}

/// <summary>Ket qua <c>/api/me/entitlements</c>: JWS compact, hoac ma HTTP + ma loi.</summary>
/// <param name="UpdateRequired">May chu tra <c>426 update_required</c> (APP_INTEGRITY 2.1) ⇒ ben goi KHOA app (ADR-0012 muc 8).</param>
public sealed record EntitlementFetch(string? Jws, HttpStatusCode Status, string? Error, UpdateRequiredResponse? UpdateRequired = null);

/// <summary>Than <c>426</c>: <c>{"error":"update_required","latest":"...","url":"..."}</c>; url chi nhan https.</summary>
public sealed record UpdateRequiredResponse(string? Latest, Uri? Url);

/// <summary>
/// Moi luot goi mang cua tinh nang Pro nam O DAY - mot cho de ra soat (QU-STD-AUTH hang C: chua dang
/// nhap thi khong lop nao duoc goi vao day, xem <see cref="ProAccount"/>). Client cong khai
/// (<c>token_endpoint_auth_method: none</c>): khong co secret nao nhung trong exe.
///
/// Loi mang (<see cref="HttpRequestException"/>, het gio) de NEM ra ngoai - ben goi quyet dinh "coi nhu
/// mien phi". Loi giao thuc (4xx/5xx co than JSON) tra ve trong ket qua.
/// </summary>
public sealed class ProHttp(HttpClient http, ProEndpoints endpoints, string clientId, string? appHeader = null)
{
    /// <summary>Gan <see cref="ProContract.AppHeader"/> (neu co) vao moi luot goi tsudev.com.</summary>
    private HttpRequestMessage Request(HttpMethod method, Uri uri, HttpContent? body = null)
    {
        var req = new HttpRequestMessage(method, uri) { Content = body };
        if (appHeader is not null) req.Headers.TryAddWithoutValidation(ProContract.AppHeader, appHeader);
        return req;
    }

    public Task<TokenResult> ExchangeCodeAsync(string code, string verifier, string redirectUri, CancellationToken cancel)
        => TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
            ["client_id"] = clientId,
        }, cancel);

    public Task<TokenResult> RefreshAsync(string refreshToken, CancellationToken cancel)
        => TokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        }, cancel);

    /// <summary>RFC 7009. Thanh cong hay khong deu khong doi viec xoa du lieu cuc bo.</summary>
    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancel)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = refreshToken,
            ["token_type_hint"] = "refresh_token",
            ["client_id"] = clientId,
        });
        using var req = Request(HttpMethod.Post, endpoints.Revocation, body);
        using var res = await http.SendAsync(req, cancel).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }

    public async Task<string> GetJwksAsync(CancellationToken cancel)
    {
        using var req = Request(HttpMethod.Get, endpoints.Jwks);
        using var res = await http.SendAsync(req, cancel).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>Dong ho cho <c>iat</c> cua bang chung DPoP (test thay duoc).</summary>
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <summary>Nonce gan nhat may chu cap qua <c>DPoP-Nonce</c> (RFC 9449 muc 8) - dung lai cho luot sau.</summary>
    private string? _dpopNonce;

    /// <summary>Do lech dong ho may nay so voi may chu, hoc tu header <c>Date</c> khi bang chung bi tu choi.</summary>
    private TimeSpan _serverSkew;

    /// <summary>Lech qua muc nay moi coi la "sai gio" (may chu cho ±60 giay, ADR-0004 tsudev muc 6).</summary>
    internal static readonly TimeSpan SkewTolerance = TimeSpan.FromSeconds(30);

    /// <summary>
    /// <c>GET /api/me/entitlements</c> kem Bearer + <c>X-Tsu-Installation</c> (AUTH_AND_ACCOUNT 14.3) va, khi co
    /// <paramref name="dpop"/>, header <c>DPoP</c> (RFC 9449) de may chu gan <c>cnf.jkt</c> vao JWS. Thu lai DUNG MOT
    /// lan khi may chu doi nonce (<c>use_dpop_nonce</c>) hoac tu choi bang chung ma dong ho may nay lech &gt; 30 giay.
    /// </summary>
    public async Task<EntitlementFetch> GetEntitlementAsync(string accessToken, string installationId, CancellationToken cancel,
                                                            IDpopKey? dpop = null)
    {
        var first = await SendEntitlementAsync(accessToken, installationId, dpop, cancel).ConfigureAwait(false);
        if (dpop is null || !first.Retry) return first.Result;
        return (await SendEntitlementAsync(accessToken, installationId, dpop, cancel).ConfigureAwait(false)).Result;
    }

    private async Task<(EntitlementFetch Result, bool Retry)> SendEntitlementAsync(
        string accessToken, string installationId, IDpopKey? dpop, CancellationToken cancel)
    {
        using var req = Request(HttpMethod.Get, endpoints.Entitlements);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.TryAddWithoutValidation(ProContract.InstallationHeader, installationId);
        if (dpop is not null)
            req.Headers.TryAddWithoutValidation(DpopProof.Header,
                DpopProof.Create(dpop, "GET", endpoints.Entitlements, accessToken, Clock() + _serverSkew, _dpopNonce));
        using var res = await http.SendAsync(req, cancel).ConfigureAwait(false);
        var text = await res.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        var json = TryParse(text);
        var retry = dpop is not null && ShouldRetryDpop(res, json);
        if (UpdateRequiredFrom(res.StatusCode, json) is { } upgrade)
            return (new EntitlementFetch(null, res.StatusCode, "update_required", upgrade), false);
        if (!res.IsSuccessStatusCode) return (new EntitlementFetch(null, res.StatusCode, Str(json, "error") ?? DpopError(res)), retry);
        return (new EntitlementFetch(Str(json, "jws"), res.StatusCode, null), false);
    }

    /// <summary>
    /// Hoc nonce moi (moi phan hoi deu co the mang <c>DPoP-Nonce</c>) va quyet dinh co thu lai:
    /// <c>use_dpop_nonce</c> kem nonce MOI, hoac <c>invalid_dpop_proof</c> ma header <c>Date</c> cho thay lech gio.
    /// </summary>
    private bool ShouldRetryDpop(HttpResponseMessage res, JsonElement? json)
    {
        var oldNonce = _dpopNonce;
        if (res.Headers.TryGetValues(DpopProof.NonceHeader, out var n) && n.FirstOrDefault() is { Length: > 0 and <= 256 } nonce)
            _dpopNonce = nonce;
        if (res.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)) return false;
        var error = Str(json, "error") ?? DpopError(res);
        if (error == "use_dpop_nonce") return _dpopNonce is not null && _dpopNonce != oldNonce;
        if (error == "invalid_dpop_proof" && res.Headers.Date is { } serverNow)
        {
            var skew = serverNow - Clock();
            if ((skew - _serverSkew).Duration() <= SkewTolerance) return false;   // khong phai do dong ho
            _serverSkew = skew;
            return true;
        }
        return false;
    }

    /// <summary>Ma loi trong <c>WWW-Authenticate: DPoP error="..."</c> (RFC 9449 muc 7.1).</summary>
    private static string? DpopError(HttpResponseMessage res)
    {
        foreach (var h in res.Headers.WwwAuthenticate)
        {
            if (!string.Equals(h.Scheme, "DPoP", StringComparison.OrdinalIgnoreCase) || h.Parameter is null) continue;
            var m = System.Text.RegularExpressions.Regex.Match(h.Parameter, "(?:^|,)\\s*error=\"([a-z_]{1,64})\"");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private async Task<TokenResult> TokenAsync(Dictionary<string, string> form, CancellationToken cancel)
    {
        using var body = new FormUrlEncodedContent(form);
        using var req = Request(HttpMethod.Post, endpoints.Token, body);
        using var res = await http.SendAsync(req, cancel).ConfigureAwait(false);
        var json = TryParse(await res.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
        if (!res.IsSuccessStatusCode)
            return new TokenResult(null, Str(json, "error") ?? "http_" + ((int)res.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), res.StatusCode);
        if (Str(json, "access_token") is not { Length: > 0 } access)
            return new TokenResult(null, "invalid_response", res.StatusCode);
        return new TokenResult(new TokenSet(access, Str(json, "refresh_token"), Str(json, "id_token")), null, res.StatusCode);
    }

    /// <summary>426 (Upgrade Required) la tin hieu khoa ke ca khi than JSON hong - may chu tu quyet (I1).</summary>
    internal static UpdateRequiredResponse? UpdateRequiredFrom(HttpStatusCode status, JsonElement? json)
    {
        if ((int)status != 426) return null;
        Uri? url = Str(json, "url") is { } u && Uri.TryCreate(u, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps
            ? parsed : null;
        return new UpdateRequiredResponse(Str(json, "latest"), url);
    }

    private static JsonElement? TryParse(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? Str(JsonElement? e, string name)
        => e is { } o && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
