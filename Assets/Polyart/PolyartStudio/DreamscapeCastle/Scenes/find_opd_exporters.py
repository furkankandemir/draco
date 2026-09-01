import os

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"
matches = []

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            try:
                with open(path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    if '.opd' in content.lower() and ('write' in content.lower() or 'export' in content.lower() or 'save' in content.lower()):
                        matches.append(path)
            except Exception:
                pass

print("C# files referencing OPD writing/exporting:")
for match in matches:
    print(match)
