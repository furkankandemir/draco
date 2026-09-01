import os

scene_path = r"c:\_dev\knightonline-mobil\Client\Assets\Scenes\LoginScene.unity"

with open(scene_path, 'rb') as f:
    content = f.read()

print(f"LoginScene size: {len(content)} bytes")

clover_pos = content.lower().find(b"clover")
print(f"Clover position in LoginScene: {clover_pos}")

if clover_pos != -1:
    start = max(0, clover_pos - 100)
    end = min(len(content), clover_pos + 200)
    print(f"Context:\n{content[start:end].decode('utf-8', errors='ignore')}")
