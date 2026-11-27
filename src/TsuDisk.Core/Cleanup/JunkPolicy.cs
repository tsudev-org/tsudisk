namespace TsuDisk.Core.Cleanup;

/// <summary>Quyet dinh cua chinh sach cho mot muc (tep/thu muc). Chi <see cref="Junk"/> moi duoc coi la rac.</summary>
public enum JunkVerdict
{
    /// <summary>Rac tai tao duoc: nam trong vung da biet, du cu, khong thuoc danh sach bao ve.</summary>
    Junk,
    /// <summary>Thuoc danh sach bao ve (bi mat, du lieu nguoi dung, he thong, bang chung): KHONG BAO GIO de xuat xoa.</summary>
    Protected,
    /// <summary>Qua moi: co the dang duoc chuong trinh khac dung.</summary>
    TooRecent,
    /// <summary>Khong nam trong vung rac da biet: TsuDisk khong doan.</summary>
    Unknown,
}

/// <summary>Mot muc da do (do tang Windows/CLI dua vao; Core khong cham o dia).</summary>
public sealed record JunkCandidate(string Path, bool IsDirectory, long SizeBytes, DateTime LastWriteUtc);

public sealed record JunkAssessment(JunkCandidate Candidate, JunkVerdict Verdict, string Reason);

/// <summary>
/// ADR-0013: chinh sach "chi bao cao" cho viec don rac. Nguyen tac cung ho ADR-0002 - TU CHOI thay vi doan:
/// mot muc chi la rac khi (1) khong thuoc danh sach bao ve, (2) nam duoi mot goc rac da biet, (3) du cu.
/// Thu tu kiem QUAN TRONG: bao ve thang moi dieu kien khac (mot tep bi mat trong %TEMP% van la tep bi mat).
/// Khong co ham xoa o day - tuyet doi. Xoa (neu co sau nay) phai co ADR rieng + xac nhan nguoi dung.
/// </summary>
public static class JunkPolicy
{
    /// <summary>Tuoi toi thieu de coi la rac: duoi nguong nay co the dang duoc dung (build, cai dat, tai ve).</summary>
    public static readonly TimeSpan MinimumAge = TimeSpan.FromDays(3);

    /// <summary>Doan duong dan (khong phan biet hoa/thuong) bao ve: du lieu nguoi dung, he thong, kho ma, bang chung.</summary>
    private static readonly string[] ProtectedSegments =
    [
        // Du lieu nguoi dung - TsuDisk khong biet gia tri cua chung.
        "documents", "desktop", "pictures", "videos", "music", "downloads", "onedrive", "$recycle.bin",
        // He thong / cai dat - xoa la hong may hoac mat kha nang go cai dat.
        "windows", "installer", "program files", "program files (x86)", "system volume information", "winsxs",
        // Kho ma, khoa, bi mat.
        ".git", ".ssh", ".gnupg", ".secrets", ".supabase", ".aws", ".azure", ".kube", ".config", "keychains",
        // Ban sao dia ao WSL/VM: mat la mat ca he dieu hanh.
        "wsl", "docker", "virtualbox vms",
        // Bang chung phap y cua su co bao mat (phieu 20261126-DD1): KHONG xoa nhu "rac" khi chua co chu du an quyet.
        "infatica", "lrpjava", "precipitator",
    ];

    /// <summary>Hau to ten tep ma hoa/bi mat/dia ao: bao ve du nam o dau.</summary>
    private static readonly string[] ProtectedExtensions =
    [
        ".pem", ".key", ".pfx", ".p12", ".jks", ".keystore", ".kdbx", ".age", ".gpg", ".asc",
        ".vhd", ".vhdx", ".vmdk", ".vdi", ".iso", ".wallet",
    ];

    /// <summary>Mau ten tep chua bi mat (khop mot phan, khong phan biet hoa/thuong).</summary>
    private static readonly string[] ProtectedNameParts =
    [
        ".env", "secret", "token", "credential", "password", "passwd", "id_rsa", "id_ed25519", "id_ecdsa", ".npmrc", ".netrc",
        ".dev.vars", "private-key", "privatekey", "apikey", "api-key",
    ];

    /// <summary>
    /// Goc rac da biet. Khop ca hai dang dau gach. Chi tinh khi <c>Path</c> nam DUOI goc do (khong phai chinh goc).
    /// Cac goc nay do Windows/ung dung tu tai tao.
    /// </summary>
    private static readonly string[] KnownJunkRoots =
    [
        "/appdata/local/temp/",
        "/windows/temp/",
        "/windows/softwaredistribution/download/",
        "/appdata/local/crashdumps/",
        "/appdata/local/d3dscache/",
        "/appdata/local/npm-cache/",
        "/appdata/local/microsoft/windows/inetcache/",
        "/.cache/",
        "/.npm/_cacache/",
    ];

    public static JunkAssessment Assess(JunkCandidate item, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(item);
        var normalized = "/" + item.Path.Replace('\\', '/').Trim('/').ToLowerInvariant() + (item.IsDirectory ? "/" : "");
        var name = normalized.TrimEnd('/')[(normalized.TrimEnd('/').LastIndexOf('/') + 1)..];

        // 1. Bao ve thang. Luu y: "windows" nam trong ProtectedSegments se chan ca "/windows/temp/": ta cho phep dung
        //    cac goc rac Windows da liet ke bang cach bo qua doan "windows" khi duong dan nam dung duoi mot goc do.
        var underKnownRoot = KnownJunkRoots.Any(root => normalized.Contains(root, StringComparison.Ordinal)
                                                        && !normalized.EndsWith(root, StringComparison.Ordinal));
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "windows" && underKnownRoot)
                continue;
            if (ProtectedSegments.Contains(segment, StringComparer.Ordinal))
                return Protect($"nằm trong vùng được bảo vệ \"{segment}\" (dữ liệu người dùng, hệ thống, khóa hoặc bằng chứng)");
        }
        if (ProtectedExtensions.Any(e => name.EndsWith(e, StringComparison.Ordinal)))
            return Protect("tệp khóa/bí mật hoặc ổ đĩa ảo: không bao giờ đề xuất xóa");
        if (ProtectedNameParts.Any(p => name.Contains(p, StringComparison.Ordinal)))
            return Protect("tên giống tệp bí mật (token/khóa/mật khẩu): chỉ chủ dự án quyết định");

        // 2. Phai nam duoi goc rac da biet; neu khong, TsuDisk khong doan.
        if (!underKnownRoot)
            return new JunkAssessment(item, JunkVerdict.Unknown, "không thuộc vùng rác đã biết: TsuDisk không tự đoán");

        // 3. Du cu. Dong ho lech/tuong lai => coi la moi (tu choi).
        var age = nowUtc - item.LastWriteUtc;
        if (age < MinimumAge)
            return new JunkAssessment(item, JunkVerdict.TooRecent,
                $"mới sửa cách đây {Math.Max(0, (int)age.TotalHours)} giờ (< {MinimumAge.TotalDays:0} ngày): có thể đang được dùng");

        return new JunkAssessment(item, JunkVerdict.Junk, "rác tái tạo được: vùng tạm đã biết, đủ cũ, không thuộc danh sách bảo vệ");

        JunkAssessment Protect(string reason) => new(item, JunkVerdict.Protected, reason);
    }

    /// <summary>Tong dung luong theo phan loai; chi <see cref="JunkVerdict.Junk"/> duoc cong vao "co the thu hoi".</summary>
    public static JunkSummary Summarize(IEnumerable<JunkAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);
        var list = assessments.ToList();
        long Sum(JunkVerdict v) => list.Where(a => a.Verdict == v).Sum(a => a.Candidate.SizeBytes);
        return new JunkSummary(Sum(JunkVerdict.Junk), Sum(JunkVerdict.Protected), Sum(JunkVerdict.TooRecent), Sum(JunkVerdict.Unknown));
    }
}

public sealed record JunkSummary(long ReclaimableBytes, long ProtectedBytes, long TooRecentBytes, long UnknownBytes);
