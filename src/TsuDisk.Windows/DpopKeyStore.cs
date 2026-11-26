using System.Runtime.Versioning;
using System.Security.Cryptography;
using TsuDisk.Core.Pro;

namespace TsuDisk.Windows;

/// <summary>
/// Khoa DPoP P-256 trong CNG (ADR-0012 muc 13, APP_INTEGRITY 4 "desktop"): uu tien TPM qua Microsoft Platform Crypto
/// Provider, may khong co TPM 2.0 (hoac TPM tu choi) thi Microsoft Software Key Storage Provider. Khoa co ten, pham vi
/// nguoi dung hien tai, <c>ExportPolicy = None</c>: khoa rieng khong xuat duoc, ke ca boi chinh app.
///
/// VI SAO KHONG dung DPAPI + khoa trong tep nhu refresh token: khoa trong TPM khong chep sang may khac duoc - JWS gan
/// <c>cnf.jkt</c> moi thuc su "cua thiet bi nay". Khong gia vo "da chung thuc": may chu chi thay jkt, khong co bang chung
/// TPM (tsudisk khong gui attestation).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CngDpopKeyStore(string keyName = CngDpopKeyStore.DefaultKeyName) : IDpopKeyStore
{
    public const string DefaultKeyName = "tsudev.tsudisk.dpop.v1";

    private static readonly CngProvider Platform = new("Microsoft Platform Crypto Provider");

    /// <summary>Thu tu tim/tao: TPM truoc, phan mem sau.</summary>
    private static readonly CngProvider[] Providers = [Platform, CngProvider.MicrosoftSoftwareKeyStorageProvider];

    public IDpopKey? OpenOrCreate()
    {
        foreach (var p in Providers)
            if (TryOpen(p) is { } existing && Wrap(existing) is { } usable) return usable;
        foreach (var p in Providers)
        {
            try
            {
                var key = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, new CngKeyCreationParameters
                {
                    Provider = p,
                    ExportPolicy = CngExportPolicies.None,
                    KeyUsage = CngKeyUsages.Signing,
                    // Pham vi nguoi dung (khong MachineKey). Ghi de: chi toi day khi khong khoa nao dang co dung duoc.
                    KeyCreationOptions = CngKeyCreationOptions.OverwriteExistingKey,
                });
                if (Wrap(key) is { } created) return created;
            }
            catch (CryptographicException) { /* TPM khong co / bi khoa / het slot ⇒ thu KSP phan mem */ }
        }
        return null;   // ca hai deu hong: goi khong kem DPoP (PRODUCT_POLICY 7.4 la SHOULD)
    }

    public void Delete()
    {
        foreach (var p in Providers)
        {
            try { TryOpen(p)?.Delete(); }   // Delete() giai phong luon handle
            catch (CryptographicException) { }
        }
    }

    private CngKey? TryOpen(CngProvider p)
    {
        try { return CngKey.Exists(keyName, p) ? CngKey.Open(keyName, p) : null; }
        catch (CryptographicException) { return null; }
    }

    /// <summary>Ky thu mot lan: TPM co the mo duoc khoa ma khong ky duoc - luc do coi nhu khong co khoa o noi nay.</summary>
    private static EcdsaDpopKey? Wrap(CngKey key)
    {
        using (key)   // ECDsaCng nhan ban sao handle
        {
            EcdsaDpopKey? wrapped = null;
            try
            {
                wrapped = new EcdsaDpopKey(new ECDsaCng(key));
                wrapped.SignP1363([0]);
                return wrapped;
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                wrapped?.Dispose();
                return null;
            }
        }
    }
}
