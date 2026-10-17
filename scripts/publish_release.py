import os
import sys
import subprocess
import urllib.request
import json

def get_token():
    p = subprocess.Popen(['git', 'credential', 'fill'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    out, _ = p.communicate('protocol=https\nhost=github.com\n\n')
    creds = dict(line.split('=', 1) for line in out.splitlines() if '=' in line)
    token = creds.get('password')
    if not token:
        raise RuntimeError("Could not retrieve GitHub token from git credential helper.")
    return token

def main():
    token = get_token()
    headers = {
        'Authorization': f'Bearer {token}',
        'User-Agent': 'iClock-Release-Publisher',
        'Accept': 'application/vnd.github.v3+json'
    }

    notes_file = os.path.join(os.path.dirname(__file__), '..', 'release-notes', 'v1.07.md')
    with open(notes_file, 'r', encoding='utf-8') as f:
        body = f.read()

    payload = {
        'tag_name': 'v1.07',
        'target_commitish': 'main',
        'name': 'iClock 1.07 for Windows',
        'body': body,
        'draft': False,
        'prerelease': False
    }

    print("Checking if release v1.07 already exists...")
    check_req = urllib.request.Request(
        'https://api.github.com/repos/francodeyvison183-collab/iClock/releases/tags/v1.07',
        headers=headers
    )
    rel = None
    try:
        with urllib.request.urlopen(check_req) as resp:
            rel = json.loads(resp.read().decode())
            print(f"Found existing release v1.07 (id: {rel['id']})")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print("Release v1.07 does not exist yet. Creating...")
            create_req = urllib.request.Request(
                'https://api.github.com/repos/francodeyvison183-collab/iClock/releases',
                data=json.dumps(payload).encode('utf-8'),
                headers=dict(headers, **{'Content-Type': 'application/json'})
            )
            with urllib.request.urlopen(create_req) as resp:
                rel = json.loads(resp.read().decode())
                print(f"Created release v1.07! (id: {rel['id']}, url: {rel.get('html_url')})")
        else:
            raise

    upload_url = rel['upload_url'].split('{')[0]
    existing_assets = {a['name']: a['id'] for a in rel.get('assets', [])}

    assets_dir = os.path.join(os.path.dirname(__file__), '..', 'release-assets')
    assets = [
        ('iClock-1.07-windows.zip', 'application/zip'),
        ('iClock-1.07-windows.exe', 'application/vnd.microsoft.portable-executable'),
        ('iClock-1.07-SHA256SUMS.txt', 'text/plain')
    ]

    for file_name, content_type in assets:
        file_path = os.path.join(assets_dir, file_name)
        if not os.path.exists(file_path):
            print(f"Error: Asset file not found: {file_path}")
            sys.exit(1)

        if file_name in existing_assets:
            print(f"Asset {file_name} already exists (id: {existing_assets[file_name]}). Deleting old asset...")
            del_req = urllib.request.Request(
                f"https://api.github.com/repos/francodeyvison183-collab/iClock/releases/assets/{existing_assets[file_name]}",
                headers=headers,
                method='DELETE'
            )
            with urllib.request.urlopen(del_req) as resp:
                print(f"Deleted old asset {file_name}")

        file_size = os.path.getsize(file_path)
        print(f"Uploading {file_name} ({file_size} bytes)...")
        with open(file_path, 'rb') as f:
            data = f.read()

        up_req = urllib.request.Request(
            f"{upload_url}?name={file_name}",
            data=data,
            headers=dict(headers, **{'Content-Type': content_type})
        )
        with urllib.request.urlopen(up_req) as resp:
            asset_info = json.loads(resp.read().decode())
            print(f"Successfully uploaded {file_name} -> {asset_info.get('browser_download_url')}")

    print("\nAll release assets uploaded successfully for v1.07!")
    print(f"Release URL: {rel.get('html_url')}")

if __name__ == '__main__':
    main()
