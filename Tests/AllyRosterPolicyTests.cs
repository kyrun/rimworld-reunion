using Kyrun.Reunion;
using Xunit;

namespace Reunion.Tests
{
    public class AllyRosterPolicyTests
    {
        private static bool IsValid(string? pawn) =>
            pawn != null && pawn != "dead" && pawn != "destroyed";

        [Fact]
        public void AddAvailableAddsValidPawn()
        {
            var available = new List<string>();

            Assert.True(AllyRosterPolicy.AddAvailable(available, "pawn-a", IsValid));
            Assert.Equal(new[] { "pawn-a" }, available);
        }

        [Theory]
        [InlineData("dead")]
        [InlineData("destroyed")]
        [InlineData(null)]
        public void AddAvailableRejectsInvalidPawn(string? pawn)
        {
            var available = new List<string?>();

            Assert.False(AllyRosterPolicy.AddAvailable(available, pawn, IsValid));
            Assert.Empty(available);
        }

        [Fact]
        public void CanAddAvailableChecksEligibilityWithoutMutatingList()
        {
            var available = new List<string>();

            Assert.True(AllyRosterPolicy.CanAddAvailable(available, "pawn-a", IsValid));
            Assert.Empty(available);
        }

        [Fact]
        public void AddAvailableDoesNotAddDuplicatePawn()
        {
            var available = new List<string> { "pawn-a" };

            Assert.False(AllyRosterPolicy.AddAvailable(available, "pawn-a", IsValid));
            Assert.Single(available);
        }

        [Fact]
        public void HasAvailableIgnoresInvalidPawns()
        {
            Assert.False(AllyRosterPolicy.HasAvailable(
                new[] { "dead", "destroyed" }, IsValid));
            Assert.True(AllyRosterPolicy.HasAvailable(
                new[] { "dead", "pawn-a" }, IsValid));
        }

        [Fact]
        public void RemoveInvalidRemovesNullDeadAndDestroyedPawns()
        {
            var available = new List<string?> { "pawn-a", null, "dead", "destroyed", "pawn-b" };

            int removed = AllyRosterPolicy.RemoveInvalid(available, IsValid);

            Assert.Equal(3, removed);
            Assert.Equal(new[] { "pawn-a", "pawn-b" }, available);
        }

        [Fact]
        public void MoveToSpawnedMovesAvailablePawnAndRecordsItsId()
        {
            var available = new List<string> { "pawn-a", "pawn-b" };
            var spawned = new List<string>();

            Assert.True(AllyRosterPolicy.MoveToSpawned(
                available, spawned, "pawn-a", pawn => pawn));

            Assert.Equal(new[] { "pawn-b" }, available);
            Assert.Equal(new[] { "pawn-a" }, spawned);
        }

        [Fact]
        public void MoveToSpawnedRemovesAllDuplicateAvailableEntries()
        {
            var available = new List<string> { "pawn-a", "pawn-b", "pawn-a" };
            var spawned = new List<string>();

            Assert.True(AllyRosterPolicy.MoveToSpawned(
                available, spawned, "pawn-a", pawn => pawn));

            Assert.Equal(new[] { "pawn-b" }, available);
            Assert.Equal(new[] { "pawn-a" }, spawned);
        }

        [Fact]
        public void MoveToSpawnedDoesNotDuplicateExistingSpawnedId()
        {
            var available = new List<string> { "pawn-a" };
            var spawned = new List<string> { "pawn-a" };

            Assert.True(AllyRosterPolicy.MoveToSpawned(
                available, spawned, "pawn-a", pawn => pawn));

            Assert.Empty(available);
            Assert.Equal(new[] { "pawn-a" }, spawned);
        }

        [Fact]
        public void MoveToSpawnedDoesNothingWhenPawnIsNotAvailable()
        {
            var available = new List<string>();
            var spawned = new List<string>();

            Assert.False(AllyRosterPolicy.MoveToSpawned(
                available, spawned, "pawn-a", pawn => pawn));

            Assert.Empty(spawned);
        }

        [Fact]
        public void RemoveSpawnedRemovesReturnedPawnId()
        {
            var spawned = new List<string> { "pawn-a", "pawn-b" };

            Assert.True(AllyRosterPolicy.RemoveSpawned(spawned, "pawn-a", pawn => pawn));
            Assert.Equal(new[] { "pawn-b" }, spawned);
        }

        [Fact]
        public void RemoveSpawnedClearsAllDuplicateIds()
        {
            var spawned = new List<string> { "pawn-a", "pawn-b", "pawn-a" };

            Assert.True(AllyRosterPolicy.RemoveSpawned(spawned, "pawn-a", pawn => pawn));
            Assert.Equal(new[] { "pawn-b" }, spawned);
        }

        [Fact]
        public void ReturningSpawnedPawnRestoresItToAvailableList()
        {
            var available = new List<string>();
            var spawned = new List<string> { "pawn-a" };

            Assert.True(AllyRosterPolicy.RemoveSpawned(spawned, "pawn-a", pawn => pawn));
            Assert.True(AllyRosterPolicy.AddAvailable(available, "pawn-a", IsValid));

            Assert.Equal(new[] { "pawn-a" }, available);
            Assert.Empty(spawned);
        }

        [Fact]
        public void RemoveSpawnedReturnsFalseWhenPawnWasNotTracked()
        {
            var spawned = new List<string> { "pawn-b" };

            Assert.False(AllyRosterPolicy.RemoveSpawned(spawned, "pawn-a", pawn => pawn));
            Assert.Equal(new[] { "pawn-b" }, spawned);
        }
    }
}
