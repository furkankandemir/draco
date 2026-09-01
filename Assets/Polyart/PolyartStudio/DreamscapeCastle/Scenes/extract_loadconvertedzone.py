import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

start_idx = -1
for idx, line in enumerate(lines):
    if 'private void LoadConvertedZone' in line:
        start_idx = idx
        break

if start_idx != -1:
    print(f"Found LoadConvertedZone at line {start_idx + 1}")
    for i in range(start_idx, min(len(lines), start_idx + 100)):
        clean_line = lines[i].encode('ascii', errors='replace').decode('ascii')
        print(f"{i+1}: {clean_line}", end='')
else:
    print("LoadConvertedZone not found")
