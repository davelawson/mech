namespace Mech;

/// <summary>A save slot in a library: its file id and slot information, or null information when the file cannot be read.</summary>
public sealed record SaveSlot(string Id, SaveInfo? Info);

/// <summary>
/// The save slots in one folder. Single-player and co-op saves live in separate libraries, and each refuses the other kind.
/// </summary>
public sealed class SaveLibrary(string directory, bool coop)
{
    public const string CoopRefusal = "This is a co-op campaign.";
    public const string SoloRefusal = "This is not a co-op campaign.";
    private const string Extension = ".json";

    public string Directory => directory;
    public bool Coop => coop;

    public string PathOf(string id)
    {
        if (id.Length == 0 || id.IndexOfAny(['/', '\\', '.']) >= 0) throw new ArgumentException("Invalid save slot.");
        return System.IO.Path.Combine(directory, id + Extension);
    }

    /// <summary>Every slot, the most recently saved first. Unreadable files are listed last.</summary>
    public IReadOnlyList<SaveSlot> List()
    {
        if (!System.IO.Directory.Exists(directory)) return [];
        var slots = new List<SaveSlot>();
        foreach (var file in System.IO.Directory.GetFiles(directory, "*" + Extension))
        {
            var id = System.IO.Path.GetFileNameWithoutExtension(file);
            if (id.Length == 0 || id.Contains('.')) continue;
            SaveInfo? info;
            try { info = new CampaignStore(file).ReadInfo(); }
            catch (Exception error) when (IsStorageError(error)) { info = null; }
            slots.Add(new(id, info));
        }
        return [.. slots.OrderBy(slot => slot.Info is null).ThenByDescending(slot => slot.Info?.SavedAt).ThenBy(slot => slot.Id, StringComparer.Ordinal)];
    }

    public bool Any() => List().Count > 0;

    /// <summary>Writes the campaign to slot <paramref name="id"/>, or to a new slot when null, and returns the slot's id.</summary>
    public string Save(Campaign campaign, string? name, string? id = null)
    {
        if ((campaign.PlayerNames is not null) != coop) throw new ArgumentException(coop ? SoloRefusal : CoopRefusal);
        id ??= Guid.NewGuid().ToString("N");
        new CampaignStore(PathOf(id)).Save(campaign, name);
        return id;
    }

    public SaveInfo Info(string id) => new CampaignStore(PathOf(id)).ReadInfo();

    public Campaign Load(string id)
    {
        var campaign = new CampaignStore(PathOf(id)).Load();
        if ((campaign.PlayerNames is not null) != coop) throw new ArgumentException(coop ? SoloRefusal : CoopRefusal);
        return campaign;
    }

    /// <summary>Moves a save from before slots into the library of its kind, keeping its campaign and saved time.</summary>
    public static void ImportLegacy(string legacyPath, SaveLibrary solo, SaveLibrary coop)
    {
        if (!System.IO.File.Exists(legacyPath)) return;
        try
        {
            var info = new CampaignStore(legacyPath).ReadInfo();
            var library = info.Players is null ? solo : coop;
            System.IO.Directory.CreateDirectory(library.Directory);
            System.IO.File.Move(legacyPath, library.PathOf(Guid.NewGuid().ToString("N")));
        }
        catch (Exception error) when (IsStorageError(error)) { }
    }

    public static bool IsStorageError(Exception error) => error is System.IO.IOException or UnauthorizedAccessException
        or System.Text.Json.JsonException or ArgumentException or NotSupportedException or InvalidOperationException;
}
