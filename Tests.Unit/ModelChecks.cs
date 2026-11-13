using Mech.Tests;
using Xunit;

namespace Mech.Tests.Unit;

/// <summary>Runs an engine-free check suite shared with the Godot test scene, reporting every failed check rather than the first.</summary>
internal static class Suite
{
    public static void Run(Action<Action<bool, string>> suite)
    {
        var failures = new List<string>();
        var passed = 0;
        suite((condition, message) => { if (condition) passed++; else failures.Add(message); });
        Assert.True(failures.Count == 0, $"{failures.Count} of {passed + failures.Count} checks failed:\n  {string.Join("\n  ", failures)}");
    }
}

// One class per suite: xUnit runs classes in parallel, so the long combat and terrain simulations overlap.
public sealed class Catalog { [Fact] public void Checks() => Suite.Run(CatalogChecks.Run); }
public sealed class Campaign { [Fact] public void Checks() => Suite.Run(CampaignChecks.Run); }
public sealed class Missions { [Fact] public void Checks() => Suite.Run(MissionChecks.Run); }
public sealed class Reputation { [Fact] public void Checks() => Suite.Run(ReputationChecks.Run); }
public sealed class Roster { [Fact] public void Checks() => Suite.Run(RosterChecks.Run); }
public sealed class Travel { [Fact] public void Checks() => Suite.Run(TravelChecks.Run); }
public sealed class Hangar { [Fact] public void Checks() => Suite.Run(HangarChecks.Run); }
// The longest suites are split into parts, one class each, so they overlap too.
public sealed class NetworkSessions { [Fact] public void Checks() => Suite.Run(NetworkChecks.RunSessions); }
public sealed class NetworkReplica { [Fact] public void Checks() => Suite.Run(NetworkChecks.RunReplica); }
public sealed class Combat { [Fact] public void Checks() => Suite.Run(CombatChecks.Run); }
public sealed class Patrol { [Fact] public void Checks() => Suite.Run(PatrolChecks.Run); }
public sealed class TerrainGeneration { [Fact] public void Checks() => Suite.Run(TerrainChecks.RunGeneration); }
public sealed class TerrainFeatures { [Fact] public void Checks() => Suite.Run(TerrainChecks.RunFeatures); }
public sealed class TerrainEngagements { [Fact] public void Checks() => Suite.Run(TerrainChecks.RunEngagements); }
public sealed class TerrainLanceEngagement { [Fact] public void Checks() => Suite.Run(TerrainChecks.RunLanceEngagement); }
