using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core;

public sealed record CombatCaseFileV1
{
    public string Schema { get; init; } = "combat-case-file/v1";
    public CombatRootV1 Root { get; init; } = new();
    public CombatTraceV1? Trace { get; init; }
}

public sealed record CombatCaseImportResultV1(
    bool IsValid,
    CombatCaseFileV1? CaseFile,
    List<string> Errors);

public static class CombatCaseImporter
{
    private static readonly JsonSerializerOptions StrictOptions = CreateStrictOptions();

    public static CombatCaseImportResultV1 Read(string json)
    {
        CombatCaseFileV1? file;
        try
        {
            file = JsonSerializer.Deserialize<CombatCaseFileV1>(json, StrictOptions);
        }
        catch (JsonException error)
        {
            return new(false, null, [$"Invalid combat-case JSON: {error.Message}"]);
        }

        if (file is null)
            return new(false, null, ["Combat-case document is null."]);

        List<string> errors = [];
        if (file.Schema != "combat-case-file/v1")
            errors.Add($"Unsupported combat-case file schema {file.Schema}.");

        var root = CombatCaptureValidator.ValidateExactReady(file.Root);
        errors.AddRange(root.Errors.Select(x => $"Root: {x}"));
        if (file.Trace is { } trace)
            errors.AddRange(CombatTraceValidator.Validate(file.Root, trace).Select(x => $"Trace: {x}"));

        return new(errors.Count == 0, file, errors);
    }

    public static string Write(CombatCaseFileV1 file)
        => JsonSerializer.Serialize(file, StrictOptions);

    private static JsonSerializerOptions CreateStrictOptions()
    {
        var options = new JsonSerializerOptions(ProtocolJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        return options;
    }
}
