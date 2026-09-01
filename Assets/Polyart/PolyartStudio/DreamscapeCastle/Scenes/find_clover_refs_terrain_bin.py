import os

terrain_path = r"c:\_dev\knightonline-mobil\Client\Assets\ALP_Assets\Realistic Terrain Textures Lite\Scenes\TerrainDATA\Terrain.asset"

if os.path.exists(terrain_path):
    with open(terrain_path, 'rb') as f:
        content = f.read()
    print(f"Terrain.asset size: {len(content)} bytes")
    clover_pos = content.lower().find(b"clover")
    print(f"Clover position: {clover_pos}")
    if clover_pos != -1:
        start = max(0, clover_pos - 100)
        end = min(len(content), clover_pos + 200)
        print(f"Context:\n{content[start:end].decode('utf-8', errors='ignore')}")
else:
    print("Terrain.asset does not exist")
