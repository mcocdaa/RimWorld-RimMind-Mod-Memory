using System;

namespace RimMind.Memory.Core
{
    public static class ImportanceDecayCalculator
    {
        public static float Decay(float importance, float rate)
        {
            return importance * (1f - rate);
        }

        public static bool ShouldRemove(float importance, float threshold)
        {
            return importance < threshold;
        }
    }
}
