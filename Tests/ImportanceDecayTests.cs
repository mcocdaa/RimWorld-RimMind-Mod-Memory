using RimMind.Memory.Core;
using Xunit;

namespace RimMind.Memory.Tests
{
    public class ImportanceDecayTests
    {
        [Fact]
        public void Decay_ReducesImportance()
        {
            float result = ImportanceDecayCalculator.Decay(0.5f, 0.02f);
            Assert.Equal(0.49f, result, 3);
        }

        [Fact]
        public void Decay_ZeroRate_NoChange()
        {
            float result = ImportanceDecayCalculator.Decay(0.8f, 0f);
            Assert.Equal(0.8f, result);
        }

        [Fact]
        public void Decay_FullRate_Zero()
        {
            float result = ImportanceDecayCalculator.Decay(0.8f, 1f);
            Assert.Equal(0f, result);
        }

        [Theory]
        [InlineData(0.04f, 0.05f, true)]
        [InlineData(0.05f, 0.05f, false)]
        [InlineData(0.049f, 0.05f, true)]
        public void ShouldRemove_Threshold(float importance, float threshold, bool expected)
        {
            Assert.Equal(expected, ImportanceDecayCalculator.ShouldRemove(importance, threshold));
        }
    }
}
