"""Build a separately locked MIT Lab derivative; never mutate the original pin.

Reuse the upstream simulator's existing IsOverOrEnding gate at the two fast
turn-start hook entries that bypass its guarded common hook enumeration.
The generated project has no deployment or external helper build targets.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess
from xml.sax.saxutils import escape

LAB = Path(__file__).resolve().parents[2]
DERIVATIVE = "terminal-hooks.1"


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest().upper()


def read(p):
    return json.loads(p.read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--game-data", type=Path, required=True)
    parser.add_argument("--english-resource", type=Path, required=True)
    args = parser.parse_args()
    base, out = args.base.resolve(), args.output.resolve()
    if not out.is_relative_to(LAB / ".build") or out.exists():
        raise RuntimeError("Fresh Lab .build output required")
    lock = read(base / "solver-lock.json")
    if lock["version"] != "0.50.1" or len(lock["source"]) != 826:
        raise RuntimeError("Unexpected reviewed base")
    for row in lock["source"]:
        if sha(base / "source" / row["path"]) != row["sha256"]:
            raise RuntimeError("Base source changed")
    for row in lock["assemblies"]:
        if sha(base / row["file"]) != row["sha256"]:
            raise RuntimeError("Base DLL changed")
    if sha(args.game_data / "sts2.dll") != "0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9":
        raise RuntimeError("Game input changed")
    out.mkdir(parents=True)
    for row in lock["source"]:
        dest = out / "source" / row["path"]
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(base / "source" / row["path"], dest)
    for name in ["LICENSE", "CombatSolver.json"]:
        shutil.copy2(base / name, out / name)
    patches = [
        ("src/Search/SimulatedCombatState.cs",
         """        if (HasPendingChoice)
            return false;
        foreach (Creature owner in participants)
            TriggerBaseSideTurnStart(simulator, owner, decrementPlating);""",
         """        if (HasPendingChoice)
            return false;
        // Lab terminal-hooks.1: reuse the common HookMirrors ending gate.
        // Native regular side-start hooks are absent after a draw-time kill.
        // Do not stop the draw/card transaction or commit a terminal here.
        if (simulator.IsOverOrEnding)
            return true;
        foreach (Creature owner in participants)
            TriggerBaseSideTurnStart(simulator, owner, decrementPlating);"""),
        ("src/Engine/InCombat/Mirrors/HookMirrors.AfterPlayerTurnStart.cs",
         """        if (simulator.HasPendingChoice) return false;

        //""",
         """        if (simulator.HasPendingChoice) return false;
        // Lab terminal-hooks.1: the vanilla fast path must obey the same
        // regular-hook ending gate as IterateCombatHookListeners.
        if (simulator.IsOverOrEnding) return true;

        //"""),
    ]
    changes = []
    for name, before, after in patches:
        p = out / "source" / name
        text = p.read_text(encoding="utf-8-sig")
        if text.count(before) != 1:
            raise RuntimeError("Reviewed hook anchor changed: " + name)
        original = sha(p)
        p.write_text(text.replace(before, after), encoding="utf-8", newline="\n")
        changes.append(dict(path=name, before=original, after=sha(p)))
    build = out / "build"
    build.mkdir()
    shutil.copy2(args.english_resource, build / "English.json")
    refs = [("sts2", args.game_data / "sts2.dll"), ("GodotSharp", args.game_data / "GodotSharp.dll"),
            ("0Harmony", args.game_data / "0Harmony.dll")]
    refs += [(r["name"], base / r["file"]) for r in lock["assemblies"] if r["name"] != "CombatSolver"]
    references = "\n".join(f'<Reference Include="{name}" HintPath="{escape(str(p.resolve()))}" Private="false"/>' for name,p in refs)
    project = f'''<Project Sdk="Godot.NET.Sdk/4.5.1">
 <PropertyGroup><TargetFramework>net9.0</TargetFramework><LangVersion>13.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AllowUnsafeBlocks>true</AllowUnsafeBlocks><EnableDynamicLoading>true</EnableDynamicLoading><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AssemblyName>CombatSolver</AssemblyName><RootNamespace>CombatSolver</RootNamespace><Version>0.50.1</Version><NoWarn>$(NoWarn);CS0436</NoWarn><NuGetAudit>false</NuGetAudit></PropertyGroup>
 <ItemGroup><Compile Include="../source/src/**/*.cs"/><EmbeddedResource Include="English.json" LogicalName="CombatSolver.UI.English.json"/><InternalsVisibleTo Include="OfflineSearchHarness"/><PackageReference Include="Krafs.Publicizer" Version="2.3.0" PrivateAssets="all"/><Publicize Include="sts2" IncludeVirtualMembers="false" IncludeCompilerGeneratedMembers="false"/></ItemGroup>
 <ItemGroup>{references}</ItemGroup>
 <ItemGroup><AssemblyMetadata Include="LabDerivative" Value="{DERIVATIVE}"/></ItemGroup>
</Project>'''
    csproj = build / "SolverLab.csproj"
    csproj.write_text(project, encoding="utf-8")
    cache = Path(os.environ["USERPROFILE"]) / ".nuget/packages"
    args_build = ["dotnet", "build", str(csproj), "-c", "Release", "--nologo", "--ignore-failed-sources",
                  "-p:NuGetAudit=false", "-p:RestoreSources=" + str(cache)]
    with (out/"build.log").open("w", encoding="utf-8") as log:
        process = subprocess.run(args_build, stdout=log, stderr=subprocess.STDOUT, timeout=180)
    if process.returncode:
        raise RuntimeError("Lab derivative build failed; see immutable build.log")
    for row in lock["assemblies"]:
        source = build / ".godot/mono/temp/bin/Release/CombatSolver.dll" if row["name"] == "CombatSolver" else base / row["file"]
        shutil.copy2(source, out / row["file"])
    source_rows = [dict(path=r["path"], sha256=sha(out/"source"/r["path"])) for r in lock["source"]]
    patch_lock = dict(schema="solver-lab-derivative/v1", id=DERIVATIVE, baseLockSha256=sha(base/"solver-lock.json"),
                      baseProducer=lock["producerBuild"], changedSources=changes,
                      projectSha256=sha(csproj), resource=dict(source=str(args.english_resource.resolve()),sha256=sha(build/"English.json")),
                      buildLogSha256=sha(out/"build.log"), toolSha256=sha(Path(__file__)),
                      scope="regular turn-start Hook ending gates only; unchanged score/config/audit; no deployment targets")
    (out/"derivative-lock.json").write_text(json.dumps(patch_lock,indent=2),encoding="utf-8")
    lock.update(producerBuild=DERIVATIVE, producerManifestHash=sha(out/"derivative-lock.json"),
                source=source_rows, assemblies=[dict(name=r["name"],file=r["file"],sha256=sha(out/r["file"])) for r in lock["assemblies"]],
                derivative=patch_lock, scope="Separately built MIT Lab derivative of fixed 0.50.1; regular turn-start ending gates, no Mod registrations")
    (out/"solver-lock.json").write_text(json.dumps(lock,indent=2),encoding="utf-8")
    print(json.dumps(dict(output=str(out),derivative=DERIVATIVE,changedSources=len(changes),sourceFiles=len(source_rows),solverSha256=sha(out/"CombatSolver.dll"))))


if __name__ == "__main__":
    main()
