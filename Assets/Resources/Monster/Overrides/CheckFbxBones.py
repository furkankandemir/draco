import re

def extract_bone_names(fbx_path):
    print(f"\n--- Extracting bone/node names from {fbx_path} ---")
    bones = []
    # Search for Model: or NodeAttribute: or typical FBX node definitions
    # Even in binary FBX files, bone/node names are stored as plain text ASCII strings.
    with open(fbx_path, 'rb') as f:
        content = f.read() # Read entire file
        
    # Find all printable ASCII strings of length 3 to 45
    matches = re.findall(rb'[a-zA-Z0-9_\:\-\|]{3,45}', content)
    unique_matches = []
    seen = set()
    for m in matches:
        try:
            s = m.decode('ascii')
            # Check for common bone names or hierarchy indicators (Root, Pelvis, Spine, Clavicle, Bip etc.)
            s_lower = s.lower()
            if any(x in s_lower for x in ['root', 'pelvis', 'spine', 'clavicle', 'thigh', 'calf', 'foot', 'upperarm', 'forearm', 'hand', 'head', 'neck', 'bip']):
                if s not in seen:
                    seen.add(s)
                    unique_matches.append(s)
        except:
            continue
            
    print(f"Found {len(unique_matches)} bone/model nodes. First 40:")
    for name in unique_matches[:40]:
        print(f"  {name}")
        
extract_bone_names("C:\\_dev\\knightonline-mobil\\Client\\Assets\\MonsterSources\\Meshes\\SkeletonWarrior.fbx")
extract_bone_names("C:\\_dev\\knightonline-mobil\\Client\\Assets\\MonsterSources\\Animations\\SkeletonWarrior@Skeleton_Anim_Run_Unarmed.fbx")
