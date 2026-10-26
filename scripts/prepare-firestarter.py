"""Prepare the MW5 Firestarter from external exports; --source overrides the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('LeftTorso', 'MachineGun1', 'Torso_Pitch', 'Weapon_Mech_FS9_Torso_Left_BH1_MG_SKM'),
    ('CenterTorso', 'Flamer1', 'Torso_Pitch', 'Weapon_Mech_FS9_Torso_Center_EH1_Flamer_SKM'),
    ('CenterTorso', 'Flamer2', 'Torso_Pitch', 'Weapon_Mech_FS9_Torso_Center_EH2_Flamer_SKM'),
    ('RightTorso', 'MachineGun1', 'Torso_Pitch', 'Weapon_Mech_FS9_Torso_Right_BH1_MG_SKM'),
    ('RightArm', 'LaserEmitter1', 'Forearm_Right', 'Weapon_Mech_FS9_Forearm_Right_EH1_Laser_SKM'),
    ('RightArm', 'Flamer2', 'Forearm_Right', 'Weapon_Mech_FS9_Forearm_Right_EH2_Flamer_SKM'),
    ('LeftArm', 'LaserEmitter1', 'Forearm_Left', 'Weapon_Mech_FS9_Forearm_Left_EH1_Laser_SKM'),
    ('LeftArm', 'Flamer2', 'Forearm_Left', 'Weapon_Mech_FS9_Forearm_Left_EH2_Flamer_SKM'),
)

if __name__ == "__main__":
    prepare('Firestarter', 'FS9', 2.48, 1.075, WEAPONS, 'prepare-firestarter.py', 0,
            "FS9-H loadout -> Firestarter_HPS visual mappings -> source blueprint skeletal meshes. Torso and forearm sockets use Torso_Pitch and Forearm bones; cockpit attaches to Cockpit.", glass_section=0, source_tree='Game')
