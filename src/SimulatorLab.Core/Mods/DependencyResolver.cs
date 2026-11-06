using System.Globalization;

namespace SimulatorLab.Core.Mods;

public sealed record DependencyDiagnostic(
    ModCatalogErrorCode Code, string ModId, string? DependencyId, string Message);

public sealed record DependencyGraphNode(
    string ModId, string Version, string WorkshopId, bool Selected, ModManifest Manifest);

public sealed record DependencyGraphEdge(
    string SourceModId, string TargetModId, string? MinimumVersion, string DeclaredPath);

public sealed record SelectedModVariant(
    string ModId, string TargetGameVersion, string RelativeDirectory,
    IReadOnlyList<ModFileSummary> Files, string Reason);

public sealed record DependencyResolution(
    string Schema,
    string SnapshotId,
    string SnapshotContentSha256,
    string TargetGameVersion,
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyGraphEdge> Edges,
    IReadOnlyList<SelectedModVariant> SelectedVariants,
    IReadOnlyList<string> DependencyClosure,
    IReadOnlyList<string> TopologicalOrder,
    IReadOnlyList<IReadOnlyList<string>> Cycles,
    IReadOnlyList<DependencyDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0 && Cycles.Count == 0;
    public IReadOnlyList<string> LoadOrder => TopologicalOrder;
    public IReadOnlyList<DependencyDiagnostic> DependencyDiagnostics => Diagnostics;
}

public static class DependencyResolver
{
    public const string TargetGameVersion = ModCatalogConstants.TargetGameVersion;

    public static DependencyResolution Resolve(ModCatalog catalog, IEnumerable<string>? requestedModIds = null)
    {
        var diagnostics = new List<DependencyDiagnostic>();
        var manifests = catalog.Manifests.ToDictionary(m => m.ModId, StringComparer.Ordinal);
        var nodes = manifests.Values.OrderBy(m => m.ModId, StringComparer.Ordinal)
            .Select(m => new DependencyGraphNode(m.ModId, m.Version, m.Source.WorkshopId, false, m)).ToList();
        var selected = SelectRoots(manifests, requestedModIds, diagnostics);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<DependencyGraphEdge>();
        var variants = new List<SelectedModVariant>();

        if (!string.Equals(catalog.TargetGameVersion, TargetGameVersion, StringComparison.Ordinal))
            diagnostics.Add(new(ModCatalogErrorCode.UnsupportedVersion, "<catalog>", null,
                $"catalog target must be exactly {TargetGameVersion}"));
        if (catalog.Errors.Any(e => e.Code == ModCatalogErrorCode.DuplicateId))
            diagnostics.Add(new(ModCatalogErrorCode.DuplicateId, "<catalog>", null, "catalog contains duplicate mod ids"));
        foreach (var error in catalog.Errors.Where(e => e.Code is ModCatalogErrorCode.InvalidValue or ModCatalogErrorCode.UnsupportedVersion))
            diagnostics.Add(new(error.Code, error.Path, null, error.Message));

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in selected.OrderBy(x => x, StringComparer.Ordinal))
            Visit(root, manifests, closure, edges, variants, diagnostics, visiting, visited, new List<string>());

        nodes = nodes.Select(n => n with { Selected = closure.Contains(n.ModId) }).ToList();
        var cycles = FindCycles(closure, edges);
        foreach (var cycle in cycles)
            diagnostics.Add(new(ModCatalogErrorCode.RuntimeFailure, cycle[0], null,
                $"dependency cycle: {string.Join(" -> ", cycle)}"));

        var order = TopologicalOrder(closure, edges, cycles);
        diagnostics = diagnostics
            .OrderBy(d => d.Code).ThenBy(d => d.ModId, StringComparer.Ordinal)
            .ThenBy(d => d.DependencyId ?? "", StringComparer.Ordinal)
            .ThenBy(d => d.Message, StringComparer.Ordinal).ToList();
        return new DependencyResolution(
            "mod-dependency-resolution/v1", catalog.SnapshotId, catalog.SnapshotContentSha256,
            TargetGameVersion, nodes, edges.OrderBy(e => e.SourceModId, StringComparer.Ordinal)
                .ThenBy(e => e.TargetModId, StringComparer.Ordinal).ToList(),
            variants.OrderBy(v => v.ModId, StringComparer.Ordinal).ToList(),
            closure.OrderBy(x => x, StringComparer.Ordinal).ToList(), order, cycles, diagnostics);
    }

    private static HashSet<string> SelectRoots(
        IReadOnlyDictionary<string, ModManifest> manifests, IEnumerable<string>? requested, List<DependencyDiagnostic> diagnostics)
    {
        var roots = (requested ?? manifests.Keys).Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        foreach (var id in roots.Where(id => !manifests.ContainsKey(id)).OrderBy(x => x, StringComparer.Ordinal))
            diagnostics.Add(new(ModCatalogErrorCode.DependencyMissing, id, id, $"requested mod '{id}' is missing"));
        return roots.Where(manifests.ContainsKey).ToHashSet(StringComparer.Ordinal);
    }

    private static void Visit(string id, IReadOnlyDictionary<string, ModManifest> manifests, HashSet<string> closure,
        List<DependencyGraphEdge> edges, List<SelectedModVariant> variants, List<DependencyDiagnostic> diagnostics,
        HashSet<string> visiting, HashSet<string> visited, List<string> path)
    {
        if (visited.Contains(id)) return;
        if (visiting.Contains(id)) return;
        if (!manifests.TryGetValue(id, out var manifest))
        {
            diagnostics.Add(new(ModCatalogErrorCode.DependencyMissing, path.FirstOrDefault() ?? id, id,
                $"dependency '{id}' is missing"));
            return;
        }
        visiting.Add(id); path.Add(id); closure.Add(id);
        ValidateManifest(manifest, diagnostics);
        var variant = manifest.CompatibilityVariants.FirstOrDefault(v =>
            string.Equals(v.TargetGameVersion, TargetGameVersion, StringComparison.Ordinal));
        if (manifest.CompatibilityVariants.Count > 0 && variant is null)
            diagnostics.Add(new(ModCatalogErrorCode.UnsupportedVersion, id, null,
                $"no exact compatibility variant for {TargetGameVersion}"));
        else if (variant is not null)
            variants.Add(new(id, TargetGameVersion, variant.RelativeDirectory, variant.Files, "exact target match"));

        foreach (var dep in manifest.Dependencies.OrderBy(d => d.ModId, StringComparer.Ordinal))
        {
            edges.Add(new(id, dep.ModId, dep.MinimumVersion, manifest.ManifestRelativePath));
            if (!manifests.TryGetValue(dep.ModId, out var dependency))
            {
                diagnostics.Add(new(ModCatalogErrorCode.DependencyMissing, id, dep.ModId,
                    $"dependency '{dep.ModId}' is missing"));
                continue;
            }
            if (dep.MinimumVersion is not null && TryParseVersion(dep.MinimumVersion, out var minimum) && TryParseVersion(dependency.Version, out var actual) && CompareVersions(actual!, minimum!) < 0)
                diagnostics.Add(new(ModCatalogErrorCode.UnsupportedVersion, id, dep.ModId,
                    $"dependency '{dep.ModId}' version {dependency.Version} is below minimum {dep.MinimumVersion}"));
            Visit(dep.ModId, manifests, closure, edges, variants, diagnostics, visiting, visited, path);
        }
        path.RemoveAt(path.Count - 1); visiting.Remove(id); visited.Add(id);
    }

    private static void ValidateManifest(ModManifest manifest, List<DependencyDiagnostic> diagnostics)
    {
        if (!TryParseVersion(manifest.Version, out _))
            diagnostics.Add(new(ModCatalogErrorCode.InvalidValue, manifest.ModId, null, "mod version is invalid"));
        if (manifest.MinimumGameVersion is not null && (!TryParseVersion(manifest.MinimumGameVersion, out var min) || CompareVersions(TargetGameVersion, min!) < 0))
            diagnostics.Add(new(ModCatalogErrorCode.UnsupportedVersion, manifest.ModId, null,
                $"minimum game version {manifest.RawMinimumGameVersion ?? manifest.MinimumGameVersion} is not satisfied"));
        foreach (var dependency in manifest.Dependencies.Where(d => d.MinimumVersion is not null && !TryParseVersion(d.MinimumVersion, out _)))
            diagnostics.Add(new(ModCatalogErrorCode.InvalidValue, manifest.ModId, dependency.ModId, "dependency minimum version is invalid"));
    }

    private static List<IReadOnlyList<string>> FindCycles(HashSet<string> closure, IReadOnlyList<DependencyGraphEdge> edges)
    {
        var result = new List<IReadOnlyList<string>>(); var state = new Dictionary<string, int>(StringComparer.Ordinal); var stack = new List<string>();
        void Dfs(string id)
        {
            state[id] = 1; stack.Add(id);
            foreach (var next in edges.Where(e => e.SourceModId == id).Select(e => e.TargetModId).Where(closure.Contains).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!state.TryGetValue(next, out var s)) Dfs(next);
                else if (s == 1) { var start = stack.IndexOf(next); if (start >= 0) result.Add(stack.Skip(start).Append(next).ToList()); }
            }
            stack.RemoveAt(stack.Count - 1); state[id] = 2;
        }
        foreach (var id in closure.OrderBy(x => x, StringComparer.Ordinal)) if (!state.ContainsKey(id)) Dfs(id);
        return result.GroupBy(c => string.Join("\u001f", c), StringComparer.Ordinal).Select(g => g.First()).OrderBy(c => string.Join("\u001f", c), StringComparer.Ordinal).ToList();
    }

    private static List<string> TopologicalOrder(HashSet<string> closure, IReadOnlyList<DependencyGraphEdge> edges, IReadOnlyList<IReadOnlyList<string>> cycles)
    {
        var cyclic = cycles.SelectMany(x => x.Take(x.Count - 1)).ToHashSet(StringComparer.Ordinal);
        var indegree = closure.Where(x => !cyclic.Contains(x)).ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        foreach (var e in edges) if (indegree.ContainsKey(e.SourceModId) && indegree.ContainsKey(e.TargetModId)) indegree[e.SourceModId]++;
        var ready = new SortedSet<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key), StringComparer.Ordinal); var order = new List<string>();
        while (ready.Count > 0) { var id = ready.Min!; ready.Remove(id); order.Add(id); foreach (var e in edges.Where(e => e.TargetModId == id).OrderBy(e => e.SourceModId, StringComparer.Ordinal)) if (indegree.ContainsKey(e.SourceModId) && --indegree[e.SourceModId] == 0) ready.Add(e.SourceModId); }
        return order;
    }

    private static int CompareVersions(string left, string right)
    {
        var a = (TryParseVersion(left, out var ln) ? ln! : left).Split('-', 2); var b = (TryParseVersion(right, out var rn) ? rn! : right).Split('-', 2); var an = a[0].Split('.').Select(int.Parse).ToArray(); var bn = b[0].Split('.').Select(int.Parse).ToArray();
        for (var i = 0; i < 3; i++) { var c = an[i].CompareTo(bn[i]); if (c != 0) return c; }
        if (a.Length == 1 && b.Length > 1) return 1; if (a.Length > 1 && b.Length == 1) return -1; return string.Compare(a.ElementAtOrDefault(1), b.ElementAtOrDefault(1), StringComparison.Ordinal);
    }
    private static bool TryParseVersion(string? raw, out string? normalized) { normalized = null; if (string.IsNullOrWhiteSpace(raw)) return false; var s = raw.Trim().TrimStart('v', 'V'); if (!System.Text.RegularExpressions.Regex.IsMatch(s, "^\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.-]+)?$")) return false; normalized = s; return true; }
}
