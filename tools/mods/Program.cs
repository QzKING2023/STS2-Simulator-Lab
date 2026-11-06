using SimulatorLab.Core.Mods;
using System.Text.Json;
var root = args.Length > 0 ? args[0] : Path.GetFullPath("local-mods/workshop-snapshot-20261106-163352");
var manifest = args.Length > 1 ? args[1] : root + ".manifest.json";
var catalog = ModCatalogParser.Parse(root, manifest);
Console.WriteLine(JsonSerializer.Serialize(new { catalog.Schema, catalog.SnapshotId, catalog.TargetGameVersion, sourceCount = catalog.Sources.Count, modCount = catalog.Manifests.Count, catalog.InputHash, errors = catalog.Errors.Select(e => e.ToString()), manifests = catalog.Manifests.Select(m => new { m.ModId, m.Version, m.ManifestRelativePath, variants = m.CompatibilityVariants.Select(v => v.TargetGameVersion) }) }, new JsonSerializerOptions { WriteIndented = true }));
return catalog.Errors.Count == 0 ? 0 : 2;
