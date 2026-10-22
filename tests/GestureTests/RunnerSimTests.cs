using Xunit;

namespace Handline.Tests
{
    /// <summary>
    /// Checks crates, beams, blockers, and coins with no scene.
    /// Added so the lane rules stay testable from dotnet test.
    /// </summary>
    public class RunnerSimTests
    {
        [Fact]
        public void Crate_HitsAStandingRunner()
        {
            RunnerSim sim = new RunnerSim(autoSpawn: false);
            sim.Spawn(ObstacleKind.Crate, 0, 2.2f);
            Run(sim, 1.2f, default(RunnerInput));
            Assert.Equal(2, sim.Lives);
        }

        [Fact]
        public void Crate_MissesAJump()
        {
            RunnerSim sim = new RunnerSim(autoSpawn: false);
            sim.Spawn(ObstacleKind.Crate, 0, 2.2f);
            RunnerInput input = default(RunnerInput);
            input.JumpHeld = true;
            Run(sim, 1.2f, input);
            Assert.Equal(3, sim.Lives);
            Assert.False(sim.GameOver);
        }

        [Fact]
        public void Beam_HitsAStandingRunner_AndMissesASlide()
        {
            RunnerSim standing = new RunnerSim(autoSpawn: false);
            standing.Spawn(ObstacleKind.Beam, 0, 2.2f);
            Run(standing, 1.2f, default(RunnerInput));
            Assert.Equal(2, standing.Lives);

            RunnerSim sliding = new RunnerSim(autoSpawn: false);
            sliding.Spawn(ObstacleKind.Beam, 0, 2.2f);
            RunnerInput input = default(RunnerInput);
            input.DuckHeld = true;
            Run(sliding, 1.2f, input);
            Assert.Equal(3, sliding.Lives);
            Assert.True(sliding.Ducking);
        }

        [Fact]
        public void Blocker_HitsTheSameLane_AndMissesAfterAStep()
        {
            RunnerSim hit = new RunnerSim(autoSpawn: false);
            hit.Spawn(ObstacleKind.Blocker, 0, 4f);
            Run(hit, 1.4f, default(RunnerInput));
            Assert.Equal(2, hit.Lives);

            RunnerSim dodge = new RunnerSim(autoSpawn: false);
            dodge.Spawn(ObstacleKind.Blocker, 0, 4f);
            RunnerInput input = default(RunnerInput);
            input.LaneStep = -1;
            dodge.Tick(0.02f, input);
            input.LaneStep = 0;
            Run(dodge, 1.4f, input);
            Assert.Equal(3, dodge.Lives);
            Assert.Equal(-1, dodge.Lane);
        }

        [Fact]
        public void Coin_AddsScoreWithoutAHit()
        {
            RunnerSim sim = new RunnerSim(autoSpawn: false);
            sim.Spawn(ObstacleKind.Coin, 0, 2.2f);
            Run(sim, 1.2f, default(RunnerInput));
            Assert.Equal(1, sim.Coins);
            Assert.Equal(3, sim.Lives);
            Assert.True(sim.Score >= 25);
        }

        [Fact]
        public void ThreeHits_EndTheRun()
        {
            RunnerSim sim = new RunnerSim(autoSpawn: false);
            for (int i = 0; i < 3; i++)
            {
                sim.Spawn(ObstacleKind.Crate, 0, 2.2f);
                Run(sim, 2.2f, default(RunnerInput));
            }

            Assert.Equal(0, sim.Lives);
            Assert.True(sim.GameOver);
        }

        [Fact]
        public void Sprinting_CoversMoreGround()
        {
            RunnerSim walk = new RunnerSim(autoSpawn: false);
            RunnerSim sprint = new RunnerSim(autoSpawn: false);
            RunnerInput fast = default(RunnerInput);
            fast.Run = 1f;
            Run(walk, 2f, default(RunnerInput));
            Run(sprint, 2f, fast);
            Assert.True(sprint.Distance > walk.Distance * 1.4f);
        }

        [Fact]
        public void Reset_KeepsObstacleIdsUnique()
        {
            RunnerSim sim = new RunnerSim(autoSpawn: true);
            Assert.NotEmpty(sim.Obstacles);
            int first = sim.Obstacles[0].Id;
            sim.Reset();
            Assert.NotEmpty(sim.Obstacles);
            Assert.NotEqual(first, sim.Obstacles[0].Id);
        }

        [Fact]
        public void Patterns_AlwaysLeaveAWayThrough()
        {
            Assert.NotEmpty(RunnerSim.Patterns);
            for (int p = 0; p < RunnerSim.Patterns.Length; p++)
            {
                int[] pattern = RunnerSim.Patterns[p];
                Assert.Equal(3, pattern.Length);
                int survivable = 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    Assert.InRange(pattern[lane], 0, 3);
                    if (pattern[lane] != RunnerSim.CellBlocker) survivable++;
                }

                Assert.True(survivable >= 1);
            }
        }

        static void Run(RunnerSim sim, float seconds, RunnerInput input)
        {
            float time = 0f;
            while (time < seconds)
            {
                sim.Tick(0.02f, input);
                time += 0.02f;
            }
        }
    }
}
