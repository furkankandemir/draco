import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

start_idx = -1
for idx, line in enumerate(lines):
    if 'private void LoadObjects()' in line:
        start_idx = idx
        break

if start_idx != -1:
    print(f"Found LoadObjects at line {start_idx + 1}")
    # Print next 150 lines
    for i in range(start_idx, min(len(lines), start_idx + 150)):
        print(f"{i+1}: {lines[i]}", end='')
else:
    print("LoadObjects method not found")
