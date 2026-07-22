using OpsManager.Service.Abstractions;

namespace OpsManager.Service.Tests;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_a_utc_timestamp_close_to_system_time()
    {
        SystemClock clock = new();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        DateTimeOffset actual = clock.UtcNow;

        Assert.InRange(actual, before, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }
}
