using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mech;

/// <summary>What a save slot shows without restoring the campaign: its name, when it was written, and the company's date and balance.</summary>
public sealed record SaveInfo(string Name, DateTimeOffset SavedAt, string Company, DateOnly Date, long Cash, int? Players);

/// <summary>A save file: its slot information followed by the campaign.</summary>
public sealed record SaveFile(int Format, SaveInfo Info, CampaignSnapshot Campaign);

/// <summary>One campaign save file. Older files hold the bare campaign without slot information; they still load.</summary>
public sealed class CampaignStore(string path)
{
    public const int Format = 1;
    public const int MaxNameLength = 40;
    private const long MaxSize = 1_048_576;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Path => path;
    public bool Exists => System.IO.File.Exists(path);

    /// <summary>A valid slot name: trimmed, not empty, and at most <see cref="MaxNameLength"/> characters.</summary>
    public static string CleanName(string? name, Campaign campaign)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = campaign.Roster?.ForceName ?? "Company";
        return clean.Length > MaxNameLength ? clean[..MaxNameLength].TrimEnd() : clean;
    }

    public void Save(Campaign campaign, string? name = null)
    {
        if (campaign.RequiresDraft) throw new InvalidOperationException("Finish the initial draft before saving the company.");
        var snapshot = campaign.Snapshot();
        _ = Campaign.Restore(snapshot);
        var info = new SaveInfo(CleanName(name, campaign), DateTimeOffset.UtcNow, campaign.Roster?.ForceName ?? "", campaign.Date, campaign.Cash,
            campaign.PlayerNames is null ? null : campaign.Players);
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        System.IO.Directory.CreateDirectory(directory);
        // Replace only after the complete new snapshot reaches disk, preserving the old save on failure.
        var temporary = System.IO.Path.Combine(directory, $".campaign-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new System.IO.FileStream(temporary, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, new SaveFile(Format, info, snapshot), JsonOptions);
                stream.Flush(true);
            }
            System.IO.File.Move(temporary, path, true);
        }
        finally
        {
            if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary);
        }
    }

    public Campaign Load() => Campaign.Restore(Read().Campaign);

    /// <summary>The slot information alone. An older file's comes from its campaign, saved when the file was last written.</summary>
    public SaveInfo ReadInfo() => Read().Info;

    private SaveFile Read()
    {
        if (new System.IO.FileInfo(path).Length > MaxSize)
            throw new System.IO.InvalidDataException("The campaign save is too large.");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(nameof(SaveFile.Info), out _) && root.TryGetProperty(nameof(SaveFile.Campaign), out _))
        {
            var file = root.Deserialize<SaveFile>(JsonOptions);
            if (file?.Info is null || file.Campaign is null) throw new System.IO.InvalidDataException("The campaign save is empty.");
            if (string.IsNullOrWhiteSpace(file.Info.Name)) throw new System.IO.InvalidDataException("The campaign save has no name.");
            return file;
        }
        var snapshot = root.Deserialize<CampaignSnapshot>(JsonOptions) ?? throw new System.IO.InvalidDataException("The campaign save is empty.");
        var company = snapshot.Roster?.ForceName ?? "";
        return new(0, new(company.Length > 0 ? company : "Company", System.IO.File.GetLastWriteTimeUtc(path), company, snapshot.Date, snapshot.Cash,
            snapshot.PlayerNames?.Length), snapshot);
    }
}
