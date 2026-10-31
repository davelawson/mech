using Godot;
using System.Text.Json;

namespace Mech.Tests;

internal static partial class CombatChecks
{
    private static ForceRoster Roster(int count = 1, string definition = "awesome")
    {
        var roster = ForceRoster.Create("Tactical checks", Enumerable.Repeat(DraftCatalog.Mech(definition), count), DraftCatalog.Warriors.Take(count));
        for (var i = 0; i < count; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        return roster;
    }
    // Existing combat checks isolate combat/locomotion on flat ground; TerrainChecks exercises production terrain.
    private static CombatSimulation Deploy(ForceRoster roster, IEnumerable<string> selected, int seed = 41) =>
        CombatSimulation.DeployOnTerrain(roster, selected, BattlefieldTerrain.Flat, seed);
    private static CombatSimulation Battle(int count = 1, string definition = "awesome", int seed = 41)
    { var roster = Roster(count, definition); return Deploy(roster, roster.Mechs.Select(item => item.Id), seed); }
    private static CombatUnit Friendly(CombatSimulation sim) => sim.Units.First(item => item.Side == CombatSide.Friendly);
    private static CombatUnit Enemy(CombatSimulation sim) => sim.Units.First(item => item.Side == CombatSide.Enemy);
    private static void Arrange(CombatSimulation sim, float distance = 8)
    {
        sim.EnemyAiEnabled = false;
        Friendly(sim).Position = Vector2.Zero; Enemy(sim).Position = new Vector2(distance, 0);
        Friendly(sim).Facing = Vector2.Right; Enemy(sim).Facing = Vector2.Left;
        sim.RefreshDetection();
    }
    public static void Run(Action<bool, string> check)
    {
        void Reject(Action action, string message)
        {
            var rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            check(rejected, message);
        }
        var roster = Roster(4); var before = JsonSerializer.Serialize(roster.Snapshot());
        var sim = Deploy(roster, roster.Mechs.Select(item => item.Id), 12);
        check(sim.Paused && sim.Units.Count == 8 && sim.Units.All(item => item.Weapons.All(weapon => weapon.Remaining == 0)), "deployment starts paused with ready independent weapons and mirrored force");
        check(sim.Units.All(item => item.Movement == MovementMode.Walk) && sim.Contacts(CombatSide.Friendly).Count == 0, "deployment starts walking outside detection");
        check(sim.Units.Where(item => item.Side == CombatSide.Enemy).Zip(sim.Units.Where(item => item.Side == CombatSide.Friendly))
            .All(pair => pair.First.Spec == pair.Second.Spec && pair.First.Warrior == pair.Second.Warrior), "opposition mirrors definitions and warrior skills");
        Reject(() => Deploy(roster, []), "empty deployment rejected");
        Reject(() => Deploy(roster, ["mech-1", "mech-1"]), "duplicate deployment rejected");
        Reject(() => Deploy(roster, ["unknown"]), "unknown deployment rejected");
        var tooMany = Roster(5);
        Reject(() => Deploy(tooMany, tooMany.Mechs.Select(item => item.Id)), "more than four rejected");
        roster.Assign("mech-1", null);
        check(CombatSimulation.UnavailableReason(roster, roster.Mechs[0])!.Contains("No assigned"), "uncrewed mech explains exclusion");
        Reject(() => Deploy(roster, ["mech-1"]), "uncrewed deployment rejected");
        roster.Assign("mech-1", roster.Warriors[0].Id);
        roster.SetHealth(roster.Warriors[0].Id, WarriorHealth.Injured, new DateOnly(3025, 1, 3), Campaign.StartDate);
        check(CombatSimulation.UnavailableReason(roster, roster.Mechs[0])!.Contains("Injured"), "injured assignment explains exclusion");
        Reject(() => Deploy(roster, ["mech-1"]), "injured pilot deployment rejected");
        roster.SetHealth(roster.Warriors[0].Id, WarriorHealth.Healthy, null, Campaign.StartDate);
        var saved = roster.Snapshot(); var stock = saved.Mechs[0];
        var damaged = stock with
        {
            Locations = stock.Locations.Select(loc => loc.Location == MechLocation.CenterTorso ? loc with { Armor = loc.Armor - 3 } : loc).ToArray(),
            Equipment = stock.Equipment.Select(item => item.Id == roster.Mechs[0].Spec.Weapons[0].Id ? item with { Condition = ComponentCondition.Destroyed } : item).ToArray(),
            Ammo = stock.Ammo.Select(item => item with { Shots = item.Shots / 2 }).ToArray()
        };
        var damagedRoster = ForceRoster.Restore(saved with { Mechs = [damaged] }, Campaign.StartDate);
        var copied = Deploy(damagedRoster, [damaged.Id]);
        check(copied.Units.All(unit => unit.Locations[MechLocation.CenterTorso].Armor == stock.Locations.Single(loc => loc.Location == MechLocation.CenterTorso).Armor
            && unit.Equipment.Values.All(condition => condition == ComponentCondition.Operational)
            && unit.Ammo.All(bin => bin.Shots == unit.Spec.Ammo.Single(item => item.Id == bin.Id).Shots)), "mirror restores armor, equipment and ammo on both sides");
        var damagedBefore = JsonSerializer.Serialize(damagedRoster.Snapshot());
        var randomized = CombatSimulation.DeployOnTerrain(damagedRoster, [damaged.Id], BattlefieldTerrain.Flat, opponent: SimulationOpponent.Randomized);
        check(Friendly(randomized).Locations[MechLocation.CenterTorso].Armor + 3 == Friendly(copied).Locations[MechLocation.CenterTorso].Armor
            && !Friendly(randomized).WeaponUsable(Friendly(randomized).Weapons[0]), "randomized battle retains friendly campaign condition");
        foreach (var battle in new[] { copied, randomized })
        {
            foreach (var unit in battle.Units) unit.ApplyDamage(MechLocation.CenterTorso, 10000);
            battle.End();
        }
        check(JsonSerializer.Serialize(damagedRoster.Snapshot()) == damagedBefore, "neither simulation mode repairs or damages the campaign roster");
        CheckRandomizedOpposition(check);
        var nonoperational = ForceRoster.Restore(saved with { Mechs = [stock with { Locations = stock.Locations.Select(loc => loc.Location == MechLocation.LeftLeg ? loc with { Structure = 0 } : loc).ToArray() }] }, Campaign.StartDate);
        check(CombatSimulation.UnavailableReason(nonoperational, nonoperational.Mechs[0])!.Contains("not operational"), "nonoperational mech explains exclusion");

        var f = Friendly(sim); var e = Enemy(sim);
        var group = sim.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Id).ToArray();
        sim.Move(group, new Vector2(96, 96));
        var destinations = group.Select(id => sim.Unit(id).Destination).ToArray();
        check(destinations.All(point => CombatRules.Clamp(point) == point) && destinations.SelectMany((point, i) => destinations.Skip(i + 1).Select(other => point.DistanceTo(other))).All(distance => distance >= CombatRules.UnitRadius * 2), "group destinations remain separated at arena corner");
        var original = f.Position; sim.Advance(1);
        check(f.Position == original && sim.Elapsed == 0, "paused commands do not advance combat");
        f.Movement = MovementMode.Run; sim.Move([f.Id], new Vector2(-10, 25));
        check(f.Destination == new Vector2(-10, 25) && f.Movement == MovementMode.Run, "new Move replaces order and preserves run mode");
        sim.Paused = false; sim.Advance(.5);
        check(f.Position != original && f.Moving, "resume executes queued movement");

        sim = Battle(); Arrange(sim); f = Friendly(sim); e = Enemy(sim);
        sim.SetPriority([f.Id], e.Id); sim.Paused = false; sim.Advance(.1);
        check(f.Position == Vector2.Zero && f.PriorityTarget == e.Id && f.Order == CombatOrder.Hold, "priority target alone does not cause pursuit");
        sim.Paused = true; sim.Attack([f.Id], e.Id); f.PreferredDistance = 3;
        f.Facing = Vector2.Down; // Already aligned: this check isolates order/target independence.
        sim.Move([f.Id], new Vector2(0, 8));
        check(f.AttackTarget is null && f.PriorityTarget == e.Id, "Move replaces Attack but retains persistent priority");
        sim.Paused = false; sim.Advance(.1);
        check(f.Position.Y > 0 && Math.Abs(f.Position.X) < .001 && f.ShotsFired > 0, "Move fires opportunistically without diverting");
        sim.Attack([f.Id], e.Id); sim.Advance(.5);
        check(f.Position.X > 0 && f.Order == CombatOrder.Attack, "Attack pursues preferred distance");
        sim.Move([f.Id], f.Position); sim.Advance(.1);
        check(f.Order == CombatOrder.Hold, "completed Move holds position");

        CheckTargeting(check);
        CheckExtendedRanges(check);
        CheckDetection(check);
        CheckSensors(check);
        CheckDamage(check);
        CheckNavigation(check);
        CheckFollowing(check);
        CheckLocomotion(check);
        CheckTorsoTwisting(check);
        CheckHeat(check);
        CheckCombatLog(check);
        CheckOutcomes(check);
        CheckLasers(check);
        CheckAutocannons(check);
        CheckMachineGuns(check);
        CheckLrms(check);
        CheckPpcs(check);

        sim = Deploy(roster, roster.Mechs.Select(item => item.Id), 12);
        foreach (var unit in sim.Units.Where(item => item.Side == CombatSide.Friendly)) unit.ApplyDamage(MechLocation.CenterTorso, 5000);
        sim.Paused = false; sim.Advance(.1); sim.End();
        check(JsonSerializer.Serialize(roster.Snapshot()) == before, "deployment, damage and completion never mutate source roster or equipment");
        check(CombatRules.Damage(DraftCatalog.Mech("jenner").Weapons.Single(item => item.Name == "SRM 4")) == 80, "missile rack delivers one full damage packet");
        check(CombatRules.Damage(DraftCatalog.Mech("catapult").Weapons.First(item => item.Name == "LRM 15")) == 150, "LRM packet retains stock per-missile damage");
        var a = Battle(seed: 73); var b = Battle(seed: 73); Arrange(a); Arrange(b); a.Paused = b.Paused = false;
        a.Advance(10); for (var i = 0; i < 100; i++) b.Advance(.1);
        check(a.Units.Zip(b.Units).All(pair => pair.First.Hits == pair.Second.Hits && pair.First.ArmorLost == pair.Second.ArmorLost), "fixed-step combat is independent of frame partitioning");
        RunEngagements(check);
    }
    private static void CheckRandomizedOpposition(Action<bool, string> check)
    {
        foreach (var definitions in new[]
        {
            new[] { "jenner" }, new[] { "awesome", "wolverine" },
            new[] { "wolverine", "catapult", "wolverine" }, new[] { "awesome", "awesome", "awesome", "awesome" },
            new[] { "awesome", "jenner", "catapult", "wolverine" }
        })
        {
            var roster = ForceRoster.Create("Random opposition checks", definitions.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(definitions.Length));
            for (var i = 0; i < definitions.Length; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
            var before = JsonSerializer.Serialize(roster.Snapshot());
            var signatures = new HashSet<string>();
            for (var seed = 0; seed < 12; seed++)
            {
                var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Reverse().Select(mech => mech.Id), BattlefieldTerrain.Flat,
                    seed, SimulationOpponent.Randomized);
                var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
                var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
                check(enemies.Length == definitions.Length && friends.Zip(enemies).All(pair => pair.First.Spec.WeightClass == pair.Second.Spec.WeightClass),
                    "random opposition matches count and each selected weight category, including repeats");
                check(enemies.All(unit => unit.Equipment.Values.All(condition => condition == ComponentCondition.Operational)
                    && unit.Locations.Values.All(loc => loc.Armor == unit.Spec.Locations.Single(stock => stock.Location == loc.Location).Armor)
                    && unit.Ammo.All(bin => bin.Shots == unit.Spec.Ammo.Single(stock => stock.Id == bin.Id).Shots)
                    && unit.Warrior.SkillPoints == unit.Warrior.SkillBudget
                    && unit.Warrior.Rank == (unit.Spec.WeightClass == MechWeightClass.Assault ? WarriorExperience.Veteran : WarriorExperience.Regular)),
                    "drafted enemies are pristine with legal pilots assigned by draft column");
                signatures.Add(string.Join(",", enemies.Select(unit => $"{unit.Spec.Id}/{unit.Warrior.Id}")));
                foreach (var unit in sim.Units) unit.ApplyDamage(MechLocation.CenterTorso, 10000);
                sim.End();
            }
            check(signatures.Count > 1, "fresh enemy drafts vary across simulation seeds");
            check(JsonSerializer.Serialize(roster.Snapshot()) == before, "random drafting and combat preserve the player's roster");
        }
        var fullRoster = InitialDraft.CreateRandom(new Random(73)).Establish();
        var expected = InitialDraft.CreateRandom(new Random(91)).Establish();
        var battle = CombatSimulation.DeployOnTerrain(fullRoster, fullRoster.Mechs.Select(mech => mech.Id), BattlefieldTerrain.Flat, 91, SimulationOpponent.Randomized);
        check(battle.Units.Where(unit => unit.Side == CombatSide.Enemy).Zip(expected.Mechs)
            .All(pair => pair.First.Spec == pair.Second.Spec && pair.First.Warrior == expected.Pilot(pair.Second)!.Spec),
            "random opposition uses the same fresh candidate pools and random selections as a player draft");
    }
    private static void CheckTargeting(Action<bool, string> check)
    {
        var sim = Battle(2); sim.EnemyAiEnabled = false;
        var f = Friendly(sim); var enemies = sim.Units.Where(item => item.Side == CombatSide.Enemy).ToArray();
        f.Position = Vector2.Zero; enemies[0].Position = new Vector2(8, 0); enemies[1].Position = new Vector2(10, 0); sim.RefreshDetection();
        sim.SetPriority([f.Id], enemies[1].Id);
        check(sim.SelectTarget(f) == enemies[1], "priority wins over nearest target");
        sim.Attack([f.Id], enemies[0].Id);
        check(sim.SelectTarget(f) == enemies[0], "explicit Attack wins over priority");
        sim.Hold([f.Id]); f.PriorityTarget = null; f.CurrentTarget = enemies[1].Id;
        check(sim.SelectTarget(f) == enemies[1], "valid current target retained over nearer equivalent target");
        f.CurrentTarget = null;
        check(sim.SelectTarget(f) == enemies[0], "nearest target selected without higher preference");
        f.Firing = FiringMode.HoldFire;
        check(sim.SelectTarget(f) is null, "Hold fire suppresses opportunity and priority targets");
        sim.Attack([f.Id], enemies[0].Id);
        check(sim.SelectTarget(f) == enemies[0], "explicit Attack permits fire during Hold fire");
        foreach (var weapon in f.Weapons) weapon.Enabled = false;
        check(f.Active && sim.SelectTarget(f) is null, "weapon toggles do not disable mech but suppress shots");
        f.Weapons[0].Enabled = true; f.PreferredDistance = 8;
        f.Facing = Vector2.Right;
        sim.Paused = false; sim.Advance(.05);
        check(f.ShotsFired == 1 && f.Weapons[0].Remaining > 0 && f.Weapons[1].Remaining == 0, "only enabled ready weapons fire and receive cooldown");
        var remaining = f.Weapons[0].Remaining;
        sim.Paused = true; sim.Advance(5);
        check(f.Weapons[0].Remaining == remaining, "cooldowns stop while paused");
        f.Weapons[1].Enabled = true; sim.Paused = false; sim.Advance(.05);
        check(f.ShotsFired == 2 && f.Weapons[0].Remaining < f.Weapons[1].Remaining, "newly enabled weapon fires on its independent clock");
        var walking = CombatSimulation.HitChance(f, enemies[0], f.Weapons[0]);
        f.Moving = true; f.Movement = MovementMode.Run;
        check(CombatSimulation.HitChance(f, enemies[0], f.Weapons[0]) < walking, "running reduces firing accuracy");
    }
    private static void CheckExtendedRanges(Action<bool, string> check)
    {
        var stock = DraftCatalog.Mech("jenner").Weapons.First(weapon => weapon.Name == "Medium Laser");
        check(stock.Short == 3 && stock.Medium == 6 && stock.Long == 9, "imported catalog retains stock weapon ranges");
        var sim = HeatFixture([stock], gunnery: 4); var f = Friendly(sim); var e = Enemy(sim);
        var weapon = f.Weapons[0];
        foreach (var (distance, chance) in new[] { (9f, 21d / 36), (9.01f, 10d / 36), (18f, 10d / 36), (18.01f, 3d / 36), (27f, 3d / 36), (27.01f, 0d) })
        {
            e.Position = Vector2.Right * distance; sim.RefreshDetection();
            check(Math.Abs(CombatSimulation.HitChance(f, e, weapon) - chance) < .000001,
                $"tripled range bracket at {distance} hexes has the expected exact hit chance");
            check((sim.SelectTarget(f) == e) == (distance <= 27) && sim.CanFire(f, e, weapon) == (distance <= 27),
                "target selection and discharge use the same tripled maximum range");
            check(Math.Abs(CombatSimulation.HitChance(e, f, e.Weapons[0]) - chance) < .000001,
                "both sides use identical extended firing brackets");
        }
        sim = HeatFixture([stock with { Rear = true }], gunnery: 4); f = Friendly(sim); e = Enemy(sim);
        f.Facing = Vector2.Left; e.Position = new(27, 0); sim.RefreshDetection();
        check(sim.SelectRearTarget(f) == e && sim.CanFire(f, e, f.Weapons[0]), "rear mounts acquire and fire at the extended range boundary");
        e.Position = new(27.01f, 0); sim.RefreshDetection();
        check(sim.SelectRearTarget(f) is null && sim.WeaponStatus(f, f.Weapons[0], e) == "Out of range", "rear targeting and readouts reject shots beyond extended range");

        sim = HeatFixture([HeatGun("minimum", 0, 1) with { Minimum = 3, Short = 6, Medium = 12, Long = 18 }], gunnery: 4);
        f = Friendly(sim); e = Enemy(sim);
        foreach (var (distance, chance) in new[] { (1.5f, 6d / 36), (3f, 10d / 36), (6f, 15d / 36), (9f, 21d / 36) })
        {
            e.Position = Vector2.Right * distance;
            check(Math.Abs(CombatSimulation.HitChance(f, e, f.Weapons[0]) - chance) < .000001,
                $"expanded minimum range preserves proportional penalty severity at {distance} hexes");
        }
        var jenner = Battle(definition: "jenner");
        check(jenner.Units.All(unit => unit.PreferredDistance == 9), "both sides prefer the tripled medium-laser short range");
        var griffin = Battle(definition: "griffin");
        check(griffin.Units.All(unit => Math.Abs(unit.PreferredDistance - 19.5f) < .001), "mixed long-range loadouts use expanded preferences beyond the old cap");
        check(jenner.Units.All(unit => Math.Abs(unit.Position.Y) == 64), "expanded deployment doubles initial separation");
        jenner.EnemyAiEnabled = false;
        foreach (var unit in jenner.Units) unit.Firing = FiringMode.HoldFire;
        f = Friendly(jenner); e = Enemy(jenner); f.Position = Vector2.Zero; e.Position = new(24, 0);
        f.Facing = Vector2.Right; jenner.RefreshDetection(); jenner.Attack([f.Id], e.Id); jenner.Paused = false; jenner.Advance(30);
        check(Math.Abs(f.Position.DistanceTo(e.Position) - 9) < .5f, "Attack settles at the extended stand-off distance rather than the original crowded range");
    }
    private static void CheckDetection(Action<bool, string> check)
    {
        var sim = new CombatSimulation(new[]
        {
            SensorUnit("shooter", "awesome", 6, CombatSide.Friendly, Vector2.Zero),
            SensorUnit("scout", "awesome", 2, CombatSide.Friendly, new Vector2(24, 6)),
            SensorUnit("target", "locust", 6, CombatSide.Enemy, new Vector2(34, 0))
        }, 41);
        sim.EnemyAiEnabled = false;
        var friends = sim.Units.Where(item => item.Side == CombatSide.Friendly).ToArray(); var enemy = Enemy(sim);
        friends[0].Position = Vector2.Zero; friends[0].Facing = Vector2.Right;
        friends[1].Position = new Vector2(24, 6); enemy.Position = new Vector2(34, 0);
        sim.RefreshDetection();
        check(friends[0].Position.DistanceTo(enemy.Position) > CombatRules.DetectionRadius(friends[0], enemy)
            && sim.Detected(CombatSide.Friendly, enemy.Id) && sim.SelectTarget(friends[0]) == enemy, "shared detection enables fire outside shooter's own detection radius");
        sim.Attack([friends[0].Id], enemy.Id); sim.Paused = false; sim.Advance(.05);
        check(friends[0].ShotsFired > 0, "shared out-of-radius contact can actually be fired upon");
        var seen = sim.Contacts(CombatSide.Friendly)[enemy.Id].LastKnownPosition;
        enemy.Position = new Vector2(45, -45); sim.RefreshDetection();
        check(!sim.Detected(CombatSide.Friendly, enemy.Id) && sim.Contacts(CombatSide.Friendly)[enemy.Id].LastKnownPosition == seen, "lost contact leaves stationary last-known marker");
        enemy.Position = new Vector2(-45, -45); sim.Advance(.1);
        check(sim.Contacts(CombatSide.Friendly)[enemy.Id].LastKnownPosition == seen && friends[0].Position.X > 0, "lost-target pursuit follows recorded position without tracking hidden movement");
        friends[0].Position = seen; sim.Advance(.05);
        check(friends[0].Order == CombatOrder.Hold && friends[0].AttackTarget is null, "arrival at unreacquired last-known position ends Attack");
        enemy.Position = new Vector2(12, -2); sim.RefreshDetection(); sim.Attack([friends[0].Id], enemy.Id);
        enemy.Position = new Vector2(-45, -45); sim.RefreshDetection(); enemy.Position = new Vector2(12, -2); sim.Advance(.05);
        check(sim.Detected(CombatSide.Friendly, enemy.Id) && friends[0].Order == CombatOrder.Attack, "reacquisition resumes Attack pursuit");
    }
    private static void CheckDamage(Action<bool, string> check)
    {
        var sim = Battle(); var unit = Friendly(sim);
        var rear = unit.Locations.Values.Select(item => item.RearArmor).ToArray();
        var arm = unit.Locations[MechLocation.LeftArm]; var torso = unit.Locations[MechLocation.LeftTorso];
        var torsoArmor = torso.Armor;
        unit.ApplyDamage(MechLocation.LeftArm, arm.Armor + arm.Structure + 3);
        check(arm.Structure == 0 && torso.Armor == torsoArmor - 3, "arm overkill transfers immediately into side torso front armor");
        unit.ApplyDamage(MechLocation.LeftArm, 2);
        check(torso.Armor == torsoArmor - 5, "subsequent destroyed-arm hit transfers inward");
        check(unit.Weapons.Where(item => item.Spec.Location == MechLocation.LeftArm).All(item => !unit.WeaponUsable(item)), "destroyed location loses mounted weapons");
        unit.ApplyDamage(MechLocation.RightTorso, 10000);
        check(!unit.Intact(MechLocation.RightArm) && !unit.Intact(MechLocation.RightTorso), "side torso loss also destroys attached arm");
        check(unit.Locations.Values.Select(item => item.RearArmor).SequenceEqual(rear), "front damage leaves rear armor unchanged");
        sim = Battle(); unit = Friendly(sim); var speed = unit.Speed;
        var leg = unit.Locations[MechLocation.LeftLeg]; var center = unit.Locations[MechLocation.CenterTorso]; var centerArmor = center.Armor;
        var sideArmor = unit.Locations[MechLocation.LeftTorso].Armor;
        unit.ApplyDamage(MechLocation.LeftLeg, leg.Armor + leg.Structure + 2);
        check(center.Armor == centerArmor - 2 && unit.Locations[MechLocation.LeftTorso].Armor == sideArmor, "leg overkill goes directly to center torso front armor");
        check(Math.Abs(unit.Speed - speed / 2) < .001 && unit.Active, "one lost leg halves movement without defeat");
        unit.ApplyDamage(MechLocation.RightLeg, unit.Locations[MechLocation.RightLeg].Armor + unit.Locations[MechLocation.RightLeg].Structure);
        check(unit.Status == CombatStatus.Disabled && unit.Speed == 0, "both legs lost disables rather than destroys");
        sim = Battle(definition: "griffin"); unit = Friendly(sim);
        foreach (var location in unit.Weapons.Select(item => item.Spec.Location).Distinct())
            unit.ApplyDamage(location, unit.Locations[location].Armor + unit.Locations[location].Structure);
        check(unit.Status == CombatStatus.Disabled, "loss of all physical weapons disables mech");
        sim = Battle(); unit = Friendly(sim); unit.ApplyDamage(MechLocation.Head, 1000);
        check(unit.Status == CombatStatus.Destroyed, "head destruction destroys mech");
        sim = Battle(); unit = Friendly(sim);
        unit.ApplyDamage(MechLocation.LeftTorso, unit.Locations[MechLocation.LeftTorso].Armor + unit.Locations[MechLocation.LeftTorso].Structure);
        unit.ApplyDamage(MechLocation.LeftArm, 3);
        check(unit.Locations[MechLocation.CenterTorso].Armor == unit.Locations[MechLocation.CenterTorso].InitialArmor - 3, "transfer continues through destroyed side torso");
        unit.ApplyDamage(MechLocation.CenterTorso, 10000);
        check(unit.Status == CombatStatus.Destroyed, "center torso loss destroys mech");
    }
    private static void CheckNavigation(Action<bool, string> check)
    {
        var sim = Battle(4); sim.EnemyAiEnabled = false;
        var friends = sim.Units.Where(item => item.Side == CombatSide.Friendly).ToArray();
        var ids = friends.Select(item => item.Id).ToArray();
        sim.Move(ids, new Vector2(0, 15)); sim.Paused = false;
        var separated = true;
        var startDepth = friends[0].Position.Y;
        for (var tick = 0; tick < 400; tick++)
        {
            sim.Advance(.1);
            separated &= sim.Units.All(item => CombatRules.Clamp(item.Position).DistanceTo(item.Position) < .001)
                && sim.Units.SelectMany((unit, i) => sim.Units.Skip(i + 1).Select(other => unit.Position.DistanceTo(other.Position))).All(distance => distance >= CombatRules.UnitRadius * 2 - .001);
        }
        check(separated && friends.All(item => item.Position.Y < startDepth - 12), "moving groups stay in arena and avoid overlap while advancing");
        sim = Battle(); Arrange(sim, 4); var f = Friendly(sim); var e = Enemy(sim);
        f.Position = new Vector2(94, 0); e.Position = new Vector2(90, 0); f.PreferredDistance = 63;
        f.Facing = Vector2.Left;
        sim.RefreshDetection(); sim.Attack([f.Id], e.Id); sim.Paused = false; sim.Advance(.2);
        check(f.ShotsFired > 0 && CombatRules.Clamp(f.Position) == f.Position, "unachievable distance does not prevent valid fire or escape arena");
        sim = Battle(); f = Friendly(sim); e = Enemy(sim); sim.Paused = false; var start = e.Position;
        sim.Advance(1);
        check(e.Order == CombatOrder.Move && e.Position.DistanceTo(Vector2.Zero) < start.DistanceTo(Vector2.Zero), "AI advances toward center before contact");
        f.Position = e.Position + new Vector2(10, 0); sim.Advance(.05);
        check(e.Order == CombatOrder.Attack && e.AttackTarget == f.Id && sim.Detected(CombatSide.Enemy, f.Id), "AI attacks detected contacts using same rules");
    }
    private static void CheckOutcomes(Action<bool, string> check)
    {
        foreach (var outcome in new[] { CombatOutcome.Victory, CombatOutcome.Defeat, CombatOutcome.Draw })
        {
            var sim = Battle(); sim.EnemyAiEnabled = false;
            if (outcome != CombatOutcome.Defeat) Enemy(sim).ApplyDamage(MechLocation.CenterTorso, 10000);
            if (outcome != CombatOutcome.Victory) Friendly(sim).ApplyDamage(MechLocation.CenterTorso, 10000);
            sim.Paused = false; sim.Advance(.05);
            check(sim.Outcome == CombatOutcome.Ongoing && sim.WindingDown, $"{outcome} begins the wind-down");
            sim.Advance(CombatRules.WindDownSeconds - .05);
            check(sim.Outcome == CombatOutcome.Ongoing && !sim.Paused, $"{outcome} wind-down continues the simulation");
            sim.Advance(.05);
            check(sim.Outcome == outcome && sim.Paused, $"{outcome} stops combat after the wind-down");
        }
        // Every location is one structure point: any successful shot is lethal. Both have a single ready gun.
        var source = Battle();
        var lethal = source.Units.Select((unit, i) => new CombatUnit(unit.Id, unit.Spec, unit.Warrior with { Gunnery = -10 }, unit.Side,
            unit.Locations.Values.Select(loc => new LocationCondition(loc.Location, 0, loc.RearArmor, 1)),
            unit.Equipment.Select(item => new EquipmentCondition(item.Key, item.Value)), unit.Ammo, new Vector2(i * 2, 0))).ToArray();
        foreach (var unit in lethal)
        {
            unit.Facing = unit.Side == CombatSide.Friendly ? Vector2.Right : Vector2.Left;
            foreach (var weapon in unit.Weapons) weapon.Enabled = false;
            unit.Weapons[0].Enabled = true;
        }
        // Damage packet 10 transfers through any nonfatal part into the one-point center torso. The PPC bolts land after their flight.
        var simultaneous = new CombatSimulation(lethal, 5) { EnemyAiEnabled = false, Paused = false };
        simultaneous.Advance(CombatRules.StepSeconds + CombatRules.PpcFlightSeconds + CombatRules.WindDownSeconds);
        check(simultaneous.Outcome == CombatOutcome.Draw && lethal.All(item => item.ShotsFired == 1), "same-instant lethal return shots resolve before outcome");
        var ended = Battle(); ended.End(); ended.Advance(2);
        check(ended.Outcome == CombatOutcome.Ended && ended.Elapsed == 0, "End Simulation works before any combat and freezes state");
    }
    private static void RunEngagements(Action<bool, string> check)
    {
        foreach (var definition in new[] { "jenner", "griffin", "awesome" })
        {
            var sim = Battle(definition: definition, seed: 183); sim.EnemyAiEnabled = true; sim.Paused = false;
            sim.Move([Friendly(sim).Id], Vector2.Zero);
            for (var second = 0; second < 1200 && sim.Outcome == CombatOutcome.Ongoing; second++)
            {
                var friendly = Friendly(sim); var enemy = Enemy(sim);
                if (sim.Detected(CombatSide.Friendly, enemy.Id)) sim.Attack([friendly.Id], enemy.Id);
                sim.Advance(1);
            }
            check(sim.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Draw, $"{definition} full engagement reaches a result");
            GD.Print($"Combat tuning: {definition} {sim.Elapsed:0}s, {sim.Outcome}");
        }
        var roster = ForceRoster.Create("Mixed lance", new[] { "firestarter", "griffin", "catapult", "awesome" }.Select(DraftCatalog.Mech), DraftCatalog.Warriors.Take(4));
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var lance = Deploy(roster, roster.Mechs.Select(item => item.Id), 183); lance.Paused = false;
        lance.Move(lance.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Id), Vector2.Zero);
        for (var second = 0; second < 1200 && lance.Outcome == CombatOutcome.Ongoing; second++)
        {
            foreach (var unit in lance.Units.Where(item => item.Side == CombatSide.Friendly && item.Active))
            {
                var target = lance.Units.Where(item => item.Side == CombatSide.Enemy && item.Active && lance.Detected(unit.Side, item.Id))
                    .OrderBy(item => item.Position.DistanceSquaredTo(unit.Position)).FirstOrDefault();
                if (target is not null) lance.Attack([unit.Id], target.Id);
            }
            lance.Advance(1);
        }
        check(lance.Outcome != CombatOutcome.Ongoing, "mixed four-on-four engagement reaches a result");
        GD.Print($"Combat tuning: mixed lance {lance.Elapsed:0}s, {lance.Outcome}");
    }
}
