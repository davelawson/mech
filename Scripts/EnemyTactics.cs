using Godot;

namespace Mech;

/// <summary>How an AI mech with an Attack order moves: keeping its preferred distance as an ordinary Attack does, keeping it toward
/// the goal the tactics worked out at their last decision (so it is not worked out again every step), heading for a strafing point,
/// or standing still.</summary>
internal enum AiMovement { Range, Keep, Point, Still }

/// <summary>Tactical values for AI-controlled groups. Distances are metres, time is seconds.</summary>
public static partial class CombatRules
{
    /// <summary>A group in contact reconsiders its targets and movement this often, and at once on first contact.</summary>
    public const double TacticsSeconds = .5;
    /// <summary>A member farther than this from its group's anchor, with no mate this close, is separated and regroups.</summary>
    public const float CohesionRadius = 300;
    /// <summary>Members keep at least this far from each other's movement goals.</summary>
    public const float GroupSpacing = 60;
    /// <summary>Sideways spacing of a group's members as it advances in its anchor's lane.</summary>
    public const float AdvanceSpacing = 100;
    /// <summary>The last survivor of a group joins another of its side whose anchor stands this close.</summary>
    public const float JoinDistance = 1000;
    /// <summary>A mech keeps a target at least this long unless it leaves reach or is defeated.</summary>
    public const double TargetHoldSeconds = 8;
    /// <summary>A new target must score this many times the current one to take its place.</summary>
    public const float TargetSwitchMargin = 1.25f;
    /// <summary>A mech shot at, hit or miss, within this long counts as under fire and runs.</summary>
    public const double UnderFireSeconds = 5;
    /// <summary>Within this of its preferred distance a mech is in range and strafes; farther out it closes or backs off.</summary>
    public const float RangeBand = 150;
    /// <summary>How far ahead along the circle around its target a strafing mech aims.</summary>
    public const float StrafeLead = 80;
    /// <summary>A strafing mech reverses direction after a random 10–20 s, or at once when terrain blocks it.</summary>
    public const double StrafeFlipMinimum = 10, StrafeFlipMaximum = 20;
    /// <summary>A mech this far beyond every visible enemy's longest stock weapon range stands still to fire.</summary>
    public const float SniperMargin = 60;
    /// <summary>The damage window for a pull-back: damage taken in the last 15 s against what the mech had at its start.</summary>
    public const double PullBackWindowSeconds = 15;
    /// <summary>A mech pulling back stands this much farther from the visible enemies than its group's front.</summary>
    public const float PullBackDistance = 200;
    /// <summary>A pulled-back mech returns once it is Excited or calmer and has not been hit for this long.</summary>
    public const double PullBackQuietSeconds = 20;
    /// <summary>A Panicked pilot routs at this strength ratio whatever their rank.</summary>
    public const float PanicRoutRatio = 1.5f;
    /// <summary>A unit whose last route search found no path searches again at most this often.</summary>
    public const double PathRetrySeconds = 1;

    // Target scores: every term is added to the base, and the total is never below 1, so the switching margin stays meaningful.
    public const float ScoreBase = 100, ScoreEngageable = 60, ScoreFocus = 40, ScorePerMate = 10, ScoreDamage = 40, ScoreHelpless = 20,
        ScorePerHundredMetres = 4, ScorePerTon = .1f;

    /// <summary>The rank that sets an AI pilot's tactics: anyone below Corporal fights as a Corporal.</summary>
    public static WarriorExperience TacticalRank(WarriorSpec warrior) =>
        (WarriorExperience)Math.Clamp((int)warrior.Rank, (int)WarriorExperience.Corporal, (int)WarriorExperience.Captain);
    private static int RankStep(WarriorSpec warrior) => (int)TacticalRank(warrior) - (int)WarriorExperience.Corporal;
    /// <summary>How strongly a pilot is drawn to its group's focus target: Corporal ×0.25, Sergeant ×0.5, Lieutenant ×1, Captain ×1.25.</summary>
    public static float FocusFactor(WarriorSpec warrior) => new[] { .25f, .5f, 1, 1.25f }[RankStep(warrior)];
    /// <summary>The share of its armor and structure a mech may lose in the damage window before it pulls back.</summary>
    public static float PullBackShare(WarriorSpec warrior) => new[] { .35f, .3f, .25f, .2f }[RankStep(warrior)];
    /// <summary>The enemy-to-own strength ratio at which a significantly damaged pilot routs.</summary>
    public static float RoutRatio(WarriorSpec warrior) => new[] { 2f, 2.5f, 3, 3.5f }[RankStep(warrior)];
    /// <summary>Corporals pull back only when Panicked, and hold their range rather than strafe.</summary>
    public static bool IsCorporal(WarriorSpec warrior) => TacticalRank(warrior) == WarriorExperience.Corporal;
    /// <summary>A mech's fighting strength as its side judges it: tonnage × the share of its stock structure left.</summary>
    public static float Strength(CombatUnit unit) => unit.Spec.Tonnage * unit.Locations.Values.Sum(part => part.Structure) / (float)unit.Spec.StructureTotal;
}

public sealed partial class CombatUnit
{
    /// <summary>The AI group this mech fights with; null for player mechs.</summary>
    internal int? TacticalGroup { get; set; }
    internal string? TacticalTarget { get; set; }
    internal double TargetSince { get; set; }
    internal AiMovement AiMove { get; set; }
    internal Vector2 AiGoal { get; set; }
    /// <summary>The kept-range goal from the last decision, null when already at range, and whether it is reached in reverse.</summary>
    internal Vector2? AiRangeGoal { get; set; }
    internal bool AiReverse { get; set; }
    internal int StrafeSign { get; set; } = 1;
    internal double NextStrafeFlip { get; set; } = double.NegativeInfinity;
    /// <summary>Pulled back behind its group's front to recover, since <see cref="PulledBackAt"/>.</summary>
    internal bool PullingBack { get; set; }
    internal double PulledBackAt { get; set; }
    /// <summary>Separated from its group and heading back to it.</summary>
    internal bool Regrouping { get; set; }
    /// <summary>Armor faces (location, rear) already found stripped: each pulls the mech back once.</summary>
    internal HashSet<(MechLocation, bool)> BreachesSeen { get; } = [];
    internal double LastShotAt { get; set; } = double.NegativeInfinity;
    internal double LastHitAt { get; set; } = double.NegativeInfinity;
    internal Vector2? LastAttackFrom { get; set; }
    /// <summary>Damage taken in the pull-back window, with when it landed.</summary>
    internal List<(double Time, int Damage)> RecentDamage { get; } = [];
    /// <summary>Fled the fight on a rout check while still armed: if the battle ends while it runs, it escapes.</summary>
    public bool Routed { get; internal set; }
    /// <summary>Route searches made, and when a failed one may be retried.</summary>
    internal int PathPlans { get; set; }
    internal bool PathFailed { get; set; }
    internal double NextPathAt { get; set; }
    /// <summary>Armor and structure left, front and rear, in tenths of a stock point.</summary>
    internal int Toughness => Locations.Values.Sum(part => part.Armor + part.RearArmor + part.Structure);
}

/// <summary>An AI group: a lance of a standard battle's opposition or a Take and Hold force, or a patrol's enemy group. Mission AIs
/// name, each step, which members engage and which targets they may engage; the group decides the rest.</summary>
internal sealed class TacticalGroupState(int index, CombatSide side, int size)
{
    public int Index { get; } = index;
    public CombatSide Side { get; } = side;
    /// <summary>Members as the battle began; a group of one has no mates to lose.</summary>
    public int Size { get; } = size;
    public string? Focus { get; set; }
    public string? Anchor { get; set; }
    public double NextDecision { get; set; }
    public bool InContact { get; set; }
    public List<CombatUnit> Engaging { get; } = [];
    public HashSet<CombatUnit> Targets { get; } = [];
    /// <summary>Where a standard battle's group advances out of contact, fixed as the advance begins.</summary>
    public Vector2? Lane { get; set; }
    /// <summary>A circle the group's movement goals stay inside while engaging, such as a garrison's leash.</summary>
    public (Vector2 Center, float Radius)? Leash { get; set; }
}

public sealed partial class CombatSimulation
{
    private const int TacticsSeedSalt = 0x5bd1e995;
    private readonly List<TacticalGroupState> _tacticalGroups = [];
    private Random? _tacticsRandom;
    private Random TacticsRandom => _tacticsRandom ??= new Random(unchecked(_seed ^ TacticsSeedSalt));
    internal IReadOnlyList<TacticalGroupState> TacticalGroups => _tacticalGroups;

    /// <summary>Splits each AI side into groups: a patrol's own groups, otherwise lances of up to four dealt out heaviest first, the
    /// garrison and each Take and Hold wave separately.</summary>
    private void AssignTacticalGroups()
    {
        var enemies = Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        var sets = enemies.Any(unit => unit.Group is not null)
            ? enemies.GroupBy(unit => unit.Group ?? -1).OrderBy(set => set.Key).Select(set => set.ToArray())
            : enemies.GroupBy(unit => unit.Wave ?? 0).OrderBy(set => set.Key).SelectMany(set => DealLances(set.ToArray()));
        foreach (var set in sets)
        {
            var group = new TacticalGroupState(_tacticalGroups.Count, CombatSide.Enemy, set.Length);
            foreach (var unit in set) unit.TacticalGroup = group.Index;
            _tacticalGroups.Add(group);
        }
    }
    /// <summary>Deals mechs, heaviest first, into as few lances of four as hold them, so each lance gets a mix of weights. Each lance
    /// keeps its mechs in their original order.</summary>
    private static IEnumerable<CombatUnit[]> DealLances(CombatUnit[] units)
    {
        var lances = (units.Length + CombatRules.EnemyDropLanceSize - 1) / CombatRules.EnemyDropLanceSize;
        var dealt = units.Select((unit, i) => (unit, i)).OrderByDescending(item => item.unit.Spec.Tonnage).ThenBy(item => item.i)
            .Select((item, k) => (item.unit, item.i, Lance: k % lances)).ToArray();
        return Enumerable.Range(0, lances).Select(lance => dealt.Where(item => item.Lance == lance).OrderBy(item => item.i).Select(item => item.unit).ToArray());
    }

    /// <summary>A mission AI puts a member under the group's tactics this step, with the targets it may engage.</summary>
    private void Engage(CombatUnit unit, IEnumerable<CombatUnit> targets, (Vector2 Center, float Radius)? leash = null)
    {
        if (unit.TacticalGroup is not { } index) return;
        var group = _tacticalGroups[index];
        group.Engaging.Add(unit); group.Targets.UnionWith(targets);
        if (leash is not null) group.Leash = leash;
    }

    private IEnumerable<CombatUnit> Fighters(TacticalGroupState group) => Units.Where(unit => unit.TacticalGroup == group.Index && unit.Fighting && !Passive(unit));
    internal static CombatUnit Anchor(IEnumerable<CombatUnit> members) => members.Select((unit, i) => (unit, i))
        .OrderByDescending(item => CombatRules.Strength(item.unit)).ThenByDescending(item => item.unit.Spec.Tonnage).ThenBy(item => item.i).First().unit;
    private static void SetGait(CombatUnit unit, bool run) => unit.Movement = run && unit.Heat < unit.HeatLimit ? MovementMode.Run : MovementMode.Walk;
    private bool UnderFire(CombatUnit unit) => Elapsed - unit.LastShotAt <= CombatRules.UnderFireSeconds + 1e-9;

    /// <summary>Runs every AI group after the mission AIs: members engaging make their tactical choices, the rest keep the group's
    /// pace.</summary>
    private void UpdateTactics()
    {
        JoinStragglers();
        foreach (var group in _tacticalGroups)
        {
            var engaging = group.Engaging.Where(unit => unit.Active && unit.Fighting).Distinct().ToList();
            var targets = group.Targets.Where(target => !target.Defeated && Detected(group.Side, target.Id)).ToList();
            var leash = group.Leash;
            group.Engaging.Clear(); group.Targets.Clear(); group.Leash = null;
            var contact = engaging.Count > 0 && targets.Count > 0;
            if (contact && (!group.InContact || Elapsed + 1e-9 >= group.NextDecision))
            {
                Decide(group, engaging, targets, leash);
                group.NextDecision = Elapsed + CombatRules.TacticsSeconds;
            }
            group.InContact = contact;
            Pace(group, engaging);
        }
    }

    /// <summary>Members outside the fight's tactics move at the group's slowest walking speed, so it advances together, and run
    /// while under fire.</summary>
    private void Pace(TacticalGroupState group, List<CombatUnit> engaging)
    {
        var members = Fighters(group).ToArray();
        if (members.Length == 0) return;
        var pace = Math.Max(CombatRules.MinimumDesiredSpeed, members.Min(unit => unit.CurrentWalkingSpeed));
        foreach (var unit in members.Where(unit => !engaging.Contains(unit) && unit.Order is CombatOrder.Move or CombatOrder.Attack))
        {
            if (UnderFire(unit) && unit.Heat < unit.HeatLimit) unit.Movement = MovementMode.Run;
            else unit.DesiredSpeed = pace;
        }
    }

    /// <summary>The last survivor of a group joins the nearest other group of its side whose anchor stands within reach.</summary>
    private void JoinStragglers()
    {
        foreach (var group in _tacticalGroups.Where(group => group.Size > 1))
        {
            var members = Fighters(group).ToArray();
            if (members.Length != 1 || !members[0].Active) continue;
            var survivor = members[0];
            var join = _tacticalGroups.Where(other => other != group && other.Side == group.Side)
                .Select(other => (other, members: Fighters(other).ToArray())).Where(item => item.members.Length > 0)
                .Select(item => (item.other, Distance: Anchor(item.members).Position.DistanceTo(survivor.Position)))
                .Where(item => item.Distance <= CombatRules.JoinDistance).OrderBy(item => item.Distance).FirstOrDefault();
            if (join.other is null) continue;
            survivor.TacticalGroup = join.other.Index; survivor.PullingBack = false;
            Note(survivor, "joins another group as the last of its own", false);
        }
    }

    /// <summary>The group's choices in contact: who pulls back or regroups, the focus target, and each member's target and movement.</summary>
    private void Decide(TacticalGroupState group, List<CombatUnit> engaging, List<CombatUnit> targets, (Vector2 Center, float Radius)? leash)
    {
        var members = Fighters(group).ToArray();
        foreach (var unit in engaging) UpdatePullBack(unit, members.Length <= 1, targets);
        var view = new TacticalView(this, group, members, engaging, targets, Elapsed);
        group.Anchor = view.Anchor.Id;
        var focus = view.Focus(group.Focus);
        if (group.Focus != focus.Id && view.Line.Count > 1) Note(view.Anchor, $"leads its group onto {SlotLabel(focus)} {focus.Spec.DisplayName}", false);
        group.Focus = focus.Id;

        foreach (var unit in engaging)
        {
            if (unit.PullingBack) { PullBack(unit, view, leash, focus.Id); continue; }
            if (view.Separated(unit)) { Regroup(unit, view.Anchor, members, leash); continue; }
            unit.Regrouping = false;
            var best = view.Choose(unit, focus.Id);
            if (best.Id != unit.TacticalTarget) { unit.TacticalTarget = best.Id; unit.TargetSince = Elapsed; }
            EngageTarget(unit, best, targets, members, leash);
        }
    }

    /// <summary>How <paramref name="unit"/>'s group sees the fight now, with every detected enemy as a target.</summary>
    internal TacticalView ViewOf(CombatUnit unit)
    {
        var group = _tacticalGroups[unit.TacticalGroup!.Value];
        var members = Fighters(group).ToArray();
        return new TacticalView(this, group, members, members.Where(item => item.Active).ToList(),
            Units.Where(item => item.Side != group.Side && !item.Defeated && Detected(group.Side, item.Id)).ToList(), Elapsed);
    }

    /// <summary>Attacks a target: closing or backing off to the preferred distance, strafing around it once in range, or standing
    /// still beyond every visible enemy's reach. Runs while closing, repositioning or under fire, and walks when hot.</summary>
    private void EngageTarget(CombatUnit unit, CombatUnit target, List<CombatUnit> targets, CombatUnit[] members, (Vector2 Center, float Radius)? leash)
    {
        AttackWith(unit, target); unit.PriorityTarget = null;
        var distance = unit.Position.DistanceTo(target.Position);
        var run = false;
        unit.AiMove = AiMovement.Keep;
        if (!Sight(unit, target) || distance > unit.PreferredDistance + CombatRules.RangeBand) run = true;
        else if (distance < unit.PreferredDistance - CombatRules.RangeBand || CombatRules.IsCorporal(unit.Warrior)) { }
        else if (targets.All(enemy => unit.Position.DistanceTo(enemy.Position) > enemy.Weapons.Select(weapon => weapon.LongRange).DefaultIfEmpty(0).Max() + CombatRules.SniperMargin))
            unit.AiMove = AiMovement.Still;
        else { unit.AiMove = AiMovement.Point; unit.AiGoal = StrafeGoal(unit, target, members, leash); }
        if (unit.AiMove == AiMovement.Keep) { unit.AiRangeGoal = RangeGoal(unit, target, out var reverse); unit.AiReverse = reverse; }
        SetGait(unit, run || UnderFire(unit));
    }

    /// <summary>A point ahead along the circle at the preferred distance around the target, on the side the mech is strafing.</summary>
    private Vector2 StrafeGoal(CombatUnit unit, CombatUnit target, CombatUnit[] members, (Vector2 Center, float Radius)? leash)
    {
        if (Elapsed >= unit.NextStrafeFlip) FlipStrafe(unit, unit.NextStrafeFlip == double.NegativeInfinity);
        var away = unit.Position - target.Position;
        away = away.LengthSquared() < 1 ? Vector2.Up : away.Normalized();
        var goal = unit.Position;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var requested = Leashed(Spaced(unit, target.Position + away.Rotated(unit.StrafeSign * CombatRules.StrafeLead / unit.PreferredDistance)
                * unit.PreferredDistance, members), leash);
            goal = Terrain.ReachableDestination(unit.Position, requested);
            // Blocked one way: strafe the other.
            if (goal.DistanceTo(requested) <= 40 && unit.BlockedSeconds < .5f) break;
            FlipStrafe(unit, false);
        }
        return goal;
    }
    private void FlipStrafe(CombatUnit unit, bool first)
    {
        unit.StrafeSign = first ? (TacticsRandom.Next(2) == 0 ? -1 : 1) : -unit.StrafeSign;
        unit.NextStrafeFlip = Elapsed + CombatRules.StrafeFlipMinimum + TacticsRandom.NextDouble() * (CombatRules.StrafeFlipMaximum - CombatRules.StrafeFlipMinimum);
    }
    /// <summary>Moves a goal out from any mate standing too close to it.</summary>
    private static Vector2 Spaced(CombatUnit unit, Vector2 goal, IEnumerable<CombatUnit> members)
    {
        foreach (var other in members.Where(other => other != unit && other.Position.DistanceTo(goal) < CombatRules.GroupSpacing))
        {
            var away = goal - other.Position;
            goal = other.Position + (away.LengthSquared() < 1 ? (unit.Position - other.Position).Normalized() : away.Normalized()) * CombatRules.GroupSpacing;
        }
        return goal;
    }
    private static Vector2 Leashed(Vector2 goal, (Vector2 Center, float Radius)? leash) =>
        leash is { } circle && goal.DistanceTo(circle.Center) > circle.Radius ? circle.Center + (goal - circle.Center).Normalized() * circle.Radius : goal;

    /// <summary>Starts or ends a pull-back. A mech pulls back when Stressed (a Corporal only when Panicked), when the last 15 s cost it
    /// its rank's share of what it had, or when a torso or leg facing its attacker is stripped of armor; it returns once Excited or
    /// calmer and has spent 20 s back without a hit. The last of a group never pulls back.</summary>
    private void UpdatePullBack(CombatUnit unit, bool last, List<CombatUnit> targets)
    {
        unit.RecentDamage.RemoveAll(item => Elapsed - item.Time > CombatRules.PullBackWindowSeconds + 1e-9);
        var from = unit.LastAttackFrom is { } shooter && Elapsed - unit.LastHitAt <= CombatRules.PullBackWindowSeconds
            ? shooter : targets.MinBy(target => target.Position.DistanceSquaredTo(unit.Position))!.Position;
        var breaches = new[] { MechLocation.LeftTorso, MechLocation.CenterTorso, MechLocation.RightTorso, MechLocation.LeftLeg, MechLocation.RightLeg }
            .Select(location => unit.Locations[location]).Select(part => (part, rear: CombatRules.IsTorso(part.Location) && unit.RearHitFrom(from)))
            .Where(item => item.rear ? item.part.InitialRearArmor > 0 && item.part.RearArmor == 0 : item.part.InitialArmor > 0 && item.part.Armor == 0)
            .Select(item => (item.part.Location, item.rear)).ToArray();
        var breached = false;
        foreach (var face in breaches) breached |= unit.BreachesSeen.Add(face);
        if (unit.PullingBack)
        {
            if (last) { unit.PullingBack = false; Note(unit, "returns to the line as the last of its group", false); }
            else if (unit.Stress <= StressLevel.Excited && Elapsed - Math.Max(unit.LastHitAt, unit.PulledBackAt) >= CombatRules.PullBackQuietSeconds - 1e-9)
            { unit.PullingBack = false; Note(unit, "returns to the line", false); }
            return;
        }
        if (last) return;
        var recent = unit.RecentDamage.Sum(item => item.Damage);
        var stressed = unit.Stress >= (CombatRules.IsCorporal(unit.Warrior) ? StressLevel.Panicked : StressLevel.Stressed);
        var reason = stressed ? unit.Stress.ToString().ToLowerInvariant()
            : recent > 0 && recent >= CombatRules.PullBackShare(unit.Warrior) * (unit.Toughness + recent) ? "heavy damage"
            : breached ? "armor breached" : null;
        if (reason is null) return;
        unit.PullingBack = true; unit.PulledBackAt = Elapsed; unit.TacticalTarget = null;
        Note(unit, $"pulls back ({reason})", false);
    }

    /// <summary>Runs to a spot behind the group's front, farther from the visible enemies, and keeps firing from there.</summary>
    private void PullBack(CombatUnit unit, TacticalView view, (Vector2 Center, float Radius)? leash, string focus)
    {
        var (targets, line, members) = (view.Targets, view.Line, view.Members);
        var enemies = targets.Aggregate(Vector2.Zero, (sum, target) => sum + target.Position) / targets.Count;
        var front = line.Count > 0 ? line.Min(other => other.Position.DistanceTo(enemies)) : unit.Position.DistanceTo(enemies);
        var behind = (line.Count > 0 ? line.Aggregate(Vector2.Zero, (sum, other) => sum + other.Position) / line.Count : unit.Position) - enemies;
        if (behind.LengthSquared() < 1) behind = unit.Position - enemies;
        if (behind.LengthSquared() < 1) behind = unit.Side == CombatSide.Enemy ? Vector2.Up : Vector2.Down;
        var spot = Terrain.ReachableDestination(unit.Position, Leashed(Spaced(unit, enemies + behind.Normalized() * (front + CombatRules.PullBackDistance), members), leash));
        HeadFor(unit, spot);
        unit.PriorityTarget = targets.Where(target => view.CanEngage(unit, target)).OrderByDescending(target => target.Id == focus)
            .ThenBy(target => target.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault()?.Id;
        SetGait(unit, true);
    }

    /// <summary>A separated member runs back toward its group's anchor, firing only at whatever it comes across.</summary>
    private void Regroup(CombatUnit unit, CombatUnit anchor, CombatUnit[] members, (Vector2 Center, float Radius)? leash)
    {
        var toward = unit.Position - anchor.Position;
        var spot = Terrain.ReachableDestination(unit.Position, Leashed(Spaced(unit,
            anchor.Position + (toward.LengthSquared() < 1 ? Vector2.Up : toward.Normalized()) * (CombatRules.CohesionRadius / 3), members), leash));
        HeadFor(unit, spot);
        unit.PriorityTarget = null; unit.TacticalTarget = null;
        SetGait(unit, true);
        if (!unit.Regrouping) Note(unit, "regroups", false);
        unit.Regrouping = true;
    }
    private void HeadFor(CombatUnit unit, Vector2 spot)
    {
        if (unit.Position.DistanceTo(spot) < 30) { if (unit.Order != CombatOrder.Hold) Hold([unit.Id]); return; }
        if (unit.Order == CombatOrder.Move && unit.Destination.DistanceTo(spot) < 50) return;
        Move([unit.Id], spot);
    }

    /// <summary>On an enemy's nerve check, unless its center torso is crippled (which surrender settles): a mech that has lost a
    /// location or half its armor and structure routs when the enemy it sees is its rank's ratio stronger than its side, or 1.5 times
    /// stronger when it is Panicked. It flees and never rallies.</summary>
    private void ConsiderRout(CombatUnit unit)
    {
        if (unit.Defeated || unit.Fleeing || unit.Unarmed || unit.TacticalGroup is null) return;
        var stock = unit.Spec.Locations.Sum(part => part.Armor + part.RearArmor + part.Structure);
        if (!unit.Locations.Values.Any(part => part.Structure == 0) && unit.Toughness * 2 > stock) return;
        var (own, enemy) = SideStrength(unit.Side);
        var ratio = own > 0 ? enemy / own : float.PositiveInfinity;
        var needed = unit.Stress == StressLevel.Panicked ? CombatRules.PanicRoutRatio : CombatRules.RoutRatio(unit.Warrior);
        if (ratio < needed) return;
        unit.Fleeing = unit.Routed = true; unit.PullingBack = false; unit.PriorityTarget = unit.TacticalTarget = null;
        Note(unit, FormattableString.Invariant($"routs · enemy strength {ratio:0.0} : 1 against {needed:0.0} : 1"), false);
        Note(unit, "flees");
    }

    /// <summary>As the battle ends, every armed mech still running from a rout gets away.</summary>
    private void SettleRouts()
    {
        foreach (var unit in Units.Where(item => item.Routed && item.Fleeing && !item.Defeated && !item.Unarmed))
        {
            unit.Fled = true; unit.Fleeing = false; unit.Moving = false; unit.CurrentSpeed = unit.AngularVelocity = 0;
            unit.Order = CombatOrder.Hold; unit.CurrentTarget = unit.CurrentRearTarget = null;
            Note(unit, "has fled the battle"); Highlight(unit, $"{Name(unit)} fled the battle.");
        }
    }
}

/// <summary>A group's view of the fight at one decision: its anchor and line, who can engage whom, and how each member scores each
/// target. It sees only what its side detects, and a target's damage only at Detailed contact.</summary>
internal sealed class TacticalView
{
    private readonly CombatSimulation _battle;
    private readonly CombatSide _side;
    private readonly Dictionary<(string, string), bool> _sight = [], _reach = [];
    private readonly Dictionary<string, bool> _spotted = [];
    private readonly double _now;
    public CombatUnit[] Members { get; }
    /// <summary>The engaging members not pulled back.</summary>
    public List<CombatUnit> Line { get; }
    public List<CombatUnit> Targets { get; }
    /// <summary>The healthiest, heaviest member of the line.</summary>
    public CombatUnit Anchor { get; }

    public TacticalView(CombatSimulation battle, TacticalGroupState group, CombatUnit[] members, List<CombatUnit> engaging, List<CombatUnit> targets, double now)
    {
        _battle = battle; _side = group.Side; Members = members; Targets = targets; _now = now;
        Line = engaging.Where(unit => !unit.PullingBack).ToList();
        Anchor = CombatSimulation.Anchor(Line.Count > 0 ? Line : engaging.Count > 0 ? engaging : members);
    }

    public bool Sees(CombatUnit from, CombatUnit to)
    {
        if (!_sight.TryGetValue((from.Id, to.Id), out var clear)) _sight[(from.Id, to.Id)] = clear = _battle.Sight(from, to);
        return clear;
    }
    /// <summary>A forward weapon in range with the shooter's own sight, or an LRM in range guided by a mate's sight.</summary>
    public bool CanEngage(CombatUnit unit, CombatUnit target)
    {
        if (_reach.TryGetValue((unit.Id, target.Id), out var can)) return can;
        var distance = unit.Position.DistanceTo(target.Position);
        var weapons = unit.Weapons.Where(weapon => !weapon.Spec.Rear && weapon.Enabled && unit.WeaponUsable(weapon) && distance <= weapon.LongRange).ToArray();
        can = weapons.Length > 0 && (Sees(unit, target) || (Spotted(target) && weapons.Any(weapon => CombatRules.IsLrm(weapon.Spec.Kind))));
        return _reach[(unit.Id, target.Id)] = can;
    }
    private bool Spotted(CombatUnit target)
    {
        if (!_spotted.TryGetValue(target.Id, out var seen))
            _spotted[target.Id] = seen = _battle.Units.Any(unit => unit.Side == _side && unit.Active && Sees(unit, target));
        return seen;
    }
    /// <summary>Away from the anchor with no mate near. Once regrouping, a mech carries on until it is well inside the group, so it
    /// does not hover at the edge.</summary>
    public bool Separated(CombatUnit unit)
    {
        var radius = unit.Regrouping ? CombatRules.CohesionRadius / 2 : CombatRules.CohesionRadius;
        return unit != Anchor && Line.Count > 1 && unit.Position.DistanceTo(Anchor.Position) > radius
            && !Members.Any(other => other != unit && other.Position.DistanceTo(unit.Position) <= radius);
    }
    /// <summary>A target's worth to a member: being in reach, the group's focus (by the member's rank), mates who can reach it, its
    /// visible damage, a fall or shutdown, and its tonnage, less its distance. Never below 1.</summary>
    public float Score(CombatUnit unit, CombatUnit target, string? focus)
    {
        var engageable = CanEngage(unit, target);
        var score = CombatRules.ScoreBase + (engageable ? CombatRules.ScoreEngageable : 0);
        // A mech joins the focus only when it can engage it now, or when closing on it keeps it near its group.
        if (target.Id == focus && (engageable || (!Targets.Any(other => CanEngage(unit, other))
            && Anchor.Position.DistanceTo(target.Position) <= unit.PreferredDistance + CombatRules.CohesionRadius)))
            score += CombatRules.ScoreFocus * CombatRules.FocusFactor(unit.Warrior);
        score += CombatRules.ScorePerMate * Line.Count(other => other != unit && CanEngage(other, target));
        // Damage, a fall or a shutdown count only once the side has a detailed look at the target.
        if (_battle.DetailsVisible(_side, target.Id))
            score += CombatRules.ScoreDamage * (1 - target.Toughness / (float)target.Spec.Locations.Sum(part => part.Armor + part.RearArmor + part.Structure))
                + (target.Prone || target.Shutdown ? CombatRules.ScoreHelpless : 0);
        score += CombatRules.ScorePerTon * target.Spec.Tonnage - CombatRules.ScorePerHundredMetres * unit.Position.DistanceTo(target.Position) / 100;
        return Math.Max(1, score);
    }
    public CombatUnit Best(CombatUnit unit, string? focus) => Targets.OrderByDescending(target => Score(unit, target, focus))
        .ThenBy(target => target.Position.DistanceSquaredTo(unit.Position)).ThenBy(target => target.Id, StringComparer.Ordinal).First();
    /// <summary>The target a member engages: its best, unless its current target still holds it. A target holds a mech for 8 s,
    /// and after that until another scores a quarter more; a target out of reach while another is in reach no longer holds it.</summary>
    public CombatUnit Choose(CombatUnit unit, string? focus)
    {
        var best = Best(unit, focus);
        var current = unit.TacticalTarget is { } id ? Targets.FirstOrDefault(target => target.Id == id) : null;
        if (current is null || (!CanEngage(unit, current) && Targets.Any(target => CanEngage(unit, target)))) return best;
        return _now - unit.TargetSince < CombatRules.TargetHoldSeconds - 1e-9
            || Score(unit, best, focus) < Score(unit, current, focus) * CombatRules.TargetSwitchMargin ? current : best;
    }
    /// <summary>The target most of the line (those not separated) would engage, ties going to the anchor's choice.</summary>
    public CombatUnit Focus(string? current)
    {
        var picks = Line.Where(unit => !Separated(unit)).Select(unit => (unit, pick: Choose(unit, current))).ToList();
        var anchorPick = picks.Where(item => item.unit == Anchor).Select(item => item.pick).FirstOrDefault() ?? Choose(Anchor, current);
        return picks.Count == 0 ? anchorPick : picks.GroupBy(item => item.pick)
            .OrderByDescending(votes => votes.Count()).ThenByDescending(votes => votes.Key == anchorPick)
            .ThenBy(votes => votes.Key.Position.DistanceSquaredTo(Anchor.Position)).First().Key;
    }
}
