import os

terrain_path = r"c:\_dev\knightonline-mobil\Client\Assets\Resources\TerrainAssets\Zone_21.asset"

fbx_guid = "abc00000000004267099929304337917"
prefab_guid = "abc00000000003265076345203326719"
name_to_find = "SM_Ground_Clover_B"

with open(terrain_path, 'rb') as f:
    content = f.read()

print(f"File size: {len(content)} bytes")

fbx_pos = content.find(fbx_guid.encode('utf-8'))
prefab_pos = content.find(prefab_guid.encode('utf-8'))
name_pos = content.find(name_to_find.encode('utf-8'))

print(f"FBX Guid position: {fbx_pos}")
print(f"Prefab Guid position: {prefab_pos}")
print(f"Name position: {name_pos}")

# Also search for 'clover' case-insensitively
clover_pos = content.lower().find(b"clover")
print(f"Any 'clover' position: {clover_pos}")
if clover_pos != -1:
    start = max(0, clover_pos - 50)
    end = min(len(content), clover_pos + 50)
    print(f"Surrounding bytes: {content[start:end]}")
