using System.Collections.Generic;
using Xunit;

namespace Handline.Tests
{
    public class FrameOrientTests
    {
        [Fact]
        public void UnrotatedTopLeft_LandsOnTheTopRow()
        {
            int x;
            int y;
            FrameOrient.Map(0, 0, 4, 4, 4, 0, false, false, out x, out y);
            Assert.Equal(0, x);
            Assert.Equal(3, y);
        }

        [Fact]
        public void Mirror_SwapsTheLeftEdgeToTheRight()
        {
            int x;
            int y;
            FrameOrient.Map(0, 0, 4, 4, 4, 0, false, true, out x, out y);
            Assert.Equal(3, x);
            Assert.Equal(3, y);
        }

        [Fact]
        public void VerticalMirror_ReadsTheCorrectedTopFromTheBufferBottom()
        {
            int x;
            int y;
            FrameOrient.Map(0, 0, 4, 4, 4, 0, true, false, out x, out y);
            Assert.Equal(0, x);
            Assert.Equal(0, y);
        }

        [Fact]
        public void CenterCrop_UsesTheMiddleOfAWideFrame()
        {
            int x;
            int y;
            FrameOrient.Map(0, 0, 4, 8, 4, 0, false, false, out x, out y);
            Assert.Equal(2, x);
            Assert.Equal(3, y);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(90)]
        [InlineData(180)]
        [InlineData(270)]
        public void Rotation_TouchesEverySourcePixelOnce(int rotation)
        {
            const int n = 8;
            HashSet<int> seen = new HashSet<int>();
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int bx;
                    int by;
                    FrameOrient.Map(x, y, n, n, n, rotation, false, false, out bx, out by);
                    Assert.InRange(bx, 0, n - 1);
                    Assert.InRange(by, 0, n - 1);
                    Assert.True(seen.Add(by * n + bx));
                }
            }

            Assert.Equal(n * n, seen.Count);
        }

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(90, false, true)]
        [InlineData(180, true, false)]
        [InlineData(270, true, true)]
        public void FillIndices_MatchesMap(int rotation, bool vertical, bool mirror)
        {
            const int width = 10;
            const int height = 6;
            const int size = 4;
            int[] table = new int[size * size];
            FrameOrient.FillIndices(table, size, width, height, rotation, vertical, mirror);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int bx;
                    int by;
                    FrameOrient.Map(x, y, size, width, height, rotation, vertical, mirror, out bx, out by);
                    Assert.Equal(by * width + bx, table[y * size + x]);
                }
            }
        }
    }
}
