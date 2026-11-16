using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TsuDisk.Core.Execution;
using TsuDisk.Core.Gpt;
using TsuDisk.Core.Health;
using TsuDisk.Core.Model;
using TsuDisk.Core.Planning;

namespace TsuDisk.Core.Serialization;

/// <summary>Moi cho doc/ghi JSON di qua day: ma sinh luc bien dich, an toan khi cat tia.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Plan))]
[JsonSerializable(typeof(RunJournal))]
[JsonSerializable(typeof(DiskLayout))]
[JsonSerializable(typeof(IReadOnlyList<DiskLayout>))]
[JsonSerializable(typeof(IReadOnlyList<FreeSpaceAdvice>))]
[JsonSerializable(typeof(Refusal))]
[JsonSerializable(typeof(WinReStatus))]
[JsonSerializable(typeof(IReadOnlyList<DiskHealth>))]
[JsonSerializable(typeof(GptReport))]
internal sealed partial class TsuDiskJsonContext : JsonSerializerContext;

public static class TsuDiskJson
{
    private static readonly TsuDiskJsonContext Context = new(new JsonSerializerOptions(TsuDiskJsonContext.Default.Options)
    {
        // Giu nguyen chu tieng Viet trong tep nhat ky de nguoi doc truc tiep duoc.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string Serialize(Plan value) => JsonSerializer.Serialize(value, Context.Plan);

    public static string Serialize(RunJournal value) => JsonSerializer.Serialize(value, Context.RunJournal);

    public static string Serialize(IReadOnlyList<DiskLayout> value) => JsonSerializer.Serialize(value, Context.IReadOnlyListDiskLayout);

    public static string Serialize(IReadOnlyList<FreeSpaceAdvice> value) => JsonSerializer.Serialize(value, Context.IReadOnlyListFreeSpaceAdvice);

    public static string Serialize(Refusal value) => JsonSerializer.Serialize(value, Context.Refusal);

    public static string Serialize(WinReStatus value) => JsonSerializer.Serialize(value, Context.WinReStatus);

    public static string Serialize(IReadOnlyList<DiskHealth> value) => JsonSerializer.Serialize(value, Context.IReadOnlyListDiskHealth);

    public static string Serialize(GptReport value) => JsonSerializer.Serialize(value, Context.GptReport);

    public static RunJournal? DeserializeJournal(string json) => JsonSerializer.Deserialize(json, Context.RunJournal);

    public static Plan? DeserializePlan(string json) => JsonSerializer.Deserialize(json, Context.Plan);
}
