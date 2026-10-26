"""Prepare the MW5 Atlas AS7-D; pass --source to override the external model directory."""
import sys
sys.dont_write_bytecode = True
from prepare_mw5_mech import prepare

WEAPONS = (
    ("LeftTorso", "Lrm10Housing", "Torso_Pitch", "Weapon_Mech_AS7_Torso_Left_MH1_Missile10_SKM"),
    ("LeftTorso", "Srm6Housing", "Torso_Pitch", "Weapon_Mech_AS7_Torso_Left_MH2_Missile6_SKM"),
    ("RightTorso", "CannonHousing", "Torso_Pitch", "Weapon_Mech_AS7_Torso_Right_BH1_AC20_SKM"),
    ("LeftArm", "LaserEmitter", "Forearm_Left", "Weapon_Mech_AS7_Forearm_Left_EH1_Laser_SKM"),
    ("RightArm", "LaserEmitter", "Forearm_Right", "Weapon_Mech_AS7_Forearm_Right_EH1_Laser_SKM"),
    ("CenterTorso", "LaserEmitter0", "Torso_Pitch", "Weapon_Mech_AS7_Torso_Center_EH1_Laser_SKM"),
    ("CenterTorso", "LaserEmitter1", "Torso_Pitch", "Weapon_Mech_AS7_Torso_Center_EH2_Laser_SKM"),
)

if __name__ == "__main__":
    prepare("Atlas", "AS7", 4.0, 1.4, WEAPONS, "prepare-atlas.py", 6,
            "AS7-D_Loadout -> Atlas_HPS stock visuals; LRM20 uses MH1 Missile10 visual with ten source firing positions, SRM6 uses MH2. Torso and arm sockets attach to Torso_Pitch and Forearm bones. Atlas_MDL attaches exterior cockpit to Cockpit.", glass_section=1)
