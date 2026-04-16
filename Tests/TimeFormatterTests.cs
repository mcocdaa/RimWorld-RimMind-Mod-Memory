using RimMind.Memory.Core;
using Xunit;

namespace RimMind.Memory.Tests
{
    public class TimeFormatterTests
    {
        private const int TicksPerHour = 2500;
        private const int TicksPerDay  = 60000;

        [Fact]
        public void FormatTimeAgo_JustNow()
        {
            Assert.Equal("Just now", TimeFormatter.FormatTimeAgo(0, 100));
            Assert.Equal("Just now", TimeFormatter.FormatTimeAgo(0, TicksPerHour - 1));
        }

        [Fact]
        public void FormatTimeAgo_HoursAgo()
        {
            Assert.Equal("About 1h ago", TimeFormatter.FormatTimeAgo(0, TicksPerHour));
            Assert.Equal("About 3h ago", TimeFormatter.FormatTimeAgo(0, 3 * TicksPerHour));
            Assert.Equal("About 5h ago", TimeFormatter.FormatTimeAgo(0, 5 * TicksPerHour));
        }

        [Fact]
        public void FormatTimeAgo_Today()
        {
            Assert.Equal("Today", TimeFormatter.FormatTimeAgo(0, 6 * TicksPerHour));
            Assert.Equal("Today", TimeFormatter.FormatTimeAgo(0, TicksPerDay - 1));
        }

        [Fact]
        public void FormatTimeAgo_DaysAgo()
        {
            Assert.Equal("1 days ago", TimeFormatter.FormatTimeAgo(0, TicksPerDay));
            Assert.Equal("2 days ago", TimeFormatter.FormatTimeAgo(0, 2 * TicksPerDay));
            Assert.Equal("3 days ago", TimeFormatter.FormatTimeAgo(0, 3 * TicksPerDay));
        }

        [Fact]
        public void FormatTimeAgo_GameDate()
        {
            Assert.Equal("Day 5 00:00", TimeFormatter.FormatTimeAgo(0, 4 * TicksPerDay));
            Assert.Equal("Day 11 00:00", TimeFormatter.FormatTimeAgo(0, 10 * TicksPerDay));
        }

        [Fact]
        public void FormatTimeAgo_NegativeDelta_ClampsToZero()
        {
            Assert.Equal("Just now", TimeFormatter.FormatTimeAgo(1000, 0));
        }

        [Fact]
        public void FormatGameDate_Basic()
        {
            Assert.Equal("Day 1 00:00", TimeFormatter.FormatGameDate(0));
            Assert.Equal("Day 1 06:00", TimeFormatter.FormatGameDate(6 * TicksPerHour));
            Assert.Equal("Day 2 00:00", TimeFormatter.FormatGameDate(TicksPerDay));
        }
    }
}
