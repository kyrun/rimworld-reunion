using Kyrun.Reunion;
using Xunit;

namespace Reunion.Tests
{
    public class AllyEventSelectionTests
    {
        private enum TestEvent
        {
            WandererJoins,
            RefugeePodCrash,
            RefugeeChased,
            PrisonerRescue,
            DownedRefugee
        }

        private static readonly TestEvent[] AllEvents =
            (TestEvent[])Enum.GetValues(typeof(TestEvent));

        private static readonly TestEvent[] SoloSafeEvents =
        {
            TestEvent.WandererJoins,
            TestEvent.RefugeePodCrash,
            TestEvent.RefugeeChased
        };

        [Fact]
        public void SoloPlayerGetsOnlyEnabledSoloSafeEventsByDefault()
        {
            var allowed = new HashSet<TestEvent>
            {
                TestEvent.WandererJoins,
                TestEvent.RefugeeChased,
                TestEvent.PrisonerRescue
            };

            List<TestEvent> result = Select(allowed, colonists: 1, harderWhenSolo: false);

            Assert.Equal(
                new[] { TestEvent.WandererJoins, TestEvent.RefugeeChased },
                result);
        }

        [Fact]
        public void SoloPlayerGetsHarderEventsWhenOptionIsEnabled()
        {
            var allowed = new HashSet<TestEvent>(AllEvents);

            List<TestEvent> result = Select(allowed, colonists: 1, harderWhenSolo: true);

            Assert.Equal(AllEvents, result);
        }

        [Fact]
        public void MultipleColonistsCanReceiveAllEnabledEvents()
        {
            var allowed = new HashSet<TestEvent>(AllEvents);

            List<TestEvent> result = Select(allowed, colonists: 2, harderWhenSolo: false);

            Assert.Equal(AllEvents, result);
        }

        [Fact]
        public void HarderEventsRemainEligibleForSoloPlayerWhenNoSoloSafeEventIsEnabled()
        {
            var allowed = new HashSet<TestEvent>
            {
                TestEvent.PrisonerRescue,
                TestEvent.DownedRefugee
            };

            List<TestEvent> result = Select(allowed, colonists: 1, harderWhenSolo: false);

            Assert.Equal(
                new[] { TestEvent.PrisonerRescue, TestEvent.DownedRefugee },
                result);
        }

        [Fact]
        public void DisabledEventsAreNeverIncluded()
        {
            var allowed = new HashSet<TestEvent>
            {
                TestEvent.WandererJoins,
                TestEvent.DownedRefugee
            };

            List<TestEvent> result = Select(allowed, colonists: 3, harderWhenSolo: false);

            Assert.Equal(
                new[] { TestEvent.WandererJoins, TestEvent.DownedRefugee },
                result);
        }

        private static List<TestEvent> Select(
            ISet<TestEvent> allowed,
            int colonists,
            bool harderWhenSolo)
        {
            return AllyEventSelection.GetAllowedEvents(
                AllEvents,
                allowed.Contains,
                SoloSafeEvents,
                colonists,
                harderWhenSolo);
        }
    }
}
