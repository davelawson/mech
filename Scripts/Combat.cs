using Godot;

namespace Mech;

public enum CombatSide { Friendly, Enemy }
public enum SimulationOpponent { Mirror, Randomized }
public enum CombatOrder { Hold, Move, Attack, Follow }
public enum MovementMode { Walk, Run }
public enum FiringMode { FireAtWill, HoldFire }
public enum CombatOutcome { Ongoing, Victory, Defeat, Draw, Ended }
public enum CombatStatus { Active, Shutdown, Disabled, Destroyed }
public enum ContactLevel { Lost, Identified, Detailed }

/// <summary>Authored tactical values. Distances are hexes, time is seconds.</summary>
public static partial class CombatRules
{
    public const float ArenaHalfSize = 96;
    public const float DeploymentDepth = ArenaHalfSize * 2 / 3;
    public const float DeploymentSpacing = 8;
    public const float WeaponRangeScale = 3;
    public const float MinimumPreferredDistance = 1.5f * WeaponRangeScale;
    public const float MaximumPreferredDistance = 21 * WeaponRangeScale;
    public const float NoviceDetectionRadius = 50;
    public static float DetectionRadius(int sensors, int targetTonnage) => NoviceDetectionRadius
        * (1 + (WarriorSkills.Novice - Math.Clamp(sensors, WarriorSkills.Expert, WarriorSkills.Novice))
            / (float)(WarriorSkills.Novice - WarriorSkills.Expert))
        * (.5f + (Math.Clamp(targetTonnage, 20, 100) - 20) / 160f);
    public static float DetectionRadius(CombatUnit observer, CombatUnit target) => DetectionRadius(observer.Warrior.Sensors, target.Spec.Tonnage);
    public const float UnitRadius = .65f;
    public const float FollowDistance = 3;
    public const float MovementScale = .18f;
    public const int AimModifier = 3;
    public const double StepSeconds = .05;
    // Shared by combat, hangar previews and future chassis; catalog MP remain stock.
    public static float MovementSpeed(MechSpec spec, MovementMode mode) =>
        (mode == MovementMode.Run ? spec.Run : spec.Walk) * MovementScale;
    public static float Cooldown(WeaponSpec weapon) => weapon.Kind switch
    {
        WeaponKind.MachineGun => 4, WeaponKind.Flamer or WeaponKind.SmallLaser => 6, WeaponKind.MediumLaser => 8,
        WeaponKind.LargeLaser or WeaponKind.ERLargeLaser or WeaponKind.PPC or WeaponKind.AC2 or WeaponKind.AC5
            or WeaponKind.AC10 or WeaponKind.AC20 or WeaponKind.LRM5 or WeaponKind.LRM10 or WeaponKind.LRM15
            or WeaponKind.LRM20 or WeaponKind.SRM2 or WeaponKind.SRM4 or WeaponKind.SRM6 => 10,
        _ => throw new ArgumentOutOfRangeException(nameof(weapon))
    };
    public static int Damage(WeaponSpec weapon) => weapon.DamageProfile.PacketDamage;
    public static Vector2 Clamp(Vector2 point) => point.Clamp(
        Vector2.One * (-ArenaHalfSize + UnitRadius), Vector2.One * (ArenaHalfSize - UnitRadius));
}

public sealed class CombatLocation
{
    public MechLocation Location { get; }
    public int Armor { get; internal set; }
    public int RearArmor { get; internal set; }
    public int Structure { get; internal set; }
    public int InitialArmor { get; }
    public int InitialRearArmor { get; }
    public int InitialStructure { get; }
    internal CombatLocation(LocationCondition source)
    {
        Location = source.Location; Armor = InitialArmor = source.Armor;
        Structure = InitialStructure = source.Structure; RearArmor = InitialRearArmor = source.RearArmor;
    }
}

public sealed class CombatWeapon
{
    public WeaponSpec Spec { get; }
    public float MinimumRange => Spec.Minimum * CombatRules.WeaponRangeScale;
    public float ShortRange => Spec.Short * CombatRules.WeaponRangeScale;
    public float MediumRange => Spec.Medium * CombatRules.WeaponRangeScale;
    public float LongRange => Spec.Long * CombatRules.WeaponRangeScale;
    public bool Enabled { get; set; } = true;
    public double Remaining { get; internal set; }
    public int Damage => CombatRules.Damage(Spec);
    internal CombatWeapon(WeaponSpec spec) { Spec = spec; }
}

/// <summary>A private copy of deployed condition. No reference to an owned mech or campaign is retained.</summary>
public sealed partial class CombatUnit
{
    public string Id { get; }
    public MechSpec Spec { get; }
    public WarriorSpec Warrior { get; }
    public CombatSide Side { get; }
    public IReadOnlyDictionary<MechLocation, CombatLocation> Locations { get; }
    public IReadOnlyList<CombatWeapon> Weapons { get; }
    public IReadOnlyDictionary<string, ComponentCondition> Equipment { get; }
    public IReadOnlyList<AmmoCondition> Ammo { get; }
    public Vector2 Position { get; internal set; }
    public Vector2 Destination { get; internal set; }
    internal IReadOnlyList<Vector2> Route { get; set; } = Array.Empty<Vector2>();
    internal int RouteIndex { get; set; }
    internal Vector2 RouteGoal { get; set; } = new(float.NaN, float.NaN);
    internal Vector2 FiringPosition { get; set; }
    internal double NextFiringSearch { get; set; }
    public Vector2 Facing { get; internal set; }
    public bool Moving { get; internal set; }
    /// <summary>Signed travel along Facing: negative means reversing.</summary>
    public float CurrentSpeed { get; internal set; }
    public float AngularVelocity { get; internal set; }
    public CombatOrder Order { get; internal set; }
    public string? AttackTarget { get; internal set; }
    public string? FollowTarget { get; internal set; }
    public string? PriorityTarget { get; internal set; }
    public string? CurrentTarget { get; internal set; }
    public MovementMode Movement { get; set; }
    public FiringMode Firing { get; set; }
    public float PreferredDistance { get; set; }
    public int ShotsFired { get; internal set; }
    public int Hits { get; internal set; }
    public int DamageDealt { get; internal set; }
    internal CombatUnit(string id, MechSpec spec, WarriorSpec warrior, CombatSide side,
        IEnumerable<LocationCondition> locations, IEnumerable<EquipmentCondition> equipment,
        IEnumerable<AmmoCondition> ammo, Vector2 position)
    {
        Id = id; Spec = spec; Warrior = warrior; Side = side; Position = Destination = position;
        Facing = side == CombatSide.Friendly ? Vector2.Up : Vector2.Down;
        Locations = locations.ToDictionary(item => item.Location, item => new CombatLocation(item));
        Equipment = equipment.ToDictionary(item => item.Id, item => item.Condition);
        Ammo = Array.AsReadOnly(ammo.ToArray());
        Weapons = Array.AsReadOnly(spec.Weapons.Select(item => new CombatWeapon(item)).ToArray());
        // A damage-weighted short-range preference keeps mixed loadouts relevant.
        var usable = Weapons.Where(WeaponUsable).ToArray();
        PreferredDistance = usable.Length == 0 ? 3 * CombatRules.WeaponRangeScale : Math.Clamp(
            usable.Sum(item => item.ShortRange * item.Damage) / usable.Sum(item => item.Damage),
            CombatRules.WeaponRangeScale, 15 * CombatRules.WeaponRangeScale);
    }
    public bool Intact(MechLocation location) => Locations[location].Structure > 0;
    public bool WeaponUsable(CombatWeapon weapon) => Intact(weapon.Spec.Location)
        && Equipment[weapon.Spec.Id] != ComponentCondition.Destroyed
        && (weapon.Spec.AmmoKind is null || Spec.Ammo.Any(bin => bin.AmmoKind == weapon.Spec.AmmoKind
            && Intact(bin.Location) && Equipment[bin.Id] != ComponentCondition.Destroyed
            && Ammo.Any(item => item.Id == bin.Id && item.Shots > 0)));
    public CombatStatus Status => !Intact(MechLocation.Head) || !Intact(MechLocation.CenterTorso)
        ? CombatStatus.Destroyed : (!Intact(MechLocation.LeftLeg) && !Intact(MechLocation.RightLeg))
            || !Weapons.Any(WeaponUsable) ? CombatStatus.Disabled : Shutdown ? CombatStatus.Shutdown : CombatStatus.Active;
    public bool Active => Status == CombatStatus.Active;
    public bool Defeated => Status is CombatStatus.Disabled or CombatStatus.Destroyed;
    private float Mobility => Defeated ? 0 : (!Intact(MechLocation.LeftLeg) || !Intact(MechLocation.RightLeg)) ? .5f : 1;
    public float UnheatedWalkingSpeed => CombatRules.MovementSpeed(Spec, MovementMode.Walk) * Mobility;
    public float UnheatedRunningSpeed => CombatRules.MovementSpeed(Spec, MovementMode.Run) * Mobility;
    public float WalkingSpeed => Active ? UnheatedWalkingSpeed * HeatSpeedMultiplier : 0;
    public float RunningSpeed => Active ? UnheatedRunningSpeed * HeatSpeedMultiplier : 0;
    public float Speed => Movement == MovementMode.Run ? RunningSpeed : WalkingSpeed;
    // Nine / Walk seconds to full run, half that to stop. No independent mass modifier.
    public float Acceleration => UnheatedRunningSpeed * Spec.Walk / 9f;
    public float Braking => Acceleration * 2;
    public float TurnRate => Mathf.DegToRad(15 * Spec.Walk);
    public int ArmorLost => Locations.Values.Sum(item => item.InitialArmor - item.Armor + item.InitialRearArmor - item.RearArmor);
    public int StructureLost => Locations.Values.Sum(item => item.InitialStructure - item.Structure);

    public void ApplyDamage(MechLocation location, int damage, Vector2? attackFrom = null)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        while (damage > 0)
        {
            var part = Locations[location];
            if (part.Structure > 0)
            {
                var rear = attackFrom is { } from && CombatRules.IsTorso(location) && RearHitFrom(from);
                var armor = Math.Min(damage, rear ? part.RearArmor : part.Armor);
                if (rear) part.RearArmor -= armor; else part.Armor -= armor;
                damage -= armor;
                var structure = Math.Min(damage, part.Structure); part.Structure -= structure; damage -= structure;
                if (part.Structure == 0)
                {
                    if (location == MechLocation.LeftTorso) DestroyAttachedArm(MechLocation.LeftArm);
                    if (location == MechLocation.RightTorso) DestroyAttachedArm(MechLocation.RightArm);
                }
            }
            var next = location switch
            {
                MechLocation.LeftArm => MechLocation.LeftTorso,
                MechLocation.RightArm => MechLocation.RightTorso,
                MechLocation.LeftTorso or MechLocation.RightTorso or MechLocation.LeftLeg or MechLocation.RightLeg => MechLocation.CenterTorso,
                _ => (MechLocation?)null
            };
            if (next is null) break;
            location = next.Value;
        }
    }
    private void DestroyAttachedArm(MechLocation location)
    { Locations[location].Armor = 0; Locations[location].Structure = 0; }
}

public sealed class CombatContact
{
    public string UnitId { get; }
    public ContactLevel Level { get; internal set; }
    public bool Detected => Level != ContactLevel.Lost;
    public bool DetailsVisible => Level == ContactLevel.Detailed;
    public Vector2 LastKnownPosition { get; internal set; }
    public double LastSeenAt { get; internal set; }
    internal CombatContact(string id) { UnitId = id; }
}

public sealed record CombatShot(string AttackerId, string TargetId, Vector2 From, Vector2 To,
    string Weapon, bool Hit, MechLocation Location, int Damage, int GeneratedHeat = 0, int TargetHeat = 0)
{
    public double Chance { get; init; }
    public double Roll { get; init; }
    public string Mount { get; init; } = "";
    public IReadOnlyList<CombatDamage> DamageApplied { get; init; } = [];
    public IReadOnlyList<string> SystemsLost { get; init; } = [];
    public CombatStatus? TargetStatus { get; init; }
}

/// <summary>Deterministic fixed-step combat with shared detection and simultaneous weapon resolution.</summary>
public sealed partial class CombatSimulation
{
    public IReadOnlyList<CombatUnit> Units { get; }
    public BattlefieldTerrain Terrain { get; }
    public bool Paused { get; set; } = true;
    public double Elapsed { get; private set; }
    public CombatOutcome Outcome { get; private set; }
    public event Action<CombatShot>? ShotFired;
    private readonly List<CombatLogEntry> _history = [];
    public IReadOnlyList<CombatLogEntry> History { get; }
    private readonly Random _random;
    private readonly Dictionary<CombatSide, Dictionary<string, CombatContact>> _contacts = new()
    { [CombatSide.Friendly] = [], [CombatSide.Enemy] = [] };
    private double _accumulator;
    internal bool EnemyAiEnabled { get; set; } = true;

    public static string? UnavailableReason(ForceRoster roster, OwnedMech mech)
    {
        var reasons = new List<string>(); var warrior = roster.Pilot(mech);
        if (!mech.Operational) reasons.Add("BattleMech not operational");
        if (warrior is null) reasons.Add("No assigned MechWarrior");
        else if (!warrior.Ready) reasons.Add(warrior.Status);
        return reasons.Count == 0 ? null : string.Join(" · ", reasons);
    }

    public static CombatSimulation Deploy(ForceRoster roster, IEnumerable<string> selected, int? seed = null, int? terrainSeed = null,
        SimulationOpponent opponent = SimulationOpponent.Mirror) =>
        CreateDeployment(roster, selected, seed, terrainSeed, null, opponent);
    internal static CombatSimulation DeployOnTerrain(ForceRoster roster, IEnumerable<string> selected, BattlefieldTerrain terrain, int seed = 41,
        SimulationOpponent opponent = SimulationOpponent.Mirror) =>
        CreateDeployment(roster, selected, seed, null, terrain, opponent);
    private static CombatSimulation CreateDeployment(ForceRoster roster, IEnumerable<string> selected, int? seed, int? terrainSeed,
        BattlefieldTerrain? terrain, SimulationOpponent opponent)
    {
        if (!Enum.IsDefined(opponent)) throw new ArgumentOutOfRangeException(nameof(opponent));
        var ids = selected.ToArray();
        if (ids.Length is < 1 or > 4 || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Select one to four different BattleMechs.");
        var force = ids.Select(id =>
        {
            var source = roster.Mechs.SingleOrDefault(item => item.Id == id)
                ?? throw new ArgumentException("Unknown BattleMech.");
            if (UnavailableReason(roster, source) is { } reason) throw new ArgumentException(reason);
            return source;
        }).ToArray();
        var combatSeed = seed ?? Random.Shared.Next();
        var enemies = opponent == SimulationOpponent.Randomized ? DraftOpposition(force, new Random(combatSeed)) : null;
        var units = new List<CombatUnit>();
        for (var i = 0; i < force.Length; i++)
        {
            var source = force[i];
            var pilot = roster.Pilot(source)!.Spec;
            var x = (i - (ids.Length - 1) / 2f) * CombatRules.DeploymentSpacing;
            units.Add(DeploymentUnit(source.Id, source, pilot, CombatSide.Friendly, new Vector2(x, CombatRules.DeploymentDepth),
                pristine: opponent == SimulationOpponent.Mirror));
            var enemy = enemies is null ? (Mech: source, Pilot: pilot) : enemies[i];
            units.Add(DeploymentUnit($"enemy-{i + 1}", enemy.Mech, enemy.Pilot, CombatSide.Enemy,
                new Vector2(x, -CombatRules.DeploymentDepth), pristine: true));
        }
        return new CombatSimulation(units, combatSeed, terrain ?? BattlefieldTerrain.Generate(terrainSeed ?? Random.Shared.Next()));
    }
    private static CombatUnit DeploymentUnit(string id, OwnedMech source, WarriorSpec pilot, CombatSide side, Vector2 position, bool pristine) =>
        new(id, source.Spec, pilot, side,
            pristine ? source.Spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)) : source.Locations,
            pristine ? source.Equipment.Select(item => new EquipmentCondition(item.Id, ComponentCondition.Operational)) : source.Equipment,
            pristine ? source.Spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)) : source.Ammo, position);

    private static (OwnedMech Mech, WarriorSpec Pilot)[] DraftOpposition(IReadOnlyList<OwnedMech> force, Random random)
    {
        var available = new List<(OwnedMech Mech, WarriorSpec Pilot)>();
        var enemies = new List<(OwnedMech Mech, WarriorSpec Pilot)>();
        foreach (var source in force)
        {
            var match = available.FindIndex(item => item.Mech.Spec.WeightClass == source.Spec.WeightClass);
            if (match < 0)
            {
                // Each legal draft contributes one mech/pilot pair per weight category.
                // Retain unused pairs; repeated categories draw from another fresh draft.
                var draft = InitialDraft.CreateRandom(random).Establish();
                available.AddRange(draft.Mechs.Select(mech => (mech, draft.Pilot(mech)!.Spec)));
                match = available.FindIndex(item => item.Mech.Spec.WeightClass == source.Spec.WeightClass);
            }
            enemies.Add(available[match]); available.RemoveAt(match);
        }
        return enemies.ToArray();
    }
    internal CombatSimulation(IEnumerable<CombatUnit> units, int seed, BattlefieldTerrain? terrain = null)
    {
        Units = Array.AsReadOnly(units.ToArray()); Terrain = terrain ?? BattlefieldTerrain.Flat;
        History = _history.AsReadOnly(); _random = new Random(seed); RefreshDetection();
    }
    public CombatUnit Unit(string id) => Units.Single(item => item.Id == id);
    public IReadOnlyDictionary<string, CombatContact> Contacts(CombatSide side) => _contacts[side];
    public bool Detected(CombatSide side, string id) => _contacts[side].TryGetValue(id, out var contact) && contact.Detected;

    public bool DetailsVisible(CombatSide side, string id) => _contacts[side].TryGetValue(id, out var contact) && contact.DetailsVisible;

    internal void RefreshDetection()
    {
        foreach (var side in Enum.GetValues<CombatSide>())
        {
            var observers = Units.Where(item => item.Side == side && item.Active).ToArray();
            foreach (var target in Units.Where(item => item.Side != side))
            {
                var level = ContactLevel.Lost;
                foreach (var observer in observers)
                {
                    var radius = CombatRules.DetectionRadius(observer, target);
                    var distance = observer.Position.DistanceSquaredTo(target.Position);
                    if (distance > radius * radius || !Terrain.ClearSight(observer.Position, target.Position)) continue;
                    level = distance <= radius * radius * .25f ? ContactLevel.Detailed : ContactLevel.Identified;
                    if (level == ContactLevel.Detailed) break;
                }
                if (!_contacts[side].TryGetValue(target.Id, out var contact))
                {
                    if (level == ContactLevel.Lost) continue;
                    contact = new CombatContact(target.Id); _contacts[side].Add(target.Id, contact);
                }
                contact.Level = level;
                if (contact.Detected) { contact.LastKnownPosition = target.Position; contact.LastSeenAt = Elapsed; }
            }
        }
    }
    public void Move(IEnumerable<string> ids, Vector2 destination)
    {
        var group = ids.Distinct().Select(Unit).Where(item => !item.Defeated).ToArray();
        var assigned = new List<Vector2>();
        for (var i = 0; i < group.Length; i++)
        {
            // Reserve separate destinations even when the clicked point is against a boundary.
            var offset = new Vector2((i % 2 - (group.Length > 1 ? .5f : 0)) * 2.4f, i / 2 * 2.4f);
            var goal = Terrain.ReachableDestination(group[i].Position, destination + offset);
            for (var attempt = 0; assigned.Any(point => point.DistanceTo(goal) < CombatRules.UnitRadius * 2.1f) && attempt < 100; attempt++)
                goal = Terrain.ReachableDestination(group[i].Position, destination + Vector2.FromAngle(attempt * 2.4f) * (2 + attempt * .15f));
            group[i].Route = Array.Empty<Vector2>(); assigned.Add(goal); group[i].Destination = goal; group[i].Order = CombatOrder.Move;
            group[i].AttackTarget = group[i].FollowTarget = null;
        }
    }
    public void Attack(IEnumerable<string> ids, string targetId)
    {
        var target = Unit(targetId);
        foreach (var unit in ids.Distinct().Select(Unit).Where(item => !item.Defeated && item.Side != target.Side && Detected(item.Side, targetId) && !target.Defeated))
        { unit.Order = CombatOrder.Attack; unit.AttackTarget = targetId; unit.FollowTarget = null; unit.NextFiringSearch = 0; }
    }
    public void Follow(IEnumerable<string> ids, string targetId)
    {
        var target = Unit(targetId);
        if (target.Defeated) return;
        foreach (var unit in ids.Distinct().Select(Unit).Where(item => !item.Defeated && item.Side == target.Side && item != target))
        {
            unit.Order = CombatOrder.Follow; unit.FollowTarget = targetId; unit.AttackTarget = null;
            unit.Route = Array.Empty<Vector2>();
        }
    }
    public void SetPriority(IEnumerable<string> ids, string? targetId)
    {
        foreach (var unit in ids.Distinct().Select(Unit).Where(item => !item.Defeated))
            if (targetId is null || (Unit(targetId).Side != unit.Side && Detected(unit.Side, targetId) && !Unit(targetId).Defeated))
                unit.PriorityTarget = targetId;
    }
    public void Hold(IEnumerable<string> ids)
    {
        foreach (var unit in ids.Distinct().Select(Unit).Where(item => !item.Defeated))
        {
            unit.Order = CombatOrder.Hold; unit.AttackTarget = unit.FollowTarget = null;
            unit.Destination = unit.Position; unit.Route = Array.Empty<Vector2>();
        }
    }
    public void End() { if (Outcome == CombatOutcome.Ongoing) { Outcome = CombatOutcome.Ended; Paused = true; } }
    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (Paused || Outcome != CombatOutcome.Ongoing) return;
        _accumulator += delta;
        while (_accumulator + 1e-9 >= CombatRules.StepSeconds && Outcome == CombatOutcome.Ongoing)
        { _accumulator -= CombatRules.StepSeconds; Step((float)CombatRules.StepSeconds); }
    }
    private void Step(float delta)
    {
        Elapsed += CombatRules.StepSeconds;
        RefreshDetection();
        if (EnemyAiEnabled) UpdateAi();
        StopInvalidFollows();
        foreach (var unit in Units) { unit.Moving = false; unit.AngularVelocity = 0; }
        foreach (var unit in Units.Where(item => item.Active)) MoveUnit(unit, delta);
        foreach (var unit in Units.Where(item => !item.Defeated))
        {
            unit.AdvanceHeat(CombatRules.StepSeconds);
            foreach (var weapon in unit.Weapons) weapon.Remaining = Math.Max(0, weapon.Remaining - delta);
        }
        RefreshDetection();
        // Update every waist before selecting rear targets or taking a firing/damage snapshot.
        foreach (var unit in Units.Where(item => item.Active))
        {
            var aim = SelectAimTarget(unit); unit.CurrentTarget = aim?.Id;
            UpdateTorso(unit, aim, delta);
        }
        var shots = new List<CombatShot>();
        foreach (var unit in Units.Where(item => item.Active))
        {
            var mainTarget = unit.CurrentTarget is { } main ? Unit(main) : null;
            var rearTarget = SelectRearTarget(unit); unit.CurrentRearTarget = rearTarget?.Id;
            foreach (var (weapon, target) in SelectHeatVolley(unit, mainTarget, rearTarget))
            {
                var chance = TerrainHitChance(unit, target, weapon);
                var roll = _random.NextDouble();
                var hit = roll < chance;
                var location = RollLocation();
                shots.Add(new(unit.Id, target.Id, unit.Position, target.Position, weapon.Spec.Name, hit, location,
                    hit ? weapon.Damage : 0, weapon.Spec.Heat, hit ? CombatRules.FlamerHeat(weapon.Spec) : 0)
                { Chance = chance, Roll = roll, Mount = DraftCatalog.Abbreviation(weapon.Spec.Location) + (weapon.Spec.Rear ? " rear" : "") });
                weapon.Remaining = CombatRules.Cooldown(weapon.Spec); unit.ShotsFired++;
                if (hit) { unit.Hits++; unit.DamageDealt += weapon.Damage; }
            }
        }
        // All shooters and rolls are captured before any damage or casualty can invalidate a return shot.
        for (var i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            if (!shot.Hit) continue;
            var target = Unit(shot.TargetId);
            var before = target.Locations.Values.ToDictionary(part => part.Location,
                part => (part.Armor, part.RearArmor, part.Structure));
            var status = target.Status;
            target.ApplyDamage(shot.Location, shot.Damage, shot.From);
            var damage = target.Locations.Values.Select(part => new CombatDamage(part.Location,
                before[part.Location].Armor - part.Armor, before[part.Location].RearArmor - part.RearArmor,
                before[part.Location].Structure - part.Structure, before[part.Location].Structure > 0 && part.Structure == 0))
                .Where(part => part.Armor + part.RearArmor + part.Structure > 0).ToArray();
            var destroyed = damage.Where(part => part.Destroyed).Select(part => part.Location).ToHashSet();
            var systems = target.Spec.Weapons.Select(item => (item.Id, item.Name, item.Location))
                .Concat(target.Spec.Components.Select(item => (item.Id, item.Name, item.Location)))
                .Concat(target.Spec.Ammo.Select(item => (item.Id, item.Name, item.Location)))
                .Where(item => destroyed.Contains(item.Location) && target.Equipment[item.Id] != ComponentCondition.Destroyed)
                .Select(item => $"{DraftCatalog.Abbreviation(item.Location)} {item.Name}").ToArray();
            shots[i] = shot with { DamageApplied = Array.AsReadOnly(damage), SystemsLost = Array.AsReadOnly(systems),
                TargetStatus = target.Status != status ? target.Status : null };
        }
        var heat = Units.ToDictionary(unit => unit.Id, _ => 0d);
        foreach (var shot in shots)
        { heat[shot.AttackerId] += shot.GeneratedHeat; heat[shot.TargetId] += shot.TargetHeat; }
        foreach (var unit in Units) unit.AddHeat(heat[unit.Id]);
        foreach (var shot in shots)
        {
            // Contacts still describe the pre-exchange observation; heat now includes the entire exchange.
            _history.Add(new CombatLogEntry(Elapsed, shot, LogUnit(Unit(shot.AttackerId)), LogUnit(Unit(shot.TargetId))));
            ShotFired?.Invoke(shot);
        }
        StopInvalidFollows();
        foreach (var unit in Units.Where(item => !item.Active))
        { unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0; unit.CurrentTarget = unit.CurrentRearTarget = null; }
        RefreshDetection();
        var friendly = Units.Any(item => item.Side == CombatSide.Friendly && !item.Defeated);
        var enemy = Units.Any(item => item.Side == CombatSide.Enemy && !item.Defeated);
        if (!friendly || !enemy)
        {
            Outcome = friendly ? CombatOutcome.Victory : enemy ? CombatOutcome.Defeat : CombatOutcome.Draw;
            Paused = true;
        }
    }
    private void StopInvalidFollows()
    {
        Hold(Units.Where(unit => !unit.Defeated && unit.Order == CombatOrder.Follow
            && (unit.FollowTarget is null || Unit(unit.FollowTarget).Defeated)).Select(unit => unit.Id));
    }
    private void UpdateAi()
    {
        foreach (var unit in Units.Where(item => item.Side == CombatSide.Enemy && item.Active))
        {
            var nearest = Units.Where(item => item.Side != unit.Side && !item.Defeated && Detected(unit.Side, item.Id))
                .OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
            if (nearest is not null) { unit.Order = CombatOrder.Attack; unit.AttackTarget = nearest.Id; }
            else if (unit.Order != CombatOrder.Attack && unit.Position.Length() > 2)
            { unit.Order = CombatOrder.Move; unit.Destination = new Vector2(unit.Position.X * .1f, 0); }
        }
    }
    private void MoveUnit(CombatUnit unit, float delta)
    {
        Vector2? goal = null;
        var reverse = false;
        if (unit.Order == CombatOrder.Move) goal = unit.Destination;
        else if (unit.Order == CombatOrder.Follow && unit.FollowTarget is { } leaderId)
        {
            var leader = Unit(leaderId);
            var away = unit.Position - leader.Position;
            var yielding = away.Length() < CombatRules.FollowDistance - .4f;
            if (away.LengthSquared() < .001f) away = -leader.Facing;
            // Leave room when the leader turns back through the group, rather than pinning it between followers.
            if (yielding || away.Length() > CombatRules.FollowDistance + .2f)
                foreach (var angle in new[] { 0f, .52f, -.52f, 1.05f, -1.05f, 1.57f, -1.57f, Mathf.Pi })
                {
                    var followGoal = Terrain.ReachableDestination(unit.Position,
                        leader.Position + away.Normalized().Rotated(angle) * CombatRules.FollowDistance);
                    if (Units.Any(other => other != unit && other.Status != CombatStatus.Destroyed
                        && other.Position.DistanceTo(followGoal) < CombatRules.UnitRadius * 2.1f)) continue;
                    goal = followGoal;
                    reverse = yielding && unit.Facing.Dot((followGoal - unit.Position).Normalized()) < -.5f;
                    break;
                }
        }
        else if (unit.Order == CombatOrder.Attack && unit.AttackTarget is { } id)
        {
            if (Detected(unit.Side, id))
            {
                var target = Unit(id);
                if (target.Defeated) { unit.Order = CombatOrder.Hold; unit.AttackTarget = null; }
                else
                {
                    var away = unit.Position - target.Position;
                    if (away.Length() < .01f) away = unit.Side == CombatSide.Friendly ? Vector2.Down : Vector2.Up;
                    if (!Terrain.ClearSight(unit.Position, target.Position))
                    {
                        if (Elapsed >= unit.NextFiringSearch)
                        {
                            var range = unit.Weapons.Where(weapon => weapon.Enabled && unit.WeaponUsable(weapon))
                                .Select(weapon => weapon.LongRange).DefaultIfEmpty(unit.PreferredDistance).Max();
                            unit.FiringPosition = Terrain.FiringPosition(unit.Position, target.Position, unit.PreferredDistance, range);
                            unit.NextFiringSearch = Elapsed + 1;
                        }
                        goal = unit.FiringPosition;
                    }
                    else if (Math.Abs(away.Length() - unit.PreferredDistance) > .4f)
                    {
                        goal = Terrain.ReachableDestination(unit.Position, target.Position + away.Normalized() * unit.PreferredDistance);
                        reverse = away.Length() < unit.PreferredDistance;
                    }
                }
            }
            else if (_contacts[unit.Side].TryGetValue(id, out var contact)) goal = contact.LastKnownPosition;
            else { unit.Order = CombatOrder.Hold; unit.AttackTarget = null; }
        }
        // End the order at its resolved destination, not at an intermediate waypoint.
        if (goal is { } desired && unit.Position.DistanceTo(desired) >= .2f)
        {
            if (unit.Route.Count == 0 || !float.IsFinite(unit.RouteGoal.X) || unit.RouteGoal.DistanceTo(desired) > .75f)
            {
                unit.Route = Terrain.FindPath(unit.Position, desired); unit.RouteIndex = 0; unit.RouteGoal = desired;
            }
            while (unit.RouteIndex < unit.Route.Count - 1 && unit.Position.DistanceTo(unit.Route[unit.RouteIndex]) < .55f)
                unit.RouteIndex++;
            if (unit.RouteIndex < unit.Route.Count) goal = unit.Route[unit.RouteIndex];
            else goal = null;
        }
        var remaining = goal is { } point ? point - unit.Position : Vector2.Zero;
        if (goal is not null && remaining.Length() < .2f)
        {
            if (unit.Order == CombatOrder.Move || (unit.AttackTarget is { } lost && !Detected(unit.Side, lost)))
            { unit.Order = CombatOrder.Hold; unit.AttackTarget = null; }
            goal = null;
        }

        var desiredSpeed = 0f;
        if (goal is not null)
        {
            var direction = Steer(unit, remaining.Normalized());
            var heading = reverse ? -direction : direction;
            var error = Math.Abs(unit.Facing.AngleTo(heading));
            Turn(unit, heading, delta);
            // Brake into sharp turns. Broad moving turns retain forward travel, never lateral sliding.
            if (error < Mathf.DegToRad(60))
            {
                var limit = (reverse ? unit.WalkingSpeed : unit.Speed) * Terrain.SpeedFactor(unit.Position);
                var arrivalSpeed = Mathf.Sqrt(2 * unit.Braking * Math.Max(0, remaining.Length() - .18f));
                desiredSpeed = Math.Min(limit * Mathf.Cos(error), arrivalSpeed) * (reverse ? -1 : 1);
            }
        }
        else if (Math.Abs(unit.CurrentSpeed) < .001f && SelectAimTarget(unit) is { } target)
            Turn(unit, (target.Position - unit.Position).Normalized(), delta);

        // A reversal must first spend its momentum; it cannot cross zero in the same step.
        if (unit.CurrentSpeed * desiredSpeed < 0) desiredSpeed = 0;
        var rate = Math.Abs(desiredSpeed) > Math.Abs(unit.CurrentSpeed) ? unit.Acceleration : unit.Braking;
        var nextSpeed = Mathf.MoveToward(unit.CurrentSpeed, desiredSpeed, rate * delta);
        var travel = (unit.CurrentSpeed + nextSpeed) * .5f * delta;
        var candidate = unit.Position + unit.Facing * travel;
        if (Math.Abs(travel) > 1e-8f)
        {
            if (CombatRules.Clamp(candidate).DistanceSquaredTo(candidate) > 1e-10f || Blocked(unit, candidate))
                nextSpeed = 0; // Contact with terrain, solid mechs or arena edges is a hard constraint.
            else { unit.Position = candidate; unit.Moving = true; }
        }
        unit.CurrentSpeed = nextSpeed;
    }
    private void Turn(CombatUnit unit, Vector2 heading, float delta)
    {
        if (heading.LengthSquared() < .001f) return;
        var fast = unit.UnheatedRunningSpeed > 0 ? Math.Clamp(Math.Abs(unit.CurrentSpeed) / unit.UnheatedRunningSpeed, 0, 1) : 0;
        var angle = Math.Clamp(unit.Facing.AngleTo(heading), -unit.TurnRate * (1 - .55f * fast) * delta,
            unit.TurnRate * (1 - .55f * fast) * delta);
        unit.Facing = unit.Facing.Rotated(angle).Normalized();
        unit.AngularVelocity = angle / delta;
    }
    private Vector2 Steer(CombatUnit unit, Vector2 direction)
    {
        var stopping = unit.Braking > 0 ? unit.CurrentSpeed * unit.CurrentSpeed / (2 * unit.Braking) : 0;
        var lookAhead = Math.Clamp(stopping + .25f, .35f, 1.5f);
        foreach (var angle in new[] { 0f, .52f, -.52f, 1.05f, -1.05f, 1.57f, -1.57f })
        {
            var steered = direction.Rotated(angle);
            var candidate = unit.Position + steered * lookAhead;
            if (CombatRules.Clamp(candidate).DistanceSquaredTo(candidate) < 1e-10f && !Blocked(unit, candidate))
                return steered;
        }
        return direction;
    }
    private bool Blocked(CombatUnit unit, Vector2 candidate) => !Terrain.CanTraverse(unit.Position, candidate) || Units.Any(other => other != unit && other.Status != CombatStatus.Destroyed
        && DistanceToSegment(other.Position, unit.Position, candidate) < CombatRules.UnitRadius * 2);
    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var t = segment.LengthSquared() < 1e-10f ? 0 : Math.Clamp((point - from).Dot(segment) / segment.LengthSquared(), 0, 1);
        return point.DistanceTo(from + segment * t);
    }
    public CombatUnit? SelectTarget(CombatUnit unit)
    {
        bool Engageable(CombatUnit target) => target.Side != unit.Side && !target.Defeated && Detected(unit.Side, target.Id)
            && Terrain.ClearSight(unit.Position, target.Position)
            && unit.Weapons.Any(weapon => !weapon.Spec.Rear && weapon.Enabled && unit.WeaponUsable(weapon) && unit.Position.DistanceTo(target.Position) <= weapon.LongRange);
        if (unit.AttackTarget is { } attack && unit.Order == CombatOrder.Attack && Engageable(Unit(attack))) return Unit(attack);
        if (unit.Firing == FiringMode.HoldFire) return null;
        if (unit.PriorityTarget is { } priority && Engageable(Unit(priority))) return Unit(priority);
        if (unit.CurrentTarget is { } current && Engageable(Unit(current))) return Unit(current);
        return Units.Where(Engageable).OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
    }
    public double TerrainHitChance(CombatUnit attacker, CombatUnit target, CombatWeapon weapon) =>
        Terrain.ClearSight(attacker.Position, target.Position)
            ? HitChance(attacker, target, weapon, Terrain.VegetationModifier(attacker.Position, target.Position)) : 0;
    public static double HitChance(CombatUnit attacker, CombatUnit target, CombatWeapon weapon, int terrainModifier = 0)
    {
        var distance = attacker.Position.DistanceTo(target.Position);
        if (distance > weapon.LongRange || !attacker.WeaponUsable(weapon) || !CombatRules.InFiringArc(attacker, target, weapon)) return 0;
        var range = distance <= weapon.ShortRange ? 0 : distance <= weapon.MediumRange ? 2 : 4;
        var minimum = distance < weapon.MinimumRange
            ? (int)Math.Ceiling((weapon.MinimumRange - distance) / CombatRules.WeaponRangeScale) : 0;
        var threshold = attacker.Warrior.Gunnery + CombatRules.AimModifier + range + minimum + terrainModifier + attacker.HeatAimModifier
            + (attacker.Moving ? attacker.Movement == MovementMode.Run ? 2 : 1 : 0)
            + (target.Moving ? target.Movement == MovementMode.Run ? 2 : 1 : 0);
        var successes = 0;
        for (var a = 1; a <= 6; a++) for (var b = 1; b <= 6; b++) if (a + b >= threshold) successes++;
        return successes / 36.0;
    }
    private MechLocation RollLocation() => (_random.Next(1, 7) + _random.Next(1, 7)) switch
    {
        2 or 7 => MechLocation.CenterTorso, 3 or 4 => MechLocation.RightArm, 5 => MechLocation.RightLeg,
        6 => MechLocation.RightTorso, 8 => MechLocation.LeftTorso, 9 => MechLocation.LeftLeg,
        10 or 11 => MechLocation.LeftArm, _ => MechLocation.Head
    };
}
