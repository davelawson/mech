using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    /// <summary>Battle silhouette outlines: mask twins and their wiring, and in rendered runs the ring itself, measured by
    /// rendering each frame with and without the outline and comparing the battlefield pixels.</summary>
    private async Task TestBattlefieldOutline()
    {
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1440, 900);
        var roster = ForceRoster.Create("Outline checks", [DraftCatalog.Mech("jenner"), DraftCatalog.Mech("atlas")], TestPilots.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var field = new CombatViewport { Position = new Vector2(40, 40), Size = new Vector2(900, 650) };
        _app.SetProcessInput(false);
        AddChild(field); await Frames();
        if (_screenshots) { GetWindow().GrabFocus(); await Frames(); }
        async Task Hover(CombatUnit? unit)
        {
            var at = field.GlobalPosition + (unit is null ? new Vector2(4, 4) : field.ScreenPoint(unit.Position, field.ModelOf(unit.Id).Height * .55f));
            // A windowed run hovers through the real pointer and window focus, so give it a few tries to settle.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (DisplayServer.GetName() != "headless") GetViewport().WarpMouse(at);
                MouseMotion(at, Vector2.Zero); await Frames();
                if (field.HoveredMech == unit?.Id || unit is { Defeated: true }) break;
                if (_screenshots) GetWindow().GrabFocus();
            }
        }
        try
        {
            var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
            field.Display(sim); await Frames();
            var models = sim.Units.Select(unit => field.ModelOf(unit.Id)).ToArray();
            Check(models.All(model => model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                    .Where(mesh => !model.SilhouetteTwins.Contains(mesh))
                    .All(mesh => Enumerable.Range(0, mesh.Mesh.GetSurfaceCount()).All(surface => mesh.GetActiveMaterial(surface)?.NextPass is null))),
                "battle models carry no inverted-hull outline pass");
            Check(models.Select(model => model.SilhouetteId).Distinct().Count() == models.Length && models.All(model => model.SilhouetteId > 0),
                "every battle model has its own silhouette mask id");
            Check(models.All(model => model.SilhouetteTwins.Count > 0
                    && model.SilhouetteTwins.All(twin => twin.Layers == MechModel.SilhouetteLayerBit && twin.GetParent() is MeshInstance3D)),
                "mask twins sit under their meshes on the silhouette layer");
            Check((field.Camera.CullMask & MechModel.SilhouetteLayerBit) == 0, "the battlefield camera never renders mask twins");
            Check(MechModel.Create(DraftCatalog.Mech("jenner"), CombatSide.Friendly) is var hangar
                && hangar.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Any(mesh => mesh.GetActiveMaterial(0)?.NextPass is ShaderMaterial)
                && hangar.SilhouetteId == 0, "hangar models keep their inverted-hull outline");
            hangar.Free();

            var friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
            var enemy = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
            friendly.Position = new Vector2(0, 150);
            foreach (var other in sim.Units.Where(unit => unit != friendly)) other.Position = new Vector2(600 + 60 * Array.IndexOf(sim.Units.ToArray(), other), -900);
            sim.RefreshDetection();
            field.Camera.Size = 220; field.Camera.Position = new Vector3(0, 150, 750); field.Camera.LookAt(new Vector3(0, 22, 150));
            field._Process(0);
            Check(field.ModelOf(friendly.Id).SilhouetteEnabled, "an active mech is outlined");
            await Hover(friendly);
            Check(field.HoveredMech == friendly.Id && field.OutlineMaterial.GetShaderParameter("hovered").AsInt32() == field.ModelOf(friendly.Id).SilhouetteId,
                "hover selects the hovered mech's wider, lighter outline");
            await Hover(null);
            Check(field.HoveredMech is null && field.OutlineMaterial.GetShaderParameter("hovered").AsInt32() == 0, "no hover, no hover outline");

            if (DisplayServer.GetName() != "headless")
            {
                var ring = await OutlineDiff(field, friendly.Id);
                Check(ring.Outside == ring.Total && ring.Total > 50, $"outline pixels lie only outside the silhouette ({ring.Outside} of {ring.Total})");
                Check(ring.Widths.Count > 10 && ring.Widths.All(width => width is CombatViewport.OutlinePixels or CombatViewport.OutlinePixels + 1)
                        && ring.Widths.Count(width => width == CombatViewport.OutlinePixels) > ring.Widths.Count / 2,
                    $"the outline is {CombatViewport.OutlinePixels} pixels across upright edges (one more where they slant): {string.Join(",", ring.Widths)}");
                Check(ring.Farthest <= 6 && ring.Missing == 0,
                    $"every outline pixel is within {CombatViewport.OutlinePixels} pixels of the silhouette and none there is left out (farthest² {ring.Farthest}, missing {ring.Missing})");
                await Hover(friendly);
                var hovered = await OutlineDiff(field, friendly.Id);
                Check(field.HoveredMech == friendly.Id, "the pointer kept hovering the mech while its outline was measured");
                Check(hovered.Widths.Count > 10 && hovered.Widths.All(width => width is CombatViewport.HoveredOutlinePixels or CombatViewport.HoveredOutlinePixels + 1)
                        && hovered.Widths.Count(width => width == CombatViewport.HoveredOutlinePixels) > hovered.Widths.Count / 2
                        && hovered.Farthest <= 20 && hovered.Missing == 0,
                    $"a hovered mech's outline is {CombatViewport.HoveredOutlinePixels} pixels wide: {string.Join(",", hovered.Widths)}, farthest² {hovered.Farthest}, missing {hovered.Missing}");
                Check(hovered.Brightness > ring.Brightness + .01f && hovered.Saturation < ring.Saturation - .05f, $"a hovered mech's outline is lighter (value {ring.Brightness:0.000} → {hovered.Brightness:0.000}, saturation {ring.Saturation:0.000} → {hovered.Saturation:0.000})");
                await Hover(null);
                await Capture("combat-outline");

                // A second mech in front: its outline crosses the rear one, and the rear outline never draws over it.
                var front = sim.Units.Last(unit => unit.Side == CombatSide.Friendly);
                front.Position = new Vector2(30, 330); field._Process(0);
                var overlap = await OutlineDiff(field, friendly.Id, front.Id);
                Check(overlap.OverFront == 0, $"a rear mech's outline never draws over the mech in front ({overlap.OverFront} pixels)");
                Check(overlap.OverRear > 0, "the front mech's outline draws over the rear mech");
                await Capture("combat-outline-overlap");
                front.Position = new Vector2(600, -900);

                // Defeated: no outline, hovered or not.
                friendly.ApplyDamage(MechLocation.CenterTorso, friendly.Locations[MechLocation.CenterTorso].Armor + friendly.Locations[MechLocation.CenterTorso].Structure);
                field._Process(0); await Hover(friendly);
                var defeated = await OutlineDiff(field, friendly.Id);
                Check(friendly.Defeated && defeated.Total == 0 && !field.ModelOf(friendly.Id).SilhouetteEnabled,
                    $"a defeated mech has no outline, even hovered ({defeated.Total} pixels)");
                await Hover(null);

                // A low rock hides the legs: their edge draws at half opacity, the upper body's at full.
                var lowRock = new BattlefieldTerrain(3, [new(TerrainKind.Rock, new Vector2(0, 240), 30, 27)], []);
                sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), lowRock);
                friendly = sim.Units.First(unit => unit.Side == CombatSide.Friendly); friendly.Position = new Vector2(0, 150);
                foreach (var other in sim.Units.Where(unit => unit != friendly)) other.Position = new Vector2(600 + 60 * Array.IndexOf(sim.Units.ToArray(), other), -900);
                sim.RefreshDetection(); field.Display(sim);
                field.Camera.Size = 220; field.Camera.Position = new Vector3(0, 75, 750); field.Camera.LookAt(new Vector3(0, 22, 150));
                field._Process(0);
                var occluded = await OutlineDiff(field, friendly.Id);
                Check(occluded.UpperAlpha > .85f, $"the visible upper body's outline is solid (alpha {occluded.UpperAlpha:0.00})");
                Check(occluded.LowerAlpha is > .3f and < .7f, $"the edge hidden behind the rock draws at half opacity (alpha {occluded.LowerAlpha:0.00})");
                await Capture("combat-outline-occluded");
            }
        }
        finally
        {
            field.QueueFree(); _app.SetProcessInput(true); GetWindow().Size = originalSize;
            await Frames();
        }
    }

    private sealed record OutlineMeasure(int Total, int Outside, List<int> Widths, float Brightness, int OverFront, int OverRear,
        float UpperAlpha, float LowerAlpha, int Farthest, int Missing, float Saturation);

    /// <summary>Renders the battlefield with and without every outline and compares the two. Rows are measured outward
    /// from the left edge of the first mech's silhouette.</summary>
    private async Task<OutlineMeasure> OutlineDiff(CombatViewport field, string id, string? frontId = null)
    {
        async Task<Image> Render()
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            return field.SceneViewport.GetTexture().GetImage();
        }
        var on = await Render();
        var mask = field.SilhouetteMask.GetTexture().GetImage();
        var material = field.OutlineMaterial;
        var shown = material.GetShaderParameter("width").AsInt32(); var hoveredShown = material.GetShaderParameter("hovered_width").AsInt32();
        material.SetShaderParameter("width", 0); material.SetShaderParameter("hovered_width", 0);
        var off = await Render();
        material.SetShaderParameter("width", shown); material.SetShaderParameter("hovered_width", hoveredShown);
        var target = field.ModelOf(id).SilhouetteId;
        var front = frontId is null ? 0 : field.ModelOf(frontId).SilhouetteId;
        int MaskId(int x, int y) => mask.GetPixel(x, y) is var texel && texel.A < .5f ? 0 : texel.R8 - 64;
        bool Differs(int x, int y) => on.GetPixel(x, y) is var a && off.GetPixel(x, y) is var b
            && Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > .02f;
        int total = 0, outside = 0, overFront = 0, overRear = 0, farthest = 0, missing = 0; var brightness = 0f; var saturation = 0f;
        var reach = Math.Max(shown, hoveredShown);
        // Squared distance to the target's nearest silhouette pixel, beyond the outline's reach when none is near.
        int Distance(int x, int y)
        {
            var best = int.MaxValue;
            for (var dy = -reach - 1; dy <= reach + 1; dy++)
                for (var dx = -reach - 1; dx <= reach + 1; dx++)
                    if (x + dx >= 0 && y + dy >= 0 && x + dx < on.GetWidth() && y + dy < on.GetHeight() && MaskId(x + dx, y + dy) == target)
                        best = Math.Min(best, dx * dx + dy * dy);
            return best;
        }
        var width = material.GetShaderParameter("hovered").AsInt32() == target ? hoveredShown : shown;
        for (var y = 0; y < on.GetHeight(); y++)
            for (var x = 0; x < on.GetWidth(); x++)
            {
                if (!Differs(x, y)) continue;
                total++; brightness += on.GetPixel(x, y).V; saturation += on.GetPixel(x, y).S;
                var under = MaskId(x, y);
                if (under == 0) outside++;
                if (front == 0 && Distance(x, y) is var away && away != int.MaxValue) farthest = Math.Max(farthest, away);
                if (front != 0 && under == front) overFront++;
                if (front != 0 && under == target) overRear++;
            }
        // Every uncovered pixel within the width, straight across from the silhouette, is outlined.
        if (front == 0)
            for (var y = 0; y < on.GetHeight(); y++)
                for (var x = 0; x < on.GetWidth(); x++)
                    if (MaskId(x, y) == 0 && Distance(x, y) <= width * width && !Differs(x, y)) missing++;
        // Rows through the silhouette: count the changed pixels left of its leftmost pixel.
        var widths = new List<int>(); var top = -1; var bottom = -1;
        var edges = new List<(int Y, int X)>();
        for (var y = 0; y < on.GetHeight(); y++)
        {
            var left = Enumerable.Range(0, on.GetWidth()).FirstOrDefault(x => MaskId(x, y) == target, -1);
            if (left < 0) continue;
            if (top < 0) top = y;
            bottom = y; edges.Add((y, left));
        }
        float Alpha(int x, int y, Color solid)
        {
            Color a = on.GetPixel(x, y), b = off.GetPixel(x, y);
            var full = new Vector3(solid.R - b.R, solid.G - b.G, solid.B - b.B);
            return full.LengthSquared() < 1e-4f ? 1 : new Vector3(a.R - b.R, a.G - b.G, a.B - b.B).Dot(full) / full.LengthSquared();
        }
        var span = bottom - top;
        Color? solid = null; var upper = new List<float>(); var lower = new List<float>();
        foreach (var (y, left) in edges)
        {
            // Skip rows near the top and bottom, where the edge runs along the row rather than across it.
            if (y < top + span * .2f || y > bottom - span * .05f || left < 6) continue;
            if (Enumerable.Range(-6, 13).Any(dy => edges.Where(edge => edge.Y == y + dy).Select(edge => edge.X).DefaultIfEmpty(-1000).First() is var other && Math.Abs(other - left) > 1)) continue;
            var across = 0;
            while (across < 8 && Differs(left - 1 - across, y)) across++;
            widths.Add(across);
            if (across == 0) continue;
            solid ??= on.GetPixel(left - 1, y);
            (y < top + span * .5f ? upper : lower).Add(Alpha(left - 1, y, solid.Value));
        }
        return new(total, outside, widths, total == 0 ? 0 : brightness / total, overFront, overRear,
            upper.Count == 0 ? 0 : upper.Average(), lower.Count == 0 ? 0 : lower.Where((_, i) => i >= lower.Count / 2).DefaultIfEmpty(0).Average(),
            farthest, missing, total == 0 ? 0 : saturation / total);
    }
}
