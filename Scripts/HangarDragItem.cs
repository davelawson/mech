using Godot;

namespace Mech;

/// <summary>What a hangar drag carries: the storage item and whether it is bought from the market rather than taken from storage.</summary>
public partial class HangarDragItem : RefCounted
{
    public string Id { get; }
    public bool Purchase { get; }
    public HangarDragItem() : this("", false) { }
    public HangarDragItem(string id, bool purchase) { Id = id; Purchase = purchase; }
}
