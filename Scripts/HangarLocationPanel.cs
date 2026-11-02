using System.Collections.Immutable;
using Godot;

namespace Mech;

/// <summary>One body part in the hangar blueprint: an armor bar against the tonnage limit (front and rear bars on the torsos), each
/// with −/+ buttons, and a structure bar beneath, each with its values written over it; then its critical slots.</summary>
public partial class HangarLocationPanel : Control
{
    public const float Padding = 5, BarHeight = 12;
    private const float BarGap = 2, FirstBar = 19;
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
    public int FrontCurrent { get; private set; }
    public int FrontInstalled { get; private set; }
    public int RearCurrent { get; private set; }
    public int RearInstalled { get; private set; }
    public int StructureCurrent { get; private set; }
    public int StructureMax { get; private set; }
    public float RowHeight { get; set; } = 15;
    private OwnedMech? _mech;
    private static readonly Color Fill = new(.035f, .07f, .085f, .84f), Edge = new("3f7f92");
    public static readonly Color Damaged = new("e8a33c"), Destroyed = new("d04a3c"), Warning = new("f2d02c");
    /// <summary>Weapons with no compatible ammunition aboard and bins no weapon fires, marked ⚠.</summary>
    public ImmutableHashSet<string> Suspect { get; private set; } = [];
    /// <summary>Installed items pending purchase from the market.</summary>
    public ImmutableHashSet<string> Purchased { get; private set; } = [];
    /// <summary>Free weapon mounts, labelled on the first empty slots, and the mounts other items cover. None while destroyed.</summary>
    public LocationMounts Mounts { get; private set; } = LocationMounts.None;
    /// <summary>A storage item dropped here, to be installed, whether it is bought from the market, and the weapon mount it was
    /// dropped on, if any.</summary>
    public event Action<string, bool, HardpointClass?>? Dropped;
    /// <summary>An armor button was clicked: whether it was the rear bar's, and the stock points to add (negative to remove).</summary>
    public event Action<bool, int>? ArmorChanged;
    public const int ShiftArmorStep = 10;
    /// <summary>The spare being dragged, if any, whether it comes from the market, and why it cannot go here (null when it can).</summary>
    public string? DragItem { get; private set; }
    public bool DragPurchase { get; private set; }
    public string? DropProblem { get; private set; }
    private static readonly Color Valid = new("6fcf6a");

    public HangarLocationPanel(MechLocation location) { Location = location; MouseFilter = MouseFilterEnum.Pass; }
    /// <summary>The name row and the bars above the slots: torsos have a rear armor bar as well.</summary>
    public static float HeaderFor(MechLocation location) => FirstBar + (EquipmentRules.HasRearArmor(location) ? 3 : 2) * (BarHeight + BarGap) + 3;
    public float Header => HeaderFor(Location);
    public static float HeightFor(MechLocation location, float row) => HeaderFor(location) + EquipmentRules.Capacity(location) * row + Padding;
    public bool HasRear => EquipmentRules.HasRearArmor(Location);
    private Rect2 Bar(int index) => new(Padding, FirstBar + index * (BarHeight + BarGap), Size.X - Padding * 2, BarHeight);
    /// <summary>The front (or rear) armor bar, less its buttons.</summary>
    public Rect2 ArmorBar(bool rear) => Bar(rear ? 1 : 0).GrowSide(Side.Right, -(BarHeight + 1) * 2);
    public Rect2 StructureBar => Bar(HasRear ? 2 : 1);
    /// <summary>The − or + button at the right end of the front or rear armor bar.</summary>
    public Rect2 ArmorButton(bool rear, bool plus)
    {
        var bar = Bar(rear ? 1 : 0);
        return new(bar.End.X - (BarHeight + 1) * (plus ? 1 : 2) + 1, bar.Position.Y, BarHeight, BarHeight);
    }
    private bool ArmorEditable => _mech is not null && _mech.LocationIntact(Location);
    private (bool Rear, bool Plus)? ButtonAt(Vector2 at)
    {
        if (!ArmorEditable) return null;
        foreach (var rear in HasRear ? new[] { false, true } : [false])
            foreach (var plus in new[] { false, true })
                if (ArmorButton(rear, plus).HasPoint(at)) return (rear, plus);
        return null;
    }
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click || ButtonAt(click.Position) is not { } button) return;
        ArmorChanged?.Invoke(button.Rear, (button.Plus ? 1 : -1) * (click.ShiftPressed ? ShiftArmorStep : 1));
        AcceptEvent();
    }

    public void Display(OwnedMech mech, ImmutableArray<CriticalBlock> blocks, LocationMounts mounts, ImmutableHashSet<string>? purchased = null)
    {
        _mech = mech; Blocks = blocks; Suspect = Loadout.Suspect(mech.Spec); Purchased = purchased ?? [];
        Mounts = mech.LocationIntact(Location) ? mounts : LocationMounts.None;
        var spec = mech.Spec.Locations.Single(item => item.Location == Location);
        var state = mech.Locations.Single(item => item.Location == Location);
        ArmorText = $"Armor {state.Armor}/{spec.Armor}";
        RearText = HasRear ? $"Rear {state.RearArmor}/{spec.RearArmor}" : "";
        StructureText = $"Structure {state.Structure}/{spec.Structure}";
        ArmorCurrent = state.Armor + state.RearArmor; ArmorInstalled = spec.Armor + spec.RearArmor;
        ArmorLimit = EquipmentRules.ArmorLimit(spec);
        FrontCurrent = state.Armor; FrontInstalled = spec.Armor; RearCurrent = state.RearArmor; RearInstalled = spec.RearArmor;
        StructureCurrent = state.Structure; StructureMax = spec.Structure;
        TooltipText = DraftCatalog.LocationName(Location);
        QueueRedraw();
    }

    public void ShowDrop(string? item, string? problem, bool purchase = false) { DragItem = item; DragPurchase = purchase; DropProblem = problem; QueueRedraw(); }
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        DragItem is not null && DropProblem is null && data.Obj is HangarDragItem drag && drag.Id == DragItem && drag.Purchase == DragPurchase;
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var drag = (HangarDragItem)data.Obj!;
        Dropped?.Invoke(drag.Id, drag.Purchase, BlockAt(atPosition) is { } block ? MountAt(block) : null);
    }

    /// <summary>The free weapon mount an empty slot stands for, or null for a regular or occupied slot.</summary>
    public HardpointClass? MountAt(CriticalBlock block)
    {
        if (block.Kind != SlotKind.Empty) return null;
        var index = Blocks.Where(item => item.Kind == SlotKind.Empty).ToList().IndexOf(block);
        return index >= 0 && index < Mounts.Free.Length ? Mounts.Free[index] : null;
    }
    public static string MountName(HardpointClass type) => type.ToString();
    public static Color MountColor(HardpointClass type) => type switch
    {
        HardpointClass.Ballistic => new("e6dc82"), HardpointClass.Energy => new("5fd3b0"), _ => new("e07a5a")
    };
    private static string MountCount(HardpointClass type, int count) =>
        count == 1 ? $"{(type == HardpointClass.Energy ? "an" : "a")} {MountName(type)} mount" : $"{count} {MountName(type)} mounts";

    public ComponentCondition Condition(CriticalBlock block) => block.ItemId is null || _mech is null
        ? ComponentCondition.Operational : _mech.Condition(block.ItemId);

    public Rect2 BlockRect(CriticalBlock block) =>
        new(Padding, Header + block.Start * RowHeight, Size.X - Padding * 2, block.Length * RowHeight - 1);

    public CriticalBlock? BlockAt(Vector2 at) => Blocks.FirstOrDefault(block => BlockRect(block).HasPoint(at));

    public override string _GetTooltip(Vector2 atPosition)
    {
        if (ButtonAt(atPosition) is { } button)
            return $"{(button.Plus ? "Add" : "Remove")} 1 {(button.Rear ? "rear " : "")}armor point (Shift: {ShiftArmorStep})";
        if (BlockAt(atPosition) is { Kind: SlotKind.Empty } empty && Mounts.Free.Length > 0)
        {
            var counts = string.Join(", ", Mounts.Free.GroupBy(type => type).Select(group => $"{group.Count()} {MountName(group.Key).ToLowerInvariant()}"));
            return $"{(MountAt(empty) is { } mount ? $"{MountName(mount)} mount" : "Empty slot")}\nFree mounts here: {counts}\nDrag a weapon from storage to install";
        }
        if (BlockAt(atPosition) is not { ItemId: { } id } block || _mech is null) return DraftCatalog.LocationName(Location);
        var split = block.Length < block.ItemSlots ? $" ({block.Length} here)" : "";
        var mass = Loadout.Tons(_mech.Spec, id) is { } tons ? $"{tons:0.#} t · " : "";
        var condition = Condition(block);
        var warning = !Suspect.Contains(id) ? "" : block.Kind == SlotKind.Ammo ? "\n⚠ No weapon uses this ammunition" : "\n⚠ No compatible ammunition installed";
        var purchase = Purchased.Contains(id) ? $"\nPending purchase · {HangarScreen.Money(EquipmentMarket.Price(_mech.Installed.First(item => item.Id == id).StorageId))}" : "";
        var remove = !Loadout.Removable(block.Kind) ? "" : condition == ComponentCondition.Destroyed ? "\nRight-click to scrap" : "\nRight-click to remove";
        var covering = !Mounts.Covered.TryGetValue(id, out var covers) ? ""
            : string.Concat(covers.GroupBy(type => type).Select(group => $"\nCovering {MountCount(group.Key, group.Count())}"));
        return $"{block.Name}\n{mass}{block.ItemSlots} {(block.ItemSlots == 1 ? "slot" : "slots")}{split}\n{condition}{purchase}{covering}{warning}{remove}";
    }

    /// <summary>A weapon's slots take its hardpoint class colour; every other item its kind's colour.</summary>
    public Color BlockColor(CriticalBlock block) =>
        block.Kind == SlotKind.Weapon && _mech?.Spec.Weapons.FirstOrDefault(item => item.Id == block.ItemId) is { } weapon
            ? MountColor(WeaponMounts.ClassOf(weapon.Kind)) : KindColor(block.Kind);
    /// <summary>A storage line's colour, as its item would show in a slot.</summary>
    public static Color StorageColor(StorageItem item) => item.Group switch
    {
        StorageGroup.Energy => MountColor(HardpointClass.Energy), StorageGroup.Ballistic => MountColor(HardpointClass.Ballistic),
        StorageGroup.Missile => MountColor(HardpointClass.Missile), StorageGroup.Ammo => KindColor(SlotKind.Ammo),
        _ => KindColor(item.Id.StartsWith("jump-jet:", StringComparison.Ordinal) ? SlotKind.JumpJet : SlotKind.HeatSink)
    };
    public static Color KindColor(SlotKind kind) => kind switch
    {
        SlotKind.Weapon => new("e0b45a"), SlotKind.Ammo => new("c9774a"), SlotKind.HeatSink => new("5aa9d6"),
        SlotKind.JumpJet => new("9a86d8"), SlotKind.Engine => new("d86a6a"), SlotKind.Gyro => new("c58fd0"),
        SlotKind.Actuator => new("7fae84"), SlotKind.Cockpit => new("8fc3c0"), _ => new("4a5552")
    };

    /// <summary>A yellow warning triangle with an exclamation mark, its top-left corner at the given point, the mark in the background colour.</summary>
    public static void DrawWarning(CanvasItem canvas, Vector2 at, float size, Color background)
    {
        canvas.DrawColoredPolygon([at + new Vector2(size / 2, 0), at + new Vector2(size, size), at + new Vector2(0, size)], Warning);
        canvas.DrawLine(at + new Vector2(size / 2, size * .3f), at + new Vector2(size / 2, size * .68f), background, 1.5f);
        canvas.DrawRect(new Rect2(at + new Vector2(size / 2 - .75f, size * .76f), new Vector2(1.5f, 1.5f)), background);
    }
    /// <summary>Outlined text over a bar, vertically centred: one string on the left and, optionally, one on the right.</summary>
    public static void DrawOverlay(CanvasItem canvas, Rect2 area, string left, string right, int size)
    {
        var font = ThemeDB.FallbackFont; var outline = new Color(0, 0, 0, .85f);
        var baseline = new Vector2(area.Position.X, area.Position.Y + area.Size.Y / 2 + size * .36f);
        foreach (var (text, align) in new[] { (left, HorizontalAlignment.Left), (right, HorizontalAlignment.Right) })
        {
            if (text.Length == 0) continue;
            canvas.DrawStringOutline(font, baseline, text, align, area.Size.X, size, 3, outline);
            canvas.DrawString(font, baseline, text, align, area.Size.X, size, Ui.Text);
        }
    }

    public override void _Draw()
    {
        if (_mech is null) return;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(Vector2.Zero, Size), Fill); DrawRect(new Rect2(Vector2.Zero, Size), Edge, false, 1);
        var width = Size.X - Padding * 2;
        DrawString(font, new(Padding, 14), DraftCatalog.LocationName(Location).ToUpperInvariant(), HorizontalAlignment.Center, width, 11, Ui.Gold);
        // Narrow panels fall back to the record sheet's AR / RR / IS abbreviations.
        var (armor, rear, structure) = font.GetStringSize(ArmorText, fontSize: 10).X <= ArmorBar(false).Size.X - 6 ? (ArmorText, RearText, StructureText)
            : (ArmorText.Replace("Armor", "AR"), RearText.Replace("Rear", "RR"), StructureText.Replace("Structure", "IS"));
        // Installed front (and rear) armor against the tonnage limit, with lost armor shaded inside the installed section; structure
        // beneath. Bar colours are dimmed so the values written over them stay legible.
        var limit = Math.Max(1, ArmorLimit);
        void Armor(bool back, int current, int installed, string text)
        {
            var bar = ArmorBar(back);
            DrawRect(bar, new Color(.2f, .25f, .27f));
            DrawRect(bar with { Size = new(bar.Size.X * installed / limit, bar.Size.Y) }, new Color(Destroyed, .7f).Darkened(.45f));
            DrawRect(bar with { Size = new(bar.Size.X * current / limit, bar.Size.Y) }, new Color("6fd0ea").Darkened(.5f));
            DrawOverlay(this, bar.GrowIndividual(-3, 0, -3, 0), text, "", 10);
            if (!ArmorEditable) return;
            foreach (var plus in new[] { false, true })
            {
                var button = ArmorButton(back, plus);
                DrawRect(button, new Color(.12f, .2f, .23f)); DrawRect(button, Edge, false, 1);
                var mid = button.GetCenter();
                DrawLine(mid - new Vector2(3, 0), mid + new Vector2(3, 0), Ui.Text, 1.5f);
                if (plus) DrawLine(mid - new Vector2(0, 3), mid + new Vector2(0, 3), Ui.Text, 1.5f);
            }
        }
        Armor(false, FrontCurrent, FrontInstalled, armor);
        if (HasRear) Armor(true, RearCurrent, RearInstalled, rear);
        var structureBar = StructureBar;
        DrawRect(structureBar, new Color(.13f, .14f, .14f));
        DrawRect(structureBar with { Size = new(structureBar.Size.X * StructureCurrent / Math.Max(1, StructureMax), structureBar.Size.Y) }, new Color(.36f, .38f, .38f));
        DrawOverlay(this, structureBar.GrowIndividual(-3, 0, -3, 0), structure, "", 10);
        foreach (var block in Blocks)
        {
            var rect = BlockRect(block); var condition = Condition(block); var mount = MountAt(block);
            var color = condition switch
            {
                ComponentCondition.Destroyed => Destroyed, ComponentCondition.Damaged => Damaged,
                _ => mount is { } type ? MountColor(type) : BlockColor(block)
            };
            DrawRect(rect, new Color(color, block.Kind == SlotKind.Empty ? .12f : .26f));
            DrawRect(rect, new Color(color, .7f), false, 1);
            // A bar down the left edge for each weapon mount the item covers, one row tall.
            var inset = 0f;
            if (block.ItemId is { } covering && Mounts.Covered.TryGetValue(covering, out var covers))
            {
                for (var row = 0; row < Math.Min(covers.Length, block.Length); row++)
                    DrawRect(new Rect2(rect.Position.X, rect.Position.Y + row * RowHeight, 3, RowHeight - 1), MountColor(covers[row]));
                inset = 3;
            }
            var size = RowHeight >= 14 ? 10 : 9;
            var text = mount is { } free ? MountName(free) : block.Length < block.ItemSlots ? $"{block.Name} ({block.Length}/{block.ItemSlots})" : block.Name;
            var baseline = rect.Position.Y + Math.Min(rect.Size.Y, RowHeight) / 2 + size * .38f;
            var suspect = block.ItemId is { } id && Suspect.Contains(id);
            var marker = suspect ? size + 4 : 0;
            DrawString(font, new(rect.Position.X + 3 + inset, baseline), text, HorizontalAlignment.Left, rect.Size.X - 6 - inset - marker, size,
                mount is not null ? color : block.Kind == SlotKind.Empty ? Ui.Muted : condition == ComponentCondition.Operational ? Ui.Text : color);
            if (suspect)
            {
                var x = rect.Position.X + 3 + inset + Math.Min(font.GetStringSize(text, fontSize: size).X, rect.Size.X - 6 - inset - marker) + 3;
                DrawWarning(this, new(x, baseline - size * .85f), size, Fill);
            }
            if (condition == ComponentCondition.Destroyed)
                DrawLine(new(rect.Position.X + 2, baseline - size * .35f), new(rect.End.X - 2, baseline - size * .35f), Destroyed, 1.5f);
        }
        if (DragItem is null) return;
        // While a spare is dragged, empty slots show green where it can go and red where it cannot, with the reason.
        var tint = DropProblem is null ? Valid : Destroyed;
        foreach (var block in Blocks.Where(block => block.Kind == SlotKind.Empty)) DrawRect(BlockRect(block), new Color(tint, .3f));
        if (DropProblem is null) return;
        var empty = Blocks.Where(block => block.Kind == SlotKind.Empty).ToArray();
        var area = empty.Length > 0 ? BlockRect(empty[0]).Merge(BlockRect(empty[^1]))
            : new Rect2(Padding, Size.Y - Padding - RowHeight, Size.X - Padding * 2, RowHeight - 1);
        if (empty.Length == 0) DrawRect(area, new Color(Destroyed, .3f));
        var fontSize = RowHeight >= 14 ? 10 : 9;
        DrawString(font, new(area.Position.X, area.Position.Y + Math.Min(area.Size.Y, RowHeight) / 2 + fontSize * .38f), DropProblem,
            HorizontalAlignment.Center, area.Size.X, fontSize, Ui.Text);
    }
}
