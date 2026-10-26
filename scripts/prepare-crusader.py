"""Prepare the MW5 Crusader from external exports; --source overrides the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('LeftTorso', 'Srm6Housing', 'Torso_Pitch', 'Weapon_Mech_CRD_Torso_Left_MH1_Missile6_SKM'),
    ('RightTorso', 'Srm6Housing', 'Torso_Pitch', 'Weapon_Mech_CRD_Torso_Right_MH1_Missile6_SKM'),
    ('RightArm', 'LaserEmitter1', 'Forearm_Right', 'Weapon_Mech_CRD_Forearm_Right_EH1_Laser_SKM'),
    ('RightArm', 'Lrm15Housing', 'Forearm_Right', 'Weapon_Mech_CRD_Forearm_Right_MH1_Missile15_SKM'),
    ('RightArm', 'MachineGun1', 'Forearm_Right', 'Weapon_Mech_CRD_Forearm_Right_BH1_MG_SKM'),
    ('LeftArm', 'LaserEmitter1', 'Forearm_Left', 'Weapon_Mech_CRD_Forearm_Left_EH1_Laser_SKM'),
    ('LeftArm', 'Lrm15Housing', 'Forearm_Left', 'Weapon_Mech_CRD_Forearm_Left_MH1_Missile15_SKM'),
    ('LeftArm', 'MachineGun1', 'Forearm_Left', 'Weapon_Mech_CRD_Forearm_Left_BH1_MG_SKM'),
)

if __name__ == "__main__":
    prepare('Crusader', 'CRD', 3.16, 1.225, WEAPONS, 'prepare-crusader.py', 6,
            "CRD-3R loadout -> Crusader_HPS visual mappings -> source blueprint skeletal meshes. Torso and forearm sockets use Torso_Pitch and Forearm bones; cockpit attaches to Cockpit.", glass_section=0, source_tree='Game/DLC4')
