import os

fbx_guid = "abc00000000004267099929304337917"
prefab_guid = "abc00000000003265076345203326719"

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"

matches = []

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.endswith('.asset') or file.endswith('.unity'):
            path = os.path.join(root, file)
            try:
                with open(path, 'r', encoding='utf-8', errors='ignore') as f:
                    content = f.read()
                    if fbx_guid in content:
                        matches.append((path, "FBX"))
                    if prefab_guid in content:
                        matches.append((path, "Prefab"))
            except Exception as e:
                pass

for path, match_type in matches:
    print(f"Found {match_type} reference in: {path}")
