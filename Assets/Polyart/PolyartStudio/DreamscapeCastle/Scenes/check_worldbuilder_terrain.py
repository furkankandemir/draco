import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

for idx, line in enumerate(lines):
    if 'terrain' in line.lower() and ('create' in line.lower() or 'load' in line.lower()):
        print(f"Line {idx+1}: {line.strip()}")
