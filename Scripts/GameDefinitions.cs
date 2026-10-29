using System.Collections.Immutable;

namespace Mech;

public enum MechLocation { Head, CenterTorso, LeftTorso, RightTorso, LeftArm, RightArm, LeftLeg, RightLeg }
public enum WarriorHealth { Healthy, Injured, Dead }
public enum ComponentCondition { Operational, Damaged, Destroyed }
public enum MechWeightClass { Light, Medium, Heavy, Assault }
public enum HeatSinkKind { Single, Double }
public enum WarriorExperience { Regular, Veteran }
public enum WarriorSpecialty { None, Gunnery, Piloting, Sensors }
public enum DamageKind { Direct, Missile }
public enum WeaponKind { MediumLaser, SmallLaser, LargeLaser, PPC, Flamer, MachineGun, AC2, ERLargeLaser,
    AC5, AC10, AC20, LRM5, LRM10, LRM15, LRM20, SRM2, SRM4, SRM6 }
public enum AmmoKind { MachineGun, AC2, AC5, AC10, AC20, LRM5, LRM10, LRM15, LRM20, SRM2, SRM4, SRM6 }
public enum ComponentKind { LifeSupport, Sensors, Cockpit, FusionEngine, Gyro, Shoulder, UpperArmActuator,
    LowerArmActuator, HandActuator, Hip, UpperLegActuator, LowerLegActuator, FootActuator, HeatSink, JumpJet }

public sealed record LocationSpec(MechLocation Location, int Armor, int RearArmor, int Structure);
public sealed record WeaponDamageSpec(DamageKind Kind, int PerProjectile, int ProjectileCount, int TargetHeat)
{
    public int PacketDamage => checked(PerProjectile * ProjectileCount);
}
public sealed record WeaponSpec(string Id, string Name, MechLocation Location, bool Rear, int Heat,
    WeaponDamageSpec DamageProfile, int Minimum, int Short, int Medium, int Long, AmmoKind? AmmoKind, WeaponKind Kind);
public sealed record ComponentSpec(string Id, string Name, MechLocation Location, int Slots, ComponentKind Kind, int HeatSinkCount);
public sealed record AmmoSpec(string Id, string Name, MechLocation Location, AmmoKind AmmoKind, int Shots);
public sealed record MechSpec(string Id, string Chassis, string Variant, int Tonnage, MechWeightClass WeightClass, int Introduced,
    string Role, int Walk, int Run, int Jump, int HeatSinks, string Engine, ImmutableArray<LocationSpec> Locations,
    ImmutableArray<WeaponSpec> Weapons, ImmutableArray<ComponentSpec> Components, ImmutableArray<AmmoSpec> Ammo,
    string Source, HeatSinkKind HeatSinkType)
{
    public string DisplayName => $"{Chassis} {Variant}";
    public string Artwork => $"res://Assets/Roster/{Id}.png";
    public int ArmorTotal => Locations.Sum(item => item.Armor + item.RearArmor);
    public int StructureTotal => Locations.Sum(item => item.Structure);
    public int CoolingPerSink => HeatSinkType switch
    { HeatSinkKind.Single => 1, HeatSinkKind.Double => 2, _ => throw new ArgumentOutOfRangeException(nameof(HeatSinkType)) };
}
public sealed record WarriorSpec(string Id, string Name, string Callsign, int Gunnery, int Piloting, int Sensors,
    WarriorExperience Rank, WarriorSpecialty Specialty)
{
    public string DisplayName => $"{Name}  “{Callsign}”";
    public string Artwork => $"res://Assets/Roster/{Id}.png";
    public string Experience => DefinitionText.Experience(this);
    public int SkillPoints => WarriorSkills.Cost(Gunnery) + WarriorSkills.Cost(Piloting) + WarriorSkills.Cost(Sensors);
    public int SkillBudget => Rank switch
    { WarriorExperience.Regular => WarriorSkills.RegularBudget, WarriorExperience.Veteran => WarriorSkills.VeteranBudget,
        _ => throw new ArgumentOutOfRangeException(nameof(Rank)) };
}
