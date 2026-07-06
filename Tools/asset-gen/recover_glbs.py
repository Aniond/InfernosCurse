# One-shot recovery for the two Salone props whose Prism tasks FINISHED
# server-side just as the 3D AI Studio key was revoked (2026-07-06). Credits
# were spent at submit; the models exist on the account. With a FRESH key in
# .env this re-polls the task status endpoints (free) and downloads the GLBs.
# Safe to re-run; skips files that already exist.
import json, os, re, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
TASKS = {
    'dais-council-bench': '66e6693b-ac56-48fd-b2ef-612ff85d3e45',
    'notary-lectern': '6383e605-0ce8-4e04-bfa4-89ef62d12a13',
}

key = ''
with open(os.path.join(HERE, '.env')) as f:
    for line in f:
        m = re.match(r"\s*3D_AI_STUDIO_API_KEY\s*=\s*(.+)", line)
        if m:
            key = m.group(1).strip().strip('"\'')
assert key, '.env has no 3D_AI_STUDIO_API_KEY'

for name, task in TASKS.items():
    out = os.path.join(HERE, 'output', 'models', f'{name}.glb')
    if os.path.exists(out):
        print(f'{name}: already downloaded, skipping')
        continue
    req = urllib.request.Request(
        f'https://api.3daistudio.com/v1/generation-request/{task}/status/',
        headers={'Authorization': f'Bearer {key}'})
    with urllib.request.urlopen(req, timeout=60) as r:
        status = json.load(r)
    if status.get('status') != 'FINISHED':
        print(f'{name}: status={status.get("status")} — not downloadable')
        continue
    url = status['results'][0]['asset']
    urllib.request.urlretrieve(url, out)
    print(f'{name}: downloaded {os.path.getsize(out)} bytes')
print('done')
