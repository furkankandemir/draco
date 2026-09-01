import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

start_idx = -1
for idx, line in enumerate(lines):
    if 'private void LoadTerrain()' in line:
        start_idx = idx
        break

if start_idx != -1:
    print(f"Found LoadTerrain at line {start_idx + 1}")
    for i in range(start_idx, min(len(lines), start_idx + 120)):
        print(f"{i+1}: {lines[i]}", end='')
else:
    print("LoadTerrain not found")
