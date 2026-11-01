using System.Globalization;
using Godot;

namespace Mech;

/// <summary>One storage line in the hangar: a spare's name and count over its mass, slots and heat. Lines with spares left
/// can be dragged onto a location panel to install one.</summary>
public partial class HangarStorageLine : VBoxContainer
{
    public StorageItem Item { get; }
    public int Count { get; }

    public HangarStorageLine(StorageItem item, int count)
    {
        Item = item; Count = count;
        MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = count > 0 ? CursorShape.Drag : CursorShape.Arrow;
        AddThemeConstantOverride("separation", 0);
        RosterUi.Text(this, $"{item.Name} ×{count}", 13, count > 0 ? Ui.Text : Ui.Muted);
        var heat = item.Heat > 0 ? $" · {item.Heat} heat" : "";
        RosterUi.Text(this, $"{item.Tons.ToString("0.#", CultureInfo.InvariantCulture)} t · {item.Slots} {(item.Slots == 1 ? "slot" : "slots")}{heat}", 11, Ui.Muted);
        TooltipText = count > 0 ? $"{item.Name}\nDrag onto a location to install" : $"{item.Name}\nNone left in storage";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Count <= 0) return default;
        var preview = new PanelContainer();
        preview.AddThemeStyleboxOverride("panel", Ui.Box(new Color(.045f, .07f, .065f, .95f), Ui.Gold));
        Ui.Label(preview, Item.Name, 13);
        SetDragPreview(preview);
        return Item.Id;
    }
}
