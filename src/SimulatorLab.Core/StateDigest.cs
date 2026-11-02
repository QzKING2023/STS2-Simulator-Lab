using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core;

public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

public static class StateDigest
{
    public static string Compute<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value, ProtocolJson.Options));
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(document.RootElement, writer);
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.Number: value.WriteTo(writer); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidOperationException($"Unsupported JSON value kind {value.ValueKind}.");
        }
    }
}

public static class CombatTraceValidator
{
    public static List<string> Validate(CombatRootV1 root, CombatTraceV1 trace)
    {
        List<string> errors = [];
        if (trace.Schema != "combat-trace/v1") errors.Add("Unsupported trace schema.");
        if (!StringComparer.Ordinal.Equals(StateDigest.Compute(trace.Header), StateDigest.Compute(root.Header))) errors.Add("Trace header differs from the captured root header.");
        var rootDigest = StateDigest.Compute(root);
        if (!StringComparer.Ordinal.Equals(trace.RootStateDigest, rootDigest)) errors.Add("Trace root digest does not match the captured root.");
        string expectedBefore = rootDigest;
        for (int i = 0; i < trace.Steps.Count; i++)
        {
            var step = trace.Steps[i];
            if (!StringComparer.Ordinal.Equals(step.BeforeStateDigest, expectedBefore)) errors.Add($"Step {i} does not continue from the preceding state.");
            if (step.Action.Kind == CombatActionKind.PlayCard && string.IsNullOrWhiteSpace(step.Action.CardInstanceId)) errors.Add($"Step {i} plays no card instance.");
            if (step.Action.Kind == CombatActionKind.UsePotion && string.IsNullOrWhiteSpace(step.Action.PotionInstanceId)) errors.Add($"Step {i} uses no potion instance.");
            if (step.Gaps == null) errors.Add($"Step {i} has no gap report.");
            expectedBefore = step.AfterStateDigest;
        }
        return errors;
    }
}
