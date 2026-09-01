import os

targets = []
# Collect all files matching Zone_*.asset, Terrain.asset, *.unity in specific folders
folders = [
    r"c:\_dev\knightonline-mobil\Client\Assets\Resources\TerrainAssets",
    r"c:\_dev\knightonline-mobil\Client\Assets\Scenes",
    r"c:\_dev\knightonline-mobil\Client\Assets\Polyart\PolyartStudio\DreamscapeCastle\Scenes",
    r"c:\_dev\knightonline-mobil\Client\Assets\TreePackVol.1"
]

for folder in folders:
    if os.path.exists(folder):
        for root, dirs, files in os.walk(folder):
            for file in files:
                if file.endswith('.asset') or file.endswith('.unity'):
                    targets.append(os.path.join(root, file))

# Add Realistic Terrain Textures Lite
rttl = r"c:\_dev\knightonline-mobil\Client\Assets\ALP_Assets\Realistic Terrain Textures Lite\Scenes\TerrainDATA\Terrain.asset"
if os.path.exists(rttl):
    targets.append(rttl)

matches = []
for target in targets:
    try:
        with open(target, 'rb') as f:
            content = f.read()
            if b"clover" in content.lower():
                matches.append(target)
    except Exception as e:
        pass

print("Search completed. Found matches in:")
for match in matches:
    print(match)
