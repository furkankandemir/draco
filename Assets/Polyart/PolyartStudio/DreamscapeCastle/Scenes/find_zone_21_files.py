import os

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"
matches = []

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.lower().startswith('zone_21') or file.lower().startswith('zone_21'):
            matches.append(os.path.join(root, file))

print("Found files starting with zone_21:")
for match in matches:
    print(match)
