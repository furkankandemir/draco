import os

scene_path = r"c:\_dev\knightonline-mobil\Client\Assets\Polyart\PolyartStudio\DreamscapeCastle\Scenes\DemoExterior.unity"

with open(scene_path, 'rb') as f:
    content = f.read()

print(f"DemoExterior.unity size: {len(content)} bytes")

# Find all occurrences of clover
pos = 0
while True:
    pos = content.lower().find(b"clover", pos)
    if pos == -1:
        break
    print(f"\n--- Found match at position {pos} ---")
    start = max(0, pos - 200)
    end = min(len(content), pos + 300)
    print(content[start:end].decode('utf-8', errors='ignore'))
    pos += 6
