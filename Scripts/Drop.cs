using Godot;

namespace Mech;

/// <summary>Drop phase numbers, recorded in docs/combat-tuning.md.</summary>
public static partial class CombatRules
{
    /// <summary>No drop site centre lies within this distance of any arena edge.</summary>
    public const float DropEdgePadding = 200;
    /// <summary>A drop zone reaches this far in from its edge: a fifth of the arena.</summary>
    public const float DropZoneDepth = ArenaHalfSize * 2 * .2f;
    /// <summary>A lance's drop site: a circle its mechs are scattered across.</summary>
    public const float DropSiteRadius = 100;
    /// <summary>Lancemates land at least this far apart.</summary>
    public const float DropMechSpacing = 40;
    /// <summary>Lances without a chosen site, such as in a test battle, drop this far apart along their zone.</summary>
    public const float DefaultDropSpacing = 300;
    /// <summary>Enemy lances are drawn from the opposition in this many mechs, in order.</summary>
    public const int EnemyDropLanceSize = ForceRoster.LanceSize;
}

/// <summary>The part of an arena a side drops into: the whole length of an edge, less the edge padding at both ends, from the
/// padding to <see cref="CombatRules.DropZoneDepth"/> in from that edge. Anchor is a point in it that terrain generation keeps
/// clear and connected; its walkable region is the one every drop site must land on.</summary>
public sealed record DropZone(ArenaEdge Edge, Vector2 Anchor)
{
    private const float Half = CombatRules.ArenaHalfSize;
    public Vector2 Inward => PatrolLayout.Inward(Edge);
    public Vector2 Along => PatrolLayout.Along(Edge);
    /// <summary>How far a point lies in from this zone's edge.</summary>
    public float Depth(Vector2 point) => Half + point.Dot(Inward);
    public bool Contains(Vector2 point) => Depth(point) >= CombatRules.DropEdgePadding - 1e-3f && Depth(point) <= CombatRules.DropZoneDepth + 1e-3f
        && Math.Abs(point.Dot(Along)) <= Half - CombatRules.DropEdgePadding + 1e-3f;
    /// <summary>The zone's outline, corner to corner.</summary>
    public Vector2[] Corners
    {
        get
        {
            const float side = Half - CombatRules.DropEdgePadding;
            return [PatrolLayout.EdgePoint(Edge, -side, CombatRules.DropEdgePadding), PatrolLayout.EdgePoint(Edge, side, CombatRules.DropEdgePadding),
                PatrolLayout.EdgePoint(Edge, side, CombatRules.DropZoneDepth), PatrolLayout.EdgePoint(Edge, -side, CombatRules.DropZoneDepth)];
        }
    }
    public static ArenaEdge Opposite(ArenaEdge edge) => edge switch
    {
        ArenaEdge.South => ArenaEdge.North, ArenaEdge.North => ArenaEdge.South, ArenaEdge.East => ArenaEdge.West, _ => ArenaEdge.East,
    };
    /// <summary>A standard battle's zone on an edge: anchored on the clear band terrain generation keeps across its middle.</summary>
    public static DropZone Standard(ArenaEdge edge) => new(edge, PatrolLayout.EdgePoint(edge, 0, Half - CombatRules.DeploymentDepth));
    /// <summary>The edge a standard battle's company drops on, drawn from the terrain seed so every replica agrees.</summary>
    public static ArenaEdge StandardEdge(int terrainSeed) => (ArenaEdge)new Random(terrainSeed ^ 0x0D20F5).Next(4);

    /// <summary>A fixed scatter of a lance of <paramref name="count"/> mechs around <paramref name="site"/>, or null when the site does
    /// not hold enough clear ground connected to the battlefield. It decides whether a site can be used, the same way on every
    /// machine; where mechs actually land at the end of a drop phase is drawn afresh.</summary>
    public Vector2[]? Scatter(BattlefieldTerrain terrain, Vector2 site, int count, int seed) => Scatter(terrain, site, count,
        new Random(unchecked(seed * 486187739 + (int)MathF.Round(site.X) * 73856093 ^ (int)MathF.Round(site.Y) * 19349663)));
    /// <summary>A lance scattered around <paramref name="site"/> with <paramref name="random"/>, or null when this draw did not fit it.</summary>
    public Vector2[]? Scatter(BattlefieldTerrain terrain, Vector2 site, int count, Random random)
    {
        var component = terrain.Component(Anchor);
        var placed = new List<Vector2>();
        for (var attempt = 0; attempt < 300 && placed.Count < count; attempt++)
        {
            var point = site + Vector2.FromAngle(random.NextSingle() * Mathf.Tau) * (CombatRules.DropSiteRadius * MathF.Sqrt(random.NextSingle()));
            if (placed.Any(other => other.DistanceTo(point) < CombatRules.DropMechSpacing)
                || !terrain.Passable(point) || terrain.Component(point) != component) continue;
            placed.Add(point);
        }
        return placed.Count == count ? [.. placed] : null;
    }
}

/// <summary>A lance's drop site so far: none until its player chooses, and whether they have confirmed it. Bounced counts the
/// sites the player chose that the leader turned away.</summary>
public sealed record DropSiteState(int Slot, Vector2? Site, bool Ready, int Bounced = 0);

/// <summary>The drop phase that opens a battle: each player chooses where in the shared drop zone their lance lands.</summary>
public sealed class DropPhase
{
    public DropZone Zone { get; }
    /// <summary>The opposition's zone, which every player sees; a patrol has none.</summary>
    public DropZone? EnemyZone { get; }
    private readonly Dictionary<int, DropSiteState> _sites;
    private readonly IReadOnlyDictionary<int, Vector2> _defaults;
    public IReadOnlyCollection<DropSiteState> Sites => _sites.Values;
    internal DropPhase(DropZone zone, DropZone? enemyZone, IReadOnlyDictionary<int, Vector2> defaults)
    {
        Zone = zone; EnemyZone = enemyZone; _defaults = defaults;
        _sites = defaults.Keys.ToDictionary(slot => slot, slot => new DropSiteState(slot, null, false));
    }
    /// <summary>Where a lance would land without a choice: evenly along the zone, as in a battle without a drop phase.</summary>
    public Vector2 DefaultSite(int slot) => _defaults[slot];
    public DropSiteState Site(int slot) => _sites[slot];
    internal void Set(DropSiteState site) => _sites[site.Slot] = site;
    public bool AllReady => _sites.Values.All(site => site.Ready && site.Site is not null);
    internal DropSiteState[] State() => [.. _sites.Values.OrderBy(site => site.Slot)];
    internal void Apply(DropSiteState[] sites)
    {
        if (sites.Length != _sites.Count || sites.Any(site => !_sites.ContainsKey(site.Slot)))
            throw new ArgumentException("The battle state does not match this battle.", nameof(sites));
        foreach (var site in sites) _sites[site.Slot] = site;
    }
}

public sealed partial class CombatSimulation
{
    /// <summary>The drop phase still under way, or null once every lance has landed. The battle holds paused through it.</summary>
    public DropPhase? Drop { get; private set; }
    /// <summary>Where each lance was dropped, so replicas and the screen can show the zone the company fights from.</summary>
    public DropZone? FriendlyZone { get; private set; }
    public DropZone? EnemyZone { get; private set; }

    /// <summary>No friendly mech is on the field during the drop phase: where each lands inside its site stays unknown until the
    /// phase ends.</summary>
    public bool AwaitingDrop(CombatUnit unit) => Drop is not null && unit.Side == CombatSide.Friendly;
    /// <summary>Where mechs land at the end of a drop phase, drawn in landing order so a site never lands the same way twice.</summary>
    private Random DropRandom => _dropRandom ??= new Random(unchecked(_seed ^ 0x6A09E667));
    private Random? _dropRandom;

    private IEnumerable<CombatUnit> LanceUnits(int slot) => Units.Where(unit => unit.Side == CombatSide.Friendly && unit.Lance == slot);

    /// <summary>Why <paramref name="slot"/>'s lance cannot drop at <paramref name="site"/>, or null when it can.</summary>
    public string? DropSiteProblem(int slot, Vector2 site)
    {
        if (FriendlyZone is not { } zone) return "There is no drop zone.";
        if (!zone.Contains(site)) return "Outside the drop zone";
        if (Drop is { } drop && drop.Sites.Any(other => other.Slot != slot && other.Site is { } taken
            && taken.DistanceTo(site) < CombatRules.DropSiteRadius * 2 - 1e-3f)) return "Overlaps another lance's drop site";
        return zone.Scatter(Terrain, site, Math.Max(1, LanceUnits(slot).Count()), unchecked(_seed + slot)) is null ? "Not enough clear ground" : null;
    }

    /// <summary>Places a lance's mechs across a drop site, facing into the arena, holding: at random points drawn from
    /// <paramref name="random"/> when given, falling back to the site's fixed scatter, which is known to fit.</summary>
    private void Land(IEnumerable<CombatUnit> lance, DropZone zone, Vector2 site, int salt, Random? random = null)
    {
        var units = lance.ToArray();
        var points = (random is null ? null : zone.Scatter(Terrain, site, units.Length, random))
            ?? zone.Scatter(Terrain, site, units.Length, unchecked(_seed + salt)) ?? throw new InvalidOperationException("The drop site has no room.");
        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            unit.Position = unit.Destination = unit.Post = points[i]; unit.Facing = zone.Inward; unit.Route = Array.Empty<Vector2>();
            unit.Order = CombatOrder.Hold; unit.AttackTarget = unit.FollowTarget = null;
        }
    }

    /// <summary>The first valid site for a lance on a spiral outward from <paramref name="start"/>.</summary>
    private Vector2 NearestSite(Func<Vector2, bool> valid, Vector2 start)
    {
        for (var slot = 0; slot < 4000; slot++)
        {
            var point = start + (slot == 0 ? Vector2.Zero : Vector2.FromAngle(slot * 2.4f) * 30 * MathF.Sqrt(slot));
            point = new(MathF.Round(point.X), MathF.Round(point.Y));
            if (valid(point)) return point;
        }
        throw new InvalidOperationException("No drop site fits the zone.");
    }
    /// <summary>A random valid site, falling back to the nearest valid site to the zone's anchor.</summary>
    private static Vector2 RandomSite(DropZone zone, Func<Vector2, bool> valid, Random random, Func<Vector2> fallback)
    {
        const float side = CombatRules.ArenaHalfSize - CombatRules.DropEdgePadding;
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var along = (random.NextSingle() * 2 - 1) * side;
            var depth = CombatRules.DropEdgePadding + random.NextSingle() * (CombatRules.DropZoneDepth - CombatRules.DropEdgePadding);
            var point = PatrolLayout.EdgePoint(zone.Edge, along, depth);
            point = new(MathF.Round(point.X), MathF.Round(point.Y));
            if (valid(point)) return point;
        }
        return fallback();
    }

    /// <summary>Lands every friendly lance on the default sites, evenly along its zone, and every enemy lance at a random valid site in
    /// its own zone. A patrol's enemies keep their posts.</summary>
    private void LandAll(DropZone zone, DropZone? enemyZone, bool openDropPhase)
    {
        FriendlyZone = zone; EnemyZone = enemyZone;
        var players = Players.ToArray();
        var center = zone.Anchor.Dot(zone.Along);
        var chosen = new List<Vector2>(); var defaults = new Dictionary<int, Vector2>();
        for (var i = 0; i < players.Length; i++)
        {
            var slot = players[i];
            var start = PatrolLayout.EdgePoint(zone.Edge, center + (i - (players.Length - 1) / 2f) * CombatRules.DefaultDropSpacing, zone.Depth(zone.Anchor));
            var count = LanceUnits(slot).Count();
            var site = NearestSite(point => zone.Contains(point) && chosen.All(other => other.DistanceTo(point) >= CombatRules.DropSiteRadius * 2)
                && zone.Scatter(Terrain, point, count, unchecked(_seed + slot)) is not null, start);
            chosen.Add(site); defaults[slot] = site; Land(LanceUnits(slot), zone, site, slot);
        }
        if (enemyZone is { } enemies)
        {
            var random = new Random(unchecked(_seed ^ 0x3E7D20));
            var lances = Units.Where(unit => unit.Side == CombatSide.Enemy).Select((unit, i) => (unit, i))
                .GroupBy(item => item.i / CombatRules.EnemyDropLanceSize, item => item.unit).ToArray();
            var taken = new List<Vector2>();
            foreach (var lance in lances)
            {
                var salt = -1 - lance.Key; var count = lance.Count();
                bool Valid(Vector2 point) => enemies.Contains(point) && taken.All(other => other.DistanceTo(point) >= CombatRules.DropSiteRadius * 2)
                    && enemies.Scatter(Terrain, point, count, unchecked(_seed + salt)) is not null;
                var site = RandomSite(enemies, Valid, random, () => NearestSite(Valid, enemies.Anchor));
                taken.Add(site); Land(lance, enemies, site, salt);
            }
        }
        // Contacts made before landing, with every mech still unplaced, are forgotten; nothing is seen until the lances land.
        foreach (var side in _contacts.Values) side.Clear();
        if (!openDropPhase) { RefreshDetection(); return; }
        Drop = new DropPhase(zone, enemyZone, defaults);
    }

    /// <summary>Chooses or moves a player's drop site. A site that is invalid, such as one overlapping another lance's, is turned away.</summary>
    internal void ChooseDropSite(int slot, Vector2 point)
    {
        if (Drop is not { } drop || !point.IsFinite()) return;
        var current = drop.Site(slot);
        if (current.Ready) return;
        var site = new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
        if (DropSiteProblem(slot, site) is not null) { drop.Set(current with { Bounced = current.Bounced + 1 }); return; }
        drop.Set(current with { Site = site });
    }

    /// <summary>Confirms or reopens a player's drop site. Once every player is ready, the lances land and the battle begins, paused.</summary>
    internal void ReadyDrop(int slot, bool ready)
    {
        if (Drop is not { } drop) return;
        var current = drop.Site(slot);
        if (ready && current.Site is null) return;
        drop.Set(current with { Ready = ready });
        if (drop.AllReady) FinishDrop();
    }

    /// <summary>The leader starts the battle at once: every lance without a site drops at a random valid one.</summary>
    internal void ForceDrop(int slot)
    {
        if (slot != Campaign.LeaderSlot || Drop is not { } drop) return;
        var random = new Random(unchecked(_seed ^ 0x5F0D2A));
        foreach (var site in drop.Sites.Where(site => site.Site is null).ToArray())
        {
            var count = LanceUnits(site.Slot).Count();
            bool Valid(Vector2 point) => DropSiteProblem(site.Slot, point) is null;
            var point = RandomSite(drop.Zone, Valid, random, () => NearestSite(Valid, drop.Zone.Anchor));
            drop.Set(site with { Site = point });
        }
        FinishDrop();
    }

    /// <summary>Every lance lands across its chosen site, in slot order, and the battle begins.</summary>
    private void FinishDrop()
    {
        var drop = Drop!;
        foreach (var slot in Players) Land(LanceUnits(slot), drop.Zone, drop.Site(slot).Site!.Value, slot, DropRandom);
        Drop = null; RefreshDetection();
    }

    /// <summary>A replica takes on the leader's drop phase, which ends when the leader's has.</summary>
    private void ApplyDrop(DropSiteState[]? sites)
    {
        if (sites is null) { Drop = null; return; }
        if (Drop is null) throw new ArgumentException("The battle state does not match this battle.", nameof(sites));
        Drop.Apply(sites);
    }
}
