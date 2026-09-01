import os

fbx_guid = "abc00000000004267099929304337917"
prefab_guid = "abc00000000003265076345203326719"

terrain_path = r"c:\_dev\knightonline-mobil\Client\Assets\Resources\TerrainAssets\Zone_21.asset"

with open(terrain_path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

print(f"Zone_21.asset lines: {len(lines)}")

fbx_matches = []
prefab_matches = []
for idx, line in enumerate(lines):
    if fbx_guid in line:
        fbx_matches.append((idx + 1, line.strip()))
    if prefab_guid in line:
        prefab_matches.append((idx + 1, line.strip()))

print(f"FBX Guid found at lines: {fbx_matches}")
print(f"Prefab Guid found at lines: {prefab_matches}")
