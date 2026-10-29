using Godot;

namespace Mech;

internal static class RosterUi
{
    public static TextureRect Picture(Node parent, string path, Vector2 minimum)
    {
        var picture = new TextureRect { Texture = Ui.LoadTexture(path), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, CustomMinimumSize = minimum };
        parent.AddChild(picture); return picture;
    }
    public static Label Text(Node parent, string text, int size = 14, Color? color = null)
    { var label = Ui.Label(parent, text, size, color); label.AutowrapMode = TextServer.AutowrapMode.WordSmart; return label; }
    public static VBoxContainer Scroll(Node parent)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        parent.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10); scroll.AddChild(body); return body;
    }
    public static void Clear(Node parent)
    { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    public static void TrapFocus(Control scope)
    {
        var controls = scope.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(item => item.IsVisibleInTree() && item.FocusMode == Control.FocusModeEnum.All && item is not BaseButton { Disabled: true }).ToArray();
        if (controls.Length == 0) return;
        for (var i = 0; i < controls.Length; i++)
        {
            var control = controls[i];
            control.FocusNext = controls[(i + 1) % controls.Length].GetPath();
            control.FocusPrevious = controls[(i + controls.Length - 1) % controls.Length].GetPath();
            control.FocusNeighborTop = control.FocusPrevious; control.FocusNeighborBottom = control.FocusNext;
            control.FocusNeighborLeft = control.FocusPrevious; control.FocusNeighborRight = control.FocusNext;
        }
    }
    public static string SkillText(WarriorSpec warrior) => $"Gunnery {warrior.Gunnery}  /  Piloting {warrior.Piloting}  /  Sensors {warrior.Sensors}";
    public static string SkillBudgetText(WarriorSpec warrior) => $"Skill points: {warrior.SkillPoints}/{warrior.SkillBudget} spent\n"
        + $"Gunnery {WarriorSkills.Cost(warrior.Gunnery)} · Piloting {WarriorSkills.Cost(warrior.Piloting)} · Sensors {WarriorSkills.Cost(warrior.Sensors)}";
}

/// <summary>A location map drawn from the selected unit's real armor/structure, rather than decorative image labels.</summary>
public partial class ArmorDiagram : Control
{
    private MechSpec _spec = null!;
    private OwnedMech? _unit;
    public void SetUnit(MechSpec spec, OwnedMech? unit) { _spec = spec; _unit = unit; QueueRedraw(); }
    public override void _Ready() { CustomMinimumSize = new Vector2(260, 278); MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        if (_spec is null) return;
        var scale = Math.Min(Size.X / 300, Size.Y / 278); var origin = new Vector2((Size.X - 300 * scale) / 2, 0);
        var boxes = new Dictionary<MechLocation, Rect2>
        {
            [MechLocation.Head] = new(119, 4, 62, 42), [MechLocation.CenterTorso] = new(111, 53, 78, 96),
            [MechLocation.RightTorso] = new(56, 57, 49, 86), [MechLocation.LeftTorso] = new(195, 57, 49, 86),
            [MechLocation.RightArm] = new(4, 68, 46, 118), [MechLocation.LeftArm] = new(250, 68, 46, 118),
            [MechLocation.RightLeg] = new(70, 156, 75, 116), [MechLocation.LeftLeg] = new(155, 156, 75, 116)
        };
        var font = ThemeDB.FallbackFont;
        foreach (var (location, box) in boxes)
        {
            var spec = _spec.Locations.Single(item => item.Location == location);
            var state = _unit?.Locations.Single(item => item.Location == location);
            var armor = state?.Armor ?? spec.Armor; var structure = state?.Structure ?? spec.Structure;
            var rect = new Rect2(origin + box.Position * scale, box.Size * scale);
            var color = structure == 0 ? new Color("a74c41") : armor < spec.Armor ? Ui.Gold : new Color("83a58d");
            DrawRect(rect, new Color(color, .14f)); DrawRect(rect, color, false, 1);
            var fs = Math.Max(9, (int)(12 * scale));
            DrawString(font, rect.Position + new Vector2(3, 15 * scale), DraftCatalog.Abbreviation(location), HorizontalAlignment.Center, rect.Size.X - 6, fs, color);
            DrawString(font, rect.Position + new Vector2(3, 30 * scale), $"{armor}/{spec.Armor}", HorizontalAlignment.Center, rect.Size.X - 6, fs, Ui.Text);
        }
    }
}

public partial class MechDetails : VBoxContainer
{
    public void Display(MechSpec spec, OwnedMech? unit = null)
    {
        RosterUi.Clear(this);
        AddThemeConstantOverride("separation", 8);
        RosterUi.Text(this, spec.DisplayName, 23, Ui.Gold);
        RosterUi.Text(this, $"{DefinitionText.WeightClass(spec.WeightClass).ToUpperInvariant()}  ·  {spec.Tonnage} TONS  ·  {spec.Role}", 12, Ui.Muted);
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 150) };
        AddChild(tabs);
        var overview = RosterUi.Scroll(tabs); overview.GetParent<Control>().Name = "Overview";
        RosterUi.Text(overview, unit?.Status ?? "Stock configuration · fully operational", 16, new Color("a5c4a5"));
        RosterUi.Text(overview, $"MOVEMENT\nWalk {spec.Walk}  ·  Run {spec.Run}  ·  Jump {spec.Jump} MP");
        RosterUi.Text(overview, $"PROTECTION\nArmor {unit?.Locations.Sum(item => item.Armor + item.RearArmor) ?? spec.ArmorTotal}/{spec.ArmorTotal}  ·  Structure {unit?.Locations.Sum(item => item.Structure) ?? spec.StructureTotal}/{spec.StructureTotal}");
        RosterUi.Text(overview, $"HEAT\n{spec.HeatSinks} {DefinitionText.HeatSinks(spec.HeatSinkType)} heat sinks · {spec.HeatSinks * spec.CoolingPerSink} heat dissipation\nAll weapons: {spec.Weapons.Sum(item => item.Heat)} heat before movement");
        RosterUi.Text(overview, $"ENGINE\n{spec.Engine}\nIntroduced {spec.Introduced}");
        RosterUi.Text(overview, "Stock 3025 specifications · ranges in hexes.\nWeapon heat is per shot; actual firing choices determine total heat.", 12, Ui.Muted);
        var armor = RosterUi.Scroll(tabs); armor.GetParent<Control>().Name = "Armor";
        var diagram = new ArmorDiagram(); armor.AddChild(diagram); diagram.SetUnit(spec, unit);
        RosterUi.Text(armor, "Front view · numbers show front armor / stock armor", 11, Ui.Muted);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 12); armor.AddChild(grid);
        foreach (var heading in new[] { "Location", "Front", "Rear", "Internal" }) Ui.Label(grid, heading, 12, Ui.Gold);
        foreach (var location in spec.Locations)
        {
            var state = unit?.Locations.Single(item => item.Location == location.Location);
            Ui.Label(grid, DraftCatalog.LocationName(location.Location), 12);
            Ui.Label(grid, $"{state?.Armor ?? location.Armor}/{location.Armor}", 12);
            Ui.Label(grid, location.RearArmor == 0 ? "—" : $"{state?.RearArmor ?? location.RearArmor}/{location.RearArmor}", 12);
            Ui.Label(grid, $"{state?.Structure ?? location.Structure}/{location.Structure}", 12);
        }
        var weapons = RosterUi.Scroll(tabs); weapons.GetParent<Control>().Name = "Weapons";
        foreach (var weapon in spec.Weapons)
        {
            var status = unit is null ? "Operational" : unit.WeaponUsable(weapon) ? unit.Condition(weapon.Id).ToString() : "Unavailable";
            RosterUi.Text(weapons, $"{weapon.Name} · {DraftCatalog.Abbreviation(weapon.Location)}{(weapon.Rear ? " · rear" : "")}  /  {status}", 14, Ui.Gold);
            RosterUi.Text(weapons, $"Heat {weapon.Heat} · Damage {DefinitionText.Damage(weapon.DamageProfile)}\nRange {weapon.Short * CombatRules.WeaponRangeScale} / {weapon.Medium * CombatRules.WeaponRangeScale} / {weapon.Long * CombatRules.WeaponRangeScale}{(weapon.Minimum > 0 ? $" · minimum {weapon.Minimum * CombatRules.WeaponRangeScale}" : "")}", 12);
        }
        RosterUi.Text(weapons, "AMMUNITION", 13, Ui.Gold);
        if (spec.Ammo.Length == 0) RosterUi.Text(weapons, "Energy weapons · no ammunition required", 12, Ui.Muted);
        foreach (var bin in spec.Ammo)
            RosterUi.Text(weapons, $"{bin.Name} · {DraftCatalog.Abbreviation(bin.Location)}\n{unit?.Ammo.Single(item => item.Id == bin.Id).Shots ?? bin.Shots}/{bin.Shots} shots · {unit?.Condition(bin.Id).ToString() ?? "Operational"}", 12);
        var systems = RosterUi.Scroll(tabs); systems.GetParent<Control>().Name = "Systems";
        RosterUi.Text(systems, "INSTALLED COMPONENTS", 13, Ui.Gold);
        foreach (var component in spec.Components.OrderBy(item => item.Location))
            RosterUi.Text(systems, $"{DraftCatalog.Abbreviation(component.Location)} · {component.Name}{(component.Slots > 1 ? $" ({component.Slots} critical slots)" : "")}\n{unit?.Condition(component.Id).ToString() ?? "Operational"}", 12);
    }
}
