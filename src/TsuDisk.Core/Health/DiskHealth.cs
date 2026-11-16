namespace TsuDisk.Core.Health;

/// <summary>
/// Suc khoe o vat ly: HealthStatus/OperationalStatus cua MSFT_PhysicalDisk va bo dem MSFT_StorageReliabilityCounter
/// (S.M.A.R.T. da chuan hoa). Truong null = thiet bi/driver khong cung cap.
/// </summary>
public sealed record DiskHealth(
    int DiskNumber,
    string Model,
    string MediaType,
    string HealthStatus,
    IReadOnlyList<string> OperationalStatus,
    int? TemperatureCelsius,
    int? TemperatureMaxCelsius,
    int? WearPercent,
    long? ReadErrorsUncorrected,
    long? WriteErrorsUncorrected,
    long? PowerOnHours);

public enum HealthLevel
{
    Good,
    Warning,
    Bad,
    Unknown,
}

public sealed record HealthVerdict(HealthLevel Level, IReadOnlyList<string> Reasons)
{
    /// <summary>Chi cho phep ghi bang phan vung khi o khong bi danh gia la hong.</summary>
    public bool AllowsWrites => Level != HealthLevel.Bad;
}

/// <summary>Quy tac danh gia - nguong theo khuyen nghi chung cua nha san xuat SSD/HDD va tai lieu Storage Spaces.</summary>
public static class HealthEvaluator
{
    public const int WearWarnPercent = 80;
    public const int WearBadPercent = 100;
    public const int TemperatureWarnCelsius = 65;

    public static HealthVerdict Evaluate(DiskHealth? health)
    {
        if (health is null)
            return new HealthVerdict(HealthLevel.Unknown, ["Không đọc được thông tin sức khỏe ổ (driver RAID/USB thường không cung cấp)."]);

        var bad = new List<string>();
        var warn = new List<string>();

        switch (health.HealthStatus)
        {
            case "Unhealthy":
                bad.Add("Windows đánh giá ổ KHÔNG khỏe mạnh (HealthStatus = Unhealthy).");
                break;
            case "Warning":
                warn.Add("Windows cảnh báo sức khỏe ổ (HealthStatus = Warning).");
                break;
        }
        foreach (var status in health.OperationalStatus)
        {
            if (status is "Predictive Failure" or "Lost Communication" or "Not Usable" or "Failed Media" or "Error" or "Non-Recoverable Error")
                bad.Add($"Trạng thái vận hành: {status}.");
            else if (status is not ("OK" or "Online"))
                warn.Add($"Trạng thái vận hành: {status}.");
        }
        if (health.WearPercent is >= WearBadPercent)
            bad.Add($"Độ mòn {health.WearPercent}% - ổ đã hết tuổi thọ ghi định mức.");
        else if (health.WearPercent is >= WearWarnPercent)
            warn.Add($"Độ mòn {health.WearPercent}% - nên chuẩn bị thay ổ.");
        if (health.ReadErrorsUncorrected is > 0 || health.WriteErrorsUncorrected is > 0)
            bad.Add($"Có lỗi đọc/ghi không sửa được (đọc {health.ReadErrorsUncorrected ?? 0}, ghi {health.WriteErrorsUncorrected ?? 0}).");
        var limit = health.TemperatureMaxCelsius is > 0 ? health.TemperatureMaxCelsius.Value : TemperatureWarnCelsius;
        if (health.TemperatureCelsius is { } t && t >= limit)
            warn.Add($"Nhiệt độ {t}°C, ngưỡng {limit}°C.");

        if (bad.Count > 0)
            return new HealthVerdict(HealthLevel.Bad, [.. bad, .. warn]);
        if (warn.Count > 0)
            return new HealthVerdict(HealthLevel.Warning, warn);
        return new HealthVerdict(HealthLevel.Good, ["Không phát hiện dấu hiệu bất thường."]);
    }
}
