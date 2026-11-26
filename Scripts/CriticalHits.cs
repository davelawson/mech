using System.Collections.Immutable;

namespace Mech;

/// <summary>One critical hit or ammunition explosion, as a shot's log reports it. <see cref="Text"/> is the finished line.</summary>
/// <summary>One critical-hit result in a shot. <paramref name="Destroyed"/> marks an item destroyed or a critical system at its
/// maximum level; <paramref name="Explosion"/> is an ammunition explosion's damage.</summary>
public sealed record CombatCritical(MechLocation Location, string ItemId, string Text, int Explosion = 0, bool Destroyed = false);

public static partial class CombatRules
{
    /// <summary>Potential critical hits for a 2d6 roll after internal structure is damaged.</summary>
    public static int CriticalHits(int roll) => roll >= 12 ? 3 : roll >= 10 ? 2 : roll >= 7 ? 1 : 0;
    /// <summary>Target-number penalty for firing a damaged weapon.</summary>
    public const int DamagedWeaponModifier = 2;
    /// <summary>Target-number penalty per Sensors damage level, for every weapon.</summary>
    public const int SensorsModifierPerLevel = 1;
    public const float SensorRangeLossPerLevel = .25f;
    public const int ShoulderModifier = 2, ArmActuatorModifier = 1;
    /// <summary>Heat per heat clock added by each engine damage level.</summary>
    public const double EngineHeatPerLevel = 5;
    public const float GyroSpeedLossPerLevel = .25f, HipSpeedMultiplier = .5f, LegActuatorSpeedLoss = .1f;
    /// <summary>A damaged Life Support injures the MechWarrior once the mech's heat reaches this.</summary>
    public const double LifeSupportInjuryHeat = 15;
    public const double AmmoExplosionChance = .5;
    /// <summary>An explosion deals this share of the bin's remaining shots × stock damage per shot.</summary>
    public const double AmmoExplosionFraction = .5;
    public const int InjuryDaysMinimum = 10, InjuryDaysMaximum = 20;

    /// <summary>Stock damage per shot of a bin, before <see cref="DamageScale"/>: a missile bin's shot is a full salvo.</summary>
    public static int AmmoShotDamage(AmmoKind kind) => kind switch
    {
        AmmoKind.MachineGun or AmmoKind.AC2 => 2, AmmoKind.AC5 => 5, AmmoKind.AC10 => 10, AmmoKind.AC20 => 20,
        AmmoKind.LRM5 => 5, AmmoKind.LRM10 => 10, AmmoKind.LRM15 => 15, AmmoKind.LRM20 => 20,
        AmmoKind.SRM2 => 4, AmmoKind.SRM4 => 8, AmmoKind.SRM6 => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static int AmmoExplosionDamage(AmmoKind kind, int shots) =>
        (int)Math.Round(shots * AmmoShotDamage(kind) * DamageScale * AmmoExplosionFraction, MidpointRounding.AwayFromZero);
}

/// <summary>Condition wording shared by combat, the hangar and the roster.</summary>
public static class CriticalText
{
    /// <summary>Engines, gyros, cockpit systems and actuators: damaged in levels, never destroyed by a critical hit.</summary>
    public static bool CriticalSystem(MechSpec spec, string id) =>
        spec.Components.Any(item => item.Id == id && item.Kind is not (ComponentKind.HeatSink or ComponentKind.JumpJet));

    /// <summary>"Operational", "Damaged", "Critically damaged" or "Destroyed". A critical system at its maximum level is critically
    /// damaged; with <paramref name="levels"/> its level count follows ("Damaged 2/3").</summary>
    public static string Condition(MechSpec spec, string id, ComponentCondition condition, int hits, bool levels = false)
    {
        if (condition != ComponentCondition.Damaged) return condition.ToString();
        if (!CriticalSystem(spec, id)) return "Damaged";
        var maximum = Repairs.Levels(spec, id);
        return hits >= maximum ? "Critically damaged" : levels ? $"Damaged {hits}/{maximum}" : "Damaged";
    }
    public static string Condition(MechSpec spec, EquipmentCondition item, bool levels = false) =>
        Condition(spec, item.Id, item.Condition, Repairs.Hits(spec, item), levels);
}

public sealed partial class CombatUnit
{
    private readonly Dictionary<string, ComponentCondition> _equipment;
    private readonly Dictionary<string, int> _hits;
    private readonly ImmutableDictionary<MechLocation, ImmutableArray<CriticalBlock>> _layout;
    /// <summary>Critical slots struck this battle; a slot is struck at most once.</summary>
    private readonly HashSet<(MechLocation Location, int Slot)> _slotsHit = [];
    /// <summary>Injuries from this battle: each ammunition explosion, and a damaged Life Support at high heat once.</summary>
    public int Injuries { get; internal set; }
    public bool LifeSupportInjury { get; internal set; }
    /// <summary>What caused each injury other than ejecting, in order, for the MechWarrior's service history.</summary>
    internal List<InjuryCause> InjuryCauses { get; } = [];

    /// <summary>Hits an item has taken: critical slots for weapons, bins, heat sinks and jump jets, damage levels for critical systems.</summary>
    public int ItemHits(string id) => _hits[id];
    public string ConditionText(string id, bool levels = false) => Intact(Location(id))
        ? CriticalText.Condition(Spec, id, Equipment[id], _hits[id], levels) : "Lost";
    private MechLocation Location(string id) => Spec.Weapons.FirstOrDefault(item => item.Id == id)?.Location
        ?? Spec.Ammo.FirstOrDefault(item => item.Id == id)?.Location ?? Spec.Components.Single(item => item.Id == id).Location;

    /// <summary>Damage levels on working critical systems of one kind, optionally in one location.</summary>
    private int Levels(ComponentKind kind, MechLocation? location = null) => Spec.Components
        .Where(item => item.Kind == kind && (location is null || item.Location == location) && Intact(item.Location)
            && Equipment[item.Id] == ComponentCondition.Damaged)
        .Sum(item => _hits[item.Id]);
    public bool CockpitHit => Levels(ComponentKind.Cockpit) > 0;
    public bool LifeSupportDamaged => Levels(ComponentKind.LifeSupport) > 0;
    /// <summary>A damaged engine heats the mech while it runs; a shut-down mech's engine adds nothing until it restarts.</summary>
    public double EngineHeatRate => !Active ? 0
        : Levels(ComponentKind.FusionEngine) * CombatRules.EngineHeatPerLevel / CombatRules.HeatClockSeconds;
    public float SensorRangeMultiplier => Math.Max(0, 1 - Levels(ComponentKind.Sensors) * CombatRules.SensorRangeLossPerLevel);
    public float GyroMultiplier => Math.Max(0, 1 - Levels(ComponentKind.Gyro) * CombatRules.GyroSpeedLossPerLevel);
    /// <summary>Ground speed left by gyro, hip and leg actuator damage. A damaged hip halves speed like a lost leg, and does not
    /// add to one.</summary>
    public float CriticalSpeedMultiplier
    {
        get
        {
            var legLost = !Intact(MechLocation.LeftLeg) || !Intact(MechLocation.RightLeg);
            var hip = !legLost && Levels(ComponentKind.Hip) > 0 ? CombatRules.HipSpeedMultiplier : 1;
            var actuators = Levels(ComponentKind.UpperLegActuator) + Levels(ComponentKind.LowerLegActuator) + Levels(ComponentKind.FootActuator);
            return GyroMultiplier * hip * Math.Max(0, 1 - actuators * CombatRules.LegActuatorSpeedLoss);
        }
    }
    /// <summary>The weapon's target-number penalty from critical damage: its own damage, sensors, and its arm's actuators.</summary>
    public int CriticalAimModifier(CombatWeapon weapon)
    {
        var modifier = (Equipment[weapon.Spec.Id] == ComponentCondition.Damaged ? CombatRules.DamagedWeaponModifier : 0)
            + Levels(ComponentKind.Sensors) * CombatRules.SensorsModifierPerLevel;
        if (weapon.Spec.Location is MechLocation.LeftArm or MechLocation.RightArm)
            modifier += Levels(ComponentKind.Shoulder, weapon.Spec.Location) * CombatRules.ShoulderModifier
                + (Levels(ComponentKind.UpperArmActuator, weapon.Spec.Location) + Levels(ComponentKind.LowerArmActuator, weapon.Spec.Location))
                * CombatRules.ArmActuatorModifier;
        return modifier;
    }

    internal void CheckLifeSupport()
    {
        if (LifeSupportInjury || Heat < CombatRules.LifeSupportInjuryHeat || !LifeSupportDamaged) return;
        LifeSupportInjury = true; Injuries++; InjuryCauses.Add(InjuryCause.LifeSupport);
    }

    /// <summary>The critical slots of a location not yet struck this battle, each with the block that fills it.</summary>
    internal IReadOnlyList<(int Slot, CriticalBlock Block)> UnstruckSlots(MechLocation location) => _layout[location]
        .SelectMany(block => Enumerable.Range(block.Start, block.Length).Select(slot => (slot, block)))
        .Where(entry => !_slotsHit.Contains((location, entry.slot))).ToArray();
    internal void StrikeSlot(MechLocation location, int slot) => _slotsHit.Add((location, slot));

    /// <summary>Applies one critical hit to an item, returning its log text, or null when it changes nothing. A bin that must
    /// explode returns its remaining shots in <paramref name="explodingShots"/>.</summary>
    internal string? CriticalHit(MechLocation location, string id, out int explodingShots)
    {
        explodingShots = 0;
        var condition = _equipment[id];
        if (condition == ComponentCondition.Destroyed) return null;
        var name = $"{DraftCatalog.Abbreviation(location)} {Name(id)}";
        var levels = Repairs.Levels(Spec, id);
        if (CriticalText.CriticalSystem(Spec, id))
        {
            if (_hits[id] >= levels) return null;
            _hits[id]++; _equipment[id] = ComponentCondition.Damaged;
            if (Spec.Components.Single(item => item.Id == id).Kind == ComponentKind.LifeSupport) CheckLifeSupport();
            return $"Critical hit: {name} {(_hits[id] >= levels ? "critically damaged" : $"damaged {_hits[id]}/{levels}")}";
        }
        if (levels > 1 && condition == ComponentCondition.Operational)
        { _hits[id] = 1; _equipment[id] = ComponentCondition.Damaged; return $"Critical hit: {name} damaged"; }
        _hits[id] = levels; _equipment[id] = ComponentCondition.Destroyed;
        var bin = Array.FindIndex(_ammo, item => item.Id == id);
        if (bin >= 0) { explodingShots = _ammo[bin].Shots; _ammo[bin] = _ammo[bin] with { Shots = 0 }; }
        return $"Critical hit: {name} destroyed";
    }
    private string Name(string id) => Spec.Weapons.FirstOrDefault(item => item.Id == id)?.Name
        ?? Spec.Ammo.FirstOrDefault(item => item.Id == id)?.Name ?? Spec.Components.Single(item => item.Id == id).Name;

    /// <summary>An ammunition explosion: damage goes straight into the location's internal structure, and what is left carries on
    /// along the usual transfer path.</summary>
    internal void ApplyInternalDamage(MechLocation location, int damage)
    {
        var part = Locations[location];
        var structure = Math.Min(damage, part.Structure); part.Structure -= structure; damage -= structure;
        AbsorbedStructure += structure;
        if (part.Structure == 0)
        {
            if (location == MechLocation.CenterTorso) AutoEject();
            if (location == MechLocation.LeftTorso) DestroyAttachedArm(MechLocation.LeftArm);
            if (location == MechLocation.RightTorso) DestroyAttachedArm(MechLocation.RightArm);
        }
        var next = location switch
        {
            MechLocation.LeftArm => MechLocation.LeftTorso, MechLocation.RightArm => MechLocation.RightTorso,
            MechLocation.LeftTorso or MechLocation.RightTorso or MechLocation.LeftLeg or MechLocation.RightLeg => MechLocation.CenterTorso,
            MechLocation.CenterTorso => MechLocation.Head, _ => (MechLocation?)null
        };
        if (damage > 0 && next is { } inward) ApplyDamage(inward, damage);
    }

    /// <summary>Equipment as the mission report carries it home: anything in a lost location counts as destroyed, and damaged
    /// items keep their hits.</summary>
    internal EquipmentCondition[] ReportedEquipment() => _equipment.Select(item =>
        !Intact(Location(item.Key)) || item.Value == ComponentCondition.Destroyed ? new EquipmentCondition(item.Key, ComponentCondition.Destroyed)
        // An ejection blows the cockpit open: it needs Repair All before the mech deploys again.
        : Ejected && item.Value == ComponentCondition.Operational && Spec.Components.Any(part => part.Id == item.Key && part.Kind == ComponentKind.Cockpit)
            ? new EquipmentCondition(item.Key, ComponentCondition.Damaged, 1)
        : item.Value == ComponentCondition.Damaged ? new EquipmentCondition(item.Key, ComponentCondition.Damaged, _hits[item.Key])
        : new EquipmentCondition(item.Key, ComponentCondition.Operational)).ToArray();

    internal EquipmentCondition[] EquipmentState() => _equipment.Select(item => new EquipmentCondition(item.Key, item.Value,
        item.Value == ComponentCondition.Damaged ? _hits[item.Key] : null)).ToArray();
    internal void ApplyEquipment(IEnumerable<EquipmentCondition> items)
    {
        foreach (var item in items)
        {
            if (!_equipment.ContainsKey(item.Id)) throw new ArgumentException("The battle state does not match this battle.", nameof(items));
            _equipment[item.Id] = item.Condition;
            _hits[item.Id] = Repairs.Hits(Spec, item);
        }
    }
}

public sealed partial class CombatSimulation
{
    private const int CriticalSeedSalt = 0x5bd1e995;
    private readonly int _seed;
    private readonly Random _criticalRandom;

    /// <summary>Applies damage, then rolls for critical hits in every location whose internal structure it reduced and that still
    /// stands. Explosions it sets off strike the same way, so they can chain.</summary>
    private void Strike(CombatUnit target, Action apply, List<CombatCritical> log)
    {
        var before = target.Locations.Values.ToDictionary(part => part.Location, part => part.Structure);
        apply();
        // Locations are fixed before rolling, so damage from an explosion the rolls set off gets only its own rolls.
        foreach (var location in target.Locations.Values.Where(part => part.Structure < before[part.Location] && part.Structure > 0)
            .Select(part => part.Location).ToArray())
            if (target.Status != CombatStatus.Destroyed) RollCriticals(target, location, log);
    }

    /// <summary>Damages one location as a single hit does, rolling for critical hits; for checks that need criticals without
    /// fighting for them.</summary>
    internal IReadOnlyList<CombatCritical> DamageWithCriticals(CombatUnit target, MechLocation location, int damage)
    {
        var log = new List<CombatCritical>();
        Strike(target, () => target.ApplyDamage(location, damage), log);
        return log;
    }

    private void RollCriticals(CombatUnit target, MechLocation location, List<CombatCritical> log)
    {
        var count = CombatRules.CriticalHits(_criticalRandom.Next(1, 7) + _criticalRandom.Next(1, 7));
        for (var i = 0; i < count && target.Intact(location); i++)
        {
            // Rerolling a slot already struck until a fresh one comes up is an even draw among the fresh slots.
            var slots = target.UnstruckSlots(location);
            if (slots.Count == 0) return;
            var (slot, block) = slots[_criticalRandom.Next(slots.Count)];
            target.StrikeSlot(location, slot);
            if (block.ItemId is not { } id || target.CriticalHit(location, id, out var shots) is not { } text) continue;
            log.Add(new(location, id, text, Destroyed: target.ItemHits(id) >= Repairs.Levels(target.Spec, id)));
            if (shots <= 0 || _criticalRandom.NextDouble() >= CombatRules.AmmoExplosionChance) continue;
            var kind = target.Spec.Ammo.Single(item => item.Id == id).AmmoKind;
            var damage = CombatRules.AmmoExplosionDamage(kind, shots);
            target.Injuries++; target.InjuryCauses.Add(InjuryCause.AmmoExplosion);
            log.Add(new(location, id, $"Ammo explosion · {DraftCatalog.Abbreviation(location)} {target.Spec.Ammo.Single(item => item.Id == id).Name} · {damage} damage", damage));
            // An explosion's damage is the victim's own: it is kept apart from the shot's credited damage. Explosions it sets off are
            // counted with it, once.
            var outer = _explosionDepth == 0;
            var (armor, structure) = (target.AbsorbedArmor, target.AbsorbedStructure);
            var whole = target.Intact(MechLocation.Head) && target.Intact(MechLocation.CenterTorso);
            _explosionDepth++;
            Strike(target, () => target.ApplyInternalDamage(location, damage), log);
            _explosionDepth--;
            if (whole && !(target.Intact(MechLocation.Head) && target.Intact(MechLocation.CenterTorso))) _fatalExplosion ??= (location, id);
            if (outer) { _explosionArmor += target.AbsorbedArmor - armor; _explosionStructure += target.AbsorbedStructure - structure; }
        }
    }
    private int _explosionDepth, _explosionArmor, _explosionStructure;
    /// <summary>The bin whose explosion destroyed the head or center torso during the shot being resolved.</summary>
    private (MechLocation Location, string BinId)? _fatalExplosion;

    /// <summary>Recovery days for a unit's injuries this battle: 10–20 days each, added together. The draw depends only on the
    /// battle's seed and the unit, so the report is the same however often it is asked for.</summary>
    public int InjuryDays(CombatUnit unit)
    {
        if (unit.Injuries == 0 || unit.WarriorKilled) return 0;
        var random = new Random(unchecked(_seed * 31 + Units.ToList().IndexOf(unit) + 1));
        return Enumerable.Range(0, unit.Injuries).Sum(_ => random.Next(CombatRules.InjuryDaysMinimum, CombatRules.InjuryDaysMaximum + 1));
    }
}
