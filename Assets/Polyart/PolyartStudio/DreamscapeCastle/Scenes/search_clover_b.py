import os

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"
matches = []

fbx_guid = "abc00000000004267099929304337917"
prefab_guid = "abc00000000003265076345203326719"
search_str = "SM_Ground_Clover_B"

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.endswith('.meta'):
            continue
        path = os.path.join(root, file)
        try:
            with open(path, 'rb') as f:
                content = f.read()
                if (search_str.encode('utf-8') in content or 
                    fbx_guid.encode('utf-8') in content or 
                    prefab_guid.encode('utf-8') in content):
                    matches.append(path)
        except Exception:
            pass

print("Search results:")
for match in matches:
    print(match)
