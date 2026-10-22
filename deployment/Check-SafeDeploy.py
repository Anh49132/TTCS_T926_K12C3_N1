"""Reject local data or actual configured secrets in the deployment commit."""
from pathlib import Path
import json
import subprocess
import re

root = Path(__file__).resolve().parent.parent
env = json.loads((root / 'data/postgres-port/render-env.json').read_text(encoding='utf-8-sig'))
secrets = [x['value'] for x in env if any(word in x['key'].lower() for word in ('apikey', 'secretkey', 'encryptionkey', 'connectionstrings')) and len(x['value']) > 20]
names = subprocess.check_output(['git', 'diff', '--cached', '--name-only', '-z'], cwd=root).decode().split('\0')
checked = 0
for name in filter(None, names):
    if re.search(r'^data/|(^|/)(App_Data|uploads|bin|obj)/|(^|/)\.env|\.(sqlite3?|db|bak|lic)([.-]|$)|(^|/)access\.json$', name, re.I):
        raise SystemExit('Deployment commit contains a forbidden data path: ' + name)
    content = subprocess.check_output(['git', 'show', ':' + name], cwd=root)
    if any(secret.encode(encoding) in content for secret in secrets for encoding in ('utf-8', 'utf-16-le')):
        raise SystemExit('Deployment commit contains a configured secret: ' + name)
    checked += 1
print(json.dumps({'staged_files_checked': checked, 'configured_secrets_found': 0, 'private_data_paths_found': 0}))
