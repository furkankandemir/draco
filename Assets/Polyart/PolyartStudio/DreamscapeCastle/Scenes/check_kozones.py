import os

path = r"c:\_dev\knightonline-mobil\Client\Assets\Resources\KOZones"
if os.path.exists(path):
    print(f"KOZones folder exists. Contents:")
    for file in os.listdir(path):
        print(file)
else:
    print("KOZones folder does not exist")
