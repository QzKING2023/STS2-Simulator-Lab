param(
    [Parameter(Mandatory = $true)]
    [string]$TargetFile
)

$ErrorActionPreference = 'Stop'
$content = [IO.File]::ReadAllText($TargetFile, [Text.Encoding]::UTF8)
$marker = 'RegisterUnpatched<TCard>'
if ($content.Contains($marker, [StringComparison]::Ordinal)) {
    foreach ($required in @(
        'unpatched/v1',
        'SourceIdentity',
        'RequiresPatchComposition',
        'sourceAssembly.ManifestModule.ModuleVersionId')) {
        if (-not $content.Contains($required, [StringComparison]::Ordinal)) {
            throw "Existing no-patch overlay is incomplete: missing $required in $TargetFile."
        }
    }
    return
}

function Replace-ExactlyOnce([string]$Text, [string]$Old, [string]$New, [string]$Label) {
    $first = $Text.IndexOf($Old, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $Text.IndexOf($Old, $first + $Old.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "CombatSolver 0.50.1 overlay anchor '$Label' was missing or ambiguous."
    }
    return $Text.Substring(0, $first) + $New + $Text.Substring($first + $Old.Length)
}

$content = Replace-ExactlyOnce $content 'using System.Text;' "using System.Text;`r`nusing System.Text.Json;" 'JsonDocument import'
$content = Replace-ExactlyOnce $content @'
internal sealed record AdaptedOnPlayRegistrationDescriptor(string Schema, MethodInfo Target,
    string PatchSignature, MethodMirrorRegistryDescriptor Mirror);
'@ @'
internal sealed record AdaptedOnPlayRegistrationDescriptor(string Schema, MethodInfo Target,
    string PatchSignature, string SourceIdentity, bool RequiresPatchComposition,
    MethodMirrorRegistryDescriptor Mirror);
'@ 'registration descriptor'
$content = Replace-ExactlyOnce $content @'
    internal sealed record Registration(MethodInfo Target, string Schema, string Signature, string Implementation,
        MethodMirrorRegistry<CardModel, CardOnPlayMirrorContext> Mirror);
'@ @'
    internal sealed record Registration(MethodInfo Target, string Schema, string Signature, string Implementation,
        string SourceIdentity, bool RequiresPatchComposition,
        MethodMirrorRegistry<CardModel, CardOnPlayMirrorContext> Mirror);
'@ 'registration record'
$content = Replace-ExactlyOnce $content @'
                value.Schema, value.Target, value.Signature, value.Mirror.DescribeMirrorSupport())).ToArray();
'@ @'
                value.Schema, value.Target, value.Signature, value.SourceIdentity,
                value.RequiresPatchComposition, value.Mirror.DescribeMirrorSupport())).ToArray();
'@ 'registration descriptor projection'
$content = Replace-ExactlyOnce $content @'
            Registrations.Add(typeof(TCard), new(target, schema, signature, MethodIdentity(handler.Method), mirror));
'@ @'
            Registrations.Add(typeof(TCard), new(target, schema, signature, MethodIdentity(handler.Method),
                string.Empty, true, mirror));
'@ 'patched registration'
$content = Replace-ExactlyOnce $content @'
    internal static bool Seal()
'@ @'
    /// <summary>
    /// Registers an exact CardModel.OnPlay mirror for a gameplay Mod card with no Harmony patches.
    /// The Mod assembly must own the exact card type and method; the manifest identity is captured in the
    /// registration signature used by root stability checks. Any later patch makes the registration unsupported.
    /// </summary>
    public static void RegisterUnpatched<TCard>(string schema, MethodInfo target, Assembly sourceAssembly,
        string modManifestJson, Action<TCard, CardOnPlayMirrorContext> handler)
        where TCard : CardModel
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(modManifestJson);
        ArgumentNullException.ThrowIfNull(handler);
        if (typeof(TCard).IsAbstract || target != ResolveOnPlay(typeof(TCard)))
            throw new ArgumentException("The target must be the exact card type's OnPlay implementation.");
        string sourceIdentity = DescribeUnpatchedSource<TCard>(sourceAssembly, modManifestJson);
        RejectPatchedStateMachine(target);
        if (DescribeActual(target, Harmony.GetPatchInfo(target), includeIndex: false).Length != 0)
            throw new ArgumentException("RegisterUnpatched requires an OnPlay method with no Harmony patches.");

        var mirror = new MethodMirrorRegistry<CardModel, CardOnPlayMirrorContext>(OnPlay);
        mirror.Register(handler);
        lock (Gate)
        {
            if (_sealed) throw new InvalidOperationException("OnPlay adaptation registration is closed after first capture.");
            Registrations.Add(typeof(TCard), new(target, schema, "unpatched/v1/" + sourceIdentity,
                MethodIdentity(handler.Method), sourceIdentity, false, mirror));
        }
    }

    private static string DescribeUnpatchedSource<TCard>(Assembly sourceAssembly, string manifestJson)
        where TCard : CardModel
    {
        if (typeof(TCard).Assembly != sourceAssembly)
            throw new ArgumentException("The registered card type must come from the declared Mod assembly.", nameof(sourceAssembly));
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        JsonElement root = document.RootElement;
        string id = RequiredManifestString(root, "id");
        string name = RequiredManifestString(root, "name");
        string version = RequiredManifestString(root, "version");
        string assemblyName = sourceAssembly.GetName().Name
            ?? throw new ArgumentException("The source assembly has no simple name.", nameof(sourceAssembly));
        if (!string.Equals(id, assemblyName, StringComparison.Ordinal))
            throw new ArgumentException("The Mod manifest ID must match the source assembly name.", nameof(manifestJson));
        if (!root.TryGetProperty("affects_gameplay", out JsonElement affectsGameplay)
            || affectsGameplay.ValueKind != JsonValueKind.True)
            throw new ArgumentException("The no-patch card adapter requires a gameplay Mod manifest.", nameof(manifestJson));

        StringBuilder identity = new();
        Field(identity, "mod-manifest/unpatched/v1");
        Field(identity, id);
        Field(identity, name);
        Field(identity, version);
        Field(identity, sourceAssembly.FullName ?? assemblyName);
        Field(identity, sourceAssembly.ManifestModule.ModuleVersionId.ToString("N"));
        Field(identity, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString()))).ToLowerInvariant();
    }

    private static string RequiredManifestString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"The Mod manifest is missing a non-empty '{property}'.", "modManifestJson");
        return value.GetString()!;
    }

    internal static bool Seal()
'@ 'no-patch API'
$content = Replace-ExactlyOnce $content @'
        string actual = DescribeActual(target, patches, includeIndex: false);
        if (actual.Length == 0) return null; // Unpatched native behavior uses the ordinary mirror.
        if (registration.Target != target || registration.Signature != actual)
            throw new PredictionUnsupportedException($"Unreviewed OnPlay patch composition for {type.FullName}.");
        return registration;
'@ @'
        string actual = DescribeActual(target, patches, includeIndex: false);
        if (!registration.RequiresPatchComposition)
        {
            if (actual.Length != 0)
                throw new PredictionUnsupportedException($"OnPlay for {type.FullName} no longer matches its no-patch registration.");
            return registration;
        }
        if (actual.Length == 0) return null; // Unpatched native behavior uses the ordinary mirror.
        if (registration.Target != target || registration.Signature != actual)
            throw new PredictionUnsupportedException($"Unreviewed OnPlay patch composition for {type.FullName}.");
        return registration;
'@ 'registration selection'
$content = Replace-ExactlyOnce $content @'
            Field(text, pair.Value.Signature);
            Field(text, pair.Value.Implementation);
'@ @'
            Field(text, pair.Value.Signature);
            Field(text, pair.Value.SourceIdentity);
            Field(text, pair.Value.RequiresPatchComposition ? "patched" : "unpatched");
            Field(text, pair.Value.Implementation);
'@ 'root signature'

$temporary = "$TargetFile.overlay.tmp"
[IO.File]::WriteAllText($temporary, $content, [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporary -Destination $TargetFile -Force
