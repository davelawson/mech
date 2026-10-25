using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mech;

public sealed class CampaignStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public bool Exists => System.IO.File.Exists(path);

    public void Save(Campaign campaign)
    {
        if (campaign.RequiresDraft) throw new InvalidOperationException("Finish the initial draft before saving the company.");
        var snapshot = campaign.Snapshot();
        _ = Campaign.Restore(snapshot);
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        System.IO.Directory.CreateDirectory(directory);
        // Replace only after the complete new snapshot reaches disk, preserving the old save on failure.
        var temporary = System.IO.Path.Combine(directory, $".campaign-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new System.IO.FileStream(temporary, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, snapshot, JsonOptions);
                stream.Flush(true);
            }
            System.IO.File.Move(temporary, path, true);
        }
        finally
        {
            if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary);
        }
    }

    public Campaign Load()
    {
        if (new System.IO.FileInfo(path).Length > 1_048_576)
            throw new System.IO.InvalidDataException("The campaign save is too large.");
        var snapshot = JsonSerializer.Deserialize<CampaignSnapshot>(System.IO.File.ReadAllText(path), JsonOptions)
            ?? throw new System.IO.InvalidDataException("The campaign save is empty.");
        return Campaign.Restore(snapshot);
    }
}
