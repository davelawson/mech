namespace Mech;

public enum HeatPolicy { Normal, Aggressive }

public static partial class CombatRules
{
    public const double WarmHeat = 10, HotHeat = 20, ShutdownHeat = 30;
    public const double HeatClockSeconds = 10;
    public static double HeatLimit(HeatPolicy policy) => policy == HeatPolicy.Aggressive ? ShutdownHeat : HotHeat;
    public static int FlamerHeat(WeaponSpec weapon) => weapon.DamageProfile.TargetHeat;
}

public sealed partial class CombatUnit
{
    public double Heat { get; private set; }
    public HeatPolicy HeatPolicy { get; set; }
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
    public double CoolingRate => EffectiveHeatSinks * Spec.CoolingPerSink / CombatRules.HeatClockSeconds;
    public double MovementHeatRate => !Moving || !Active ? 0
        : (CurrentSpeed > UnheatedWalkingSpeed + .00001f ? 2 : 1) / CombatRules.HeatClockSeconds;

    internal void AdvanceHeat(double delta) => AddHeat((MovementHeatRate - CoolingRate) * delta);
    internal void AddHeat(double amount)
    {
        // Normalize numerical drift so an exact threshold is not crossed a step early.
        Heat = Math.Max(0, Math.Round(Heat + amount, 9));
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
    private sealed record HeatCandidate(CombatWeapon Weapon, CombatUnit Target, double ExpectedDamage);
    private sealed record HeatVolley(double ExpectedDamage, int[] Indices);

    /// <summary>Exact bounded selection, independent of rolls, emitted in stable catalog order.</summary>
    internal IReadOnlyList<(CombatWeapon Weapon, CombatUnit Target)> SelectHeatVolley(
        CombatUnit unit, CombatUnit? mainTarget, CombatUnit? rearTarget)
    {
        var limit = CombatRules.HeatLimit(unit.HeatPolicy);
        if (!unit.Active || unit.Heat >= limit) return [];
        var candidates = new List<HeatCandidate>();
        foreach (var weapon in unit.Weapons)
        {
            var target = weapon.Spec.Rear ? rearTarget : mainTarget;
            if (weapon.Remaining > .000001 || target is null || !CanFire(unit, target, weapon)) continue;
            candidates.Add(new(weapon, target, weapon.Damage * TerrainHitChance(unit, target, weapon)));
        }
        if (candidates.Count == 0) return [];
        // Weapon heat is integral; current heat is continuous. The policy ceiling is strictly exclusive.
        var capacity = Math.Max(0, (int)Math.Ceiling(limit - unit.Heat - 1e-9) - 1);
        var best = new HeatVolley?[capacity + 1]; best[0] = new(0, []);
        for (var i = 0; i < candidates.Count; i++)
        {
            var cost = candidates[i].Weapon.Spec.Heat;
            for (var heat = capacity; heat >= cost; heat--)
            {
                if (best[heat - cost] is not { } previous) continue;
                var volley = new HeatVolley(previous.ExpectedDamage + candidates[i].ExpectedDamage, [.. previous.Indices, i]);
                if (BetterVolley(volley, best[heat])) best[heat] = volley;
            }
        }
        var selected = best[0]!;
        for (var heat = 1; heat <= capacity; heat++)
            // Ascending cost makes equal-damage ties favor lower heat before catalog order.
            if (best[heat] is { } volley && volley.ExpectedDamage > selected.ExpectedDamage + 1e-9) selected = volley;
        return selected.Indices.Select(index => (candidates[index].Weapon, candidates[index].Target)).ToArray();
    }
    private static bool BetterVolley(HeatVolley candidate, HeatVolley? current)
    {
        if (current is null || candidate.ExpectedDamage > current.ExpectedDamage + 1e-9) return true;
        if (Math.Abs(candidate.ExpectedDamage - current.ExpectedDamage) > 1e-9) return false;
        for (var i = 0; i < Math.Min(candidate.Indices.Length, current.Indices.Length); i++)
            if (candidate.Indices[i] != current.Indices[i]) return candidate.Indices[i] < current.Indices[i];
        return candidate.Indices.Length < current.Indices.Length;
    }
}
