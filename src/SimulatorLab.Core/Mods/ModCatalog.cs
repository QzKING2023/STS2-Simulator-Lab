using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimulatorLab.Core.Mods;

public static class ModCatalogConstants
{
    public const string TargetGameVersion = "0.111.0";
}

public enum ModCatalogErrorCode
{
    MissingFile, InvalidFormat, UnsupportedVersion, MissingField, InvalidValue, DuplicateId, PathEscape,
    DependencyMissing, RuntimeFailure
}

public sealed record ModCatalogError(ModCatalogErrorCode Code, string Path, string Message)
{
    public override string ToString() => $"{Code}:{Path}:{Message}";
}

public sealed record ModFileSummary(string RelativePath, long Length, string Sha256);
public sealed record ModDependency(string ModId, string? MinimumVersion, string? RawMinimumVersion);
public sealed record ModCompatibilityVariant(string TargetGameVersion, string RelativeDirectory, IReadOnlyList<ModFileSummary> Files);

public sealed record ModSource(
    string SnapshotId, string WorkshopId, string SnapshotRelativeRoot, DateTimeOffset CapturedUtc,
    string SnapshotContentSha256, string MetadataPath, IReadOnlyList<ModFileSummary> Files, string SourceDigest);

public sealed record ModManifest(
    string ModId, string DisplayName, string Version, string? MinimumGameVersion, string? RawMinimumGameVersion,
    bool AffectsGameplay, IReadOnlyList<ModDependency> Dependencies, ModSource Source, string ManifestRelativePath,
    bool HasDll, bool HasPck, IReadOnlyList<ModFileSummary> PackageFiles,
    IReadOnlyList<ModCompatibilityVariant> CompatibilityVariants, IReadOnlyList<string> NormalizationWarnings);

public sealed record ModCatalog(
    string Schema, string SnapshotId, string SnapshotContentSha256, string TargetGameVersion,
    IReadOnlyList<ModSource> Sources, IReadOnlyList<ModManifest> Manifests, string InputHash,
    IReadOnlyList<ModCatalogError> Errors)
{
    public string ModelSummary => JsonSerializer.Serialize(new { SnapshotId, TargetGameVersion, Sources = Sources.Select(x => new { x.WorkshopId, x.SourceDigest }), Manifests = Manifests.Select(x => new { x.ModId, x.Version, x.ManifestRelativePath, Dependencies = x.Dependencies.Select(d => new { d.ModId, d.MinimumVersion }), Variants = x.CompatibilityVariants.Select(v => new { v.TargetGameVersion, v.RelativeDirectory, Files = v.Files.Select(f => new { f.RelativePath, f.Length, f.Sha256 }) }) }), Errors = Errors.Select(e => new { e.Code, e.Path }) });
}

public static class ModCatalogParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] RequiredManifestFields = ["id", "name", "version"];

    public static ModCatalog Parse(string snapshotDirectory, string snapshotManifestPath, string targetGameVersion = ModCatalogConstants.TargetGameVersion)
    {
        var errors = new List<ModCatalogError>();
        if (!IsSafeVersion(targetGameVersion)) errors.Add(new(ModCatalogErrorCode.UnsupportedVersion, targetGameVersion, "target game version is invalid"));
        if (!Directory.Exists(snapshotDirectory)) return Empty(snapshotDirectory, errors, new(ModCatalogErrorCode.MissingFile, snapshotDirectory, "snapshot directory is missing"));
        if (!File.Exists(snapshotManifestPath)) return Empty(snapshotDirectory, errors, new(ModCatalogErrorCode.MissingFile, snapshotManifestPath, "snapshot manifest is missing"));

        SnapshotDto? snapshot;
        try { snapshot = JsonSerializer.Deserialize<SnapshotDto>(File.ReadAllText(snapshotManifestPath), JsonOptions); }
        catch (JsonException ex) { return Empty(snapshotDirectory, errors, new(ModCatalogErrorCode.InvalidFormat, snapshotManifestPath, ex.Message)); }
        if (snapshot is null || snapshot.Items is null) return Empty(snapshotDirectory, errors, new(ModCatalogErrorCode.InvalidFormat, snapshotManifestPath, "snapshot manifest has no items"));

        var sources = new List<ModSource>();
        var manifests = new List<ModManifest>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var snapshotId = snapshot.SnapshotDirectory ?? Path.GetFileName(snapshotDirectory);
        foreach (var item in snapshot.Items.OrderBy(x => x.WorkshopId ?? "", StringComparer.Ordinal).ThenBy(x => x.SnapshotPath ?? "", StringComparer.Ordinal))
        {
            var workshop = item.WorkshopId?.Trim() ?? "";
            var root = item.SnapshotPath?.Trim() ?? "";
            if (workshop.Length == 0 || root.Length == 0) { errors.Add(new(ModCatalogErrorCode.InvalidValue, workshop, "workshopId or snapshotPath is empty")); continue; }
            if (!SafeRelative(root)) { errors.Add(new(ModCatalogErrorCode.PathEscape, root, "snapshot path escapes root")); continue; }
            var itemDir = Path.Combine(snapshotDirectory, root.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(itemDir)) { errors.Add(new(ModCatalogErrorCode.MissingFile, root, "snapshot item directory is missing")); continue; }
            var fileRows = (item.Files ?? []).Select(f => new ModFileSummary(Normalize(f.Path), f.Length, (f.Sha256 ?? "").ToLowerInvariant())).OrderBy(f => f.RelativePath, StringComparer.Ordinal).ToList();
            foreach (var row in fileRows.Where(f => !SafeRelative(f.RelativePath))) errors.Add(new(ModCatalogErrorCode.PathEscape, row.RelativePath, "file path escapes item root"));
            fileRows = fileRows.Where(f => SafeRelative(f.RelativePath)).ToList();
            var sourceDigest = Digest(fileRows.Select(f => $"{f.RelativePath}\n{f.Length}\n{f.Sha256}"));
            var source = new ModSource(snapshotId, workshop, root, ParseDate(snapshot.CapturedUtc), snapshot.SnapshotContentSha256 ?? "", item.Metadata?.FirstOrDefault()?.Path ?? "", fileRows, sourceDigest);
            sources.Add(source);
            foreach (var manifestFile in fileRows.Where(f => f.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            {
                var full = Path.Combine(itemDir, manifestFile.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!IsWithin(itemDir, full)) { errors.Add(new(ModCatalogErrorCode.PathEscape, manifestFile.RelativePath, "manifest path escapes item root")); continue; }
                JsonElement rootJson;
                try { using var doc = JsonDocument.Parse(File.ReadAllText(full)); rootJson = doc.RootElement.Clone(); }
                catch (FileNotFoundException) { errors.Add(new(ModCatalogErrorCode.MissingFile, manifestFile.RelativePath, "manifest file is missing")); continue; }
                catch (JsonException ex) { errors.Add(new(ModCatalogErrorCode.InvalidFormat, manifestFile.RelativePath, ex.Message)); continue; }
                if (rootJson.ValueKind != JsonValueKind.Object) { errors.Add(new(ModCatalogErrorCode.InvalidFormat, manifestFile.RelativePath, "manifest root must be an object")); continue; }
                var parsed = ParseManifest(rootJson, manifestFile.RelativePath, source, fileRows, targetGameVersion, itemDir, errors);
                if (parsed is null) continue;
                if (!seenIds.Add(parsed.ModId)) { errors.Add(new(ModCatalogErrorCode.DuplicateId, parsed.ManifestRelativePath, $"duplicate mod id '{parsed.ModId}'")); continue; }
                manifests.Add(parsed);
            }
        }
        sources = sources.OrderBy(s => s.SnapshotRelativeRoot, StringComparer.Ordinal).ThenBy(s => s.WorkshopId, StringComparer.Ordinal).ToList();
        manifests = manifests.OrderBy(m => m.ManifestRelativePath, StringComparer.Ordinal).ThenBy(m => m.Source.WorkshopId, StringComparer.Ordinal).ThenBy(m => m.ModId, StringComparer.Ordinal).ToList();
        var inputHash = Digest(sources.SelectMany(s => s.Files.OrderBy(f => f.RelativePath, StringComparer.Ordinal).Select(f => $"{s.WorkshopId}/{f.RelativePath}\n{f.Length}\n{f.Sha256}")) .Concat(manifests.Select(m => $"manifest:{m.Source.WorkshopId}/{m.ManifestRelativePath}\n{m.ModId}\n{m.Version}")));
        return new ModCatalog("mod-catalog/v1", snapshotId, snapshot.SnapshotContentSha256 ?? "", targetGameVersion, sources, manifests, inputHash, errors.OrderBy(e => e.Code).ThenBy(e => e.Path, StringComparer.Ordinal).ToList());
    }

    private static ModManifest? ParseManifest(JsonElement json, string path, ModSource source, IReadOnlyList<ModFileSummary> files, string target, string itemDir, List<ModCatalogError> errors)
    {
        foreach (var field in RequiredManifestFields) if (!json.TryGetProperty(field, out var p) || p.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(p.GetString())) { errors.Add(new(ModCatalogErrorCode.MissingField, path, $"required field '{field}' is missing")); return null; }
        var id = json.GetProperty("id").GetString()!.Trim(); var name = json.GetProperty("name").GetString()!.Trim(); var versionRaw = json.GetProperty("version").GetString()!.Trim();
        if (!TryVersion(versionRaw, out var version)) { errors.Add(new(ModCatalogErrorCode.InvalidValue, path, "version is invalid")); return null; }
        string? rawGame = json.TryGetProperty("min_game_version", out var gp) && gp.ValueKind == JsonValueKind.String ? gp.GetString() : null;
        string? game = null; var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(rawGame)) { if (!TryVersion(rawGame!, out game)) { errors.Add(new(ModCatalogErrorCode.InvalidValue, path, "min_game_version is invalid")); return null; } }
        var deps = new List<ModDependency>();
        if (json.TryGetProperty("dependencies", out var dp) && dp.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            if (dp.ValueKind != JsonValueKind.Array) { errors.Add(new(ModCatalogErrorCode.InvalidFormat, path, "dependencies must be an array")); return null; }
            foreach (var d in dp.EnumerateArray()) { if (d.ValueKind != JsonValueKind.Object || !d.TryGetProperty("id", out var di) || di.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(di.GetString())) { errors.Add(new(ModCatalogErrorCode.MissingField, path, "dependency id is missing")); continue; } var raw = d.TryGetProperty("min_version", out var mi) && mi.ValueKind == JsonValueKind.String ? mi.GetString() : null; string? min = null; if (!string.IsNullOrWhiteSpace(raw) && !TryVersion(raw!, out min)) errors.Add(new(ModCatalogErrorCode.InvalidValue, path, $"dependency version '{raw}' is invalid")); deps.Add(new(di.GetString()!.Trim(), min, raw)); }
        }
        var package = files.Where(f => !f.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || string.Equals(f.RelativePath, path, StringComparison.OrdinalIgnoreCase)).ToList();
        var variants = ParseVariants(files, itemDir, path, target, errors);
        var hasDll = json.TryGetProperty("has_dll", out var hd) && hd.ValueKind == JsonValueKind.True || files.Any(f => f.RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        var hasPck = json.TryGetProperty("has_pck", out var hp) && hp.ValueKind == JsonValueKind.True || files.Any(f => f.RelativePath.EndsWith(".pck", StringComparison.OrdinalIgnoreCase));
        var affects = json.TryGetProperty("affects_gameplay", out var ag) && ag.ValueKind == JsonValueKind.True;
        return new ModManifest(id, name, version!, game, rawGame, affects, deps.OrderBy(d => d.ModId, StringComparer.Ordinal).ToList(), source, path, hasDll, hasPck, package, variants, warnings);
    }

    private static IReadOnlyList<ModCompatibilityVariant> ParseVariants(IReadOnlyList<ModFileSummary> files, string itemDir, string manifestPath, string target, List<ModCatalogError> errors)
    {
        var result = new List<ModCompatibilityVariant>();
        foreach (var f in files.Where(f => f.RelativePath.EndsWith("variants.manifest", StringComparison.OrdinalIgnoreCase)))
        {
            try { using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(itemDir, f.RelativePath.Replace('/', Path.DirectorySeparatorChar)))); if (doc.RootElement.TryGetProperty("variants", out var vs) && vs.ValueKind == JsonValueKind.Array) foreach (var v in vs.EnumerateArray()) { var t = v.TryGetProperty("compatTarget", out var tp) ? tp.GetString() : null; if (string.IsNullOrWhiteSpace(t)) continue; var candidate = files.Select(x => x.RelativePath.Replace('\\', '/')).FirstOrDefault(x => x.Contains("/" + t + "/", StringComparison.Ordinal)); var dir = candidate is null ? "" : candidate[..candidate.IndexOf("/" + t + "/", StringComparison.Ordinal)]; var relative = string.IsNullOrEmpty(dir) ? t! : dir + "/" + t; result.Add(new(t!, relative, files.Where(x => x.RelativePath.StartsWith(relative + "/", StringComparison.Ordinal)).ToList())); } }
            catch (JsonException ex) { errors.Add(new(ModCatalogErrorCode.InvalidFormat, f.RelativePath, ex.Message)); }
        }
        foreach (var dir in files.Select(f => string.Join('/', f.RelativePath.Split('/').Take(Math.Max(0, f.RelativePath.Split('/').Length - 1)))).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal)) { var name = dir.Split('/').Last(); if (IsSafeVersion(name) && files.Any(f => f.RelativePath.StartsWith(dir + "/", StringComparison.Ordinal)) && !result.Any(v => v.RelativeDirectory == dir)) result.Add(new(name, dir, files.Where(f => f.RelativePath.StartsWith(dir + "/", StringComparison.Ordinal)).ToList())); }
        return result.OrderBy(v => v.TargetGameVersion, StringComparer.Ordinal).ThenBy(v => v.RelativeDirectory, StringComparer.Ordinal).ToList();
    }

    private static ModCatalog Empty(string path, List<ModCatalogError> errors, ModCatalogError error) => new("mod-catalog/v1", Path.GetFileName(path), "", ModCatalogConstants.TargetGameVersion, [], [], "", errors.Append(error).ToList());
    private static DateTimeOffset ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : DateTimeOffset.UnixEpoch;
    private static bool TryVersion(string raw, out string? normalized) { normalized = null; var s = raw.Trim().TrimStart('v', 'V'); if (!IsSafeVersion(s)) return false; normalized = s; return true; }
    private static bool IsSafeVersion(string s) => System.Text.RegularExpressions.Regex.IsMatch(s ?? "", "^\\d+\\.\\d+\\.\\d+(?:[-+][0-9A-Za-z.-]+)?$");
    private static bool SafeRelative(string p) => !string.IsNullOrWhiteSpace(p) && !Path.IsPathRooted(p) && p.Replace('\\', '/').Split('/').All(x => x is not ".." and not "");
    private static string Normalize(string? p) => (p ?? "").Replace('\\', '/').TrimStart('/');
    private static bool IsWithin(string root, string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string Digest(IEnumerable<string> rows) { using var sha = SHA256.Create(); var bytes = Encoding.UTF8.GetBytes(string.Join("\n", rows.OrderBy(x => x, StringComparer.Ordinal))); return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant(); }

    private sealed class SnapshotDto { public string? Schema { get; set; } public string? CapturedUtc { get; set; } public string? SnapshotDirectory { get; set; } public string? SnapshotContentSha256 { get; set; } public List<ItemDto>? Items { get; set; } }
    private sealed class ItemDto { public string? WorkshopId { get; set; } public string? SnapshotPath { get; set; } public List<FileDto>? Files { get; set; } public List<MetadataDto>? Metadata { get; set; } }
    private sealed class FileDto { public string? Path { get; set; } public long Length { get; set; } public string? Sha256 { get; set; } }
    private sealed class MetadataDto { public string? Path { get; set; } }
}



