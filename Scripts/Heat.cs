namespace Mech;

public static partial class CombatRules
{
    public const double WarmHeat = 10, HotHeat = 20, ShutdownHeat = 30;
    public const double HeatClockSeconds = 10;
    /// <summary>Each this many heat points between a mech and the ambient temperature move 1 heat per heat clock.</summary>
    public const double AmbientExchangeDifference = 5;
    /// <summary>Heat per second from travelling at walking speed, and above the unheated walking limit.</summary>
    public const double WalkingHeatRate = 1 / HeatClockSeconds, RunningHeatRate = 2 / HeatClockSeconds;
    /// <summary>Every mech starts each battle with this heat limit; the enemy keeps it.</summary>
    public const int DefaultHeatLimit = 10, MaxHeatLimit = 30;
    public static int ClampHeatLimit(int limit) => Math.Clamp(limit, 0, MaxHeatLimit);
    /// <summary>Ammo economy runs in whole steps from 0 (fire freely) to 10 (only shots with no penalty modifier at all).</summary>
    public const int MaxAmmoEconomy = 10;
    public static int ClampAmmoEconomy(int economy) => Math.Clamp(economy, 0, MaxAmmoEconomy);
    /// <summary>The largest penalty modifier an ammo weapon accepts at an economy step: +10 at 0, which no shot can exceed and
    /// still hit, down to +0 at 10.</summary>
    public static int AmmoAllowance(int economy) => MaxAmmoEconomy - ClampAmmoEconomy(economy);
    /// <summary>A weapon or jump may start while heat is at or below the limit, so it can carry heat past it.</summary>
    public static bool WithinHeatLimit(double heat, int limit) => heat <= limit + 1e-9;
    /// <summary>A weapon that generates no heat may fire at any heat; any other fires only while the running heat is within the limit.</summary>
    public static bool HeatPermits(double runningHeat, int limit, WeaponSpec weapon) => weapon.Heat <= 0 || WithinHeatLimit(runningHeat, limit);
    /// <summary>Expected damage per point of heat; a weapon that generates no heat ranks above every one that does.</summary>
    public static double DamagePerHeat(double expected, int heat) => heat > 0 ? expected / heat : double.PositiveInfinity;
    public static int FlamerHeat(WeaponSpec weapon) => weapon.DamageProfile.TargetHeat;
}

public sealed partial class CombatUnit
{
    public double Heat { get; private set; }
    private int _heatLimit = CombatRules.DefaultHeatLimit;
    /// <summary>Whole heat 0–30: weapons and jumps are held once heat rises above it.</summary>
    public int HeatLimit { get => _heatLimit; set => _heatLimit = CombatRules.ClampHeatLimit(value); }
    private int _ammoEconomy;
    /// <summary>Whole steps 0–10: how sparingly ammo-fed weapons fire. See <see cref="CombatRules.AmmoAllowance"/>.</summary>
    public int AmmoEconomy { get => _ammoEconomy; set => _ammoEconomy = CombatRules.ClampAmmoEconomy(value); }
    public bool Shutdown { get; private set; }
    public string HeatBand => Shutdown ? "Shutdown" : Heat >= CombatRules.HotHeat ? "Hot"
        : Heat >= CombatRules.WarmHeat ? "Warm" : "Cool";
    public int HeatAimModifier => Heat >= CombatRules.HotHeat ? 2 : Heat >= CombatRules.WarmHeat ? 1 : 0;
    public float HeatSpeedMultiplier => Heat >= CombatRules.HotHeat ? .75f : Heat >= CombatRules.WarmHeat ? .9f : 1;
    // Catalog sink entries have explicit grouped sink contributions. Internal sinks are the stock remainder.
    public int EffectiveHeatSinks => Math.Max(0, Spec.HeatSinks - Spec.Components
        .Where(component => component.Kind == ComponentKind.HeatSink
            && (!Intact(component.Location) || Equipment[component.Id] == ComponentCondition.Destroyed))
        .Sum(component => component.HeatSinkCount));
    /// <summary>The battlefield's climate; tests on hand-built terrain have none and exchange no ambient heat.</summary>
    internal BiomeSpec? Climate { get; set; }
    public double CoolingEfficiency => Climate?.CoolingEfficiency ?? 1;
    /// <summary>Heat-sink cooling per second, after the climate's cooling efficiency.</summary>
    public double CoolingRate => EffectiveCooling * CoolingEfficiency / CombatRules.HeatClockSeconds;
    /// <summary>Heat per heat clock the working sinks remove before climate: a damaged sink item cools at half its rate, rounded down.</summary>
    public int EffectiveCooling => EffectiveHeatSinks * Spec.CoolingPerSink - Spec.Components
        .Where(component => component.Kind == ComponentKind.HeatSink && Intact(component.Location)
            && Equipment[component.Id] == ComponentCondition.Damaged)
        .Sum(component => component.HeatSinkCount * Spec.CoolingPerSink - component.HeatSinkCount * Spec.CoolingPerSink / 2);
    /// <summary>Heat per second gained (positive) or shed (negative) toward the ambient temperature, unaffected by efficiency.</summary>
    public double AmbientHeatRate => Climate is { } climate
        ? (climate.Ambient - Heat) / CombatRules.AmbientExchangeDifference / CombatRules.HeatClockSeconds : 0;
    public double MovementHeatRate => !Moving || !Active ? 0
        : Gait == Gait.Running ? CombatRules.RunningHeatRate : CombatRules.WalkingHeatRate;

    internal void AdvanceHeat(double delta) => AddHeat((MovementHeatRate + EngineHeatRate - CoolingRate + AmbientHeatRate) * delta);
    internal void AddHeat(double amount)
    {
        // Normalize numerical drift so an exact threshold is not crossed a step early.
        Heat = Math.Max(0, Math.Round(Heat + amount, 9));
        CheckLifeSupport();
        if (Heat >= CombatRules.ShutdownHeat) Shutdown = true;
        else if (Heat < CombatRules.WarmHeat) Shutdown = false;
        if (Shutdown)
        {
            Moving = false; CurrentSpeed = AngularVelocity = 0;
            CurrentTarget = CurrentRearTarget = null;
        }
    }
}

public sealed partial class CombatSimulation
{
    /// <summary>Ready, enabled weapons with a target, best expected damage per heat first (weapons that generate no heat before all
    /// others), then higher expected damage, then mount order.</summary>
    internal IReadOnlyList<(CombatWeapon Weapon, CombatUnit Target)> HeatOrderedCandidates(
        CombatUnit unit, CombatUnit? mainTarget, CombatUnit? rearTarget)
    {
        if (!unit.Active) return [];
        var candidates = new List<(CombatWeapon Weapon, CombatUnit Target, double Expected, int Index)>();
        for (var i = 0; i < unit.Weapons.Count; i++)
        {
            var weapon = unit.Weapons[i];
            var target = weapon.Spec.Rear ? rearTarget : mainTarget;
            if (weapon.Remaining > .000001 || target is null || !CanFire(unit, target, weapon)) continue;
            // A weapon that cannot hit never fires, and so never spends heat; nor does an ammo weapon its economy holds back.
            var chance = TerrainHitChance(unit, target, weapon);
            if (chance > 0 && !SavingAmmo(unit, target, weapon)) candidates.Add((weapon, target, weapon.Damage * chance, i));
        }
        return candidates.OrderByDescending(item => Math.Round(CombatRules.DamagePerHeat(item.Expected, item.Weapon.Spec.Heat), 9))
            .ThenByDescending(item => Math.Round(item.Expected, 9)).ThenBy(item => item.Index).Select(item => (item.Weapon, item.Target)).ToArray();
    }

    /// <summary>An ammo-fed weapon whose shot carries more penalty modifiers than the unit's ammo economy allows.</summary>
    public bool SavingAmmo(CombatUnit unit, CombatUnit target, CombatWeapon weapon) => weapon.Spec.AmmoKind is not null
        && TerrainPenalty(unit, target, weapon) > CombatRules.AmmoAllowance(unit.AmmoEconomy);

    /// <summary>Weapons are checked one at a time in <see cref="HeatOrderedCandidates"/> order. A weapon that generates no heat
    /// always fires; any other fires only while the heat so far this step, including weapons already chosen, is at or below the
    /// unit's limit.</summary>
    internal IReadOnlyList<(CombatWeapon Weapon, CombatUnit Target)> SelectHeatVolley(
        CombatUnit unit, CombatUnit? mainTarget, CombatUnit? rearTarget)
    {
        var heat = unit.Heat; var selected = new List<(CombatWeapon Weapon, CombatUnit Target)>();
        foreach (var shot in HeatOrderedCandidates(unit, mainTarget, rearTarget))
        {
            if (!CombatRules.HeatPermits(heat, unit.HeatLimit, shot.Weapon.Spec)) continue;
            selected.Add(shot); heat += shot.Weapon.Spec.Heat;
        }
        return selected;
    }
}
