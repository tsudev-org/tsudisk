namespace TsuDisk.Core.Integrity;

/// <summary>
/// Nguyen van modal khoa - chep DUNG tu <c>.standards/docs/APP_INTEGRITY.md</c> muc 2.2 (test doi chieu tung chu).
/// <c>{0}</c> = ban moi, <c>{1}</c> = ban dang chay.
/// </summary>
public sealed record UpdateRequiredText(string Title, string BodyFormat, string Primary, string Secondary)
{
    public static readonly UpdateRequiredText Vi = new(
        "Cần cập nhật ứng dụng",
        "Đã có phiên bản {0}. Để bảo vệ an toàn cho bạn và hệ thống, phiên bản {1} không còn được sử dụng. Vui lòng cập nhật lên phiên bản mới nhất.",
        "Cập nhật ngay",
        "Xuất dữ liệu");

    public static readonly UpdateRequiredText En = new(
        "Update required",
        "Version {0} is available. To keep you and the system safe, version {1} can no longer be used. Please update to the latest version.",
        "Update now",
        "Export data");

    public static UpdateRequiredText For(string language) => language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? En : Vi;

    /// <summary>426 khong noi ban nao ⇒ dung chu "mới nhất"/"latest" cho <c>{mới}</c>.</summary>
    public string Body(AppVersion? latest, AppVersion? current)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, BodyFormat,
            latest?.Text ?? (this == En ? "latest" : "mới nhất"), current?.Text ?? "?");
}
