import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

print("WorldBuilder.cs lines:", len(lines))

# Find Start, Awake, Initialize
for idx, line in enumerate(lines[:300]):
    if 'void Start' in line or 'void Awake' in line or 'public void Initialize' in line or 'void OnEnable' in line:
        print(f"\n--- Line {idx+1} ---")
        for i in range(idx, min(len(lines), idx + 40)):
            print(f"{i+1}: {lines[i]}", end='')
