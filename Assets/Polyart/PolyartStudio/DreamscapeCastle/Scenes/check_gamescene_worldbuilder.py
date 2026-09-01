import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\GameSceneController.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

print(f"GameSceneController.cs lines: {len(lines)}")

matches = []
for idx, line in enumerate(lines):
    if 'worldbuilder' in line.lower():
        matches.append(idx)

for idx in matches:
    print(f"\n--- Line {idx+1} ---")
    start = max(0, idx - 10)
    end = min(len(lines), idx + 20)
    for i in range(start, end):
        print(f"{i+1}: {lines[i]}", end='')
