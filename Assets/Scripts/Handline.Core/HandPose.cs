namespace Handline
{
    /// <summary>
    /// MediaPipe hand landmarks. Zero is the wrist. Tips are 4, 8, 12, 16, and 20.
    /// X increases to the right and Y increases downward, both in the same 0-1 space as MoveNet.
    /// </summary>
    public struct HandPoint
    {
        public float X;
        public float Y;
        public float Z;
        public float Score;

        public HandPoint(float x, float y, float z, float score)
        {
            X = x;
            Y = y;
            Z = z;
            Score = score;
        }
    }

    /// <summary>
    /// Twenty-one measured points for one hand. Fingers are not guessed from the wrist.
    /// </summary>
    public sealed class HandPose
    {
        public const int Wrist = 0;
        public const int ThumbCmc = 1;
        public const int ThumbMcp = 2;
        public const int ThumbIp = 3;
        public const int ThumbTip = 4;
        public const int IndexMcp = 5;
        public const int IndexPip = 6;
        public const int IndexDip = 7;
        public const int IndexTip = 8;
        public const int MiddleMcp = 9;
        public const int MiddlePip = 10;
        public const int MiddleDip = 11;
        public const int MiddleTip = 12;
        public const int RingMcp = 13;
        public const int RingPip = 14;
        public const int RingDip = 15;
        public const int RingTip = 16;
        public const int PinkyMcp = 17;
        public const int PinkyPip = 18;
        public const int PinkyDip = 19;
        public const int PinkyTip = 20;
        public const int Count = 21;

        public bool Left;
        public float Presence;
        public HandPoint[] Points;

        public HandPose()
        {
            Points = new HandPoint[Count];
        }

        public void Clear()
        {
            Presence = 0f;
            if (Points == null) return;
            for (int i = 0; i < Points.Length; i++) Points[i] = default(HandPoint);
        }
    }

    /// <summary>
    /// Reads a few poses from a measured hand so a gesture script has a starting point.
    /// Open, fist, and point use the four fingers. The thumb is not required.
    /// </summary>
    public static class HandGesture
    {
        public enum Kind
        {
            None = 0,
            Open = 1,
            Fist = 2,
            Point = 3
        }

        public static Kind Read(HandPose hand)
        {
            if (hand == null || hand.Points == null || hand.Points.Length < HandPose.Count) return Kind.None;
            if (hand.Points[HandPose.Wrist].Score < 0.35f) return Kind.None;

            bool index = Extended(hand, HandPose.IndexTip, HandPose.IndexPip);
            bool middle = Extended(hand, HandPose.MiddleTip, HandPose.MiddlePip);
            bool ring = Extended(hand, HandPose.RingTip, HandPose.RingPip);
            bool pinky = Extended(hand, HandPose.PinkyTip, HandPose.PinkyPip);
            int raised = 0;
            if (index) raised++;
            if (middle) raised++;
            if (ring) raised++;
            if (pinky) raised++;

            if (index && !middle && !ring) return Kind.Point;
            if (raised >= 4) return Kind.Open;
            if (raised == 0) return Kind.Fist;
            return Kind.None;
        }

        /// <summary>
        /// A finger counts as extended when its tip is farther from the wrist than its middle joint.
        /// </summary>
        static bool Extended(HandPose hand, int tip, int pip)
        {
            HandPoint wrist = hand.Points[HandPose.Wrist];
            float tipDistance = Distance(hand.Points[tip], wrist);
            float pipDistance = Distance(hand.Points[pip], wrist);
            return hand.Points[tip].Score >= 0.35f && tipDistance > pipDistance * 1.08f;
        }

        static float Distance(HandPoint a, HandPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)System.Math.Sqrt((dx * dx) + (dy * dy));
        }
    }
}
