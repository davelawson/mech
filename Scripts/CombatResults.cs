using Godot;

namespace Mech;

/// <summary>The post-battle summary's three tabs: the outcome with the company's state, a table of every mech's record by lance and
/// enemy group, and the battle's highlights.</summary>
public partial class CombatScreen
{
    private const int MechColumn = 190, PilotColumn = 150, StatusColumn = 116, NumberColumn = 66, WeaponColumn = 190;
    public static readonly Color FriendlyHighlight = new("9fd3ff"), EnemyHighlight = new("e7a185");
    public TabContainer ResultsTabs { get; private set; } = null!;
    private int _resultsTab;
    /// <summary>Mechs whose weapon table is open, kept while the results are rebuilt.</summary>
    public HashSet<string> ExpandedResults { get; } = [];
    private readonly Dictionary<string, (Control Row, Control Weapons)> _resultRows = [];
    /// <summary>Each mech's row in the Mechs tab and the weapon table beneath it, by unit id.</summary>
    public IReadOnlyDictionary<string, (Control Row, Control Weapons)> ResultRows => _resultRows;
    public VBoxContainer HighlightList { get; private set; } = null!;

    public static Color ConditionColor(MechCondition condition) => condition switch
    {
        MechCondition.Undamaged => new Color("4fbf5a"), MechCondition.Scratched => new Color("b8e6c0"),
        MechCondition.Damaged => new Color("f1d34c"), MechCondition.Crippled => new Color("f0973c"), _ => new Color("ec5a4a")
    };

    private string LanceTitle(int lance) => Coop && PlayerNames?.ElementAtOrDefault(lance) is { } player
        ? $"{ForceRoster.LanceName(lance)} · {player}" : ForceRoster.LanceName(lance);

    /// <summary>"Injured (14 days)": a pilot's condition with the recovery time from this battle's injuries.</summary>
    private string PilotText(CombatUnit unit)
    {
        var condition = BattleSummary.Pilot(unit);
        var days = Simulation!.InjuryDays(unit);
        return BattleSummary.Text(condition) + (days > 0 && condition is PilotCondition.Injured or PilotCondition.Ejected ? $" ({days} days)" : "");
    }

    /// <summary>One line per company mech under its lance, coloured by the mech's condition.</summary>
    private void ShowCompanyStatus(VBoxContainer body)
    {
        RosterUi.Text(body, Coop ? "THE COMPANY" : "YOUR FORCE", 16, Ui.Gold);
        foreach (var lance in Simulation!.Units.Where(unit => unit.Side == CombatSide.Friendly).GroupBy(unit => unit.Lance ?? 0).OrderBy(group => group.Key))
        {
            RosterUi.Text(body, LanceTitle(lance.Key).ToUpperInvariant(), 13, Ui.Muted);
            foreach (var unit in lance)
            {
                var condition = BattleSummary.Condition(unit);
                RosterUi.Text(body, $"{unit.Spec.DisplayName} · “{unit.Warrior.Callsign}” {unit.Warrior.Name}: {condition} · {PilotText(unit)}",
                    14, ConditionColor(condition));
            }
        }
    }

    private static Label Cell(Node parent, string text, int width, Color? color = null, bool number = true, int size = 13)
    {
        var label = Ui.Label(parent, text, size, color);
        label.CustomMinimumSize = new Vector2(width, 0); label.ClipText = true;
        label.HorizontalAlignment = number ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        label.MouseFilter = MouseFilterEnum.Ignore;
        return label;
    }

    private static HBoxContainer Line(Node parent)
    {
        var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; line.AddThemeConstantOverride("separation", 6);
        parent.AddChild(line); return line;
    }

    /// <summary>The Mechs tab: a table per lance and per enemy group. Clicking a mech opens or closes its weapon table.</summary>
    private void ShowResultTables()
    {
        _resultRows.Clear();
        var scroll = new ScrollContainer { Name = "Mechs", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ResultsTabs.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 4); scroll.AddChild(body);
        var units = Simulation!.Units;
        foreach (var lance in units.Where(unit => unit.Side == CombatSide.Friendly).GroupBy(unit => unit.Lance ?? 0).OrderBy(group => group.Key))
            ResultTable(body, LanceTitle(lance.Key), lance);
        var enemies = units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        if (enemies.All(unit => unit.Group is null)) ResultTable(body, "Opposition", enemies);
        else foreach (var group in enemies.GroupBy(unit => unit.Group).OrderBy(group => group.Key ?? int.MaxValue))
            ResultTable(body, group.Key is { } number ? $"Group {number + 1}" : "Opposition", group);
    }

    private void ResultTable(VBoxContainer body, string title, IEnumerable<CombatUnit> units)
    {
        var heading = RosterUi.Text(body, title.ToUpperInvariant(), 16, Ui.Gold);
        heading.CustomMinimumSize = new Vector2(0, 34); heading.VerticalAlignment = VerticalAlignment.Bottom;
        var spans = Line(body);
        Cell(spans, "", MechColumn + PilotColumn + 2 * StatusColumn + 18, number: false);
        foreach (var span in new[] { "DAMAGE TAKEN", "DAMAGE DEALT" })
        {
            var label = Cell(spans, span, 3 * NumberColumn + 12, Ui.Gold, size: 11); label.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var header = Line(body);
        Cell(header, "Mech", MechColumn, Ui.Muted, false, 12); Cell(header, "Pilot", PilotColumn, Ui.Muted, false, 12);
        Cell(header, "Pilot Status", StatusColumn, Ui.Muted, false, 12); Cell(header, "Mech Status", StatusColumn, Ui.Muted, false, 12);
        foreach (var name in new[] { "Total", "Armour", "Internal", "Total", "Armour", "Internal", "Systems", "Comps", "Kills" })
            Cell(header, name, NumberColumn, Ui.Muted, size: 12);
        header.GetChild<Label>(header.GetChildCount() - 3).TooltipText = "Systems damaged by critical hits";
        header.GetChild<Label>(header.GetChildCount() - 2).TooltipText = "Components (locations) destroyed";
        var kills = header.GetChild<Label>(header.GetChildCount() - 1); kills.TooltipText = "Mechs destroyed or disabled";
        foreach (var label in header.GetChildren().OfType<Label>().Where(item => item.TooltipText.Length > 0)) label.MouseFilter = MouseFilterEnum.Pass;
        foreach (var unit in units) ResultRow(body, unit);
    }

    private void ResultRow(VBoxContainer body, CombatUnit unit)
    {
        var row = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = "Click to show or hide its weapons" };
        row.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line)); body.AddChild(row);
        var line = Line(row);
        var condition = BattleSummary.Condition(unit); var total = BattleSummary.Total(unit);
        Cell(line, unit.Spec.DisplayName, MechColumn, number: false);
        var pilot = Cell(line, unit.Warrior.Callsign, PilotColumn, number: false);
        pilot.MouseFilter = MouseFilterEnum.Pass;
        pilot.TooltipText = $"{unit.Warrior.DisplayName}\n" + (unit.Side == CombatSide.Friendly ? $"{unit.Warrior.Experience}\n" : "")
            + RosterUi.SkillLines(unit.Warrior);
        var pilotStatus = Cell(line, BattleSummary.Text(BattleSummary.Pilot(unit)), StatusColumn, number: false);
        if (Simulation!.InjuryDays(unit) is var days and > 0) { pilotStatus.MouseFilter = MouseFilterEnum.Pass; pilotStatus.TooltipText = $"Recovers in {days} days"; }
        Cell(line, condition.ToString(), StatusColumn, ConditionColor(condition), false);
        foreach (var value in new[] { unit.ArmorLost + unit.StructureLost, unit.ArmorLost, unit.StructureLost, total.DamageDealt, total.ArmorDealt,
            total.InternalDealt, total.SystemsDamaged, total.ComponentsDestroyed, total.Kills })
            Cell(line, value.ToString(), NumberColumn);
        var weapons = WeaponTable(body, unit); weapons.Visible = ExpandedResults.Contains(unit.Id);
        _resultRows[unit.Id] = (row, weapons);
        row.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            ToggleResult(unit.Id); row.AcceptEvent();
        };
    }

    /// <summary>Opens or closes a mech's weapon table.</summary>
    public void ToggleResult(string unitId)
    {
        if (!_resultRows.TryGetValue(unitId, out var entry)) return;
        entry.Weapons.Visible = !entry.Weapons.Visible;
        if (entry.Weapons.Visible) ExpandedResults.Add(unitId); else ExpandedResults.Remove(unitId);
    }

    private static Control WeaponTable(VBoxContainer body, CombatUnit unit)
    {
        var margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 36); margin.AddThemeConstantOverride("margin_bottom", 6);
        body.AddChild(margin);
        var table = new VBoxContainer(); table.AddThemeConstantOverride("separation", 2); margin.AddChild(table);
        if (unit.Weapons.Count == 0 || unit.Weapons.All(weapon => weapon.Tally.ShotsFired == 0))
        { RosterUi.Text(table, "No shots fired", 13, Ui.Muted); return margin; }
        var header = Line(table);
        Cell(header, "Weapon", WeaponColumn, Ui.Muted, false, 12);
        foreach (var name in new[] { "Shots", "Hits", "Accuracy", "Total", "Armour", "Internal", "Systems", "Comps", "Kills" })
            Cell(header, name, NumberColumn, Ui.Muted, size: 12);
        foreach (var weapon in unit.Weapons)
        {
            var tally = weapon.Tally; var line = Line(table);
            var destroyed = !unit.Intact(weapon.Spec.Location) || unit.Equipment[weapon.Spec.Id] == ComponentCondition.Destroyed;
            Cell(line, BattleSummary.WeaponName(weapon) + (destroyed ? " (destroyed)" : ""), WeaponColumn, destroyed ? Ui.Muted : null, false);
            Cell(line, tally.ShotsFired.ToString(), NumberColumn); Cell(line, tally.Hits.ToString(), NumberColumn);
            Cell(line, tally.Accuracy is { } accuracy ? $"{accuracy * 100:0}%" : "—", NumberColumn);
            foreach (var value in new[] { tally.DamageDealt, tally.ArmorDealt, tally.InternalDealt, tally.SystemsDamaged, tally.ComponentsDestroyed, tally.Kills })
                Cell(line, value.ToString(), NumberColumn);
        }
        return margin;
    }

    /// <summary>The Highlights tab: the battle's significant moments in order, tinted by side.</summary>
    private void ShowHighlights()
    {
        var scroll = new ScrollContainer { Name = "Highlights", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        ResultsTabs.AddChild(scroll);
        HighlightList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; HighlightList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(HighlightList);
        if (Simulation!.Highlights.Count == 0) { RosterUi.Text(HighlightList, "Nothing of note happened.", 14, Ui.Muted); return; }
        foreach (var highlight in Simulation.Highlights)
            RosterUi.Text(HighlightList, highlight.Line, 14,
                Simulation.Unit(highlight.UnitId).Side == CombatSide.Friendly ? FriendlyHighlight : EnemyHighlight);
    }
}
