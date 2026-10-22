namespace Handline
{
    /// <summary>
    /// MoveNet's 17 body points, in the order the model publishes them.
    /// Added so every file uses the same joint index. Count is not a point; it is the array length.
    /// Nose through RightEar are facial points. The camera stage erases those before drawing.
    /// </summary>
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

    /// <summary>
    /// One measured point. X and Y are 0-1 in the upright crop, Y growing downward.
    /// Score is the model's confidence. Added so a weak point can be ignored.
    /// </summary>
    public struct Joint
    {
        public float X;
        public float Y;
        public float Score;

        /// <summary>Stores one sample. Added so inference can fill a pose without a builder.</summary>
        public Joint(float x, float y, float score)
        {
            X = x;
            Y = y;
            Score = score;
        }
    }

    /// <summary>
    /// One person's 17 points. Added as the shared pose the camera stage, the overlay, and the lane all read.
    /// </summary>
    public sealed class PoseFrame
    {
        public Joint[] Joints;

        /// <summary>Allocates an empty pose. Added so a missing person still has a slot to clear.</summary>
        public PoseFrame()
        {
            Joints = new Joint[(int)JointId.Count];
        }

        /// <summary>
        /// Returns one joint, or an empty joint when the index is missing.
        /// Added so a short array cannot throw while a pose is still loading.
        /// </summary>
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

    /// <summary>
    /// The lane's reading of a pose: jump, slide, one lane step, and a sprint amount.
    /// Added so the gesture rules can be tested without a camera or a scene.
    /// </summary>
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
