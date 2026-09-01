import os

foliage_dir = r"c:\_dev\knightonline-mobil\Client\Assets\Polyart\PolyartStudio\DreamscapeCastle\Prefabs\Foliage"

categories = {
    "Cimenler (Grass)": [],
    "Calilar (Bushes)": [],
    "Cicekler (Flowers)": [],
    "Yoncalar (Clovers)": [],
    "Diger Bitkiler (Other Foliage)": []
}

for root, dirs, files in os.walk(foliage_dir):
    for file in files:
        if file.endswith('.prefab') and not file.endswith('_impostor.prefab'):
            name = os.path.splitext(file)[0]
            name_lower = name.lower()
            
            # Simple keyword matching for classification
            if "grass" in name_lower or "lawn" in name_lower:
                categories["Cimenler (Grass)"].append(name)
            elif "bush" in name_lower or "shrub" in name_lower:
                categories["Calilar (Bushes)"].append(name)
            elif "clover" in name_lower:
                categories["Yoncalar (Clovers)"].append(name)
            elif "flower" in name_lower or "lily" in name_lower or "lavender" in name_lower or "potted" in name_lower:
                categories["Cicekler (Flowers)"].append(name)
            else:
                # Let's check if it fits in other categories by folder name or general names
                if "wild" in root.lower():
                    # Check names
                    if "taro" in name_lower or "monstera" in name_lower or "cactus" in name_lower:
                        categories["Diger Bitkiler (Other Foliage)"].append(name)
                    else:
                        categories["Diger Bitkiler (Other Foliage)"].append(name)
                elif "trees" in root.lower() or "pine" in name_lower:
                    # Skip big trees since the user asked for grass/bushes/etc.
                    pass
                else:
                    categories["Diger Bitkiler (Other Foliage)"].append(name)

for cat, items in categories.items():
    print(f"\n[{cat}]")
    for item in sorted(set(items)):
        print(f"  - {item}")
