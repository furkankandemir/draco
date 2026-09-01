import os
import re

assets_dir = r"c:\_dev\knightonline-mobil\Client\Assets"
menu_items = []

for root, dirs, files in os.walk(assets_dir):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            try:
                with open(path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    if '[MenuItem' in content or 'MenuItem' in content:
                        # Find menu item lines
                        for line in content.split('\n'):
                            if '[MenuItem' in line:
                                menu_items.append((file, line.strip()))
            except Exception:
                pass

print("Found MenuItems in C# scripts:")
for file, item in menu_items:
    print(f"{file}: {item}")
