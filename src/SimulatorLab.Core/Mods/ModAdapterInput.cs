using System.Text.Json;

namespace SimulatorLab.Core.Mods;

/// <summary>
/// Adapter-side, JSON-friendly input. It is intentionally separate from the
/// typed model records above; no instance of this type is accepted by the
/// simulator registry.
/// </summary>
public sealed record AdapterFileInput(string? Path = null, bool Present = true);

public sealed record AdapterDependencyInput(string? ModId = null, string? MinimumVersion = null);

public sealed record AdapterResourceInput(
    string? ResourceId = null,
    int? Amount = null,
    string? Reason = null);

public sealed record AdapterHookInput(
    string? Id = null,
    string? HookPoint = null,
    string? Description = null,
    string? EvidenceRef = null);

public sealed record AdapterObjectInput(
    string? Kind = null,
    string? Id = null,
    string? Name = null,
    string? Description = null,
    int? Cost = null,
    int? Duration = null,
    IReadOnlyList<string>? StartingCardIds = null,
    IReadOnlyList<string>? StartingRelicIds = null,
    IReadOnlyList<string>? PowerIds = null,
    IReadOnlyList<string>? IntentIds = null,
    IReadOnlyList<string>? EnemyIds = null,
    IReadOnlyList<string>? OptionIds = null,
    IReadOnlyList<string>? ContentIds = null,
    IReadOnlyList<AdapterHookInput>? Hooks = null,
    IReadOnlyList<AdapterResourceInput>? ResourceResults = null,
    bool IsGeneratedCard = false);

public sealed record ModAdapterDocument(
    string? ModId = null,
    string? DisplayName = null,
    string? Version = null,
    string? TargetGameVersion = null,
    string? SourcePath = null,
    IReadOnlyList<AdapterFileInput>? Files = null,
    IReadOnlyList<AdapterDependencyInput>? Dependencies = null,
    IReadOnlyList<string>? ResolvedDependencyIds = null,
    IReadOnlyList<AdapterObjectInput>? Objects = null,
    IReadOnlyList<AdapterResourceInput>? ResourceResults = null,
    int? MaxObjects = null);

public sealed record AdapterParseResult(
    ModAdapterDocument? Document,
    IReadOnlyList<AdapterErrorRecord> Errors)
{
    public bool IsSuccess => Document is not null && Errors.Count == 0;
}

public static class ModAdapterDocumentParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AdapterParseResult Parse(string json, string path = "<fixture>")
    {
        if (string.IsNullOrWhiteSpace(json))
            return new(null, [AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, null, path, "adapter document is empty")]);

        try
        {
            var document = JsonSerializer.Deserialize<ModAdapterDocument>(json, Options);
            return document is null
                ? new(null, [AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, null, path, "adapter document is null")])
                : new(document, []);
        }
        catch (JsonException ex)
        {
            return new(null, [AdapterErrorRecord.Create(AdapterErrorCode.InvalidFormat, null, path, ex.Message)]);
        }
    }
}
