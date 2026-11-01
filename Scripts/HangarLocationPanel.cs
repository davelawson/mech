using System.Collections.Immutable;
using Godot;

namespace Mech;

/// <summary>One body part in the hangar blueprint: armor and structure, an armor bar against the tonnage limit, and its critical slots.</summary>
public partial class HangarLocationPanel : Control
{
    public const float HeaderHeight = 50, Padding = 5;
    public MechLocation Location { get; }
    public ImmutableArray<CriticalBlock> Blocks { get; private set; } = [];
    public string ArmorText { get; private set; } = "";
    public string RearText { get; private set; } = "";
    public string StructureText { get; private set; } = "";
    /// <summary>Armor currently carried, front and rear together.</summary>
    public int ArmorCurrent { get; private set; }
    /// <summary>Armor the variant has installed, front and rear together.</summary>
    public int ArmorInstalled { get; private set; }
    /// <summary>The most armor this location could carry at the mech's tonnage.</summary>
    public int ArmorLimit { get; private set; }
    public float RowHeight { get; set; } = 15;
    private OwnedMech? _mech;
    private static readonly Color Fill = new(.035f, .07f, .085f, .84f), Edge = new("3f7f92");
    public static readonly Color Damaged = new("e8a33c"), Destroyed = new("d04a3c"), Warning = new("f2d02c");
    /// <summary>Weapons with no compatible ammunition aboard and bins no weapon fires, marked ⚠.</summary>
    public ImmutableHashSet<string> Suspect { get; private set; } = [];

    public HangarLocationPanel(MechLocation location) { Location = location; MouseFilter = MouseFilterEnum.Pass; }
    public static float HeightFor(MechLocation location, float row) => HeaderHeight + EquipmentRules.Capacity(location) * row + Padding;

    public void Display(OwnedMech mech, ImmutableArray<CriticalBlock> blocks)
    {
        _mech = mech; Blocks = blocks; Suspect = Loadout.Suspect(mech.Spec);
        var spec = mech.Spec.Locations.Single(item => item.Location == Location);
        var state = mech.Locations.Single(item => item.Location == Location);
        ArmorText = $"Armor {state.Armor}/{spec.Armor}";
        RearText = spec.RearArmor > 0 ? $"Rear {state.RearArmor}/{spec.RearArmor}" : "";
        StructureText = $"Structure {state.Structure}/{spec.Structure}";
        ArmorCurrent = state.Armor + state.RearArmor; ArmorInstalled = spec.Armor + spec.RearArmor;
        ArmorLimit = EquipmentRules.ArmorLimit(spec);
        TooltipText = DraftCatalog.LocationName(Location);
        QueueRedraw();
    }

    public ComponentCondition Condition(CriticalBlock block) => block.ItemId is null || _mech is null
        ? ComponentCondition.Operational : _mech.Condition(block.ItemId);

    public Rect2 BlockRect(CriticalBlock block) =>
        new(Padding, HeaderHeight + block.Start * RowHeight, Size.X - Padding * 2, block.Length * RowHeight - 1);

    public CriticalBlock? BlockAt(Vector2 at) => Blocks.FirstOrDefault(block => BlockRect(block).HasPoint(at));

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (BlockAt(atPosition) is not { ItemId: { } id } block || _mech is null) return DraftCatalog.LocationName(Location);
        var split = block.Length < block.ItemSlots ? $" ({block.Length} here)" : "";
        var mass = Loadout.Tons(_mech.Spec, id) is { } tons ? $"{tons:0.#} t · " : "";
        var condition = Condition(block);
        var warning = !Suspect.Contains(id) ? "" : block.Kind == SlotKind.Ammo ? "\n⚠ No weapon uses this ammunition" : "\n⚠ No compatible ammunition installed";
        var remove = !Loadout.Removable(block.Kind) ? "" : condition == ComponentCondition.Destroyed ? "\nRight-click to scrap" : "\nRight-click to remove";
        return $"{block.Name}\n{mass}{block.ItemSlots} {(block.ItemSlots == 1 ? "slot" : "slots")}{split}\n{condition}{warning}{remove}";
    }

    public static Color KindColor(SlotKind kind) => kind switch
    {
        SlotKind.Weapon => new("e0b45a"), SlotKind.Ammo => new("c9774a"), SlotKind.HeatSink => new("5aa9d6"),
        SlotKind.JumpJet => new("9a86d8"), SlotKind.Engine => new("d86a6a"), SlotKind.Gyro => new("c58fd0"),
        SlotKind.Actuator => new("7fae84"), SlotKind.Cockpit => new("8fc3c0"), _ => new("4a5552")
    };

    /// <summary>A yellow warning triangle with an exclamation mark, its top-left corner at the given point.</summary>
    private void DrawWarning(Vector2 at, float size)
    {
        DrawColoredPolygon([at + new Vector2(size / 2, 0), at + new Vector2(size, size), at + new Vector2(0, size)], Warning);
        DrawLine(at + new Vector2(size / 2, size * .3f), at + new Vector2(size / 2, size * .68f), Fill, 1.5f);
        DrawRect(new Rect2(at + new Vector2(size / 2 - .75f, size * .76f), new Vector2(1.5f, 1.5f)), Fill);
    }

    public override void _Draw()
    {
        if (_mech is null) return;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), Fill); DrawRect(new Rect2(Vector2.Zero, Size), Edge, false, 1);
        var width = Size.X - Padding * 2;
        DrawString(font, new(Padding, 14), DraftCatalog.LocationName(Location).ToUpperInvariant(), HorizontalAlignment.Left, width, 11, Ui.Gold);
        // Narrow panels fall back to the record sheet's AR / RR / IS abbreviations.
        var (armor, rear, structure) = font.GetStringSize(ArmorText + "  " + RearText, fontSize: 10).X <= width ? (ArmorText, RearText, StructureText)
            : (ArmorText.Replace("Armor", "AR"), RearText.Replace("Rear", "RR"), StructureText.Replace("Structure", "IS"));
        DrawString(font, new(Padding, 27), armor, HorizontalAlignment.Left, width, 10, Ui.Text);
        if (rear.Length > 0) DrawString(font, new(Padding, 27), rear, HorizontalAlignment.Right, width, 10, Ui.Text);
        DrawString(font, new(Padding, 39), structure, HorizontalAlignment.Left, width, 10, Ui.Muted);
        // Installed armor against the tonnage limit, with lost armor shaded inside the installed section.
        var bar = new Rect2(Padding, 43, width, 4);
        DrawRect(bar, new Color(.2f, .25f, .27f));
        var limit = Math.Max(1, ArmorLimit);
        DrawRect(bar with { Size = new(width * ArmorInstalled / limit, bar.Size.Y) }, new Color(Destroyed, .7f));
        DrawRect(bar with { Size = new(width * ArmorCurrent / limit, bar.Size.Y) }, new Color("6fd0ea"));
        foreach (var block in Blocks)
        {
            var rect = BlockRect(block); var condition = Condition(block);
            var color = condition switch { ComponentCondition.Destroyed => Destroyed, ComponentCondition.Damaged => Damaged, _ => KindColor(block.Kind) };
            DrawRect(rect, new Color(color, block.Kind == SlotKind.Empty ? .12f : .26f));
            DrawRect(rect, new Color(color, .7f), false, 1);
            var size = RowHeight >= 14 ? 10 : 9;
            var text = block.Length < block.ItemSlots ? $"{block.Name} ({block.Length}/{block.ItemSlots})" : block.Name;
            var baseline = rect.Position.Y + Math.Min(rect.Size.Y, RowHeight) / 2 + size * .38f;
            var suspect = block.ItemId is { } id && Suspect.Contains(id);
            var marker = suspect ? size + 4 : 0;
            DrawString(font, new(rect.Position.X + 3, baseline), text, HorizontalAlignment.Left, rect.Size.X - 6 - marker, size,
                block.Kind == SlotKind.Empty ? Ui.Muted : condition == ComponentCondition.Operational ? Ui.Text : color);
            if (suspect)
            {
                var x = rect.Position.X + 3 + Math.Min(font.GetStringSize(text, fontSize: size).X, rect.Size.X - 6 - marker) + 3;
                DrawWarning(new(x, baseline - size * .85f), size);
            }
            if (condition == ComponentCondition.Destroyed)
                DrawLine(new(rect.Position.X + 2, baseline - size * .35f), new(rect.End.X - 2, baseline - size * .35f), Destroyed, 1.5f);
        }
    }
}
