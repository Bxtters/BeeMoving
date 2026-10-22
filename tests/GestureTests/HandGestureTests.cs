using Xunit;

namespace Handline.Tests
{
    public class HandGestureTests
    {
        [Fact]
        public void OpenPalm_IsOpen()
        {
            HandPose hand = HandAtWrist(0.5f, 0.8f);
            Raise(hand, HandPose.IndexPip, HandPose.IndexTip, 0.55f, 0.32f);
            Raise(hand, HandPose.MiddlePip, HandPose.MiddleTip, 0.55f, 0.30f);
            Raise(hand, HandPose.RingPip, HandPose.RingTip, 0.55f, 0.34f);
            Raise(hand, HandPose.PinkyPip, HandPose.PinkyTip, 0.55f, 0.40f);
            Assert.Equal(HandGesture.Kind.Open, HandGesture.Read(hand));
        }

        [Fact]
        public void CurledFingers_AreAFist()
        {
            HandPose hand = HandAtWrist(0.5f, 0.8f);
            Curl(hand, HandPose.IndexPip, HandPose.IndexTip);
            Curl(hand, HandPose.MiddlePip, HandPose.MiddleTip);
            Curl(hand, HandPose.RingPip, HandPose.RingTip);
            Curl(hand, HandPose.PinkyPip, HandPose.PinkyTip);
            Assert.Equal(HandGesture.Kind.Fist, HandGesture.Read(hand));
        }

        [Fact]
        public void IndexAlone_IsPoint()
        {
            HandPose hand = HandAtWrist(0.5f, 0.8f);
            Raise(hand, HandPose.IndexPip, HandPose.IndexTip, 0.55f, 0.30f);
            Curl(hand, HandPose.MiddlePip, HandPose.MiddleTip);
            Curl(hand, HandPose.RingPip, HandPose.RingTip);
            Curl(hand, HandPose.PinkyPip, HandPose.PinkyTip);
            Assert.Equal(HandGesture.Kind.Point, HandGesture.Read(hand));
        }

        [Fact]
        public void MissingHand_IsNone()
        {
            Assert.Equal(HandGesture.Kind.None, HandGesture.Read(null));
        }

        static HandPose HandAtWrist(float x, float y)
        {
            HandPose hand = new HandPose();
            for (int i = 0; i < HandPose.Count; i++)
            {
                hand.Points[i] = new HandPoint(x, y, 0f, 1f);
            }

            return hand;
        }

        static void Raise(HandPose hand, int pip, int tip, float pipY, float tipY)
        {
            hand.Points[pip] = new HandPoint(0.5f, pipY, 0f, 1f);
            hand.Points[tip] = new HandPoint(0.5f, tipY, 0f, 1f);
        }

        static void Curl(HandPose hand, int pip, int tip)
        {
            hand.Points[pip] = new HandPoint(0.5f, 0.62f, 0f, 1f);
            hand.Points[tip] = new HandPoint(0.5f, 0.7f, 0f, 1f);
        }
    }
}
