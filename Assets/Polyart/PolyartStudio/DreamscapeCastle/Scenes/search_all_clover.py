import os

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"
matches = []

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.endswith('.asset') or file.endswith('.unity'):
            path = os.path.join(root, file)
            try:
                with open(path, 'rb') as f:
                    content = f.read()
                    if b"clover" in content.lower():
                        matches.append(path)
            except Exception as e:
                pass

print(f"Total files searched. Found matches in:")
for match in matches:
    print(match)
