"""Pin an additive MIT RandomForeseer source subset; no Mod initialization/download."""
from pathlib import Path
import argparse,hashlib,json,shutil

def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--base',required=True);parser.add_argument('--source',required=True);parser.add_argument('--output',required=True);args=parser.parse_args()
 lab=Path(__file__).resolve().parents[2];base=Path(args.base).resolve();source=Path(args.source).resolve();out=Path(args.output).resolve()
 if not out.is_relative_to(lab/'.build') or out.exists(): raise SystemExit('Output must be a fresh Lab .build directory')
 lock=json.loads((base/'input-lock.json').read_text(encoding='utf-8-sig'))
 if lock['schema']!='foreseer-source-reuse/v1' or lock['version']!='0.13.19':raise SystemExit('Unsupported source baseline')
 for r in lock['inputs']:
  p=(base/r['path']).resolve()
  if not p.is_relative_to(base) or digest(p)!=r['sha256']:raise SystemExit('Changed source baseline')
 for name in ['LICENSE','RandomForeseer.json']:
  if digest(source/name)!=digest(base/name):raise SystemExit('Source license/version manifest differs from pinned baseline')
 added=['RandomForeseerCode/OutOfCombat/Events/ReflectionsPrediction.cs','RandomForeseerCode/Common/PredictedCard.cs','RandomForeseerCode/Common/PredictionUtils.cs']
 if any(r['path'] in added for r in lock['inputs']):raise SystemExit('Baseline already contains added subset')
 for name in added:
  if not (source/name).is_file():raise SystemExit('Missing upstream source')
 out.mkdir(parents=True)
 for r in lock['inputs']:
  dest=out/r['path'];dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(base/r['path'],dest)
 for name in added:
  dest=out/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source/name,dest);lock['inputs'].append(dict(path=name,sha256=digest(dest)))
 lock['baseLockSha256']=digest(base/'input-lock.json');lock['scope']='Prior ten inputs unchanged; Reflections single-event scalar card upgrade/RNG projection. Original clone-on-write algorithm; no UI or Mod initialization. Shatter prediction unknown.'
 for r in lock['inputs']:
  if digest(out/r['path'])!=r['sha256']:raise SystemExit('Snapshot copy/hash mismatch')
 (out/'input-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
 print(json.dumps(dict(output=str(out),inputs=len(lock['inputs']),lockSha=digest(out/'input-lock.json'))))
if __name__=='__main__':main()
