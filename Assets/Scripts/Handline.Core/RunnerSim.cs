using System;
using System.Collections.Generic;

namespace Handline
{
    public enum ObstacleKind
    {
        Crate = 1,
        Beam = 2,
        Blocker = 3,
        Coin = 4
    }

    public sealed class Obstacle
    {
        public int Id;
        public ObstacleKind Kind;
        public int Lane;
        public float Z;
        public bool Alive = true;
    }

    public struct RunnerInput
    {
        public bool JumpHeld;
        public bool DuckHeld;
        public int LaneStep;
        public float Run;
    }

    /// <summary>
    /// Three-lane runner. The player stays at z = 0 and the track comes toward them.
    /// A crate is cleared by jumping, a beam by sliding, a blocker by leaving the lane.
    /// </summary>
    public sealed class RunnerSim
    {
        public const float LaneSpacing = 1.85f;
        public const float JumpVelocity = 8.6f;
        public const float Gravity = 26f;
        public const float CrateClearance = 0.75f;
        public const float BeamClearance = 1.18f;
        public const float HeadStand = 1.68f;
        public const float HeadDuck = 0.90f;
        public const float HurtDuration = 1.15f;
        public const int CellEmpty = 0;
        public const int CellCrate = 1;
        public const int CellBeam = 2;
        public const int CellBlocker = 3;

        public static readonly int[][] Patterns = new int[][]
        {
            new[] { 1, 0, 0 },
            new[] { 0, 1, 0 },
            new[] { 0, 0, 1 },
            new[] { 2, 0, 0 },
            new[] { 0, 2, 0 },
            new[] { 0, 0, 2 },
            new[] { 3, 0, 0 },
            new[] { 0, 3, 0 },
            new[] { 0, 0, 3 },
            new[] { 3, 3, 0 },
            new[] { 3, 0, 3 },
            new[] { 0, 3, 3 },
            new[] { 1, 0, 3 },
            new[] { 3, 1, 0 },
            new[] { 0, 3, 1 },
            new[] { 2, 3, 0 },
            new[] { 0, 2, 3 },
            new[] { 3, 0, 2 },
            new[] { 1, 2, 0 },
            new[] { 0, 1, 2 },
            new[] { 2, 0, 1 }
        };

        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly Random rng;
        int nextId = 1;
        bool jumpArmed = true;

        public bool AutoSpawn { get; set; }
        public int Lives { get; private set; }
        public int Coins { get; private set; }
        public int Score { get; private set; }
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float FeetY { get; private set; }
        public float VelocityY { get; private set; }
        public float LanePosition { get; private set; }
        public float RunAmount { get; private set; }
        public float HurtTimer { get; private set; }
        public int Lane { get; private set; }
        public bool Grounded { get; private set; }
        public bool Ducking { get; private set; }
        public bool GameOver { get; private set; }
        public IReadOnlyList<Obstacle> Obstacles { get { return obstacles; } }

        public RunnerSim(int seed = 1, bool autoSpawn = true)
        {
            rng = new Random(seed);
            AutoSpawn = autoSpawn;
            Reset();
        }

        public void Reset()
        {
            obstacles.Clear();
            Lives = 3;
            Coins = 0;
            Score = 0;
            Distance = 0f;
            Speed = 0f;
            FeetY = 0f;
            VelocityY = 0f;
            LanePosition = 0f;
            Lane = 0;
            RunAmount = 0f;
            HurtTimer = 0f;
            Grounded = true;
            Ducking = false;
            GameOver = false;
            jumpArmed = true;
            // Ids keep climbing so a restarted run cannot reuse a despawned prop.
            if (AutoSpawn)
            {
                SpawnGate(34f);
            }
        }

        public Obstacle Spawn(ObstacleKind kind, int lane, float z)
        {
            Obstacle obstacle = new Obstacle();
            obstacle.Id = nextId++;
            obstacle.Kind = kind;
            obstacle.Lane = Mathx.Clamp(lane, -1, 1);
            obstacle.Z = z;
            obstacles.Add(obstacle);
            return obstacle;
        }

        public void Tick(float dt, RunnerInput input)
        {
            if (dt < 0f) dt = 0f;
            if (dt > 0.05f) dt = 0.05f;
            if (HurtTimer > 0f) HurtTimer -= dt;

            if (!GameOver)
            {
                if (input.LaneStep != 0)
                {
                    int dir = input.LaneStep > 0 ? 1 : -1;
                    Lane = Mathx.Clamp(Lane + dir, -1, 1);
                }

                if (!input.JumpHeld)
                {
                    jumpArmed = true;
                }

                if (jumpArmed && input.JumpHeld && Grounded)
                {
                    VelocityY = JumpVelocity;
                    jumpArmed = false;
                    Grounded = false;
                }

                VelocityY -= Gravity * dt;
                FeetY += VelocityY * dt;
                if (FeetY <= 0f)
                {
                    FeetY = 0f;
                    if (VelocityY < 0f) VelocityY = 0f;
                    Grounded = true;
                }
                else
                {
                    Grounded = false;
                }

                Ducking = input.DuckHeld && !input.JumpHeld && Grounded;
                RunAmount = Mathx.Clamp01(input.Run);
                LanePosition = Mathx.MoveTowards(LanePosition, Lane, dt * 7f);

                float cruise = 9f + Distance * 0.025f;
                if (cruise > 18f) cruise = 18f;
                Speed = cruise * (0.75f + 0.50f * RunAmount);

                for (int i = 0; i < obstacles.Count; i++)
                {
                    obstacles[i].Z -= Speed * dt;
                }

                Distance += Speed * dt;
                Collide();
                if (AutoSpawn)
                {
                    KeepGatesAhead();
                }
            }
            else
            {
                Speed = 0f;
                RunAmount = 0f;
                Ducking = false;
            }

            Cleanup();
            Score = (int)Math.Floor(Distance) + Coins * 25;
        }

        void Collide()
        {
            float head = FeetY + (Ducking ? HeadDuck : HeadStand);
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle obstacle = obstacles[i];
                if (!obstacle.Alive) continue;
                if (obstacle.Z > 0.85f || obstacle.Z < -0.60f) continue;
                if (Math.Abs(LanePosition - obstacle.Lane) > 0.45f) continue;

                if (obstacle.Kind == ObstacleKind.Coin)
                {
                    obstacle.Alive = false;
                    Coins += 1;
                    continue;
                }

                bool hit = false;
                if (obstacle.Kind == ObstacleKind.Crate) hit = FeetY < CrateClearance;
                else if (obstacle.Kind == ObstacleKind.Beam) hit = head > BeamClearance;
                else hit = true;

                if (hit)
                {
                    obstacle.Alive = false;
                    Hurt();
                }
            }
        }

        void Hurt()
        {
            if (HurtTimer > 0f || GameOver) return;
            Lives -= 1;
            HurtTimer = HurtDuration;
            if (Lives <= 0)
            {
                Lives = 0;
                GameOver = true;
                Speed = 0f;
            }
        }

        void KeepGatesAhead()
        {
            float maxZ = 0f;
            bool any = false;
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (!obstacles[i].Alive) continue;
                if (!any || obstacles[i].Z > maxZ)
                {
                    maxZ = obstacles[i].Z;
                    any = true;
                }
            }

            if (!any || maxZ < 40f)
            {
                float gap = 22f - Distance * 0.012f;
                if (gap < 14f) gap = 14f;
                float z = any ? maxZ + gap : 34f;
                if (z < 24f) z = 24f;
                SpawnGate(z);
            }
        }

        void SpawnGate(float z)
        {
            int[] pattern = Patterns[rng.Next(Patterns.Length)];
            int emptyLane = 99;
            for (int i = 0; i < 3; i++)
            {
                int lane = i - 1;
                int cell = pattern[i];
                if (cell == CellCrate) Spawn(ObstacleKind.Crate, lane, z);
                else if (cell == CellBeam) Spawn(ObstacleKind.Beam, lane, z);
                else if (cell == CellBlocker) Spawn(ObstacleKind.Blocker, lane, z);
                else emptyLane = lane;
            }

            if (emptyLane != 99 && rng.NextDouble() < 0.55)
            {
                Spawn(ObstacleKind.Coin, emptyLane, z);
            }
        }

        void Cleanup()
        {
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                Obstacle obstacle = obstacles[i];
                if (!obstacle.Alive || obstacle.Z < -8f)
                {
                    obstacles.RemoveAt(i);
                }
            }
        }
    }
}
