using System;

namespace Handline
{
    public static class Mathx
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (maxDelta < 0f) maxDelta = 0f;
            float delta = target - current;
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + (delta > 0f ? maxDelta : -maxDelta);
        }
    }
}
