namespace Mech;

/// <summary>Authored briefing terms; acceptance does not execute or settle a mission.</summary>
public sealed record MissionOffer(string Id, string Name, string Briefing, string Objective,
    string Opposition, long Payment, string Destination, string TravelEstimate, string Deadline);

public static class MissionCatalog
{
    public static readonly MissionOffer EasyPeasy = new("easy-peasy", "Easy Peasy",
        "Raiders have occupied an abandoned depot. Remove them and secure the site.",
        "Defeat the defending force.", "One light mech", 100_000, "Unassigned", "Unavailable", "None");
    public static IReadOnlyList<MissionOffer> Offers { get; } = Array.AsReadOnly(new[] { EasyPeasy });
}

public sealed record MissionSnapshot(string? SelectedMissionId, string[] SeenMissionIds);

/// <summary>The offer pool, browsing history, and single accepted mission, without scene dependencies.</summary>
public sealed class MissionBoard
{
    private readonly MissionOffer[] _offers;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    public IReadOnlyList<MissionOffer> Offers { get; }
    public string? SelectedMissionId { get; private set; }
    public MissionOffer? Current => _offers.SingleOrDefault(item => item.Id == SelectedMissionId);
    public IEnumerable<MissionOffer> Available => _offers.Where(item => item.Id != SelectedMissionId);
    public bool HasUnseenOffers => Available.Any(item => !_seen.Contains(item.Id));

    public MissionBoard(IEnumerable<MissionOffer>? offers = null)
    {
        _offers = (offers ?? MissionCatalog.Offers).ToArray();
        if (_offers.Any(item => item is null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name))
            || _offers.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != _offers.Length)
            throw new ArgumentException("Mission offers need unique IDs and names.", nameof(offers));
        Offers = Array.AsReadOnly(_offers);
    }

    public void MarkOffersSeen()
    {
        foreach (var offer in Available) _seen.Add(offer.Id);
    }

    public void Accept(string id)
    {
        if (SelectedMissionId is not null) throw new InvalidOperationException("Abandon the current mission before accepting another.");
        if (!_offers.Any(item => item.Id == id)) throw new ArgumentException("The mission is not available.", nameof(id));
        SelectedMissionId = id;
        _seen.Add(id);
    }

    public void Abandon()
    {
        if (SelectedMissionId is null) throw new InvalidOperationException("There is no current mission to abandon.");
        SelectedMissionId = null;
    }

    public MissionSnapshot Snapshot() => new(SelectedMissionId, _seen.Order(StringComparer.Ordinal).ToArray());

    public static MissionBoard Restore(MissionSnapshot snapshot, IEnumerable<MissionOffer>? offers = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var board = new MissionBoard(offers);
        if (snapshot.SeenMissionIds is null
            || snapshot.SeenMissionIds.Any(id => id is null || !board._offers.Any(item => item.Id == id))
            || snapshot.SeenMissionIds.Distinct(StringComparer.Ordinal).Count() != snapshot.SeenMissionIds.Length
            || snapshot.SelectedMissionId is not null && (!board._offers.Any(item => item.Id == snapshot.SelectedMissionId)
                || !snapshot.SeenMissionIds.Contains(snapshot.SelectedMissionId, StringComparer.Ordinal)))
            throw new ArgumentException("The mission save contains invalid state.", nameof(snapshot));
        board.SelectedMissionId = snapshot.SelectedMissionId;
        board._seen.UnionWith(snapshot.SeenMissionIds);
        return board;
    }
}
