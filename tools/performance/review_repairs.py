"""Bounded production review of the repair pilot; never rewrite an output."""
import argparse
import copy
import json
import subprocess
from pilot import LAB, read, write_new


def call(dll, args):
    p = subprocess.run(['dotnet', str(dll), *map(str, args)], capture_output=True,
                       text=True, encoding='utf-8', errors='replace', timeout=120)
    return dict(exit=p.returncode, stdout=p.stdout, stderr=p.stderr)


def review(build, directory, output):
    dll = build / 'bin/Release/net9.0/OfflineSearchHarness.dll'
    full = (directory / 'full-run-progress.json').exists()
    entry = '--review-full-run' if full else '--review'
    result = call(dll, [entry, directory])
    result['entry'] = entry
    if full:
        process = read(directory / 'process.json')
        report = read(directory / 'result.json')
        steps = [read(p) for p in directory.glob('solver-online-step-*.json')]
        isolation = report['guards']['isolation']
        checks = dict(rawExit=process['rawExit'] == 0 and process['exitConsistent'] and not process['watchdogExpired'],
                      complete=report['result']['Stop'] == 'Completed' and report['result']['DataComplete'],
                      terminal=report['result']['Terminal'] in ['RunVictory', 'Death'],
                      confirmed=report['result']['Confirmed'] == report['result']['Dispatched'],
                      actualSteps=bool(steps) and all(s['exact'] and s['parentUnchanged'] and s['expected'] == s['actual'] for s in steps),
                      isolation=not report['guards']['faults'] and isolation['backend'] == 'MemorySaveStore'
                          and not isolation['shouldSave'] and not isolation['steamInitialized'] and not isolation['uiInstance'])
        result['checks'] = checks
        if not all(checks.values()):
            result['exit'] = 1
            result['stderr'] += '\nExplicit full-run integrity checks failed'
    result.update(buildId=read(build / 'build-manifest.json')['buildId'], directory=str(directory))
    write_new(output, result)
    if result['exit'] != 0:
        raise RuntimeError(result['stderr'])
    return result


def controls(build, directory, output):
    output.mkdir(exist_ok=False)
    rows = [json.loads(l)['payload'] for l in (directory / 'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    steps = [read(p) for p in directory.glob('solver-online-step-*.json')]
    step = sorted((s for s in steps if (s['action'].get('Choice') or {}).get('Effect') == 'Discard'), key=lambda s: s['query'])[0]
    root = step['root']
    value = dict(rows=rows, step=step, query=read(directory / f"solver-query-{step['query']}.json"),
                 dispatch=next(r for r in rows if r['kind'] == 'dispatch' and r['root'] == root),
                 confirmed=next(r for r in rows if r['kind'] == 'confirmed' and r['root'] == root))
    cases = [('valid', value, 0)]
    def choice(x): return x['step']['action']['Choice']
    def child(x): return x['step']['children'][0]
    for name, mutate in [
        ('stale-key', lambda x: choice(x)['Cards'][0].update(StateKey='stale')),
        ('wrong-duplicate', lambda x: choice(x)['Cards'][0].update(SourceOccurrence=1)),
        ('wrong-upgrade', lambda x: child(x)['candidates'][0].update(upgrade=99)),
        ('wrong-model', lambda x: child(x)['candidates'][0].update(model='CARD.STRIKE_IRONCLAD')),
        ('missing-child', lambda x: x['step'].update(children=[])),
        ('duplicate-child', lambda x: x['step']['children'].append(copy.deepcopy(child(x)))),
        ('wrong-owner', lambda x: child(x).update(root='a1')),
        ('missing-return', lambda x: x.update(rows=[r for r in x['rows'] if not (r['kind'] == 'choice-returned' and r['root'] == root)])),
        ('wrong-destination', lambda x: x['step']['nativeAfter']['combat'].update(discard=[])),
    ]:
        bad = copy.deepcopy(value)
        mutate(bad)
        cases.append((name, bad, 1))
    results = []
    dll = build / 'bin/Release/net9.0/OfflineSearchHarness.dll'
    for name, payload, expected in cases:
        path = output / (name + '.json')
        write_new(path, payload)
        r = call(dll, ['--check-hand-discard-evidence', path])
        results.append(dict(name=name, expected=expected, matched=r['exit'] == expected, **r))
    write_new(output / 'summary.json', dict(buildId=read(build / 'build-manifest.json')['buildId'], controls=results))
    if not all(r['matched'] for r in results):
        raise RuntimeError('Production discard control failed')
    print(json.dumps(dict(controls=len(results), passed=True)))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('mode', choices=['review', 'controls'])
    ap.add_argument('--build', type=lambda x: (LAB / x).resolve(), required=True)
    ap.add_argument('--directory', type=lambda x: (LAB / x).resolve(), required=True)
    ap.add_argument('--output', type=lambda x: (LAB / x).resolve(), required=True)
    a = ap.parse_args()
    if not a.output.is_relative_to(LAB / '.build/runs'):
        raise ValueError('Evidence output must stay in Lab')
    if a.mode == 'review':
        print(json.dumps(review(a.build, a.directory, a.output)))
    else:
        controls(a.build, a.directory, a.output)
