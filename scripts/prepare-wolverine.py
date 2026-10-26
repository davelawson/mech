"""Prepare the MW5 Wolverine from external UE Viewer exports (Python 3 + Pillow).

Run: python3 scripts/prepare-wolverine.py [--source /path/to/Wolverine/Model]
Only the runtime GLB, required imported textures and manifest belong in the project.
"""
import sys

sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

# Verified stock loadout -> hardpoint visuals -> blueprint SKM -> source attachment bone.
WEAPONS = (
    ('RightArm', 'CannonHousing', 'Forearm_Right', 'Weapon_Mech_WVR_Forearm_Right_BH1_AC5_SKM'),
    ('LeftTorso', 'Srm6Housing', 'Torso_Pitch', 'Weapon_Mech_WVR_Torso_Left_MH1_Missile6_SKM'),
    ('Head', 'LaserEmitter', 'Torso_Pitch', 'Weapon_Mech_WVR_Head_EH1_Laser_SKM'),
)

if __name__ == "__main__":
    prepare('Wolverine', 'WVR', 2.94, 1.175, WEAPONS, 'prepare-wolverine.py', 6,
            'WVR-6R_MDA/Loadout -> Wolverine_HPS visual mappings -> weapon blueprint SKM defaults. Mech_SKL sockets and component locals are identity. Wolverine_MDL attaches exterior cockpit to Cockpit. Head laser attaches via Torso_Pitch but belongs to Head damage location.')
