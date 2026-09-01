import os

editor_dir = r"c:\_dev\knightonline-mobil\Client\Assets\Scripts\Editor"
matches = []

for root, dirs, files in os.walk(editor_dir):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            try:
                with open(path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    if 'kozoneasset' in content.lower():
                        matches.append(path)
            except Exception:
                pass

print("Editor scripts referencing KOZoneAsset:")
for match in matches:
    print(match)
