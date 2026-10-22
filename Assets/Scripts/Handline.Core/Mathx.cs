using System;

namespace Handline
{
    /// <summary>
    /// Small numeric helpers with no Unity reference.
    /// Added so the gesture rules and the lane sim can be tested with dotnet test.
    /// </summary>
    public static class Mathx
    {
        /// <summary>Keeps a float inside a range. Adds a hard limit on speed, lanes, and scores.</summary>
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Same limit for whole numbers. Adds lane and index clamping.</summary>
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Clamps to 0-1. Adds a normalized blend weight.</summary>
        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        /// <summary>
        /// Steps current toward target by at most maxDelta.
        /// Added so sprint eases in and out instead of snapping.
        /// A negative step is treated as zero, which adds a guard against time going backwards.
        /// </summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta < 0f) maxDelta = 0f;
            float delta = target - current;
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + (delta > 0f ? maxDelta : -maxDelta);
        }
    }
}
