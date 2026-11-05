namespace Mech;

public enum ReputationTier { Hated, Hostile, Neutral, Friendly, Honored }

/// <summary>How a contract ended. Abandoning it or missing its deadline means no battle was fought.</summary>
public enum MissionOutcome { Success, Failure, Abandoned, MissedDeadline }

/// <summary>The facts of a finished mission that reputation weighs. Armor lost is in tenths of a stock point, the combat scale.
/// Facts whose mechanic does not exist yet (fleeing, buildings, salvage) are reported as zero until it does.</summary>
public sealed record MissionConduct(MissionOutcome Outcome, string[] DeployedMechIds, int ArmorLost,
    int EnemyWarriorsSpared, int EnemyMechsFled, int SalvagePicksTaken, int BuildingsDestroyed)
{
    public bool Fought => Outcome is MissionOutcome.Success or MissionOutcome.Failure;

    /// <summary>The most armor the deployed mechs' tonnages allow, in tenths: the yardstick for effort, whatever they actually carried.</summary>
    public int MaximumArmor => DeployedMechIds.Sum(id => DraftCatalog.Mech(id).Locations.Sum(EquipmentRules.ArmorLimit)) * CombatRules.DamageScale;

    /// <summary>A failure still shows effort when the force lost a quarter of the armor its tonnage allows.</summary>
    public bool ReasonableEffort => Fought && ArmorLost * ReputationTuning.EffortArmorDivisor >= MaximumArmor;
}

/// <summary>Numbers for reputation, recorded in docs/design.md. Changes are in tenths of a point.</summary>
public static class ReputationTuning
{
    public const int Limit = 100;
    public const int Scale = 10;
    // A value at or beyond a threshold reaches that tier.
    public const int Hated = -60, Hostile = -20, Friendly = 20, Honored = 60;

    // Mission changes, in tenths.
    public const int EmployerSuccess = 60, EmployerFailureWithEffort = -20, EmployerFailureWithoutEffort = -80, EmployerSalvagePerPick = -20;
    public const int OpponentBase = -30, OpponentWarriorSpared = 10, OpponentMechFled = 10;
    public const int ComStarSuccess = 10, ComStarFailureWithoutEffort = -10, ComStarWarriorSpared = 5, ComStarMechFled = 5,
        ComStarSalvagePerPick = -10, ComStarBuilding = -5, ComStarPirateWork = -20;
    public const int OwnerBuilding = -10, OwnerPirateWork = -20;
    /// <summary>Buildings a battle may destroy before anyone minds.</summary>
    public const int BuildingAllowance = 3;
    /// <summary>Effort means losing at least 1/4 of the armor the deployed tonnage allows.</summary>
    public const int EffortArmorDivisor = 4;
}

/// <summary>A save's reputation with each tracked faction, in tenths of a point, keyed by faction name.</summary>
public sealed record ReputationSnapshot(Dictionary<string, int> Tenths);

/// <summary>The mercenary force's standing with each tracked faction, from −100 to +100. Everyone starts at 0, neutral,
/// and standing never drifts back on its own.</summary>
public sealed class Reputation
{
    private readonly Dictionary<Faction, int> _tenths = Enum.GetValues<Faction>().ToDictionary(faction => faction, _ => 0);

    /// <summary>Standing in tenths of a point, which keeps half-point changes exact.</summary>
    public int Tenths(Faction faction) => _tenths[faction];
    /// <summary>Whole-point standing, rounded half away from zero.</summary>
    public int Value(Faction faction) => (int)Math.Round(_tenths[faction] / (double)ReputationTuning.Scale, MidpointRounding.AwayFromZero);
    public ReputationTier Tier(Faction faction) => TierFor(Value(faction));
    public bool Hates(Faction faction) => Tier(faction) == ReputationTier.Hated;

    public static ReputationTier TierFor(int value) => value switch
    {
        <= ReputationTuning.Hated => ReputationTier.Hated,
        <= ReputationTuning.Hostile => ReputationTier.Hostile,
        >= ReputationTuning.Honored => ReputationTier.Honored,
        >= ReputationTuning.Friendly => ReputationTier.Friendly,
        _ => ReputationTier.Neutral,
    };

    /// <summary>"Friendly +24", "Neutral 0" or "Hostile −31".</summary>
    public string Text(Faction faction) => $"{Tier(faction)} {Signed(Value(faction))}";
    public static string Signed(int value) => value switch { > 0 => $"+{value}", < 0 => $"−{-value}", _ => "0" };

    /// <summary>"Owner: Draconis Combine · Friendly +24"; a system held by anyone untracked, or contested, shows no standing.
    /// Empty for unclaimed systems.</summary>
    public string OwnerText(StarSystem system) => system.OwnerText.Length == 0 ? ""
        : $"Owner: {system.OwnerText}" + (system.Faction is { } faction ? $" · {Text(faction)}" : "");

    /// <summary>Changes standing by a number of tenths, held within ±100.</summary>
    public void Adjust(Faction faction, int tenths)
    {
        var limit = ReputationTuning.Limit * ReputationTuning.Scale;
        _tenths[faction] = Math.Clamp(_tenths[faction] + tenths, -limit, limit);
    }

    /// <summary>What a finished mission does to each tracked faction's standing, in tenths. Changes from several roles add up:
    /// ComStar as employer also gains its honor changes. Pirates and local forces hold no reputation.</summary>
    public static IReadOnlyDictionary<Faction, int> MissionChanges(MissionOffer offer, MissionConduct conduct, StarChart? chart = null)
    {
        var changes = new Dictionary<Faction, int>();
        void Add(Faction? faction, int tenths) { if (faction is { } f && tenths != 0) changes[f] = changes.GetValueOrDefault(f) + tenths; }
        var owner = (chart ?? StarChart.Instance)[offer.SystemId].Faction;
        var overSalvage = Math.Max(0, conduct.SalvagePicksTaken - offer.SalvagePicks);
        var excessBuildings = Math.Max(0, conduct.BuildingsDestroyed - ReputationTuning.BuildingAllowance);

        var noEffort = conduct.Outcome != MissionOutcome.Success && !conduct.ReasonableEffort;
        Add(offer.Employer.Faction, conduct.Outcome == MissionOutcome.Success ? ReputationTuning.EmployerSuccess
            : noEffort ? ReputationTuning.EmployerFailureWithoutEffort : ReputationTuning.EmployerFailureWithEffort);
        Add(offer.Employer.Faction, overSalvage * ReputationTuning.EmployerSalvagePerPick);
        Add(Faction.ComStar, conduct.Outcome == MissionOutcome.Success ? ReputationTuning.ComStarSuccess : noEffort ? ReputationTuning.ComStarFailureWithoutEffort : 0);
        Add(Faction.ComStar, overSalvage * ReputationTuning.ComStarSalvagePerPick);
        if (conduct.Fought)
        {
            Add(offer.Opponent.Faction, ReputationTuning.OpponentBase + conduct.EnemyWarriorsSpared * ReputationTuning.OpponentWarriorSpared
                + conduct.EnemyMechsFled * ReputationTuning.OpponentMechFled);
            Add(Faction.ComStar, conduct.EnemyWarriorsSpared * ReputationTuning.ComStarWarriorSpared + conduct.EnemyMechsFled * ReputationTuning.ComStarMechFled);
            Add(Faction.ComStar, excessBuildings * ReputationTuning.ComStarBuilding);
            Add(owner, excessBuildings * ReputationTuning.OwnerBuilding);
        }
        // Raiding for pirates is dishonorable in itself, and the world they hit remembers it.
        if (conduct.Fought && offer.Employer.Kind == PartyKind.Pirates)
        {
            Add(Faction.ComStar, ReputationTuning.ComStarPirateWork);
            Add(owner, ReputationTuning.OwnerPirateWork);
        }
        return changes;
    }

    /// <summary>Applies a finished mission's changes and returns them. Called once per mission at settlement.</summary>
    public IReadOnlyDictionary<Faction, int> ApplyMissionOutcome(MissionOffer offer, MissionConduct conduct, StarChart? chart = null)
    {
        var changes = MissionChanges(offer, conduct, chart);
        foreach (var (faction, tenths) in changes) Adjust(faction, tenths);
        return changes;
    }

    public ReputationSnapshot Snapshot() => new(_tenths.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value));

    public static Reputation Restore(ReputationSnapshot snapshot)
    {
        var limit = ReputationTuning.Limit * ReputationTuning.Scale;
        if (snapshot?.Tenths is not { } tenths || tenths.Count != Enum.GetValues<Faction>().Length
            || tenths.Any(pair => !Enum.TryParse<Faction>(pair.Key, out var faction) || faction.ToString() != pair.Key || Math.Abs(pair.Value) > limit))
            throw new ArgumentException("The reputation save contains invalid state.", nameof(snapshot));
        var reputation = new Reputation();
        foreach (var (name, value) in tenths) reputation._tenths[Enum.Parse<Faction>(name)] = value;
        return reputation;
    }
}
