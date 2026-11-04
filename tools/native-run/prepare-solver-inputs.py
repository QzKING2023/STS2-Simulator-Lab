"""Snapshot an already certified Lab solver artifact; never build/deploy a game Mod."""
from pathlib import Path
import hashlib, json, shutil, sys

lab = Path(__file__).resolve().parents[2]
out = Path(sys.argv[1]).resolve()
if not out.is_relative_to(lab / '.build') or out.exists():
    raise ValueError('Fresh Lab .build directory required')
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
frozen = lab / '.build/runs/reward-repair-final-build-01/build-manifest.json'
producer = read(frozen)
if producer['buildId'] != 'D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D':
    raise ValueError('Unexpected solver producer')
cache = lab / '.build/local-reference/CombatSolver-v040-0bb36bc7e397'
dllrow = next(r for r in producer['manifest']['artifacts'] if r['path'].endswith('CombatSolver.dll'))
dll = lab / dllrow['path']
if sha(dll) != dllrow['sha256']: raise ValueError('Certified solver artifact changed')
source = []
for r in producer['manifest']['compiledSource']:
    if r['path'].startswith('src') or r['path'] in ['CombatSolver.csproj', 'lab-overlay-lock.json']:
        p = cache / r['path']
        if sha(p) != r['sha256']: raise ValueError('Solver source changed: ' + str(p))
        source.append((p, r))
manifest = read(cache / 'CombatSolver.json')
if (manifest['id'], manifest['version'], manifest['min_game_version'], manifest['affects_gameplay']) != ('CombatSolver', '0.50.1', '0.111.0', False):
    raise ValueError('Actual solver manifest mismatch')
out.mkdir(parents=True)
shutil.copy2(dll, out / dll.name)
assemblies = [dict(name='CombatSolver', file=dll.name, sha256=sha(dll))]
deps = lab / '.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix/dependencies/ritsu'
for p in sorted(deps.rglob('*.dll')):
    shutil.copy2(p, out / p.name)
    assemblies.append(dict(name=p.stem, file=p.name, sha256=sha(p)))
if len(assemblies) != 6: raise ValueError('Expected solver plus five Ritsu libraries')
for name in ['LICENSE', 'CombatSolver.json']:
    shutil.copy2(cache / name, out / name)
for p, r in source:
    dest = out / 'source' / r['path']; dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(p, dest)
lock = dict(schema='solver-compute-inputs/v1', producerBuild=producer['buildId'],
    producerManifestHash=sha(frozen), version=manifest['version'], gameVersion='0.111.0',
    licenseSha256=sha(out / 'LICENSE'), manifestSha256=sha(out / 'CombatSolver.json'),
    assemblies=assemblies, source=[r for _, r in source],
    scope='Existing MIT Lab derivative, generic overlays present but no Eleanor registrations or gameplay Mod inputs loaded')
(out / 'solver-lock.json').write_text(json.dumps(lock, indent=2), encoding='utf-8')
print(json.dumps(dict(snapshot=str(out), solverSha256=sha(out / dll.name), sourceFiles=len(source), assemblies=len(assemblies))))
