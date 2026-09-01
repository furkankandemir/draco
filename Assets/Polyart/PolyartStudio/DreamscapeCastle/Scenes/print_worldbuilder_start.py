import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\World\WorldBuilder.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()

for i in range(170, min(len(lines), 300)):
    # print using ascii/ignore to prevent windows console encoding errors
    clean_line = lines[i].encode('ascii', errors='replace').decode('ascii')
    print(f"{i+1}: {clean_line}", end='')
