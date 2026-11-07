using Kyrun.Reunion;
using Xunit;

namespace Reunion.Tests
{
    public class EventSchedulePolicyTests
    {
        [Fact]
        public void ActiveEventBlocksNormalScheduling()
        {
            Assert.False(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: -1,
                currentTick: 1000,
                forced: false,
                settingsUpdated: false));
        }

        [Fact]
        public void SettingsUpdateDoesNotInterruptAnActiveEvent()
        {
            Assert.False(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: -1,
                currentTick: 1000,
                forced: false,
                settingsUpdated: true));
        }

        [Fact]
        public void ForcedSchedulingCanOverrideAnActiveEventGuard()
        {
            Assert.True(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: -1,
                currentTick: 1000,
                forced: true,
                settingsUpdated: false));
        }

        [Fact]
        public void ExistingFutureScheduleIsPreserved()
        {
            Assert.False(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: 1500,
                currentTick: 1000,
                forced: false,
                settingsUpdated: false));
        }

        [Fact]
        public void SettingsUpdateCanReplaceAnExistingFutureSchedule()
        {
            Assert.True(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: 1500,
                currentTick: 1000,
                forced: false,
                settingsUpdated: true));
        }

        [Fact]
        public void ForcedSchedulingCanReplaceAnExistingFutureSchedule()
        {
            Assert.True(EventSchedulePolicy.ShouldSchedule(
                nextEventTick: 1500,
                currentTick: 1000,
                forced: true,
                settingsUpdated: false));
        }

        [Theory]
        [InlineData(0, 1000)]
        [InlineData(1000, 1000)]
        [InlineData(900, 1000)]
        public void MissingDueOrPastScheduleCanBeReplaced(int nextEventTick, int currentTick)
        {
            Assert.True(EventSchedulePolicy.ShouldSchedule(
                nextEventTick,
                currentTick,
                forced: false,
                settingsUpdated: false));
        }
    }
}
