import os

terrain_path = r"c:\_dev\knightonline-mobil\Client\Assets\Polyart\PolyartStudio\DreamscapeCastle\Scenes\Terrain Data\CastleTerrain_Demo.asset"

if os.path.exists(terrain_path):
    with open(terrain_path, 'rb') as f:
        content = f.read()
    print(f"CastleTerrain_Demo.asset size: {len(content)} bytes")
    clover_pos = content.lower().find(b"clover")
    print(f"Clover position: {clover_pos}")
    if clover_pos != -1:
        start = max(0, clover_pos - 100)
        end = min(len(content), clover_pos + 200)
        print(f"Context:\n{content[start:end].decode('utf-8', errors='ignore')}")
else:
    print("CastleTerrain_Demo.asset does not exist")
