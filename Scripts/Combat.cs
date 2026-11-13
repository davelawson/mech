using System.Text.Json.Serialization;
using Godot;

namespace Mech;

public enum CombatSide { Friendly, Enemy }
/// <summary>A simulated deployment's opposition: a mirror of the force, a fresh draft matched to it, or a patrol rolled from reputation.</summary>
public enum SimulationOpponent { Mirror, Randomized, Patrol }
public enum CombatOrder { Hold, Move, Attack, Follow }
public enum MovementMode { Walk, Run }
public enum FiringMode { FireAtWill, HoldFire }
public enum CombatOutcome { Ongoing, Victory, Defeat, Draw, Ended }
public enum CombatStatus { Active, Shutdown, Disabled, Destroyed, Extracted }
public enum ContactLevel { Lost, Identified, Detailed }

/// <summary>A deployed mech's condition after a mission battle, keyed by its owned id. Armor lost is front plus rear.</summary>
public sealed record DeployedMechReport(string MechId, LocationCondition[] Locations, EquipmentCondition[] Equipment, bool WarriorKilled, int ArmorLost, int Kills = 0);
/// <summary>What a mission battle reports to the campaign. Enemy MechWarriors spared are those whose head survived.</summary>
public sealed record MissionReport(MissionOutcome Outcome, DeployedMechReport[] Mechs, int EnemyWarriorsSpared);

/// <summary>Authored tactical values. Distances are metres, time is seconds.</summary>
public static partial class CombatRules
{
    public const float ArenaHalfSize = 2880;
    public const float DeploymentDepth = ArenaHalfSize * 2 / 3;
    public const float DeploymentSpacing = 240;
    /// <summary>Metres per stock catalog range point.</summary>
    public const float WeaponRangeScale = 90;
    public const float MinimumPreferredDistance = 1.5f * WeaponRangeScale;
    public const float MaximumPreferredDistance = 21 * WeaponRangeScale;
    public const float NoviceDetectionRadius = 1500;
    public static float DetectionRadius(int sensors, int targetTonnage) => NoviceDetectionRadius
        * (1 + (WarriorSkills.Novice - Math.Clamp(sensors, WarriorSkills.Expert, WarriorSkills.Novice))
            / (float)(WarriorSkills.Novice - WarriorSkills.Expert))
        * (.5f + (Math.Clamp(targetTonnage, 20, 100) - 20) / 160f);
    public static float DetectionRadius(CombatUnit observer, CombatUnit target) => DetectionRadius(observer.Warrior.Sensors, target.Spec.Tonnage);
    public const float UnitRadius = 9.75f;
    public const float FollowDistance = 90;
    /// <summary>Metres per second per stock movement point: exactly 10 km/h.</summary>
    public const float MovementScale = 25f / 9;
    public const int AimModifier = 3;
    public const double StepSeconds = .05;
    // Shared by combat, hangar previews and future chassis; catalog MP remain stock.
    public static float MovementSpeed(MechSpec spec, MovementMode mode) =>
        (mode == MovementMode.Run ? spec.Run : spec.Walk) * MovementScale;
    public static int KilometresPerHour(int movementPoints) => Mathf.RoundToInt(movementPoints * MovementScale * 3.6f);
    public static int JumpMetres(int jumpPoints) => Mathf.RoundToInt(jumpPoints * JumpMetresPerMp);
    /// <summary>Walk and run in km/h and jump range in metres, the jump left out without jump jets. Screens never show MP.</summary>
    public static string MovementSummary(MechSpec spec, string separator = "  ·  ") => string.Join(separator,
        new[] { $"Walk {KilometresPerHour(spec.Walk)} km/h", $"Run {KilometresPerHour(spec.Run)} km/h" }
            .Concat(spec.Jump > 0 ? [$"Jump {JumpMetres(spec.Jump)} m"] : Array.Empty<string>()));
    /// <summary>The short form for small cards: "40/60 km/h · J 120 m".</summary>
    public static string CompactMovement(MechSpec spec) =>
        $"{KilometresPerHour(spec.Walk)}/{KilometresPerHour(spec.Run)} km/h" + (spec.Jump > 0 ? $" · J {JumpMetres(spec.Jump)} m" : "");
    public static float Cooldown(WeaponSpec weapon) => WeaponDefinitions.Of(weapon.Kind).Cooldown;
    public static int Damage(WeaponSpec weapon) => weapon.DamageProfile.PacketDamage;
    /// <summary>Armor, structure and damage count tenths of a stock point, so every value stays whole.</summary>
    public const int DamageScale = 10;
    /// <summary>Time from the first defeat to the decided outcome, letting in-flight effects conclude.</summary>
    public const double WindDownSeconds = 3;
    public const double LaserBurnSeconds = 1;
    public const double LaserSecondHalfDelay = LaserBurnSeconds / 2;
    public static bool IsLaser(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.Laser;
    public static bool IsAutocannon(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.Autocannon;
    /// <summary>Autocannon shells fly straight at one speed for every caliber: one second at 1800 metres.</summary>
    public const float ShellSpeed = 1800;
    public static double ShellFlightSeconds(float distance) => distance / ShellSpeed;
    public static bool IsMachineGun(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.MachineGun;
    /// <summary>A machine gun's discharge is a one-second burst of tracers; its damage lands with the first round.</summary>
    public const double MachineGunBurstSeconds = 1;
    public const float BulletSpeed = 3600;
    public static double BulletFlightSeconds(float distance) => distance / BulletSpeed;
    public static bool IsLrm(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.Lrm;
    public static bool IsSrm(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.Srm;
    /// <summary>LRM and SRM racks fire salvos whose missiles roll separately.</summary>
    public static bool FiresSalvo(WeaponKind kind) => WeaponDefinitions.Of(kind).FiresSalvo;
    /// <summary>LRM salvos fly slower than shells so the swarm reads: 3.6 seconds at the 1890 m long range.</summary>
    public const float MissileSpeed = 525;
    public static double MissileFlightSeconds(float distance) => distance / MissileSpeed;
    /// <summary>SRMs leave the rack at this speed and accelerate steadily.</summary>
    public const double SrmLaunchSpeed = 168;
    // Calibrated so an SRM still reaches its 810 m long range in 810 / MissileSpeed seconds.
    private const double SrmCalibrationRange = 9 * WeaponRangeScale;
    private const double SrmCalibrationSeconds = SrmCalibrationRange / MissileSpeed;
    public const double SrmAcceleration = 2 * (SrmCalibrationRange - SrmLaunchSpeed * SrmCalibrationSeconds)
        / (SrmCalibrationSeconds * SrmCalibrationSeconds);
    public static double SrmFlightSeconds(float distance) =>
        (-SrmLaunchSpeed + Math.Sqrt(SrmLaunchSpeed * SrmLaunchSpeed + 2 * SrmAcceleration * distance)) / SrmAcceleration;
    public static bool IsPpc(WeaponKind kind) => WeaponDefinitions.Of(kind).Family == WeaponFamily.Ppc;
    /// <summary>A PPC bolt reaches a target at its long range in this time, inside its beam's half second;
    /// that fixes its speed, so nearer targets are struck sooner.</summary>
    public const double PpcLongRangeSeconds = .4;
    public static float PpcSpeed(WeaponSpec weapon) => weapon.Long * WeaponRangeScale / (float)PpcLongRangeSeconds;
    public static double PpcFlightSeconds(float distance, WeaponSpec weapon) => distance / PpcSpeed(weapon);
    /// <summary>A PPC hit also deals half its damage, rounded down, shared at random among the adjacent locations except the head.</summary>
    public static int PpcSplash(int damage) => damage / 2;
    /// <summary>Splash is rolled one stock point at a time; any remainder is rolled last.</summary>
    public const int SplashChunk = DamageScale;
    public static IEnumerable<MechLocation> PpcSplashLocations(MechLocation location) =>
        AdjacentLocations(location).Where(item => item != MechLocation.Head);
    /// <summary>A PPC hit's electro-interference adds this to the target's own attacks; a new hit restarts the timer.</summary>
    public const int InterferenceModifier = 2;
    public const double InterferenceSeconds = 5;
    /// <summary>Target-number penalty for an LRM salvo guided by a spotter's sight.</summary>
    public const int IndirectFireModifier = 1;
    /// <summary>The first half of a laser burn rounds up.</summary>
    public static int LaserFirstHalf(int damage) => (damage + 1) / 2;
    // 2d6 hit-location weights in 36ths; they also weight a laser's sweep into an adjacent location.
    public static int LocationWeight(MechLocation location) => location switch
    {
        MechLocation.CenterTorso => 7, MechLocation.LeftLeg or MechLocation.RightLeg => 4, MechLocation.Head => 1, _ => 5
    };
    public static IReadOnlyList<MechLocation> AdjacentLocations(MechLocation location) => location switch
    {
        MechLocation.Head => [MechLocation.CenterTorso],
        MechLocation.CenterTorso => [MechLocation.Head, MechLocation.LeftTorso, MechLocation.RightTorso, MechLocation.LeftLeg, MechLocation.RightLeg],
        MechLocation.LeftTorso => [MechLocation.CenterTorso, MechLocation.LeftArm, MechLocation.LeftLeg],
        MechLocation.RightTorso => [MechLocation.CenterTorso, MechLocation.RightArm, MechLocation.RightLeg],
        MechLocation.LeftArm => [MechLocation.LeftTorso],
        MechLocation.RightArm => [MechLocation.RightTorso],
        MechLocation.LeftLeg => [MechLocation.CenterTorso, MechLocation.LeftTorso],
        MechLocation.RightLeg => [MechLocation.CenterTorso, MechLocation.RightTorso],
        _ => throw new ArgumentOutOfRangeException(nameof(location))
    };
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
    /// <summary>The lance a friendly mech deployed from, which is the player slot that commands it; null for enemies.</summary>
    public int? Lance { get; }
    /// <summary>A patrol's enemy group, which wakes and hunts together; null outside patrols and for friendlies.</summary>
    public int? Group { get; }
    /// <summary>Where the mech deployed, which a patrol group returns to after a fruitless search.</summary>
    internal Vector2 Post { get; set; }
    public IReadOnlyDictionary<MechLocation, CombatLocation> Locations { get; }
    public IReadOnlyList<CombatWeapon> Weapons { get; }
    public IReadOnlyDictionary<string, ComponentCondition> Equipment { get; }
    public IReadOnlyList<AmmoCondition> Ammo { get; }
    private readonly AmmoCondition[] _ammo;
    public Vector2 Position { get; internal set; }
    public Vector2 Destination { get; internal set; }
    internal IReadOnlyList<Vector2> Route { get; set; } = Array.Empty<Vector2>();
    internal int RouteIndex { get; set; }
    internal float BlockedSeconds { get; set; }
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
    /// <summary>Enemies this unit's hits left disabled or destroyed.</summary>
    public int Kills { get; internal set; }
    /// <summary>Seconds of electro-interference left from the last PPC hit.</summary>
    public double InterferenceRemaining { get; internal set; }
    public int InterferenceAimModifier => InterferenceRemaining > 0 ? CombatRules.InterferenceModifier : 0;
    internal CombatUnit(string id, MechSpec spec, WarriorSpec warrior, CombatSide side,
        IEnumerable<LocationCondition> locations, IEnumerable<EquipmentCondition> equipment,
        IEnumerable<AmmoCondition> ammo, Vector2 position, int? lance = null, int? group = null)
    {
        Id = id; Spec = spec; Warrior = warrior; Side = side; Position = Destination = Post = position; Lance = side == CombatSide.Friendly ? lance ?? 0 : null;
        Group = side == CombatSide.Enemy ? group : null;
        Facing = side == CombatSide.Friendly ? Vector2.Up : Vector2.Down;
        Locations = locations.ToDictionary(item => item.Location, item => new CombatLocation(item));
        Equipment = equipment.ToDictionary(item => item.Id, item => item.Condition);
        _ammo = ammo.ToArray(); Ammo = Array.AsReadOnly(_ammo);
        Weapons = Array.AsReadOnly(spec.Weapons.Select(item => new CombatWeapon(item)).ToArray());
        // A damage-weighted short-range preference keeps mixed loadouts relevant.
        var usable = Weapons.Where(WeaponUsable).ToArray();
        PreferredDistance = usable.Length == 0 ? 3 * CombatRules.WeaponRangeScale : Math.Clamp(
            usable.Sum(item => item.ShortRange * item.Damage) / usable.Sum(item => item.Damage),
            CombatRules.WeaponRangeScale, 15 * CombatRules.WeaponRangeScale);
    }
    public bool Intact(MechLocation location) => Locations[location].Structure > 0;
    /// <summary>Physically able to fire: the weapon and at least one compatible bin survive, whether or not it holds shots.</summary>
    public bool WeaponIntact(CombatWeapon weapon) => Intact(weapon.Spec.Location)
        && Equipment[weapon.Spec.Id] != ComponentCondition.Destroyed
        && (weapon.Spec.AmmoKind is not { } kind || AmmoBins(kind).Any());
    public bool WeaponUsable(CombatWeapon weapon) => WeaponIntact(weapon)
        && (weapon.Spec.AmmoKind is not { } kind || AmmoRemaining(kind) > 0);
    /// <summary>Intact compatible bins form one pool per ammunition kind.</summary>
    private IEnumerable<AmmoSpec> AmmoBins(AmmoKind kind) => Spec.Ammo.Where(bin => bin.AmmoKind == kind
        && Intact(bin.Location) && Equipment[bin.Id] != ComponentCondition.Destroyed);
    public int Shots(string binId) => _ammo.Single(item => item.Id == binId).Shots;
    public int AmmoRemaining(AmmoKind kind) => AmmoBins(kind).Sum(bin => Shots(bin.Id));
    /// <summary>Spends one shot from the intact bin with the fewest remaining, breaking ties in catalog order.</summary>
    internal bool ConsumeAmmo(AmmoKind kind)
    {
        var bin = AmmoBins(kind).Where(item => Shots(item.Id) > 0).OrderBy(item => Shots(item.Id)).FirstOrDefault();
        if (bin is null) return false;
        var index = Array.FindIndex(_ammo, item => item.Id == bin.Id);
        _ammo[index] = _ammo[index] with { Shots = _ammo[index].Shots - 1 };
        return true;
    }
    // Running out of ammunition withholds fire but, like a weapon toggle, is not physical weapon loss.
    /// <summary>Left the battlefield through a patrol's extraction zone: out of the fight, but neither disabled nor destroyed.</summary>
    public bool Extracted { get; internal set; }
    public CombatStatus Status => Extracted ? CombatStatus.Extracted : !Intact(MechLocation.Head) || !Intact(MechLocation.CenterTorso)
        ? CombatStatus.Destroyed : (!Intact(MechLocation.LeftLeg) && !Intact(MechLocation.RightLeg))
            || !Weapons.Any(WeaponIntact) ? CombatStatus.Disabled : Shutdown ? CombatStatus.Shutdown : CombatStatus.Active;
    public bool Active => Status == CombatStatus.Active;
    /// <summary>The MechWarrior dies only when the head is destroyed, including by overkill carried up from the center torso.</summary>
    public bool WarriorKilled => !Intact(MechLocation.Head);
    public bool Defeated => IsDefeated(Status);
    /// <summary>Out of the fight. An extracted mech counts too: nothing can target, follow or order it any longer.</summary>
    internal static bool IsDefeated(CombatStatus status) => status is CombatStatus.Disabled or CombatStatus.Destroyed or CombatStatus.Extracted;
    /// <summary>Occupies ground: wrecks and extracted mechs do not.</summary>
    public bool Solid => Status is not (CombatStatus.Destroyed or CombatStatus.Extracted);
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
                // Overkill on the center torso carries up into the cockpit.
                MechLocation.CenterTorso => MechLocation.Head,
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

/// <summary>Part of a PPC hit's splash, rolled to one adjacent location.</summary>
public sealed record CombatSplash(MechLocation Location, int Damage);

/// <summary>One missile of an LRM salvo: its own hit roll and, rolled either way, its own location.</summary>
public sealed record CombatMissile(bool Hit, double Roll, MechLocation Location);

public sealed record CombatShot(string AttackerId, string TargetId, Vector2 From, Vector2 To,
    string Weapon, bool Hit, MechLocation Location, int Damage, int GeneratedHeat = 0, int TargetHeat = 0)
{
    public double Chance { get; init; }
    public double Roll { get; init; }
    public string Mount { get; init; } = "";
    public string WeaponId { get; init; } = "";
    public IReadOnlyList<CombatDamage> DamageApplied { get; init; } = [];
    public IReadOnlyList<string> SystemsLost { get; init; } = [];
    public CombatStatus? TargetStatus { get; init; }
    /// <summary>Shared by a laser discharge and its delayed burn continuation.</summary>
    public long Sequence { get; init; }
    /// <summary>A hit laser's adjacent location and damage, rolled at discharge and applied after half the burn.</summary>
    public MechLocation? SecondLocation { get; init; }
    public int SecondDamage { get; init; }
    /// <summary>True for the second half of a laser burn, logged separately from its discharge.</summary>
    public bool Burn { get; init; }
    /// <summary>A shell's or missile salvo's time from discharge to impact; its damage waits for the impact entry.</summary>
    public double FlightSeconds { get; init; }
    [JsonIgnore] public bool InFlight => FlightSeconds > 0 && !Impact;
    /// <summary>True when a hit shell or salvo lands, logged separately from its discharge.</summary>
    public bool Impact { get; init; }
    /// <summary>A shell or salvo that reached a destroyed target explodes on the wreck without damage.</summary>
    public bool Wreck { get; init; }
    /// <summary>An LRM salvo's missiles. Hit is true when any missile hits; Damage totals the hits.</summary>
    public IReadOnlyList<CombatMissile> Missiles { get; init; } = [];
    public int MissileDamage { get; init; }
    /// <summary>An LRM salvo fired without the shooter's own sight, guided by SpotterId.</summary>
    public bool Indirect { get; init; }
    public string? SpotterId { get; init; }
    /// <summary>A PPC hit's splash damage, one entry per chunk in the adjacent location it was rolled to, applied with the primary damage.</summary>
    public IReadOnlyList<CombatSplash> Splashes { get; init; } = [];
    [JsonIgnore] public int Splash => Splashes.Sum(item => item.Damage);
    [JsonIgnore] public int TotalDamage => Damage + Splash;
}

/// <summary>Deterministic fixed-step combat with shared detection and simultaneous weapon resolution.</summary>
public sealed partial class CombatSimulation
{
    public IReadOnlyList<CombatUnit> Units { get; }
    public BattlefieldTerrain Terrain { get; }
    /// <summary>Paused while any player holds a pause, or the game is waiting for a player to reconnect. Setting it holds or
    /// releases every player's pause at once, as single-player pausing does.</summary>
    public bool Paused
    {
        get => _pauseHolds.Count > 0;
        set { _pauseHolds.Clear(); if (value) _pauseHolds.UnionWith(Players); }
    }
    /// <summary>The player slots commanding friendly lances in this battle.</summary>
    public IReadOnlyList<int> Players { get; }
    /// <summary>The player slots whose pause toggle is on. Every toggle starts on.</summary>
    public IReadOnlySet<int> PauseHolds => _pauseHolds;
    private readonly HashSet<int> _pauseHolds = [];
    public double Elapsed { get; private set; }
    public CombatOutcome Outcome { get; private set; }
    /// <summary>Simulation time at which a wind-down started by a side's defeat decides the outcome.</summary>
    public double? WindDownEnds { get; private set; }
    public bool WindingDown => WindDownEnds is not null;
    /// <summary>Elapsed time including the unstepped remainder, for smooth effect timing.</summary>
    public double Clock => Elapsed + _accumulator;
    private readonly List<(CombatShot Discharge, double Due)> _burns = [];
    private readonly List<(CombatShot Discharge, double Due)> _projectiles = [];
    private long _sequence;
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
        if (mech.Lance is null) reasons.Add("In the lance pool");
        if (mech.Repair is { } repair) reasons.Add(repair.Started is null ? "Repair queued" : "Under repair");
        if (!mech.Operational) reasons.Add("BattleMech not operational");
        if (warrior is null) reasons.Add("No assigned MechWarrior");
        else if (!warrior.Ready) reasons.Add(warrior.Status);
        return reasons.Count == 0 ? null : string.Join(" · ", reasons);
    }

    public static CombatSimulation Deploy(ForceRoster roster, IEnumerable<string> selected, int? seed = null, int? terrainSeed = null,
        SimulationOpponent opponent = SimulationOpponent.Mirror, Biome biome = Biome.Valley) =>
        CreateDeployment(roster, selected, seed, terrainSeed, null, opponent, biome);
    internal static CombatSimulation DeployOnTerrain(ForceRoster roster, IEnumerable<string> selected, BattlefieldTerrain terrain, int seed = 41,
        SimulationOpponent opponent = SimulationOpponent.Mirror) =>
        CreateDeployment(roster, selected, seed, null, terrain, opponent, Biome.Valley);
    private static CombatSimulation CreateDeployment(ForceRoster roster, IEnumerable<string> selected, int? seed, int? terrainSeed,
        BattlefieldTerrain? terrain, SimulationOpponent opponent, Biome biome)
    {
        if (!Enum.IsDefined(opponent) || opponent == SimulationOpponent.Patrol) throw new ArgumentOutOfRangeException(nameof(opponent));
        var force = DeployedForce(roster, selected);
        var combatSeed = seed ?? Random.Shared.Next();
        var enemies = opponent == SimulationOpponent.Randomized ? DraftOpposition(force, new Random(combatSeed)) : null;
        var friendly = force.Select(source => Deployed(source, roster.Pilot(source)!.Spec, pristine: opponent == SimulationOpponent.Mirror)).ToArray();
        var enemy = force.Select((source, i) => enemies is null ? Deployed(source, roster.Pilot(source)!.Spec, pristine: true)
            : Deployed(enemies[i].Mech, enemies[i].Pilot, pristine: true)).ToArray();
        return Deploy(friendly, enemy, combatSeed, terrain ?? BattlefieldTerrain.Generate(terrainSeed ?? Random.Shared.Next(), biome));
    }

    /// <summary>Fights a mission: the selected mechs as they stand against its opposition, stock and fully supplied,
    /// with pilots skilled by rank, on the biome of the mission's system.</summary>
    /// <summary>A simulated deployment chooses its biome; a mission's comes from its system.</summary>
    public static CombatSimulation DeployMission(ForceRoster roster, IEnumerable<string> selected, MissionOffer offer, int? seed = null, int? terrainSeed = null,
        Biome? biome = null)
    {
        var force = DeployedForce(roster, selected);
        var friendly = force.Select(source => Deployed(source, roster.Pilot(source)!.Spec, pristine: false)).ToArray();
        var enemy = offer.Opposition.Select((item, i) => Stock(DraftCatalog.Mech(item.MechId), MissionTuning.EnemyPilot(item.Rank, i + 1)) with { Group = item.Group }).ToArray();
        var world = biome ?? StarChart.Instance[offer.SystemId].Biome ?? Biome.Valley;
        var combatSeed = seed ?? Random.Shared.Next(); var fieldSeed = terrainSeed ?? Random.Shared.Next();
        if (offer.Kind == MissionKind.Patrol) return DeployPatrol(friendly, enemy, combatSeed, fieldSeed, world, offer.PatrolLocations);
        return Deploy(friendly, enemy, combatSeed, BattlefieldTerrain.Generate(fieldSeed, world));
    }

    private static OwnedMech[] DeployedForce(ForceRoster roster, IEnumerable<string> selected)
    {
        var ids = selected.ToArray();
        if (ids.Length == 0 || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Select one to four different BattleMechs.");
        var force = ids.Select(id =>
        {
            var source = roster.Mechs.SingleOrDefault(item => item.Id == id)
                ?? throw new ArgumentException("Unknown BattleMech.");
            if (UnavailableReason(roster, source) is { } reason) throw new ArgumentException(reason);
            return source;
        }).ToArray();
        // Each lance deploys from its own mechs, at most a lance's worth.
        if (force.GroupBy(item => item.Lance).Any(group => group.Count() > ForceRoster.LanceSize))
            throw new ArgumentException("Select one to four different BattleMechs.");
        return force;
    }

    /// <summary>A mech ready to deploy, keyed by its owned id; enemies are renumbered on deployment.</summary>
    private sealed record DeployedMech(string Id, MechSpec Spec, WarriorSpec Pilot, IEnumerable<LocationCondition> Locations,
        IEnumerable<EquipmentCondition> Equipment, IEnumerable<AmmoCondition> Ammo, int? Lance = null, int? Group = null);

    private static DeployedMech Deployed(OwnedMech source, WarriorSpec pilot, bool pristine) => pristine
        ? Stock(source.Spec, pilot) with { Id = source.Id, Lance = source.Lance }
        : new(source.Id, source.Spec, pilot, source.Locations, source.Equipment, source.Ammo, source.Lance);

    private static DeployedMech Stock(MechSpec spec, WarriorSpec pilot) => new(spec.Id, spec, pilot,
        spec.Locations.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)),
        spec.Weapons.Select(item => item.Id).Concat(spec.Components.Select(item => item.Id)).Concat(spec.Ammo.Select(item => item.Id))
            .Select(id => new EquipmentCondition(id, ComponentCondition.Operational)),
        spec.Ammo.Select(item => new AmmoCondition(item.Id, item.Shots)));

    /// <summary>Each side is spread across its own deployment line, so uneven sides stay centred.</summary>
    private static CombatSimulation Deploy(IReadOnlyList<DeployedMech> friendly, IReadOnlyList<DeployedMech> enemy, int seed, BattlefieldTerrain terrain)
    {
        static float X(int i, int count) => (i - (count - 1) / 2f) * CombatRules.DeploymentSpacing;
        var units = new List<CombatUnit>();
        for (var i = 0; i < Math.Max(friendly.Count, enemy.Count); i++)
        {
            if (i < friendly.Count)
                units.Add(Unit(friendly[i], friendly[i].Id, CombatSide.Friendly, new Vector2(X(i, friendly.Count), CombatRules.DeploymentDepth)));
            if (i < enemy.Count)
                units.Add(Unit(enemy[i], $"enemy-{i + 1}", CombatSide.Enemy, new Vector2(X(i, enemy.Count), -CombatRules.DeploymentDepth)));
        }
        return new CombatSimulation(units, seed, terrain);
        static CombatUnit Unit(DeployedMech mech, string id, CombatSide side, Vector2 position) =>
            new(id, mech.Spec, mech.Pilot, side, mech.Locations, mech.Equipment, mech.Ammo, position, mech.Lance);
    }

    /// <summary>The battlefield facts of a fought mission. Only victory succeeds; defeat, a draw or withdrawing fails.</summary>
    public MissionReport MissionReport()
    {
        if (Outcome == CombatOutcome.Ongoing) throw new InvalidOperationException("The battle is still being fought.");
        return new(Outcome == CombatOutcome.Victory ? MissionOutcome.Success : MissionOutcome.Failure,
            Units.Where(unit => unit.Side == CombatSide.Friendly).Select(unit => new DeployedMechReport(unit.Id,
                unit.Locations.Values.Select(loc => new LocationCondition(loc.Location, loc.Armor, loc.RearArmor, loc.Structure)).ToArray(),
                unit.Equipment.Select(item => new EquipmentCondition(item.Key, item.Value)).ToArray(), unit.WarriorKilled, unit.ArmorLost, unit.Kills)).ToArray(),
            Units.Count(unit => unit.Side == CombatSide.Enemy && !unit.WarriorKilled));
    }

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
        var climate = Terrain.Biome is { } biome ? Biomes.Spec(biome) : null;
        foreach (var unit in Units) unit.Climate = climate;
        History = _history.AsReadOnly(); _random = new Random(seed); RefreshDetection();
        var players = Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Lance!.Value).Distinct().Order().ToArray();
        Players = players.Length > 0 ? players : [Campaign.LeaderSlot];
        Paused = true;
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
                foreach (var observer in target.Extracted ? [] : observers)
                {
                    var radius = CombatRules.DetectionRadius(observer, target);
                    var distance = observer.Position.DistanceSquaredTo(target.Position);
                    if (distance > radius * radius || !Sight(observer, target)) continue;
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
            var offset = new Vector2((i % 2 - (group.Length > 1 ? .5f : 0)) * 36, i / 2 * 36);
            var goal = Terrain.ReachableDestination(group[i].Position, destination + offset);
            for (var attempt = 0; assigned.Any(point => point.DistanceTo(goal) < CombatRules.UnitRadius * 2.1f) && attempt < 100; attempt++)
                goal = Terrain.ReachableDestination(group[i].Position, destination + Vector2.FromAngle(attempt * 2.4f) * (30 + attempt * 2.25f));
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
        if (Replica) { AdvanceReplica(delta); return; }
        if (Paused || Outcome != CombatOutcome.Ongoing) return;
        _accumulator += delta;
        while (_accumulator + 1e-9 >= CombatRules.StepSeconds && Outcome == CombatOutcome.Ongoing)
        { _accumulator -= CombatRules.StepSeconds; Step((float)CombatRules.StepSeconds); }
    }
    private void Step(float delta)
    {
        Elapsed += CombatRules.StepSeconds;
        RefreshDetection();
        if (Patrol is not null) UpdatePatrolAwareness();
        if (EnemyAiEnabled) { UpdateAi(); if (Patrol is not null) UpdatePatrolAi(); }
        StopInvalidFollows();
        foreach (var unit in Units) { unit.Moving = unit.TorsoTurning = false; unit.AngularVelocity = 0; }
        foreach (var unit in Units.Where(item => item.Airborne)) AdvanceJump(unit, delta);
        foreach (var unit in Units.Where(item => item.Active && !item.Airborne)) MoveUnit(unit, delta);
        foreach (var unit in Units.Where(item => !item.Defeated))
        {
            if (!unit.Airborne) unit.JumpRecharge = Math.Max(0, Math.Round(unit.JumpRecharge - CombatRules.StepSeconds, 9));
            unit.AdvanceHeat(CombatRules.StepSeconds);
            foreach (var weapon in unit.Weapons) weapon.Remaining = Math.Max(0, weapon.Remaining - delta);
            unit.InterferenceRemaining = Math.Max(0, Math.Round(unit.InterferenceRemaining - CombatRules.StepSeconds, 9));
        }
        RefreshDetection();
        // Update every waist before selecting rear targets or taking a firing/damage snapshot.
        foreach (var unit in Units.Where(item => item.Active))
        {
            var aim = SelectAimTarget(unit); unit.CurrentTarget = aim?.Id;
            UpdateTorso(unit, aim, delta);
        }
        var shots = new List<CombatShot>();
        // Due burns precede this exchange's discharges. A shooter destroyed since firing has already cancelled its burn.
        for (var i = 0; i < _burns.Count; i++)
        {
            var (discharge, due) = _burns[i];
            if (due > Elapsed + 1e-6) continue;
            _burns.RemoveAt(i--);
            var attacker = Unit(discharge.AttackerId); var target = Unit(discharge.TargetId);
            if (!target.Solid) continue;
            shots.Add(discharge with { From = attacker.Position, To = target.Position, Location = discharge.SecondLocation!.Value,
                Damage = discharge.SecondDamage, GeneratedHeat = 0, TargetHeat = 0, SecondLocation = null, SecondDamage = 0, Burn = true,
                DamageApplied = [], SystemsLost = [], TargetStatus = null });
            attacker.DamageDealt += discharge.SecondDamage;
        }
        // Shells and missile salvos always land, even after their shooter is destroyed. A wreck takes the explosion without damage.
        for (var i = 0; i < _projectiles.Count; i++)
        {
            var (discharge, due) = _projectiles[i];
            if (due > Elapsed + 1e-6) continue;
            _projectiles.RemoveAt(i--);
            var wreck = !Unit(discharge.TargetId).Solid;
            shots.Add(discharge with { To = Unit(discharge.TargetId).Position, Damage = wreck ? 0 : discharge.Damage, Splashes = wreck ? [] : discharge.Splashes, GeneratedHeat = 0,
                Impact = true, Wreck = wreck, DamageApplied = [], SystemsLost = [], TargetStatus = null });
            if (!wreck) Unit(discharge.AttackerId).DamageDealt += discharge.TotalDamage;
        }
        foreach (var unit in Units.Where(item => item.Active && !WindingDown))
        {
            var mainTarget = unit.CurrentTarget is { } main ? Unit(main) : null;
            var rearTarget = SelectRearTarget(unit); unit.CurrentRearTarget = rearTarget?.Id;
            foreach (var (weapon, target) in SelectHeatVolley(unit, mainTarget, rearTarget))
            {
                // A shared bin emptied by an earlier weapon in this volley withholds the rest; they remain ready.
                if (weapon.Spec.AmmoKind is { } ammo && !unit.ConsumeAmmo(ammo)) continue;
                var chance = TerrainHitChance(unit, target, weapon);
                var spotter = IndirectSpotter(unit, target, weapon);
                var salvo = CombatRules.FiresSalvo(weapon.Spec.Kind);
                // Each missile rolls its own hit and location at the salvo's chance.
                CombatMissile[] missiles = salvo ? Enumerable.Range(0, weapon.Spec.DamageProfile.ProjectileCount)
                    .Select(_ => { var draw = _random.NextDouble(); return new CombatMissile(draw < chance, draw, RollLocation()); }).ToArray() : [];
                var roll = salvo ? missiles.Min(missile => missile.Roll) : _random.NextDouble();
                var hit = roll < chance;
                var location = salvo ? (missiles.FirstOrDefault(missile => missile.Hit) ?? missiles[0]).Location : RollLocation();
                var laser = hit && CombatRules.IsLaser(weapon.Spec.Kind);
                var shell = CombatRules.IsAutocannon(weapon.Spec.Kind);
                var ppc = CombatRules.IsPpc(weapon.Spec.Kind);
                var bullets = CombatRules.IsMachineGun(weapon.Spec.Kind);
                // A burn only spreads to an adjacent location across a target in motion at the moment it is struck.
                var second = !laser ? (MechLocation?)null : target.InMotion ? RollAdjacentLocation(location) : location;
                var damage = !hit ? 0 : laser ? CombatRules.LaserFirstHalf(weapon.Damage)
                    : salvo ? missiles.Count(missile => missile.Hit) * weapon.Spec.DamageProfile.PerProjectile : weapon.Damage;
                var distance = unit.Position.DistanceTo(target.Position);
                var shot = new CombatShot(unit.Id, target.Id, unit.Position, target.Position, weapon.Spec.Name, hit, location,
                    damage, weapon.Spec.Heat, hit ? CombatRules.FlamerHeat(weapon.Spec) : 0)
                {
                    Chance = chance, Roll = roll, WeaponId = weapon.Spec.Id, Mount = DraftCatalog.Abbreviation(weapon.Spec.Location) + (weapon.Spec.Rear ? " rear" : ""),
                    Sequence = ++_sequence, SecondLocation = second, SecondDamage = laser ? weapon.Damage - damage : 0,
                    FlightSeconds = shell ? CombatRules.ShellFlightSeconds(distance) : bullets ? CombatRules.BulletFlightSeconds(distance) : ppc ? CombatRules.PpcFlightSeconds(distance, weapon.Spec) : CombatRules.IsSrm(weapon.Spec.Kind) ? CombatRules.SrmFlightSeconds(distance)
                        : salvo ? CombatRules.MissileFlightSeconds(distance) : 0,
                    Missiles = missiles, MissileDamage = salvo ? weapon.Spec.DamageProfile.PerProjectile : 0,
                    Indirect = spotter is not null, SpotterId = spotter?.Id, Splashes = ppc && hit ? RollSplash(location, CombatRules.PpcSplash(weapon.Damage)) : []
                };
                shots.Add(shot);
                if (Patrol is not null) HeardFire(unit, target, shot);
                if (laser) _burns.Add((shot, Elapsed + CombatRules.LaserSecondHalfDelay));
                if (shot.InFlight && hit) _projectiles.Add((shot, Elapsed + shot.FlightSeconds));
                weapon.Remaining = CombatRules.Cooldown(weapon.Spec); unit.ShotsFired++;
                if (hit) { unit.Hits++; if (!shot.InFlight) unit.DamageDealt += damage; }
            }
        }
        // All shooters and rolls are captured before any damage or casualty can invalidate a return shot.
        for (var i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            if (!shot.Hit || shot.InFlight || shot.Wreck) continue;
            var target = Unit(shot.TargetId);
            var before = target.Locations.Values.ToDictionary(part => part.Location,
                part => (part.Armor, part.RearArmor, part.Structure));
            var status = target.Status;
            if (shot.Missiles.Count == 0) target.ApplyDamage(shot.Location, shot.Damage, shot.From);
            else foreach (var missile in shot.Missiles.Where(item => item.Hit)) target.ApplyDamage(missile.Location, shot.MissileDamage, shot.From);
            if (shot.Splash > 0)
            {
                foreach (var splash in shot.Splashes.GroupBy(item => item.Location)) target.ApplyDamage(splash.Key, splash.Sum(item => item.Damage), shot.From);
                target.InterferenceRemaining = CombatRules.InterferenceSeconds;
            }
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
            // The hit that disables or destroys an enemy earns its shooter the kill.
            if (!CombatUnit.IsDefeated(status) && target.Defeated && Unit(shot.AttackerId).Side != target.Side) Unit(shot.AttackerId).Kills++;
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
            _history.Add(new CombatLogEntry(Elapsed, shot, LogUnit(Unit(shot.AttackerId)), LogUnit(Unit(shot.TargetId)),
                shot.SpotterId is { } spotter ? LogUnit(Unit(spotter)) : null));
            ShotFired?.Invoke(shot);
        }
        StopInvalidFollows();
        foreach (var unit in Units.Where(item => !item.Active))
        { unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0; unit.CurrentTarget = unit.CurrentRearTarget = null; }
        // A burn lands in the step it comes due even if its shooter dies in that step; later destruction cuts it off.
        _burns.RemoveAll(burn => Unit(burn.Discharge.AttackerId).Status == CombatStatus.Destroyed);
        RefreshDetection();
        if (Patrol is not null) { UpdatePatrolObjectives(); return; }
        var friendly = Units.Any(item => item.Side == CombatSide.Friendly && !item.Defeated);
        var enemy = Units.Any(item => item.Side == CombatSide.Enemy && !item.Defeated);
        // The first defeat starts an irrevocable wind-down; effects already in flight may still turn it into a draw.
        if (WindDownEnds is null && (!friendly || !enemy)) WindDownEnds = Elapsed + CombatRules.WindDownSeconds;
        if (WindDownEnds is { } ends && Elapsed + 1e-6 >= ends)
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
        foreach (var unit in Units.Where(item => item.Side == CombatSide.Enemy && item.Active && item.Group is null))
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
            var yielding = away.Length() < CombatRules.FollowDistance - 12;
            if (away.LengthSquared() < .9f) away = -leader.Facing;
            // Leave room when the leader turns back through the group, rather than pinning it between followers.
            if (yielding || away.Length() > CombatRules.FollowDistance + 6)
                foreach (var angle in new[] { 0f, .52f, -.52f, 1.05f, -1.05f, 1.57f, -1.57f, Mathf.Pi })
                {
                    var followGoal = Terrain.ReachableDestination(unit.Position,
                        leader.Position + away.Normalized().Rotated(angle) * CombatRules.FollowDistance);
                    if (Units.Any(other => other != unit && other.Solid
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
                    if (away.Length() < .3f) away = unit.Side == CombatSide.Friendly ? Vector2.Down : Vector2.Up;
                    if (!Sight(unit, target))
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
                    else if (Math.Abs(away.Length() - unit.PreferredDistance) > 12)
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
        if (goal is { } desired && unit.Position.DistanceTo(desired) >= 6)
        {
            // A slightly stale route is kept while travelling, but its end is refreshed so a moving goal cannot strand the unit short of it.
            var finishing = unit.Route.Count > 0 && unit.Position.DistanceTo(unit.Route[^1]) < 16.5f && unit.RouteGoal != desired;
            if (unit.Route.Count == 0 || !float.IsFinite(unit.RouteGoal.X) || unit.RouteGoal.DistanceTo(desired) > 22.5f || finishing)
            {
                unit.Route = Terrain.FindPath(unit.Position, desired); unit.RouteIndex = 0; unit.RouteGoal = desired;
            }
            while (unit.RouteIndex < unit.Route.Count - 1 && unit.Position.DistanceTo(unit.Route[unit.RouteIndex]) < 16.5f)
                unit.RouteIndex++;
            if (AiJump(unit, desired)) return;
            if (unit.RouteIndex < unit.Route.Count) goal = unit.Route[unit.RouteIndex];
            else goal = null;
        }
        var remaining = goal is { } point ? point - unit.Position : Vector2.Zero;
        if (goal is not null && remaining.Length() < 6)
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
                var arrivalSpeed = Mathf.Sqrt(2 * unit.Braking * Math.Max(0, remaining.Length() - 5.4f));
                desiredSpeed = Math.Min(limit * Mathf.Cos(error), arrivalSpeed) * (reverse ? -1 : 1);
            }
        }
        else if (Math.Abs(unit.CurrentSpeed) < .03f && SelectAimTarget(unit) is { } target)
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
            {
                nextSpeed = 0; // Contact with terrain, solid mechs or arena edges is a hard constraint.
                // A unit wedged against an obstacle replans from where it stands rather than pressing on forever.
                unit.BlockedSeconds += delta;
                if (unit.BlockedSeconds > 1.5f) { unit.Route = Array.Empty<Vector2>(); unit.BlockedSeconds = 0; }
            }
            else { unit.Position = candidate; unit.Moving = true; unit.BlockedSeconds = 0; }
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
        var lookAhead = Math.Clamp(stopping + 7.5f, 10.5f, 45);
        foreach (var angle in new[] { 0f, .52f, -.52f, 1.05f, -1.05f, 1.57f, -1.57f })
        {
            var steered = direction.Rotated(angle);
            var candidate = unit.Position + steered * lookAhead;
            if (CombatRules.Clamp(candidate).DistanceSquaredTo(candidate) < 1e-10f && !Blocked(unit, candidate))
                return steered;
        }
        return direction;
    }
    // A jumping mech occupies only its landing point.
    private bool Blocked(CombatUnit unit, Vector2 candidate) => !Terrain.CanTraverse(unit.Position, candidate) || Units.Any(other => other != unit && other.Solid
        && DistanceToSegment(other.Airborne ? other.JumpTo : other.Position, unit.Position, candidate) < CombatRules.UnitRadius * 2);
    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var t = segment.LengthSquared() < 1e-10f ? 0 : Math.Clamp((point - from).Dot(segment) / segment.LengthSquared(), 0, 1);
        return point.DistanceTo(from + segment * t);
    }
    public CombatUnit? SelectTarget(CombatUnit unit)
    {
        if (Passive(unit)) return null;
        bool Engageable(CombatUnit target) => target.Side != unit.Side && !target.Defeated && Detected(unit.Side, target.Id)
            && Sight(unit, target)
            && unit.Weapons.Any(weapon => !weapon.Spec.Rear && weapon.Enabled && unit.WeaponUsable(weapon) && unit.Position.DistanceTo(target.Position) <= weapon.LongRange);
        if (unit.AttackTarget is { } attack && unit.Order == CombatOrder.Attack && Engageable(Unit(attack))) return Unit(attack);
        if (unit.Firing == FiringMode.HoldFire) return null;
        if (unit.PriorityTarget is { } priority && Engageable(Unit(priority))) return Unit(priority);
        if (unit.CurrentTarget is { } current && Engageable(Unit(current))) return Unit(current);
        return Units.Where(Engageable).OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
    }
    public double TerrainHitChance(CombatUnit attacker, CombatUnit target, CombatWeapon weapon) =>
        Sight(attacker, target)
            ? HitChance(attacker, target, weapon, Terrain.VegetationModifier(attacker.Position, target.Position))
            : IndirectSpotter(attacker, target, weapon) is { } spotter ? HitChance(attacker, target, weapon,
                Terrain.VegetationModifier(spotter.Position, target.Position) + CombatRules.IndirectFireModifier) : 0;
    /// <summary>Clear sight, or an indirect LRM salvo guided by a spotter.</summary>
    public bool LineOfFire(CombatUnit unit, CombatUnit target, CombatWeapon weapon) =>
        Sight(unit, target) || IndirectSpotter(unit, target, weapon) is not null;
    /// <summary>
    /// LRMs fire indirectly only at an Attack or priority target the shooter cannot see itself,
    /// guided by an active friendly with its own clear sight: least vegetation to the target, then nearest.
    /// </summary>
    public CombatUnit? IndirectSpotter(CombatUnit unit, CombatUnit target, CombatWeapon weapon)
    {
        if (!CombatRules.IsLrm(weapon.Spec.Kind) || !(unit.Order == CombatOrder.Attack && unit.AttackTarget == target.Id
            || unit.PriorityTarget == target.Id) || Sight(unit, target)) return null;
        return Units.Where(item => item != unit && item.Side == unit.Side && item.Active && Sight(item, target))
            .OrderBy(item => Terrain.VegetationModifier(item.Position, target.Position))
            .ThenBy(item => item.Position.DistanceSquaredTo(target.Position)).FirstOrDefault();
    }
    public static double HitChance(CombatUnit attacker, CombatUnit target, CombatWeapon weapon, int terrainModifier = 0)
    {
        var distance = attacker.Position.DistanceTo(target.Position);
        if (distance > weapon.LongRange || !attacker.WeaponUsable(weapon) || !CombatRules.InFiringArc(attacker, target, weapon)) return 0;
        var range = distance <= weapon.ShortRange ? 0 : distance <= weapon.MediumRange ? 2 : 4;
        var minimum = distance < weapon.MinimumRange
            ? (int)Math.Ceiling((weapon.MinimumRange - distance) / CombatRules.WeaponRangeScale) : 0;
        var threshold = attacker.Warrior.Gunnery + CombatRules.AimModifier + range + minimum + terrainModifier + attacker.HeatAimModifier
            + attacker.InterferenceAimModifier
            // Reversing spoils the attacker's own aim as badly as running; as a target it still counts as walking.
            // Jumping replaces either side's ground movement modifier.
            + (attacker.Airborne ? CombatRules.JumpToHitModifier
                : attacker.Moving ? attacker.Movement == MovementMode.Run || attacker.CurrentSpeed < 0 ? 2 : 1 : 0)
            + (target.Airborne ? CombatRules.JumpToHitModifier : target.Moving ? target.Movement == MovementMode.Run ? 2 : 1 : 0);
        var successes = 0;
        for (var a = 1; a <= 6; a++) for (var b = 1; b <= 6; b++) if (a + b >= threshold) successes++;
        return successes / 36.0;
    }
    private MechLocation RollAdjacentLocation(MechLocation location)
    {
        var adjacent = CombatRules.AdjacentLocations(location);
        var roll = _random.Next(adjacent.Sum(CombatRules.LocationWeight));
        foreach (var candidate in adjacent)
            if ((roll -= CombatRules.LocationWeight(candidate)) < 0) return candidate;
        throw new InvalidOperationException();
    }
    // Each splash chunk lands on an equally likely adjacent non-head location.
    private CombatSplash[] RollSplash(MechLocation location, int damage)
    {
        var adjacent = CombatRules.PpcSplashLocations(location).ToArray();
        return Enumerable.Range(0, (damage + CombatRules.SplashChunk - 1) / CombatRules.SplashChunk)
            .Select(i => new CombatSplash(adjacent[_random.Next(adjacent.Length)], Math.Min(CombatRules.SplashChunk, damage - i * CombatRules.SplashChunk))).ToArray();
    }
    private MechLocation RollLocation() => (_random.Next(1, 7) + _random.Next(1, 7)) switch
    {
        2 or 7 => MechLocation.CenterTorso, 3 or 4 => MechLocation.RightArm, 5 => MechLocation.RightLeg,
        6 => MechLocation.RightTorso, 8 => MechLocation.LeftTorso, 9 => MechLocation.LeftLeg,
        10 or 11 => MechLocation.LeftArm, _ => MechLocation.Head
    };
}
