import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

print(f"WorldBuilder.cs lines: {len(lines)}")

matches = []
for idx, line in enumerate(lines):
    if '.opd' in line.lower():
        matches.append(idx)

for idx in matches:
    print(f"\n--- Line {idx+1} ---")
    start = max(0, idx - 15)
    end = min(len(lines), idx + 25)
    for i in range(start, end):
        print(f"{i+1}: {lines[i]}", end='')
