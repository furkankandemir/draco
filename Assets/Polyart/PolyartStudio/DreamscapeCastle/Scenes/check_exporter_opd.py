import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\Editor\KOTerrainExporterWindow.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

for idx, line in enumerate(lines):
    if '.opd' in line.lower() or 'opd' in line.lower():
        print(f"Line {idx+1}: {line.strip()}")
