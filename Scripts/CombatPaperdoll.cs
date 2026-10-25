using Godot;

namespace Mech;

/// <summary>A shared front/rear schematic. The armor rim and internal core are independent.</summary>
public partial class CombatPaperdoll : Control
{
    public CombatUnit? Unit { get; private set; }
    public static readonly Color Full = new("b4b8b6"), LightDamage = new("f1d34c"),
        ModerateDamage = new("ef8a32"), CriticalDamage = new("e84c43");
    public static Color ConditionColor(int remaining, int maximum) => remaining >= maximum ? Full
        : remaining * 3 > maximum * 2 ? LightDamage : remaining * 3 > maximum ? ModerateDamage : CriticalDamage;
    private static readonly Dictionary<MechLocation, Vector2[]> Shapes = new()
    {
        [MechLocation.Head] = [new(76, 25), new(102, 25), new(106, 36), new(100, 49), new(78, 49), new(72, 36)],
        [MechLocation.CenterTorso] = [new(72, 56), new(106, 56), new(111, 88), new(101, 123), new(77, 123), new(67, 88)],
        [MechLocation.RightTorso] = [new(36, 55), new(65, 58), new(60, 92), new(67, 116), new(43, 112), new(33, 87)],
        [MechLocation.LeftTorso] = [new(113, 58), new(142, 55), new(145, 87), new(135, 112), new(111, 116), new(118, 92)],
        [MechLocation.RightArm] = [new(7, 54), new(29, 54), new(28, 92), new(33, 129), new(25, 144), new(7, 140)],
        [MechLocation.LeftArm] = [new(149, 54), new(171, 54), new(171, 140), new(153, 144), new(145, 129), new(150, 92)],
        [MechLocation.RightLeg] = [new(48, 123), new(83, 130), new(79, 173), new(78, 208), new(38, 208), new(42, 173)],
        [MechLocation.LeftLeg] = [new(95, 130), new(130, 123), new(136, 173), new(140, 208), new(100, 208), new(99, 173)]
    };
    public override void _Ready()
    {
        CustomMinimumSize = new(0, 220); MouseFilter = MouseFilterEnum.Ignore;
        Resized += () => { CustomMinimumSize = new(0, Math.Min(244, Size.X * 244 / 300)); QueueRedraw(); };
    }
    public void Display(CombatUnit? unit) { Unit = unit; Visible = unit is not null; QueueRedraw(); }
    public static string Readout(CombatUnit unit) => "ARMOR / STOCK · INTERNAL / STOCK\n"
        + string.Join("\n", unit.Spec.Locations.Select(spec =>
        {
            var state = unit.Locations[spec.Location];
            return $"{DraftCatalog.Abbreviation(spec.Location)}  A {state.Armor}/{spec.Armor} · I {state.Structure}/{spec.Structure}"
                + (state.Structure == 0 ? " · LOST" : "")
                + (spec.RearArmor > 0 ? $"\n       Rear {state.RearArmor}/{spec.RearArmor}" : "");
        }));
    public override void _Draw()
    {
        if (Unit is null) return;
        var scale = Math.Min(Size.X / 300, Size.Y / 244);
        var origin = new Vector2((Size.X - 300 * scale) / 2, 0);
        Vector2 At(Vector2 point) => origin + point * scale;
        void Text(Vector2 at, string text, Color color, int size = 11) =>
            DrawString(ThemeDB.FallbackFont, At(at), text, HorizontalAlignment.Left, -1, Math.Max(9, (int)(size * scale)), color);
        void Region(MechLocation location, Vector2[] shape, bool rear)
        {
            var spec = Unit.Spec.Locations.Single(item => item.Location == location);
            var state = Unit.Locations[location]; var center = shape.Aggregate(Vector2.Zero, (sum, p) => sum + p) / shape.Length;
            var outer = shape.Select(At).ToArray();
            DrawColoredPolygon(outer, new Color("26312f"));
            if (state.Structure > 0)
            {
                var armor = rear ? state.RearArmor : state.Armor; var maximum = rear ? spec.RearArmor : spec.Armor;
                if (armor > 0) DrawColoredPolygon(outer, ConditionColor(armor, maximum));
                DrawColoredPolygon(shape.Select(p => At(center + (p - center) * .78f)).ToArray(), new Color("17221f"));
                DrawColoredPolygon(shape.Select(p => At(center + (p - center) * .65f)).ToArray(), ConditionColor(state.Structure, spec.Structure));
            }
            DrawPolyline(outer.Append(outer[0]).ToArray(), new Color("62706b"), 1, true);
            var abbreviation = DraftCatalog.Abbreviation(location);
            var fontSize = Math.Max(9, (int)(10 * scale));
            var textWidth = ThemeDB.FallbackFont.GetStringSize(abbreviation, fontSize: fontSize).X;
            DrawString(ThemeDB.FallbackFont, At(center) + new Vector2(-textWidth / 2, fontSize * .35f), abbreviation,
                HorizontalAlignment.Left, -1, fontSize, state.Structure > 0 ? new Color("101916") : Ui.Muted);
        }
        Text(new(62, 14), "FRONT", Ui.Muted);
        Text(new(204, 38), "REAR TORSO", Ui.Muted, 10);
        foreach (var (location, shape) in Shapes) Region(location, shape, false);
        // Rear view: anatomical left is on the viewer's left.
        Region(MechLocation.LeftTorso, [new(202, 55), new(225, 58), new(225, 112), new(205, 105)], true);
        Region(MechLocation.CenterTorso, [new(231, 57), new(259, 57), new(261, 113), new(229, 113)], true);
        Region(MechLocation.RightTorso, [new(265, 58), new(288, 55), new(285, 105), new(265, 112)], true);
        Text(new(199, 139), "RIM: ARMOR", Ui.Muted, 10);
        Text(new(199, 155), "CORE: INTERNAL", Ui.Muted, 10);
        var colors = new[] { Full, LightDamage, ModerateDamage, CriticalDamage };
        var labels = new[] { "Full", "Light", "Moderate", "Critical" };
        for (var i = 0; i < colors.Length; i++)
        {
            DrawRect(new Rect2(At(new(i * 76 + 3, 228)), new Vector2(8, 8) * scale), colors[i]);
            Text(new(i * 76 + 15, 236), labels[i], Ui.Muted, 10);
        }
    }
}
