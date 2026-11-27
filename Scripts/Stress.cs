using Godot;

namespace Mech;

/// <summary>How shaken a MechWarrior is. Stressed plays every skill but Grit one rating worse, Panicked two.</summary>
public enum StressLevel { Calm, Steady, Excited, Stressed, Panicked }

/// <summary>One nerve check: 2d6 + Grit + modifiers. 11 or less calms the pilot a step, 15 or more stresses them a step, and a
/// Panicked pilot who reaches 15 or more again has an even chance to break and eject; otherwise they hold on. A pilot whose mech is
/// undamaged this battle grows no worse than Excited, and one whose mech has lost no internal structure never breaks.</summary>
public sealed record CombatNerveCheck(int Roll, int Grit, int Modifier, string Modifiers, StressLevel Before, StressLevel After, bool Broke,
    bool HeldOn = false)
{
    public int Result => Roll + Grit + Modifier;
    public string Text => $"Nerve check · rolled {Roll} + Grit {Grit}" + (Modifiers.Length > 0 ? $" ({Modifiers})" : "")
        + $" = {Result} · " + (Broke ? "BREAKS AND EJECTS" : HeldOn ? "Panicked · holds on" : After == Before ? Before.ToString() : $"{Before} → {After}");
}

/// <summary>A battle event that is not a shot: stress changes, surrender, ejection and fleeing. Friendly-only entries are hidden
/// from the combat log when they concern an enemy, and appear only in the debug log.</summary>
public sealed record CombatEvent(double Time, string UnitId, string Text, bool Public = true, string DebugText = "")
{
    public string Line(bool debug) => $"{TimeSpan.FromSeconds(Time):mm\\:ss\\.ff} ● {(debug && DebugText.Length > 0 ? DebugText : Text)}";
}

public static partial class CombatRules
{
    public const double NerveCheckSeconds = 5;
    /// <summary>A result at or below this calms the pilot one step; at or above <see cref="StressingResult"/> stresses them one step.</summary>
    public const int CalmingResult = 11, StressingResult = 15;
    /// <summary>Each full 10 heat adds 1 to the nerve check.</summary>
    public const int NerveHeatStep = 10;
    /// <summary>Each full 200 damage (tenths of a stock point) taken since the last check adds 1.</summary>
    public const int NerveDamageStep = 200;
    /// <summary>The most a stress level worsens a skill rating.</summary>
    public const int MaxStressPenalty = 2;
    /// <summary>The chance a Panicked pilot whose check would stress them further breaks and ejects.</summary>
    public const double PanicEjectChance = .5;
    public static int StressPenalty(StressLevel level) => level switch { StressLevel.Stressed => 1, StressLevel.Panicked => 2, _ => 0 };
    /// <summary>An enemy considers surrender or ejection once its center torso keeps half its stock structure or less.</summary>
    public static bool CenterTorsoCrippled(CombatUnit unit) =>
        unit.Locations[MechLocation.CenterTorso].Structure * 2 <= unit.Spec.Locations.Single(item => item.Location == MechLocation.CenterTorso).Structure;
    /// <summary>A fleeing mech counts as clear of an enemy this far beyond its longest weapon range.</summary>
    public const float FleeMargin = 60;
    /// <summary>A fleeing mech leaves the battle this close to the arena edge.</summary>
    public const float FleeEdge = 30;
}

public sealed partial class CombatUnit
{
    public StressLevel Stress { get; internal set; }
    /// <summary>Seconds to this pilot's next nerve check.</summary>
    internal double NerveRemaining { get; set; }
    internal int NerveDamage { get; set; }
    internal bool ShotAtSinceCheck { get; set; }
    internal bool KnockedDownSinceCheck { get; set; }
    internal bool HeadDamagedSinceCheck { get; set; }
    internal int InjuriesAtLastCheck { get; set; }
    internal int AlliesDefeatedAtLastCheck { get; set; }
    public int StressPenalty => CombatRules.StressPenalty(Stress);
    /// <summary>Armor or structure lost this battle: a pilot cannot grow Stressed without it.</summary>
    public bool DamagedThisBattle => ArmorLost > 0 || StructureLost > 0;
    /// <summary>Internal structure lost this battle: a Panicked pilot cannot break and eject without it.</summary>
    public bool InternallyDamagedThisBattle => StructureLost > 0;
    /// <summary>Skills as the pilot plays them now: worse while Stressed or Panicked. Grit itself is never affected.</summary>
    public int Gunnery => Warrior.Gunnery + StressPenalty;
    public int Piloting => Warrior.Piloting + StressPenalty;
    public int Sensors => Warrior.Sensors + StressPenalty;

    /// <summary>The MechWarrior has left the cockpit: the mech is disabled and the pilot survives, injured.</summary>
    public bool Ejected { get; internal set; }
    /// <summary>An enemy whose surrender was accepted: defeated, powered down, and not to be targeted or salvaged.</summary>
    public bool Surrendered { get; internal set; }
    /// <summary>Has offered to surrender this battle; a mech offers once.</summary>
    public bool SurrenderOffered { get; internal set; }
    /// <summary>Running from the battle: after a refused surrender, or after a rout (<see cref="Routed"/>).</summary>
    public bool Fleeing { get; internal set; }
    /// <summary>Left the battle over the arena edge while fleeing.</summary>
    public bool Fled { get; internal set; }
    /// <summary>Every weapon lost. An unarmed mech is not disabled: it can still walk off, flee or surrender.</summary>
    public bool Unarmed => !Weapons.Any(WeaponIntact);
    /// <summary>Still in the fight: on the field, able to shoot and not running away.</summary>
    public bool Fighting => !Defeated && !Unarmed && !Fleeing;

    /// <summary>The pilot leaves the cockpit, injured. A head already lost or a struck cockpit has no pilot left to eject.</summary>
    internal bool Eject()
    {
        if (Ejected || !Intact(MechLocation.Head) || CockpitHit || Surrendered || Fled || Extracted) return false;
        Ejected = true; Injuries++; Fleeing = false;
        Moving = false; CurrentSpeed = AngularVelocity = 0; Order = CombatOrder.Hold; AttackTarget = FollowTarget = null;
        CurrentTarget = CurrentRearTarget = null; Route = Array.Empty<Vector2>();
        return true;
    }

    /// <summary>" · Panicked" for a stressed friendly, " · Fleeing" for a fleeing mech, empty otherwise.</summary>
    public string ConductText() => Fleeing && !Defeated ? " · Fleeing" : "";
}

public sealed partial class CombatSimulation
{
    private const int StressSeedSalt = 0x1b873593;
    private readonly Random _stressRandom;
    /// <summary>Replaces the 2d6 nerve roll, for checks that need a particular result.</summary>
    internal Func<int>? NerveRollOverride { get; set; }
    /// <summary>Replaces the even chance a Panicked pilot breaks, for checks that need a particular outcome.</summary>
    internal Func<bool>? PanicEjectOverride { get; set; }
    /// <summary>Whether nerve checks are made at all; checks that need steady pilots turn them off.</summary>
    internal bool NerveChecksEnabled { get; set; } = true;
    /// <summary>Whether an enemy's surrender waits for the leader's answer, pausing the battle. Without prompts (headless runs),
    /// every offer is refused at once.</summary>
    public bool PromptSurrender { get; set; }
    private readonly List<string> _surrenderOffers = [];
    /// <summary>Enemy mechs waiting for an answer to their surrender, oldest first. The battle stays paused while any wait.</summary>
    public IReadOnlyList<string> SurrenderOffers => _surrenderOffers;
    private readonly List<CombatEvent> _events = [];
    public IReadOnlyList<CombatEvent> Events => _events;

    private void Note(CombatUnit unit, string text, bool isPublic = true)
    {
        var log = LogUnit(unit);
        _events.Add(new(Elapsed, unit.Id, $"{(log.Visibility == ContactLevel.Lost ? $"{SlotLabel(unit)} unseen mech" : log.Label(false))} {text}",
            isPublic, $"{log.Label(true)} {text}"));
    }

    private void StartNerveClocks()
    {
        foreach (var unit in Units) unit.NerveRemaining = _stressRandom.NextDouble() * CombatRules.NerveCheckSeconds;
    }

    /// <summary>Mechs of a side out of the fight, for nerve checks: extracted mechs left safely, so they do not count.</summary>
    private int AlliesDefeated(CombatUnit unit) => Units.Count(item => item != unit && item.Side == unit.Side && item.Defeated && !item.Extracted && !item.Inbound);

    /// <summary>The nerve check's modifiers now, as a total and the text the log shows.</summary>
    internal (int Modifier, string Text) NerveModifiers(CombatUnit unit)
    {
        var parts = new List<string>(); var total = 0;
        void Add(int value, string name) { if (value != 0) { total += value; parts.Add($"{name} {value:+0;-0}"); } }
        Add((int)(unit.Heat / CombatRules.NerveHeatStep), "heat");
        Add(unit.Locations.Values.Count(part => part.Structure == 0), "locations lost");
        Add(unit.Injuries, "injuries");
        Add(unit.Injuries - unit.InjuriesAtLastCheck, "new injuries");
        Add(unit.NerveDamage / CombatRules.NerveDamageStep, "damage");
        Add(unit.KnockedDownSinceCheck ? 1 : 0, "knocked down");
        Add(unit.HeadDamagedSinceCheck ? 1 : 0, "head hit");
        Add(unit.ShotAtSinceCheck ? 1 : -1, unit.ShotAtSinceCheck ? "under fire" : "not under fire");
        var defeated = AlliesDefeated(unit);
        // Any loss weighs on the pilot once, however many; each fresh loss weighs once more on the next check.
        Add(defeated > 0 ? 1 : 0, "allies down");
        Add(Math.Max(0, defeated - unit.AlliesDefeatedAtLastCheck), "ally just lost");
        var own = Units.Count(item => item.Side == unit.Side && item.Fighting);
        var seen = Units.Count(item => item.Side != unit.Side && !item.Defeated && Detected(unit.Side, item.Id));
        Add(seen > own ? 1 : 0, "outnumbered");
        return (total, string.Join(", ", parts));
    }

    /// <summary>Counts down every pilot's nerve clock and makes the checks that come due. A passive patrol group, unaware of the
    /// battle, makes none until it wakes.</summary>
    private void AdvanceNerve()
    {
        foreach (var unit in Units.Where(item => !item.Defeated))
        {
            unit.NerveRemaining = Math.Round(unit.NerveRemaining - CombatRules.StepSeconds, 9);
            if (unit.NerveRemaining > 1e-9) continue;
            unit.NerveRemaining += CombatRules.NerveCheckSeconds;
            if (NerveChecksEnabled && !Passive(unit)) NerveCheck(unit);
        }
    }

    internal CombatNerveCheck NerveCheck(CombatUnit unit)
    {
        var (modifier, text) = NerveModifiers(unit);
        var roll = NerveRollOverride?.Invoke() ?? _stressRandom.Next(1, 7) + _stressRandom.Next(1, 7);
        var result = roll + unit.Warrior.Grit + modifier;
        var before = unit.Stress;
        var pushed = before == StressLevel.Panicked && result >= CombatRules.StressingResult;
        // A Panicked pilot whose mech still has all its internal structure holds on without a chance to break.
        var broke = pushed && unit.InternallyDamagedThisBattle
            && (PanicEjectOverride?.Invoke() ?? _stressRandom.NextDouble() < CombatRules.PanicEjectChance);
        if (result <= CombatRules.CalmingResult && before > StressLevel.Calm) unit.Stress = before - 1;
        else if (result >= CombatRules.StressingResult && before < StressLevel.Panicked
            && (before != StressLevel.Excited || unit.DamagedThisBattle)) unit.Stress = before + 1;
        unit.Use = unit.Use with
        {
            PressuredNerveChecks = unit.Use.PressuredNerveChecks + (result >= SkillGrowth.PressuredNerveResult ? 1 : 0),
            HeldOn = unit.Use.HeldOn + (pushed && !broke ? 1 : 0), Stressed = unit.Use.Stressed || unit.Stress >= StressLevel.Stressed,
        };
        unit.NerveDamage = 0; unit.ShotAtSinceCheck = unit.KnockedDownSinceCheck = unit.HeadDamagedSinceCheck = false;
        unit.InjuriesAtLastCheck = unit.Injuries; unit.AlliesDefeatedAtLastCheck = AlliesDefeated(unit);
        var check = new CombatNerveCheck(roll, unit.Warrior.Grit, modifier, text, before, unit.Stress, broke, pushed && !broke);
        // Every check goes to the debug log with its roll and modifiers; the combat log shows only a friendly's change of level,
        // since enemy stress is never shown to the player.
        Note(unit, check.Text, unit.Side == CombatSide.Friendly && (unit.Stress != before || pushed));
        if (unit.Stress == StressLevel.Panicked && !unit.PanicHighlighted) { unit.PanicHighlighted = true; Highlight(unit, $"{Name(unit)} panicked."); }
        if (broke) Eject(unit, "panics and ejects", $"{unit.Warrior.Callsign} panicked and ejected from the {unit.Spec.DisplayName}.");
        // A crippled center torso is settled by surrender or ejection; any other badly damaged enemy may rout.
        else if (unit.Side == CombatSide.Enemy && EnemyAiEnabled)
        {
            if (CombatRules.CenterTorsoCrippled(unit)) ConsiderSurrender(unit); else ConsiderRout(unit);
        }
        return check;
    }

    /// <summary>The fighting strength a side's pilot sees: its own side's fighting mechs, and the enemy mechs it has detected, each
    /// by tonnage × the share of its stock structure left.</summary>
    internal (float Own, float Enemy) SideStrength(CombatSide side) =>
        (Units.Where(item => item.Side == side && item.Fighting).Sum(CombatRules.Strength),
            Units.Where(item => item.Side != side && item.Fighting && Detected(side, item.Id)).Sum(CombatRules.Strength));
    internal bool ExpectsDefeat(CombatSide side) { var (own, enemy) = SideStrength(side); return enemy > own; }

    /// <summary>After an enemy's nerve check: a crippled center torso makes it surrender if it expects to lose, and eject if it
    /// expects to win. A mech whose offer was refused fights on.</summary>
    private void ConsiderSurrender(CombatUnit unit)
    {
        if (unit.Defeated || !CombatRules.CenterTorsoCrippled(unit)) return;
        if (!ExpectsDefeat(unit.Side)) Eject(unit, "ejects", $"{unit.Warrior.Callsign} ejected from the crippled {unit.Spec.DisplayName}.");
        else if (!unit.SurrenderOffered) OfferSurrender(unit);
    }

    /// <summary>Enemies who lose their last weapon offer surrender at once.</summary>
    private void OfferUnarmedSurrenders()
    {
        foreach (var unit in Units.Where(item => item.Side == CombatSide.Enemy && !item.Defeated && item.Unarmed && !item.SurrenderOffered).ToArray())
            OfferSurrender(unit);
    }

    private void OfferSurrender(CombatUnit unit)
    {
        unit.SurrenderOffered = true;
        Note(unit, "offers to surrender"); Highlight(unit, $"{Name(unit)} offered to surrender.");
        if (!PromptSurrender) { AnswerSurrender(unit.Id, false); return; }
        unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0; unit.Order = CombatOrder.Hold; unit.AttackTarget = null;
        unit.Route = Array.Empty<Vector2>();
        _surrenderOffers.Add(unit.Id);
    }

    /// <summary>Accepts or refuses a surrender. Accepted, the mech powers down and its pilot becomes a hostage; refused, an unarmed
    /// mech flees and an armed one fights on.</summary>
    internal void AnswerSurrender(string unitId, bool accept)
    {
        _surrenderOffers.Remove(unitId);
        var unit = Unit(unitId);
        if (unit.Defeated) return;
        if (accept)
        {
            unit.Surrendered = true; unit.Fleeing = false;
            unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0; unit.Order = CombatOrder.Hold;
            unit.AttackTarget = unit.FollowTarget = unit.CurrentTarget = unit.CurrentRearTarget = null;
            Note(unit, "surrender accepted · pilot taken hostage");
            Highlight(unit, $"{Name(unit)}'s surrender was accepted: {unit.Warrior.Callsign} was taken hostage.");
            return;
        }
        Note(unit, "surrender refused"); Highlight(unit, $"{Name(unit)}'s surrender was refused.");
        if (unit.Unarmed) { unit.Fleeing = true; Note(unit, "flees"); }
    }

    /// <summary>A player's Eject order.</summary>
    internal void EjectOrdered(CombatUnit unit) { if (!unit.Defeated) Eject(unit, "ejects", $"{unit.Warrior.Callsign} ejected from the {unit.Spec.DisplayName} on orders."); }

    private void Eject(CombatUnit unit, string text, string highlight)
    {
        if (unit.Eject()) { _surrenderOffers.Remove(unit.Id); Note(unit, text); Highlight(unit, highlight); }
    }

    /// <summary>Notes the automatic ejections the step's damage caused: a destroyed center torso throws the pilot clear.</summary>
    private void NoteAutoEjections(IEnumerable<CombatUnit> ejected)
    {
        foreach (var unit in ejected) Note(unit, "center torso destroyed · pilot ejects");
    }

    /// <summary>Fleeing mechs run first to the nearest point beyond every known enemy's reach, then on away from the enemy to the
    /// arena edge, where they leave the battle.</summary>
    private void UpdateFleeing()
    {
        foreach (var unit in Units.Where(item => item.Fleeing && !item.Defeated))
        {
            var limit = CombatRules.ArenaHalfSize - CombatRules.UnitRadius - CombatRules.FleeEdge;
            if (Math.Abs(unit.Position.X) >= limit || Math.Abs(unit.Position.Y) >= limit)
            {
                unit.Fled = true; unit.Fleeing = false; unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0;
                unit.Order = CombatOrder.Hold; unit.CurrentTarget = unit.CurrentRearTarget = null;
                Note(unit, "has fled the battle"); Highlight(unit, $"{Name(unit)} fled the battle.");
                continue;
            }
            unit.DesiredSpeed = Math.Max(CombatRules.MinimumDesiredSpeed, unit.UnheatedRunningSpeed);
            if (Elapsed < unit.NextFiringSearch && unit.Order == CombatOrder.Move) continue;
            unit.NextFiringSearch = Elapsed + 1;
            unit.Order = CombatOrder.Move; unit.AttackTarget = unit.FollowTarget = null;
            unit.Destination = FleeGoal(unit); unit.Route = Array.Empty<Vector2>();
        }
    }

    /// <summary>Where a fleeing mech heads now: the nearest reachable point clear of every known enemy's longest weapon range, or,
    /// once clear, the arena edge directly away from the enemies it knows of.</summary>
    internal Vector2 FleeGoal(CombatUnit unit)
    {
        var threats = Units.Where(item => item.Side != unit.Side && item.Fighting && _contacts[unit.Side].ContainsKey(item.Id))
            .Select(item => (Position: _contacts[unit.Side][item.Id].LastKnownPosition,
                Reach: item.Weapons.Where(item.WeaponIntact).Max(weapon => weapon.LongRange) + CombatRules.FleeMargin)).ToArray();
        bool Clear(Vector2 point) => threats.All(threat => threat.Position.DistanceTo(point) >= threat.Reach);
        var center = threats.Length == 0 ? Vector2.Zero : threats.Aggregate(Vector2.Zero, (sum, threat) => sum + threat.Position) / threats.Length;
        var away = unit.Position - center;
        if (away.LengthSquared() < 1) away = unit.Side == CombatSide.Enemy ? Vector2.Up : Vector2.Down;
        if (!Clear(unit.Position))
        {
            for (var radius = 100f; radius <= CombatRules.ArenaHalfSize * 2; radius += 100)
            {
                var best = Enumerable.Range(0, 24).Select(i => CombatRules.Clamp(unit.Position + Vector2.FromAngle(i * Mathf.Tau / 24) * radius))
                    .Where(Clear).OrderBy(point => point.DistanceSquaredTo(unit.Position)).Cast<Vector2?>().FirstOrDefault();
                if (best is { } point) return Terrain.ReachableDestination(unit.Position, point);
            }
        }
        // The edge straight away from the enemy: scale the heading until it meets the arena boundary.
        var heading = away.Normalized();
        var edge = CombatRules.ArenaHalfSize - CombatRules.UnitRadius;
        var scale = Math.Min(Math.Abs(heading.X) > 1e-4f ? (edge - Math.Sign(heading.X) * unit.Position.X) / Math.Abs(heading.X) : float.MaxValue,
            Math.Abs(heading.Y) > 1e-4f ? (edge - Math.Sign(heading.Y) * unit.Position.Y) / Math.Abs(heading.Y) : float.MaxValue);
        return Terrain.ReachableDestination(unit.Position, CombatRules.Clamp(unit.Position + heading * Math.Max(0, scale)));
    }
}
