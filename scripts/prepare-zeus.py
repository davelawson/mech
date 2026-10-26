"""Prepare the MW5 Zeus from external exports; --source overrides the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('LeftTorso', 'LaserEmitter1', 'Torso_Pitch', 'Weapon_Mech_ZEU_Torso_Left_EH1_Laser_SKM'),
    ('LeftTorso', 'LaserEmitter2', 'Torso_Pitch', 'Weapon_Mech_ZEU_Torso_Left_EH2_Laser_SKM'),
    ('CenterTorso', 'LaserEmitter1', 'Torso_Pitch', 'Weapon_Mech_ZEU_Torso_Center_EH1_Laser_SKM'),
    ('RightArm', 'Lrm15Housing', 'Forearm_Right', 'Weapon_Mech_ZEU_Forearm_Right_MH1_Missile15_SKM'),
    ('LeftArm', 'CannonHousing', 'Forearm_Left', 'Weapon_Mech_ZEU_Forearm_Left_BH1_AC5_SKM'),
)

if __name__ == "__main__":
    prepare('Zeus', 'ZEU', 3.64, 1.3, WEAPONS, 'prepare-zeus.py', 0,
            "ZEU-6S loadout -> Zeus_HPS visual mappings -> source blueprint skeletal meshes. Torso and forearm sockets use Torso_Pitch and Forearm bones; cockpit attaches to Cockpit.", glass_section=0, source_tree='Game')
