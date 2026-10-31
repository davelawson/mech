using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestDetectionOverlay()
    {
        var warriors = new[] { DraftCatalog.Warriors.First(warrior => warrior.Sensors == 6) }
            .Concat(DraftCatalog.Warriors.Where(warrior => warrior.Sensors == 2).Take(2)).ToArray();
        var roster = ForceRoster.Create("Range checks", Enumerable.Repeat(DraftCatalog.Mech("locust"), 3), warriors);
        for (var i = 0; i < 3; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        for (var i = 0; i < friends.Length; i++) friends[i].Position = new(i * 20, 0);
        var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy); enemy.Position = new(5, 0);
        sim.RefreshDetection();
        var field = new CombatViewport { Size = new(900, 650) };
        AddChild(field);
        try
        {
            field.Display(sim); field.Selection = [friends[0].Id, friends[1].Id]; await Frames();
            Check(field.DetectionRanges.Select(range => range.Observer).SequenceEqual(friends.Take(2))
                && field.DetectionRanges.Select(range => range.Identification).SequenceEqual([50f, 100f])
                && field.DetectionRanges.All(range => range.Detail is null),
                "generic group overlay includes each selected active friendly with its own sensor radius");
            Check(field.SensorRangeText == "Selected mechs · 100t: identify 50–100 hexes (blue) · 20–100t: 25–100 hexes",
                "generic group readout distinguishes contributor ranges from the target-mass span");
            await Capture("combat-range-union");
            field.InspectedEnemy = enemy.Id; await Frames();
            Check(field.DetectionRanges.Select(range => range.Identification).SequenceEqual([25f, 50f])
                && field.DetectionRanges.Select(range => range.Detail).SequenceEqual([(float?)12.5f, 25f])
                && field.SensorRangeText.Contains("identify 25–50 hexes (blue) · status 12.5–25 (gold)"),
                "target-specific group readout reports separate identification and status minimum–maximum ranges");
            await Capture("combat-range-union-inspection");
            field.Selection = [friends[1].Id, friends[0].Id];
            Check(field.DetectionRanges.Select(range => range.Identification).Order().SequenceEqual([25f, 50f]),
                "changing selection order preserves the group's coverage");
            foreach (var location in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
                friends[1].ApplyDamage(location, friends[1].Locations[location].Armor + friends[1].Locations[location].Structure);
            field.Selection = [friends[1].Id, friends[0].Id]; await Frames();
            Check(field.DetectionRanges is [{ Observer: var observer, Identification: 25f, Detail: 12.5f }] && observer == friends[0]
                && field.SensorRangeText.StartsWith(friends[0].Warrior.Callsign),
                "disabled selections are excluded and one active contributor keeps the single-mech readout");
            friends[0].ApplyDamage(MechLocation.Head, 10000); await Frames();
            Check(field.DetectionRanges.Count == 0 && field.SensorRangeText == "",
                "destroyed and disabled selections produce no overlay despite an unselected active observer");
            field.Selection = []; Check(field.DetectionRanges.Count == 0, "empty selection produces no overlay");
            field.Selection = [friends[2].Id]; field.InspectedEnemy = null;
            Check(field.DetectionRanges is [{ Identification: 100f, Detail: null }] && field.SensorRangeText.Contains("circle: 100t"),
                "single-mech generic readout retains its existing form");
            field.InspectedEnemy = enemy.Id; enemy.Position = new(-90, -90); sim.RefreshDetection(); field._Process(0);
            Check(field.DetectionRanges is [{ Identification: 100f, Detail: null }] && field.SensorRangeText.Contains("circle: 100t"),
                "losing the inspected contact restores generic identification ranges");
        }
        finally { field.QueueFree(); await Frames(); }
    }
}
