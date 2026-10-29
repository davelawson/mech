using Godot;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static void CheckCombatLog(Action<bool, string> check)
    {
        var sim = HeatFixture([HeatGun("first", 3, 1), HeatGun("second", 4, 1)]);
        var friendly = Friendly(sim); var enemy = Enemy(sim); enemy.Firing = FiringMode.HoldFire;
        var armor = enemy.ArmorLost; var structure = enemy.StructureLost;
        var emitted = new List<CombatShot>(); sim.ShotFired += emitted.Add;
        sim.Paused = false; sim.Advance(.05);
        check(sim.History.Count == 2 && emitted.SequenceEqual(sim.History.Select(entry => entry.Shot)),
            "each weapon firing records a resolved event before emitting ShotFired");
        check(sim.History.All(entry => entry.Attacker.Heat == 7) && sim.History.Sum(entry => entry.Shot.GeneratedHeat) == 7,
            "shot heat contributions share the correct simultaneous exchange total");
        check(sim.History.Sum(entry => entry.Shot.DamageApplied.Sum(part => part.Armor + part.RearArmor)) == enemy.ArmorLost - armor
            && sim.History.Sum(entry => entry.Shot.DamageApplied.Sum(part => part.Structure)) == enemy.StructureLost - structure,
            "per-shot damage attribution sums to the actual armor and structure losses");
        var first = sim.History[0];
        check(first.Shot.Roll == new Random(41).NextDouble() && first.Shot.Hit == (first.Shot.Roll < first.Shot.Chance),
            "log records the original percentage draw without consuming extra random values");
        check(first.DebugText.IndexOf("heat", StringComparison.Ordinal) < first.DebugText.IndexOf("hex", StringComparison.Ordinal)
            && first.DebugText.Contains("chance 100.0%") && first.DebugText.Contains("roll "),
            "terse entries place heat before range and show percentage chance and draw");
        var snapshot = first.DebugText; enemy.ApplyDamage(MechLocation.CenterTorso, 10000); enemy.AddHeat(29);
        check(first.DebugText == snapshot, "old combat events retain immutable condition and heat snapshots");

        sim = HeatFixture([HeatGun("miss", 2, 1)], seed: 1, gunnery: 8);
        Enemy(sim).Firing = FiringMode.HoldFire; sim.Paused = false; sim.Advance(.05);
        check(sim.History.Count == 1 && !sim.History[0].Shot.Hit && sim.History[0].Shot.DamageApplied.Count == 0
            && sim.History[0].Attacker.Heat == 2 && sim.History[0].DebugText.Contains("MISS"),
            "misses are retained with roll, zero damage and firing heat");

        // Destroy a side torso with a known first hit; its attached arm and mounted systems must be attributed too.
        var gun = HeatGun("cannon", 0, 1000);
        var seed = Enumerable.Range(1, 100).First(candidate =>
        {
            var probe = HeatFixture([gun], seed: candidate); Enemy(probe).Firing = FiringMode.HoldFire;
            probe.Paused = false; probe.Advance(.05);
            return probe.History[0].Shot.Location == MechLocation.RightTorso;
        });
        sim = HeatFixture([gun], components: [new("sink", "Heat Sink", MechLocation.RightArm, 1)], seed: seed);
        sim.Paused = false; sim.Advance(.05);
        check(sim.History.Count == 2 && sim.Units.All(unit => unit.Defeated), "lethal simultaneous return fire is retained on both sides");
        var lethal = sim.History[0];
        check(lethal.Shot.DamageApplied.Any(part => part.Location == MechLocation.RightTorso && part.Destroyed)
            && lethal.Shot.DamageApplied.Any(part => part.Location == MechLocation.RightArm && part.Destroyed)
            && lethal.Shot.SystemsLost.Contains("RA cannon") && lethal.Shot.SystemsLost.Contains("RA Heat Sink")
            && lethal.Shot.TargetStatus == CombatStatus.Destroyed,
            "log attributes transfer, detached limbs, mounted equipment and mech destruction to the causing shot");

        sim = HeatFixture([HeatGun("secret-weapon", 3, 1)]); enemy = Enemy(sim); enemy.Position = new Vector2(40, 0);
        sim.RefreshDetection(); sim.Paused = false; sim.Advance(.05);
        var outgoing = sim.History.Single(entry => entry.Attacker.Side == CombatSide.Friendly);
        var incoming = sim.History.Single(entry => entry.Attacker.Side == CombatSide.Enemy);
        check(outgoing.Target.Visibility == ContactLevel.Identified && !outgoing.CombatText!.Contains("armour")
            && !outgoing.CombatText.Contains($"E1 {enemy.Spec.DisplayName}") && outgoing.DebugText.Contains("armour"),
            $"identified targets hide exact damage and variant in Combat while Debug retains them: {outgoing.Target.Visibility}, {outgoing.CombatText}");
        check(!incoming.CombatText!.Contains("secret-weapon") && !incoming.CombatText.Contains("chance")
            && !incoming.CombatText.Contains("heat +3") && incoming.CombatText.Contains("armour"),
            "identified attackers hide weapon, roll and shooter heat while friendly damage remains visible");
        var originalText = outgoing.CombatText;
        enemy.Position = new Vector2(2, 0); sim.RefreshDetection();
        check(sim.DetailsVisible(CombatSide.Friendly, enemy.Id) && outgoing.CombatText == originalText,
            "later detailed contact cannot reveal historical hidden information");
        var lostAttacker = incoming.Attacker with { Visibility = ContactLevel.Lost };
        var unknown = incoming with { Attacker = lostAttacker };
        check(unknown.CombatText!.Contains("Unknown attacker") && !unknown.CombatText.Contains("hex")
            && !unknown.CombatText.Contains("E1") && unknown.DebugText.Contains("E1"),
            "unknown incoming hits expose neither enemy identity nor range but remain complete in Debug");
        check((unknown with { Shot = unknown.Shot with { Hit = false } }).CombatText is null,
            "unseen enemy misses do not appear in the visibility-restricted log");

        sim = HeatFixture([HeatGun("flamer", 3, 1) with { Name = "Flamer" }]); sim.Paused = false; sim.Advance(.05);
        check(sim.History.All(entry => entry.Attacker.Heat == 5 && entry.Target.Heat == 5 && entry.Shot.TargetHeat == 2
            && entry.DebugText.Contains("target heat +2 → 5.0")), "flamer events record both firing heat and received heat at exchange end");
    }
}
