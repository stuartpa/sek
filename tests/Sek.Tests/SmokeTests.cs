using Xunit;

namespace Sek.Tests;

/// <summary>Confirms the test harness and project references build and run.</summary>
public class SmokeTests
{
    [Fact]
    public void Harness_Runs()
    {
        Assert.Equal(4, 2 + 2);
    }
}
