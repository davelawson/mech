using Xunit;

namespace Mech.Tests.Unit;

/// <summary>Measures how often each skill use happens in simulated battles, to set the skill gain lottery's weights. Explicit: it
/// takes about ten minutes, so it runs only when asked for (see docs/combat-tuning.md).</summary>
public sealed class SkillUseRates
{
    [Fact(Explicit = true)]
    public void Measure() => TestContext.Current.TestOutputHelper!.WriteLine(SkillUseMeasure.Run(30));
}
