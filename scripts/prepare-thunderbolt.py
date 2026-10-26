"""Prepare the MW5 Thunderbolt from external exports; --source overrides the model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ('LeftTorso', 'LaserEmitter1', 'Torso_Pitch', 'Weapon_Mech_TDR_Torso_Left_EH1_Laser_SKM'),
    ('LeftTorso', 'LaserEmitter2', 'Torso_Pitch', 'Weapon_Mech_TDR_Torso_Left_EH2_Laser_SKM'),
    ('LeftTorso', 'LaserEmitter3', 'Torso_Pitch', 'Weapon_Mech_TDR_Torso_Left_EH3_Laser_SKM'),
    ('RightTorso', 'Lrm15Housing', 'Torso_Pitch', 'Weapon_Mech_TDR_Torso_Right_MH1_Missile15_SKM'),
    ('RightTorso', 'Srm2Housing', 'Torso_Pitch', 'Weapon_Mech_TDR_Torso_Right_MH2_Missile2_SKM'),
    ('RightArm', 'LaserEmitter1', 'Forearm_Right', 'Weapon_Mech_TDR_Forearm_Right_EH1_Laser_SKM'),
    ('LeftArm', 'MachineGun1', 'Forearm_Left', 'Weapon_Mech_TDR_Forearm_Left_BH1_MG_SKM'),
    ('LeftArm', 'MachineGun2', 'Forearm_Left', 'Weapon_Mech_TDR_Forearm_Left_BH2_MG_SKM'),
)

if __name__ == "__main__":
    prepare('Thunderbolt', 'TDR', 3.16, 1.225, WEAPONS, 'prepare-thunderbolt.py', 2,
            "TDR-5S loadout -> Thunderbolt_HPS visual mappings -> source blueprint skeletal meshes. Torso and forearm sockets use Torso_Pitch and Forearm bones; cockpit attaches to Cockpit.", glass_section=0, source_tree='Game')
