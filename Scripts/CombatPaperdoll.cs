using Godot;

namespace Mech;

/// <summary>One line of a location's systems tooltip: identical systems in the same condition. <see cref="Status"/> words a
/// critical system's damage ("Damaged 1/3", "Critically damaged") when the condition alone does not.</summary>
public sealed record SystemGroup(int Count, string Name, ComponentCondition Condition, string? Status = null)
{
    public string Text => Count > 1 ? $"{Count} x {Name}" : Name;
    public string StatusText => Status ?? Condition.ToString();
}

/// <summary>
/// A shared front/rear schematic. The armor rim and internal core are independent.
/// Compact dolls (unit cards) draw only the regions; the full doll lists each location's systems on hover.
/// Both carry a jump-fuel bar on the left and a heat bar on the right, without text. The full doll of a friendly mech
/// adds a draggable heat-limit handle beside its heat bar. The full doll also has a speed gauge and, beneath it, an ammo
/// economy gauge pinned along its foot, each with a draggable pointer for a friendly mech.
/// </summary>
public partial class CombatPaperdoll : Control
{
    public CombatUnit? Unit { get; private set; }
    public bool Compact { get; init; }
    /// <summary>Raised with the whole heat limit 0–30 while the player clicks or drags the heat bar.</summary>
    public event Action<int>? HeatLimitChanged;
    /// <summary>Only the sidebar doll of a friendly mech still in the battle offers the heat-limit handle.</summary>
    public bool EditsHeatLimit => !Compact && Unit is { Side: CombatSide.Friendly, Defeated: false };
    private int? _dragLimit;
    /// <summary>The limit the handle shows: the one being dragged, else the unit's own.</summary>
    public int? DisplayedHeatLimit => !EditsHeatLimit ? null : _dragLimit ?? Unit!.HeatLimit;
    /// <summary>Raised with the desired speed in metres per second while the player clicks or drags the speed gauge.</summary>
    public event Action<float>? DesiredSpeedChanged;
    public bool EditsDesiredSpeed => EditsHeatLimit;
    private float? _dragSpeed;
    /// <summary>The desired speed the pointer shows: the one being dragged, else the unit's own.</summary>
    public float? DisplayedDesiredSpeed => !EditsDesiredSpeed ? null : _dragSpeed ?? Unit!.DesiredSpeed;
    /// <summary>Only the full doll carries the speed gauge.</summary>
    public bool ShowsSpeed => !Compact && Unit is not null;
    /// <summary>Raised with the whole ammo economy 0–10 while the player clicks or drags the ammo gauge.</summary>
    public event Action<int>? AmmoEconomyChanged;
    /// <summary>The full doll shows the ammo gauge for a mech with ammo-fed weapons.</summary>
    public bool ShowsAmmo => !Compact && Unit is not null && Unit.Weapons.Any(weapon => weapon.Spec.AmmoKind is not null);
    public bool EditsAmmoEconomy => EditsHeatLimit && ShowsAmmo;
    private int? _dragEconomy;
    public int? DisplayedAmmoEconomy => !EditsAmmoEconomy ? null : _dragEconomy ?? Unit!.AmmoEconomy;
    public static readonly Color Full = new("b4b8b6"), LightDamage = new("f1d34c"),
        ModerateDamage = new("ef8a32"), CriticalDamage = new("e84c43"), Healthy = new("6fcf6a");
    public static Color SystemColor(ComponentCondition condition) => condition switch
    {
        ComponentCondition.Destroyed => CriticalDamage, ComponentCondition.Damaged => LightDamage, _ => Healthy
    };
    public static Color SystemColor(SystemGroup system) =>
        system.StatusText == "Critically damaged" ? ModerateDamage : SystemColor(system.Condition);
    public static Color ConditionColor(int remaining, int maximum) => remaining >= maximum ? Full
        : remaining * 3 > maximum * 2 ? LightDamage : remaining * 3 > maximum ? ModerateDamage : CriticalDamage;
    public static readonly Color FuelColor = new("a8d8f0"), BarTrack = new("121a18"), BarTick = new("c8cdcb");
    /// <summary>Heat colour by band: yellow below 10, orange below 20, red from 20.</summary>
    public static Color HeatColor(double heat) => heat < CombatRules.WarmHeat ? LightDamage : heat < CombatRules.HotHeat ? ModerateDamage : CriticalDamage;
    /// <summary>The heat bar's fill, full at the shutdown threshold.</summary>
    public static float HeatFraction(CombatUnit unit) => (float)Math.Clamp(unit.Heat / CombatRules.ShutdownHeat, 0, 1);
    /// <summary>Jump readiness as "fuel": empty from takeoff, refilling over the recharge after landing.</summary>
    public static float FuelFraction(CombatUnit unit) => unit.Airborne ? 0
        : (float)Math.Clamp(1 - unit.JumpRecharge / CombatRules.JumpRechargeSeconds, 0, 1);
    /// <summary>Only friendly mechs with jump jets show fuel; the slot stays reserved otherwise.</summary>
    public bool ShowsFuel => Unit is { Side: CombatSide.Friendly, Spec.Jump: > 0 };
    public static readonly Color ReverseColor = new("3a7a3c");
    /// <summary>The speed gauge's fill: green going forward, dark green in reverse.</summary>
    public static Color SpeedColor(CombatUnit unit) => unit.CurrentSpeed < 0 ? ReverseColor : Healthy;
    /// <summary>The gauge's fill fraction, reverse shown as positive, full at 150 km/h.</summary>
    public static float SpeedFraction(CombatUnit unit) => Math.Clamp(Math.Abs(unit.CurrentSpeed) / CombatRules.SpeedGaugeMaximum, 0, 1);
    public static int Kmh(float metresPerSecond) => Mathf.RoundToInt(metresPerSecond * CombatRules.KmhPerMetrePerSecond);
    /// <summary>"Running 62 km/h / 70 km/h": gait, signed actual speed (negative in reverse) and, when given, the desired speed.</summary>
    public static string SpeedTooltip(CombatUnit unit, float? desired)
    {
        var speed = Kmh(unit.CurrentSpeed);
        var gait = unit.Gait switch { Gait.Reverse => "Reverse", Gait.Running => "Running", Gait.Walking => "Walking", _ => "Stopped" };
        return $"{gait} {(unit.Gait == Gait.Reverse ? -Math.Abs(speed) : Math.Abs(speed))} km/h" + (desired is { } wanted ? $" / {Kmh(wanted)} km/h" : "");
    }
    public static Color AmmoColor => LightDamage;
    /// <summary>Shots left in intact bins over the stock load, so destroyed bins show as lost ammunition.</summary>
    public static float AmmoFraction(CombatUnit unit)
    {
        var (left, stock) = AmmoShots(unit);
        return stock == 0 ? 0 : Math.Clamp((float)left / stock, 0, 1);
    }
    public static (int Left, int Stock) AmmoShots(CombatUnit unit) =>
        (unit.Spec.Ammo.Where(bin => unit.Intact(bin.Location) && unit.Equipment[bin.Id] != ComponentCondition.Destroyed).Sum(bin => unit.Shots(bin.Id)),
            unit.Spec.Ammo.Sum(bin => bin.Shots));
    /// <summary>"Economy 4 · fires at +6 or better · 140/150 shots"; without an economy (an enemy) only the shots.</summary>
    public static string AmmoTooltip(CombatUnit unit, int? economy)
    {
        var (left, stock) = AmmoShots(unit);
        var shots = $"{left}/{stock} shots";
        return economy switch
        {
            null => shots,
            0 => $"No restriction · {shots}",
            CombatRules.MaxAmmoEconomy => $"Optimal shots only · {shots}",
            { } step => $"Economy {step} · fires at +{CombatRules.AmmoAllowance(step)} or better · {shots}"
        };
    }
    private static Texture2D? _fuelIcon, _heatIcon, _speedIcon, _ammoIcon;
    private static Texture2D SpeedIcon => _speedIcon ??= GD.Load<Texture2D>("res://Assets/Icons/speed.svg");
    private static Texture2D AmmoIcon => _ammoIcon ??= GD.Load<Texture2D>("res://Assets/Icons/ammo.svg");
    private static Texture2D FuelIcon => _fuelIcon ??= GD.Load<Texture2D>("res://Assets/Icons/jump-fuel.svg");
    private static Texture2D HeatIcon => _heatIcon ??= GD.Load<Texture2D>("res://Assets/Icons/heat.svg");
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
    // Rear view: anatomical left is on the viewer's left.
    private static readonly (MechLocation Location, Vector2[] Shape)[] RearShapes =
    [
        (MechLocation.LeftTorso, [new(202, 55), new(225, 58), new(225, 112), new(205, 105)]),
        (MechLocation.CenterTorso, [new(231, 57), new(259, 57), new(261, 113), new(229, 113)]),
        (MechLocation.RightTorso, [new(265, 58), new(288, 55), new(285, 105), new(265, 112)])
    ];
    // The compact doll tucks the rear strip beside the front view.
    private static readonly Vector2 CompactRearShift = new(-26, 0);
    private static readonly Rect2 CompactBounds = new(4, 22, 262, 188);
    private MechLocation? _tooltipLocation;
    public override void _Ready()
    {
        // Pass keeps wheel scrolling in the sidebar while the doll shows tooltips.
        CustomMinimumSize = Compact ? Vector2.Zero : new(0, FullMinimumHeight);
        MouseFilter = Compact ? MouseFilterEnum.Ignore : MouseFilterEnum.Pass;
        // A width-dependent minimum height can oscillate with an automatic scrollbar.
        // Keep layout stable and fit the drawing to the allocated size instead.
        Resized += QueueRedraw;
    }
    public void Display(CombatUnit? unit) { Unit = unit; Visible = unit is not null; QueueRedraw(); }
    /// <summary>Weapons, components and ammunition in one location, grouped by name and condition in mount order.</summary>
    public static IReadOnlyList<SystemGroup> LocationSystems(CombatUnit unit, MechLocation location)
    {
        var lost = !unit.Intact(location);
        return unit.Weapons.Select(weapon => weapon.Spec).Where(item => item.Location == location).Select(item => (item.Id, Name: item.Rear ? $"{item.Name} (rear)" : item.Name))
            .Concat(unit.Spec.Components.Where(item => item.Location == location).Select(item => (item.Id, item.Name)))
            .Concat(unit.Spec.Ammo.Where(item => item.Location == location).Select(item => (item.Id, item.Name)))
            .Select(item => (item.Name, Condition: lost ? ComponentCondition.Destroyed : unit.Equipment[item.Id],
                Status: lost ? null : unit.ConditionText(item.Id, levels: true)))
            .Select(item => item with { Status = item.Status == item.Condition.ToString() ? null : item.Status })
            .GroupBy(item => item).Select(group => new SystemGroup(group.Count(), group.Key.Name, group.Key.Condition, group.Key.Status)).ToArray();
    }
    // The full doll's front schematic spans these heights; the bars run alongside it, outside the arms and rear inset.
    private const float FullLeft = 7, FullRight = 288, FullTop = 25, FullBottom = 208;
    // The full doll is cropped to the schematic with a small margin. The height matches the width-bound drawing in the
    // usual sidebar, so the details below sit directly under the feet.
    private const float FullViewTop = 23, FullViewBottom = 210, FullMinimumHeight = 163 + GaugeRows * SpeedStripHeight;
    /// <summary>Each strip along the full doll's foot holds one gauge and its pointer: speed, then ammo economy.</summary>
    private const float SpeedStripHeight = 28, SpeedTrackHeight = 10, SpeedTrackTop = 4;
    private const int GaugeRows = 2;
    /// <summary>The height the schematic and its vertical bars are fitted into.</summary>
    private float SchematicHeight => Compact ? Size.Y : Math.Max(1, Size.Y - GaugeRows * SpeedStripHeight);
    /// <summary>Bar width in pixels: about 5–8 on a card, twice that in the sidebar.</summary>
    private float BarWidth => Compact ? Math.Clamp(Size.Y * .1f, 5, 8) : Math.Clamp(Size.Y * .05f, 10, 16);
    private float BarGap => BarWidth * .6f;
    /// <summary>The heat-limit pointer's width, reserved beyond the sidebar heat bar.</summary>
    private float PointerWidth => Compact ? 0 : BarWidth * .8f;
    private float DrawScale => Compact ? Math.Min(Math.Max(1, Size.X - 2 * (BarWidth + BarGap + 1)) / CompactBounds.Size.X, Size.Y / CompactBounds.Size.Y)
        : Math.Min(Math.Max(1, Size.X - 2 * (BarWidth + BarGap + 1) - PointerWidth) / (FullRight - FullLeft), SchematicHeight / (FullViewBottom - FullViewTop));
    private Vector2 Origin => Compact ? (Size - CompactBounds.Size * DrawScale) / 2 - CompactBounds.Position * DrawScale
        : new Vector2((Size.X - PointerWidth - (FullRight - FullLeft) * DrawScale) / 2 - FullLeft * DrawScale, -FullViewTop * DrawScale);
    /// <summary>The whole fuel bar slot, icon included, beside the doll's left edge.</summary>
    public Rect2 FuelBarRect => BarSlot(left: true);
    /// <summary>The whole heat bar slot, icon included, beside the doll's right edge.</summary>
    public Rect2 HeatBarRect => BarSlot(left: false);
    private Rect2 BarSlot(bool left)
    {
        var scale = DrawScale; var origin = Origin;
        var (minX, maxX, top, bottom) = Compact ? (CompactBounds.Position.X, CompactBounds.End.X, CompactBounds.Position.Y, CompactBounds.End.Y)
            : (FullLeft, FullRight, FullTop, FullBottom);
        var x = left ? origin.X + minX * scale - BarGap - BarWidth : origin.X + maxX * scale + BarGap;
        return new Rect2(x, origin.Y + top * scale, BarWidth, (bottom - top) * scale);
    }
    /// <summary>The bar's track above its icon, within a bar slot.</summary>
    private static Rect2 Track(Rect2 slot)
    {
        var iconSize = Math.Min(slot.Size.X * 2, slot.Size.Y * .4f);
        return new Rect2(slot.Position, new Vector2(slot.Size.X, Math.Max(1, slot.Size.Y - iconSize - 2)));
    }
    /// <summary>The heat bar's track, where 0 heat is the bottom and the shutdown threshold the top.</summary>
    public Rect2 HeatTrack => Track(HeatBarRect);
    /// <summary>Where the heat-limit handle can be grabbed: the track and its pointer, with a little slack.</summary>
    private Rect2 HeatGrabArea => HeatTrack.GrowIndividual(3, 6, PointerWidth + 3, 6);
    /// <summary>The whole-number limit for a height in this control, 0 at the track bottom and 30 at its top.</summary>
    public int HeatLimitAt(float y)
    {
        var track = HeatTrack;
        return CombatRules.ClampHeatLimit((int)Math.Round((track.End.Y - y) / track.Size.Y * CombatRules.MaxHeatLimit));
    }
    /// <summary>The height of a heat value on the heat track.</summary>
    public float HeatTrackY(double heat) => HeatTrack.End.Y - HeatTrack.Size.Y * (float)(heat / CombatRules.ShutdownHeat);
    /// <summary>A gauge strip under the schematic, from the fuel bar's left edge to beyond the heat pointer: row 0 is speed, 1 ammo.</summary>
    private Rect2 GaugeStrip(int row)
    {
        var left = FuelBarRect.Position.X; var right = HeatBarRect.End.X + PointerWidth;
        // Directly under the drawn feet, so a width-bound schematic leaves no gap above the gauges.
        var top = Math.Min(SchematicHeight, Origin.Y + FullViewBottom * DrawScale) + row * SpeedStripHeight;
        return new Rect2(left, top, right - left, SpeedStripHeight);
    }
    private Rect2 GaugeIcon(int row)
    {
        var strip = GaugeStrip(row); var size = SpeedStripHeight - 8; return new Rect2(strip.Position.X, strip.Position.Y + 2, size, size);
    }
    private Rect2 GaugeTrack(int row)
    {
        var strip = GaugeStrip(row); var left = GaugeIcon(row).End.X + 4;
        return new Rect2(left, strip.Position.Y + SpeedTrackTop, Math.Max(1, strip.End.X - left - 2), SpeedTrackHeight);
    }
    /// <summary>Where a gauge's pointer can be grabbed: the track and the pointer beneath it, with a little slack.</summary>
    private Rect2 GrabArea(Rect2 track) => track.GrowIndividual(6, 3, 6, PointerWidth + 3);
    public Rect2 SpeedStrip => GaugeStrip(0);
    /// <summary>The horizontal speed track: 0 at its left edge, 150 km/h at its right.</summary>
    public Rect2 SpeedTrack => GaugeTrack(0);
    private Rect2 SpeedGrabArea => GrabArea(SpeedTrack);
    public Rect2 AmmoStrip => GaugeStrip(1);
    /// <summary>The ammo track: economy 0 at its left edge, 10 at its right; the fill shows ammunition left.</summary>
    public Rect2 AmmoTrack => GaugeTrack(1);
    private Rect2 AmmoGrabArea => GrabArea(AmmoTrack);
    public float AmmoTrackX(int economy) => AmmoTrack.Position.X + AmmoTrack.Size.X * CombatRules.ClampAmmoEconomy(economy) / CombatRules.MaxAmmoEconomy;
    /// <summary>The nearest whole economy step for a position on the ammo track.</summary>
    public int AmmoEconomyAt(float x) => CombatRules.ClampAmmoEconomy((int)Math.Round((x - AmmoTrack.Position.X) / AmmoTrack.Size.X * CombatRules.MaxAmmoEconomy));
    public float SpeedTrackX(float metresPerSecond) => SpeedTrack.Position.X + SpeedTrack.Size.X * Math.Clamp(metresPerSecond / CombatRules.SpeedGaugeMaximum, 0, 1);
    /// <summary>The desired speed for a position on the track, in metres per second: whole km/h, snapping to the walk and run notches
    /// within a few pixels, and held between 10 km/h and the current running speed.</summary>
    public float DesiredSpeedAt(float x)
    {
        var unit = Unit!;
        var top = Math.Max(CombatRules.MinimumDesiredSpeed, unit.CurrentRunningSpeed);
        foreach (var notch in new[] { unit.CurrentWalkingSpeed, unit.CurrentRunningSpeed })
            if (Math.Abs(x - SpeedTrackX(notch)) <= SpeedSnapPixels) return Math.Clamp(notch, CombatRules.MinimumDesiredSpeed, top);
        var kmh = Mathf.Round((x - SpeedTrack.Position.X) / SpeedTrack.Size.X * CombatRules.SpeedGaugeMaximum * CombatRules.KmhPerMetrePerSecond);
        return Math.Clamp(kmh / CombatRules.KmhPerMetrePerSecond, CombatRules.MinimumDesiredSpeed, top);
    }
    private const float SpeedSnapPixels = 4;
    public override void _GuiInput(InputEvent @event)
    {
        if (!EditsHeatLimit) { _dragLimit = null; _dragSpeed = null; _dragEconomy = null; return; }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed && HeatGrabArea.HasPoint(button.Position)) { DragHeatLimit(button.Position.Y); AcceptEvent(); }
            else if (button.Pressed && SpeedGrabArea.HasPoint(button.Position)) { DragSpeed(button.Position.X); AcceptEvent(); }
            else if (button.Pressed && EditsAmmoEconomy && AmmoGrabArea.HasPoint(button.Position)) { DragEconomy(button.Position.X); AcceptEvent(); }
            else if (!button.Pressed && (_dragLimit is not null || _dragSpeed is not null || _dragEconomy is not null))
            { _dragLimit = null; _dragSpeed = null; _dragEconomy = null; QueueRedraw(); AcceptEvent(); }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (_dragLimit is not null) { DragHeatLimit(motion.Position.Y); AcceptEvent(); }
            else if (_dragSpeed is not null) { DragSpeed(motion.Position.X); AcceptEvent(); }
            else if (_dragEconomy is not null) { DragEconomy(motion.Position.X); AcceptEvent(); }
        }
    }
    private void DragEconomy(float x)
    {
        var economy = AmmoEconomyAt(x);
        if (economy != (_dragEconomy ?? Unit!.AmmoEconomy)) AmmoEconomyChanged?.Invoke(economy);
        _dragEconomy = economy; QueueRedraw();
    }
    private void DragSpeed(float x)
    {
        var speed = DesiredSpeedAt(x);
        if (Math.Abs(speed - (_dragSpeed ?? Unit!.DesiredSpeed)) > .001f) DesiredSpeedChanged?.Invoke(speed);
        _dragSpeed = speed; QueueRedraw();
    }
    private void DragHeatLimit(float y)
    {
        var limit = HeatLimitAt(y);
        if (limit != (_dragLimit ?? Unit!.HeatLimit)) HeatLimitChanged?.Invoke(limit);
        _dragLimit = limit; QueueRedraw();
    }
    /// <summary>The location drawn under a point in this control's coordinates, front or rear.</summary>
    public MechLocation? LocationAt(Vector2 at)
    {
        if (Unit is null) return null;
        var point = (at - Origin) / DrawScale;
        foreach (var (location, shape) in Shapes) if (Geometry2D.IsPointInPolygon(point, shape)) return location;
        var rear = Compact ? point - CompactRearShift : point;
        foreach (var (location, shape) in RearShapes) if (Geometry2D.IsPointInPolygon(rear, shape)) return location;
        return null;
    }
    public string LocationTooltip(MechLocation location) => Unit is null ? "" : DraftCatalog.LocationName(location).ToUpperInvariant() + "\n"
        + (LocationSystems(Unit, location) is { Count: > 0 } systems
            ? string.Join("\n", systems.Select(item => item.Condition == ComponentCondition.Operational ? item.Text : $"{item.Text} · {item.StatusText}"))
            : "No systems");
    public override string _GetTooltip(Vector2 atPosition)
    {
        if (DisplayedHeatLimit is { } limit && HeatGrabArea.HasPoint(atPosition)) { _tooltipLocation = null; return $"Heat limit {limit}"; }
        if (ShowsSpeed && SpeedStrip.HasPoint(atPosition)) { _tooltipLocation = null; return SpeedTooltip(Unit!, DisplayedDesiredSpeed); }
        if (ShowsAmmo && AmmoStrip.HasPoint(atPosition)) { _tooltipLocation = null; return AmmoTooltip(Unit!, DisplayedAmmoEconomy); }
        if (ShowsStress && StressRect.Grow(4).HasPoint(atPosition)) { _tooltipLocation = null; return StressTooltip(Unit!); }
        _tooltipLocation = Compact ? null : LocationAt(atPosition);
        return _tooltipLocation is { } location ? LocationTooltip(location) : "";
    }
    public override GodotObject? _MakeCustomTooltip(string forText)
    {
        if (Unit is null || _tooltipLocation is not { } location) return null;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        Ui.Label(box, DraftCatalog.LocationName(location).ToUpperInvariant(), 12, Ui.Gold);
        var systems = LocationSystems(Unit, location);
        if (systems.Count == 0) Ui.Label(box, "No systems", 12, Ui.Muted);
        foreach (var system in systems)
            Ui.Label(box, system.Condition == ComponentCondition.Operational ? system.Text : $"{system.Text} · {system.StatusText}", 12, SystemColor(system));
        return box;
    }
    /// <summary>A vertical bar filling from the bottom, with its tinted icon beneath it in the same slot.</summary>
    private void Bar(Rect2 slot, float fraction, Color color, Texture2D icon, bool ticks)
    {
        var iconSize = Math.Min(slot.Size.X * 2, slot.Size.Y * .4f);
        var track = Track(slot);
        DrawRect(track, BarTrack);
        var fill = track.Size.Y * fraction;
        if (fill > 0) DrawRect(new Rect2(track.Position.X, track.End.Y - fill, track.Size.X, fill), color);
        if (ticks)
            foreach (var heat in new[] { CombatRules.WarmHeat, CombatRules.HotHeat })
            {
                var y = track.End.Y - track.Size.Y * (float)(heat / CombatRules.ShutdownHeat);
                DrawLine(new(track.Position.X, y), new(track.End.X, y), BarTick, 1);
            }
        DrawRect(track, new Color("62706b"), false, 1);
        DrawTextureRect(icon, new Rect2(slot.Position.X + (slot.Size.X - iconSize) / 2, slot.End.Y - iconSize, iconSize, iconSize), false, color);
    }
    /// <summary>"Stress: Stressed · skills play 1 worse · Grit Veteran".</summary>
    public static string StressTooltip(CombatUnit unit) => $"Stress: {unit.Stress}"
        + (unit.StressPenalty > 0 ? $" · skills play {unit.StressPenalty} worse" : " · no effect on skills")
        + $" · Grit {WarriorSkills.Descriptor(unit.Warrior.Grit)}\nA nerve check every {CombatRules.NerveCheckSeconds:0} s: pull a stressed pilot out of the fight to calm them.";
    public override void _Draw()
    {
        if (Unit is null) return;
        var scale = DrawScale; var origin = Origin;
        Vector2 At(Vector2 point) => origin + point * scale;
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
            if (Compact) return;
            var abbreviation = DraftCatalog.Abbreviation(location);
            var fontSize = Math.Max(9, (int)(10 * scale));
            var textWidth = ThemeDB.FallbackFont.GetStringSize(abbreviation, fontSize: fontSize).X;
            DrawString(ThemeDB.FallbackFont, At(center) + new Vector2(-textWidth / 2, fontSize * .35f), abbreviation,
                HorizontalAlignment.Left, -1, fontSize, state.Structure > 0 ? new Color("101916") : Ui.Muted);
        }
        if (ShowsFuel) Bar(FuelBarRect, FuelFraction(Unit), FuelColor, FuelIcon, false);
        Bar(HeatBarRect, HeatFraction(Unit), HeatColor(Unit.Heat), HeatIcon, true);
        foreach (var (location, shape) in Shapes) Region(location, shape, false);
        foreach (var (location, shape) in RearShapes)
            Region(location, Compact ? shape.Select(point => point + CompactRearShift).ToArray() : shape, true);
        if (DisplayedHeatLimit is { } limit) HeatLimitHandle(limit);
        if (ShowsSpeed) SpeedGauge(Unit);
        if (ShowsAmmo) AmmoGauge(Unit);
        if (ShowsStress) StressFace(Unit);
    }
    /// <summary>A friendly MechWarrior's stress level, centered above the schematic. Enemy stress is never shown.</summary>
    public bool ShowsStress => !Compact && Unit is { Side: CombatSide.Friendly, Defeated: false };
    public static Color StressColor(StressLevel level) => level switch
    {
        StressLevel.Calm => new Color("b8e6c0"), StressLevel.Steady => new Color("4fbf5a"), StressLevel.Excited => new Color("f1d34c"),
        StressLevel.Stressed => new Color("f0973c"), _ => new Color("ec5a4a")
    };
    private static readonly Dictionary<StressLevel, Texture2D> _stressIcons = [];
    /// <summary>The face for a stress level: a white glyph in <c>Assets/Icons/</c>, tinted with <see cref="StressColor"/> when drawn.</summary>
    public static Texture2D StressIcon(StressLevel level) => _stressIcons.TryGetValue(level, out var icon) ? icon
        : _stressIcons[level] = GD.Load<Texture2D>($"res://Assets/Icons/stress-{level.ToString().ToLowerInvariant()}.svg");
    private const float StressIconSize = 20;
    public Rect2 StressRect => new((Size.X - StressIconSize) / 2, 1, StressIconSize, StressIconSize);
    private void StressFace(CombatUnit unit) => DrawTextureRect(StressIcon(unit.Stress), StressRect, false, StressColor(unit.Stress));
    /// <summary>A yellow track filling from the left with the ammunition left, ticks at each economy step, the shells icon to its
    /// left and, for a friendly mech, the economy pointer beneath it.</summary>
    private void AmmoGauge(CombatUnit unit)
    {
        var track = AmmoTrack;
        DrawRect(track, BarTrack);
        var fill = track.Size.X * AmmoFraction(unit);
        if (fill > 0) DrawRect(new Rect2(track.Position, new Vector2(fill, track.Size.Y)), AmmoColor);
        for (var step = 1; step < CombatRules.MaxAmmoEconomy; step++)
        {
            // Ticks over the yellow fill are dark so they stay visible.
            var x = AmmoTrackX(step);
            DrawLine(new(x, track.End.Y - track.Size.Y * .45f), new(x, track.End.Y), x <= track.Position.X + fill ? BarTrack : BarTick, 1);
        }
        DrawRect(track, new Color("62706b"), false, 1);
        DrawTextureRect(AmmoIcon, GaugeIcon(1), false, AmmoColor);
        if (DisplayedAmmoEconomy is { } economy) GaugePointer(track, AmmoTrackX(economy), _dragEconomy is null ? null : economy.ToString());
    }
    /// <summary>A line across a gauge track and a triangle beneath it; the value shows above while dragging.</summary>
    private void GaugePointer(Rect2 track, float at, string? label)
    {
        var width = PointerWidth;
        DrawLine(new(at, track.Position.Y), new(at, track.End.Y), HeatLimitColor, 2);
        DrawColoredPolygon([new(at, track.End.Y + 1), new(at - width * .7f, track.End.Y + 1 + width), new(at + width * .7f, track.End.Y + 1 + width)], HeatLimitColor);
        if (label is null) return;
        const int fontSize = 12;
        var size = ThemeDB.FallbackFont.GetStringSize(label, fontSize: fontSize);
        var box = new Rect2(Math.Clamp(at - size.X / 2 - 3, 0, Math.Max(0, Size.X - size.X - 6)), track.Position.Y - size.Y - 6, size.X + 6, size.Y + 2);
        DrawRect(box, BarTrack); DrawRect(box, new Color("62706b"), false, 1);
        DrawString(ThemeDB.FallbackFont, new(box.Position.X + 3, box.End.Y - 4), label, HorizontalAlignment.Left, -1, fontSize, HeatLimitColor);
    }
    /// <summary>A track filling from the left, with notches every 20 km/h, taller ones at the walking and running speeds, the icon
    /// to its left and, for a friendly mech, the desired-speed pointer beneath it.</summary>
    private void SpeedGauge(CombatUnit unit)
    {
        var track = SpeedTrack; var color = SpeedColor(unit);
        DrawRect(track, BarTrack);
        var fill = track.Size.X * SpeedFraction(unit);
        if (fill > 0) DrawRect(new Rect2(track.Position, new Vector2(fill, track.Size.Y)), color);
        for (var kmh = 20; kmh < 150; kmh += 20)
        {
            var x = SpeedTrackX(kmh / CombatRules.KmhPerMetrePerSecond);
            DrawLine(new(x, track.End.Y - track.Size.Y * .45f), new(x, track.End.Y), BarTick, 1);
        }
        foreach (var notch in new[] { unit.CurrentWalkingSpeed, unit.CurrentRunningSpeed })
        {
            var x = SpeedTrackX(notch);
            DrawLine(new(x, track.Position.Y - 3), new(x, track.End.Y + 3), SpeedNotchColor, 2);
        }
        DrawRect(track, new Color("62706b"), false, 1);
        DrawTextureRect(SpeedIcon, GaugeIcon(0), false, color);
        if (DisplayedDesiredSpeed is { } desired) GaugePointer(track, SpeedTrackX(desired), _dragSpeed is null ? null : $"{Kmh(desired)} km/h");
    }
    public static readonly Color SpeedNotchColor = new("f2f4f3");
    /// <summary>A line across the heat track at the limit and a pointer beyond its right edge; the number shows while dragging.</summary>
    private void HeatLimitHandle(int limit)
    {
        var track = HeatTrack; var y = HeatTrackY(limit); var width = PointerWidth;
        DrawLine(new(track.Position.X, y), new(track.End.X, y), HeatLimitColor, 2);
        DrawColoredPolygon([new(track.End.X + 1, y), new(track.End.X + 1 + width, y - width * .7f), new(track.End.X + 1 + width, y + width * .7f)], HeatLimitColor);
        if (_dragLimit is null) return;
        var text = limit.ToString(); const int fontSize = 12;
        var size = ThemeDB.FallbackFont.GetStringSize(text, fontSize: fontSize);
        var box = new Rect2(track.Position.X - BarGap - size.X - 6, y - size.Y / 2 - 1, size.X + 6, size.Y + 2);
        DrawRect(box, BarTrack); DrawRect(box, new Color("62706b"), false, 1);
        DrawString(ThemeDB.FallbackFont, new(box.Position.X + 3, y + fontSize * .35f), text, HorizontalAlignment.Left, -1, fontSize, HeatLimitColor);
    }
    public static readonly Color HeatLimitColor = new("f2f4f3");
}
