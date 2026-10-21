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

    version = sys.argv[1] if len(sys.argv) > 1 else '1.10'
    tag_name = f'v{version}'
    release_name = f'iClock {version} for Windows'

    notes_file = os.path.join(os.path.dirname(__file__), '..', 'release-notes', f'{tag_name}.md')
    with open(notes_file, 'r', encoding='utf-8') as f:
        body = f.read()

    payload = {
        'tag_name': tag_name,
        'target_commitish': 'main',
        'name': release_name,
        'body': body,
        'draft': False,
        'prerelease': False
    }

    print(f"Checking if release {tag_name} already exists...")
    check_req = urllib.request.Request(
        f'https://api.github.com/repos/francodeyvison183-collab/iClock/releases/tags/{tag_name}',
        headers=headers
    )
    rel = None
    try:
        with urllib.request.urlopen(check_req) as resp:
            rel = json.loads(resp.read().decode())
            print(f"Found existing release {tag_name} (id: {rel['id']})")
            update_req = urllib.request.Request(
                f"https://api.github.com/repos/francodeyvison183-collab/iClock/releases/{rel['id']}",
                data=json.dumps(payload).encode('utf-8'),
                headers=dict(headers, **{'Content-Type': 'application/json'}),
                method='PATCH'
            )
            with urllib.request.urlopen(update_req) as uresp:
                rel = json.loads(uresp.read().decode())
                print("Updated release notes body successfully.")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            print(f"Release {tag_name} does not exist yet. Creating...")
            create_req = urllib.request.Request(
                'https://api.github.com/repos/francodeyvison183-collab/iClock/releases',
                data=json.dumps(payload).encode('utf-8'),
                headers=dict(headers, **{'Content-Type': 'application/json'})
            )
            with urllib.request.urlopen(create_req) as resp:
                rel = json.loads(resp.read().decode())
                print(f"Created release {tag_name}! (id: {rel['id']}, url: {rel.get('html_url')})")
        else:
            raise

    import time

    # Refresh release info to get updated assets list
    refresh_req = urllib.request.Request(
        f"https://api.github.com/repos/francodeyvison183-collab/iClock/releases/{rel['id']}",
        headers=headers
    )
    with urllib.request.urlopen(refresh_req) as resp:
        rel = json.loads(resp.read().decode())
        existing_assets = {a['name']: a['id'] for a in rel.get('assets', [])}

    upload_url = rel['upload_url'].split('{')[0]

    assets_dir = os.path.join(os.path.dirname(__file__), '..', 'release-assets')
    assets = [
        (f'iClock-{version}-windows.zip', 'application/zip'),
        (f'iClock-{version}-windows.exe', 'application/vnd.microsoft.portable-executable'),
        (f'iClock-{version}-SHA256SUMS.txt', 'text/plain')
    ]

    for file_name, content_type in assets:
        file_path = os.path.join(assets_dir, file_name)
        if not os.path.exists(file_path):
            print(f"Error: Asset file not found: {file_path}")
            sys.exit(1)

        if file_name in existing_assets:
            print(f"Asset {file_name} already exists (id: {existing_assets[file_name]}). Deleting old asset...")
            for del_attempt in range(1, 4):
                try:
                    del_req = urllib.request.Request(
                        f"https://api.github.com/repos/francodeyvison183-collab/iClock/releases/assets/{existing_assets[file_name]}",
                        headers=headers,
                        method='DELETE'
                    )
                    with urllib.request.urlopen(del_req, timeout=30) as resp:
                        print(f"Deleted old asset {file_name}")
                        break
                except Exception as e:
                    print(f"Delete attempt {del_attempt} failed ({e}), retrying...")
                    time.sleep(2)

        file_size = os.path.getsize(file_path)
        with open(file_path, 'rb') as f:
            data = f.read()

        upload_success = False
        for attempt in range(1, 6):
            print(f"Uploading {file_name} ({file_size} bytes, attempt {attempt}/5)...")
            try:
                up_req = urllib.request.Request(
                    f"{upload_url}?name={file_name}",
                    data=data,
                    headers=dict(headers, **{
                        'Content-Type': content_type,
                        'Content-Length': str(file_size)
                    })
                )
                with urllib.request.urlopen(up_req, timeout=90) as resp:
                    asset_info = json.loads(resp.read().decode())
                    print(f"Successfully uploaded {file_name} -> {asset_info.get('browser_download_url')}")
                    upload_success = True
                    break
            except Exception as e:
                print(f"Upload failed: {e}. Retrying in {attempt * 3}s...")
                time.sleep(attempt * 3)

        if not upload_success:
            print(f"Failed to upload {file_name} after 5 attempts.")
            sys.exit(1)

    print(f"\nAll release assets uploaded successfully for {tag_name}!")
    print(f"Release URL: {rel.get('html_url')}")

if __name__ == '__main__':
    main()
