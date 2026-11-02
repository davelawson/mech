using Godot;

namespace Mech;

/// <summary>One storage or market line in the hangar: a spare's name and count, or an item for sale and its price, drawn as a slot in
/// the colour its item takes in a location panel. Lines with spares left, and every market line, can be dragged onto a location
/// panel to install one.</summary>
public partial class HangarStorageLine : PanelContainer
{
    public StorageItem Item { get; }
    public int Count { get; }
    /// <summary>The price of a market line; null for a storage line.</summary>
    public long? Price { get; }
    /// <summary>The slot colour: the weapon's hardpoint class, or the item kind's colour.</summary>
    public Color Color { get; }
    private bool Draggable => Price is not null || Count > 0;

    public HangarStorageLine(StorageItem item, int count) : this(item, count, null) { }
    public static HangarStorageLine ForSale(StorageItem item, long price) => new(item, 0, price);
    private HangarStorageLine(StorageItem item, int count, long? price)
    {
        Item = item; Count = count; Price = price; Color = HangarLocationPanel.StorageColor(item);
        MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = Draggable ? CursorShape.Drag : CursorShape.Arrow;
        AddThemeStyleboxOverride("panel", Slot(Draggable ? .26f : .12f));
        if (price is { } cost)
        {
            // The price sits in its own column on the right; long names are cut short rather than wrapped.
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 6); AddChild(row);
            var name = Ui.Label(row, item.Name, 13);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill; name.MouseFilter = MouseFilterEnum.Ignore;
            Ui.Label(row, HangarScreen.Money(cost), 13, Ui.Muted).MouseFilter = MouseFilterEnum.Ignore;
        }
        else RosterUi.Text(this, $"{item.Name} ×{count}", 13, Draggable ? Ui.Text : Ui.Muted).MouseFilter = MouseFilterEnum.Ignore;
        TooltipText = price is not null ? $"{item.Name}\nDrag onto a location to buy and install"
            : count > 0 ? $"{item.Name}\nDrag onto a location to install" : $"{item.Name}\nNone left in storage";
    }

    private StyleBoxFlat Slot(float alpha)
    {
        var box = Ui.Box(new Color(Color, alpha), new Color(Color, .7f));
        box.ContentMarginLeft = box.ContentMarginRight = 6; box.ContentMarginTop = box.ContentMarginBottom = 2;
        return box;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Draggable) return default;
        var preview = new PanelContainer();
        var box = Slot(.26f); box.BgColor = new Color(.045f, .07f, .065f, .95f).Blend(box.BgColor);
        preview.AddThemeStyleboxOverride("panel", box);
        Ui.Label(preview, Item.Name, 13);
        SetDragPreview(preview);
        return new HangarDragItem(Item.Id, Price is not null);
    }
}
