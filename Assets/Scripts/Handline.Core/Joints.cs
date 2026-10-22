namespace Handline
{
    public enum JointId
    {
        Nose = 0,
        LeftEye = 1,
        RightEye = 2,
        LeftEar = 3,
        RightEar = 4,
        LeftShoulder = 5,
        RightShoulder = 6,
        LeftElbow = 7,
        RightElbow = 8,
        LeftWrist = 9,
        RightWrist = 10,
        LeftHip = 11,
        RightHip = 12,
        LeftKnee = 13,
        RightKnee = 14,
        LeftAnkle = 15,
        RightAnkle = 16,
        Count = 17
    }

    public struct Joint
    {
        public float X;
        public float Y;
        public float Score;

        public Joint(float x, float y, float score)
        {
            X = x;
            Y = y;
            Score = score;
        }
    }

    /// <summary>
    /// One MoveNet pose. X increases to the right, Y increases downward, both 0-1.
    /// </summary>
    public sealed class PoseFrame
    {
        public Joint[] Joints;

        public PoseFrame()
        {
            Joints = new Joint[(int)JointId.Count];
        }

        public Joint Get(JointId id)
        {
            int index = (int)id;
            if (Joints == null || index < 0 || index >= Joints.Length)
            {
                return default(Joint);
            }

            return Joints[index];
        }
    }

    public struct GestureSignal
    {
        public bool Tracking;
        public bool JumpHeld;
        public bool DuckHeld;
        public int LaneStep;
        public float Run;
        public float Confidence;
        public string Label;
    }
}
