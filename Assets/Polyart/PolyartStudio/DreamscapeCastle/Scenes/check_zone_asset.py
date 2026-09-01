import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\Import\KOZoneAsset.cs"

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

print("KOZoneAsset.cs content:")
# print using ascii/ignore to prevent console encoding issues
print(content.encode('ascii', errors='replace').decode('ascii'))
