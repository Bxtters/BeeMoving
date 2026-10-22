using System;
using UnityEngine;
using UnityEngine.UI;

namespace Handline
{
    /// <summary>
    /// Measures a hand on each MoveNet wrist and keeps the 21 points for gestures.
    /// The points are in the same 0-1 space as the body. They are not saved.
    /// </summary>
    public sealed partial class Camera_MoCap
    {
        const string HandModelFile = "hand_landmark_lite.tflite";
        const int MaxHandsPerFrame = 4;
        const float HandHold = 0.35f;

        static readonly int[] FingerBones =
        {
            0, 1, 1, 2, 2, 3, 3, 4,
            0, 5, 5, 6, 6, 7, 7, 8,
            0, 9, 9, 10, 10, 11, 11, 12,
            0, 13, 13, 14, 14, 15, 15, 16,
            0, 17, 17, 18, 18, 19, 19, 20,
            5, 9, 9, 13, 13, 17
        };

        /// <summary>
        /// The stage that is running. Gesture code can read GetHand from here.
        /// </summary>
        public static Camera_MoCap Current { get; private set; }

        HandEngine handsEngine;
        readonly HandPose[,] hands = new HandPose[MaxPeople, 2];
        readonly float[,] handSeen = new float[MaxPeople, 2];
        readonly bool[,] handTouched = new bool[MaxPeople, 2];
        readonly HandPose handScratch = new HandPose();
        readonly int[] jobPerson = new int[MaxHandsPerFrame];
        readonly int[] jobSide = new int[MaxHandsPerFrame];
        readonly float[] jobScore = new float[MaxHandsPerFrame];
        Color32[] handPixels;
        RectTransform fingerRoot;
        Image[][][] fingerLines;
        Image[][][] fingerDots;
        int jobCount;

        struct HandBasis
        {
            public float Cx;
            public float Cy;
            public float RightX;
            public float RightY;
            public float DownX;
            public float DownY;
            public float Box;
        }

        /// <summary>
        /// The measured hand for one person. Pass true for the left wrist.
        /// Points are empty when that hand is not in frame.
        /// </summary>
        public HandPose GetHand(int person, bool left)
        {
            if (person < 0 || person >= MaxPeople) return null;
            return hands[person, left ? 0 : 1];
        }

        /// <summary>
        /// Open, fist, or point for one measured hand. None when the hand is missing.
        /// </summary>
        public HandGesture.Kind GetGesture(int person, bool left)
        {
            return HandGesture.Read(GetHand(person, left));
        }

        /// <summary>
        /// Allocates an empty hand for every wrist.
        /// </summary>
        void InitHands()
        {
            for (int person = 0; person < MaxPeople; person++)
            {
                for (int side = 0; side < 2; side++) hands[person, side] = new HandPose();
            }
        }

        /// <summary>
        /// Drops the hand model. Body tracking does not depend on it.
        /// </summary>
        void ReleaseHands()
        {
            if (handsEngine == null) return;
            handsEngine.Dispose();
            handsEngine = null;
            if (handPixels != null) Array.Clear(handPixels, 0, handPixels.Length);
        }

        /// <summary>
        /// Builds the finger lines on top of the body. They stay hidden until a hand is measured.
        /// </summary>
        void BuildHandGraphics(Sprite sprite)
        {
            if (stage == null) return;
            GameObject root = new GameObject("Fingers", typeof(RectTransform));
            root.transform.SetParent(stage, false);
            fingerRoot = root.GetComponent<RectTransform>();
            Stretch(fingerRoot);
            int bones = FingerBones.Length / 2;
            fingerLines = new Image[MaxPeople][][];
            fingerDots = new Image[MaxPeople][][];
            for (int person = 0; person < MaxPeople; person++)
            {
                Color color = PersonColors[person];
                fingerLines[person] = new Image[2][];
                fingerDots[person] = new Image[2][];
                for (int side = 0; side < 2; side++)
                {
                    fingerLines[person][side] = new Image[bones];
                    for (int i = 0; i < bones; i++)
                    {
                        fingerLines[person][side][i] = MakeImage("Finger", fingerRoot, sprite, color);
                        fingerLines[person][side][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        fingerLines[person][side][i].enabled = false;
                    }

                    fingerDots[person][side] = new Image[HandPose.Count];
                    for (int i = 0; i < HandPose.Count; i++)
                    {
                        fingerDots[person][side][i] = MakeImage("Finger Joint", fingerRoot, sprite, color);
                        fingerDots[person][side][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        fingerDots[person][side][i].enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Measures the strongest wrists in this camera frame.
        /// </summary>
        void ReadHands(Color32[] source, int width, int height)
        {
            if (handsEngine == null || source == null || width <= 16 || height <= 16) return;
            jobCount = 0;
            for (int person = 0; person < peopleLimit && person < MaxPeople; person++)
            {
                PoseFrame pose = stable[person];
                if (!slotUsed[person] || pose == null || pose.Joints == null) continue;
                ConsiderHand(person, 0, WristScore(pose, true));
                ConsiderHand(person, 1, WristScore(pose, false));
            }

            for (int person = 0; person < MaxPeople; person++)
            {
                handTouched[person, 0] = false;
                handTouched[person, 1] = false;
            }

            int size = handsEngine.InputSize;
            if (size < 32) size = 224;
            if (handPixels == null || handPixels.Length != size * size) handPixels = new Color32[size * size];

            for (int job = 0; job < jobCount; job++)
            {
                int person = jobPerson[job];
                int side = jobSide[job];
                PoseFrame pose = stable[person];
                bool left = side == 0;
                Joint wrist = pose.Joints[left ? (int)JointId.LeftWrist : (int)JointId.RightWrist];
                Joint elbow = pose.Joints[left ? (int)JointId.LeftElbow : (int)JointId.RightElbow];
                HandBasis basis;
                if (!SampleHand(source, width, height, size, wrist, elbow, elbow.Score >= DrawScore, out basis))
                {
                    continue;
                }

                handScratch.Clear();
                bool found = false;
                try
                {
                    found = handsEngine.TryRead(handPixels, size, handScratch);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Hand landmarks stopped. " + ex.Message);
                    found = false;
                }
                finally
                {
                    Array.Clear(handPixels, 0, handPixels.Length);
                }

                if (!found)
                {
                    continue;
                }

                KeepHand(person, side, left, basis);
            }

            float now = Time.unscaledTime;
            for (int person = 0; person < MaxPeople; person++)
            {
                for (int side = 0; side < 2; side++)
                {
                    if (handTouched[person, side]) continue;
                    if (handSeen[person, side] > 0f && now - handSeen[person, side] < HandHold) continue;
                    ClearHandSide(person, side);
                }
            }
        }

        /// <summary>
        /// Draws every finger that was measured this session.
        /// </summary>
        void DrawHands(float width, float height)
        {
            if (fingerLines == null || width < 2f || height < 2f) return;
            int bones = FingerBones.Length / 2;
            for (int person = 0; person < MaxPeople; person++)
            {
                for (int side = 0; side < 2; side++)
                {
                    HandPose hand = hands[person, side];
                    bool show = hand != null && hand.Points != null && hand.Points[HandPose.Wrist].Score >= DrawScore;
                    Image[] lines = fingerLines[person][side];
                    for (int i = 0; i < bones; i++)
                    {
                        int aId = FingerBones[i * 2];
                        int bId = FingerBones[i * 2 + 1];
                        if (!show || hand.Points[aId].Score < DrawScore || hand.Points[bId].Score < DrawScore)
                        {
                            lines[i].enabled = false;
                            continue;
                        }

                        Vector2 pa = ToPoint(hand.Points[aId], width, height);
                        Vector2 pb = ToPoint(hand.Points[bId], width, height);
                        Vector2 delta = pb - pa;
                        float length = delta.magnitude;
                        if (length < 1.5f)
                        {
                            lines[i].enabled = false;
                            continue;
                        }

                        RectTransform bone = lines[i].rectTransform;
                        bone.anchoredPosition = (pa + pb) * 0.5f;
                        bone.sizeDelta = new Vector2(length, 4f);
                        bone.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                        lines[i].enabled = true;
                    }

                    Image[] dots = fingerDots[person][side];
                    for (int i = 0; i < HandPose.Count; i++)
                    {
                        if (!show || hand.Points[i].Score < DrawScore)
                        {
                            dots[i].enabled = false;
                            continue;
                        }

                        bool tip = i == HandPose.ThumbTip || i == HandPose.IndexTip || i == HandPose.MiddleTip || i == HandPose.RingTip || i == HandPose.PinkyTip;
                        float mark = tip ? 10f : 7f;
                        RectTransform joint = dots[i].rectTransform;
                        joint.sizeDelta = new Vector2(mark, mark);
                        joint.anchoredPosition = ToPoint(hand.Points[i], width, height);
                        dots[i].enabled = true;
                    }
                }
            }

            if (fingerRoot != null) fingerRoot.SetAsLastSibling();
        }

        /// <summary>
        /// Forgets both hands for one person.
        /// </summary>
        void ClearHand(int person)
        {
            ClearHandSide(person, 0);
            ClearHandSide(person, 1);
        }

        void ConsiderHand(int person, int side, float score)
        {
            if (score < DrawScore) return;
            if (jobCount < MaxHandsPerFrame)
            {
                jobPerson[jobCount] = person;
                jobSide[jobCount] = side;
                jobScore[jobCount] = score;
                jobCount++;
                return;
            }

            int worst = 0;
            for (int i = 1; i < jobCount; i++)
            {
                if (jobScore[i] < jobScore[worst]) worst = i;
            }

            if (score <= jobScore[worst]) return;
            jobPerson[worst] = person;
            jobSide[worst] = side;
            jobScore[worst] = score;
        }

        static float WristScore(PoseFrame pose, bool left)
        {
            int id = left ? (int)JointId.LeftWrist : (int)JointId.RightWrist;
            if (pose.Joints == null || id >= pose.Joints.Length) return 0f;
            return pose.Joints[id].Score;
        }

        /// <summary>
        /// Copies one hand out of the camera into a square whose top points along the fingers.
        /// </summary>
        bool SampleHand(Color32[] source, int srcWidth, int srcHeight, int size, Joint wrist, Joint elbow, bool hasElbow, out HandBasis basis)
        {
            basis = default(HandBasis);
            float dx = hasElbow ? wrist.X - elbow.X : 0f;
            float dy = hasElbow ? wrist.Y - elbow.Y : -0.18f;
            float length = Mathf.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.04f)
            {
                dx = 0f;
                dy = -0.18f;
                length = 0.18f;
            }

            dx /= length;
            dy /= length;
            float box = Mathf.Clamp(length * 2.6f, 0.12f, 0.5f);
            basis.Box = box;
            basis.Cx = wrist.X + (dx * box * 0.22f);
            basis.Cy = wrist.Y + (dy * box * 0.22f);
            basis.DownX = -dx;
            basis.DownY = -dy;
            basis.RightX = basis.DownY;
            basis.RightY = -basis.DownX;

            int rotation = webcam.videoRotationAngle + extraRotation;
            bool vertical = webcam.videoVerticallyMirrored;
            int limit = source.Length;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float nx = basis.Cx + ((u - 0.5f) * box * basis.RightX) + ((v - 0.5f) * box * basis.DownX);
                    float ny = basis.Cy + ((u - 0.5f) * box * basis.RightY) + ((v - 0.5f) * box * basis.DownY);
                    int destX = (int)(nx * HandlineSize);
                    int destY = (int)(ny * HandlineSize);
                    if (destX < 0) destX = 0;
                    if (destY < 0) destY = 0;
                    if (destX >= HandlineSize) destX = HandlineSize - 1;
                    if (destY >= HandlineSize) destY = HandlineSize - 1;
                    int bx;
                    int by;
                    FrameOrient.Map(destX, destY, HandlineSize, srcWidth, srcHeight, rotation, vertical, mirror, out bx, out by);
                    int index = (by * srcWidth) + bx;
                    handPixels[row + x] = (uint)index < (uint)limit ? source[index] : default(Color32);
                }
            }

            return true;
        }

        /// <summary>
        /// Stores the measured fingers in stage space and eases them so a gesture does not flicker.
        /// </summary>
        void KeepHand(int person, int side, bool left, HandBasis basis)
        {
            HandPose dest = hands[person, side];
            dest.Left = left;
            dest.Presence = handScratch.Presence;
            for (int i = 0; i < HandPose.Count; i++)
            {
                HandPoint sample = handScratch.Points[i];
                float nx = basis.Cx + ((sample.X - 0.5f) * basis.Box * basis.RightX) + ((sample.Y - 0.5f) * basis.Box * basis.DownX);
                float ny = basis.Cy + ((sample.X - 0.5f) * basis.Box * basis.RightY) + ((sample.Y - 0.5f) * basis.Box * basis.DownY);
                HandPoint previous = dest.Points[i];
                if (previous.Score >= DrawScore)
                {
                    nx = previous.X + ((nx - previous.X) * 0.65f);
                    ny = previous.Y + ((ny - previous.Y) * 0.65f);
                }

                dest.Points[i] = new HandPoint(nx, ny, sample.Z, sample.Score);
            }

            handSeen[person, side] = Time.unscaledTime;
            handTouched[person, side] = true;
        }

        void ClearHandSide(int person, int side)
        {
            if (person < 0 || person >= MaxPeople) return;
            if (hands[person, side] != null) hands[person, side].Clear();
            handSeen[person, side] = 0f;
        }

        /// <summary>
        /// Maps a hand point into the stage. Y in the pose grows downward.
        /// </summary>
        static Vector2 ToPoint(HandPoint point, float width, float height)
        {
            float x = Mathf.Clamp01(point.X) * width;
            float y = (1f - Mathf.Clamp01(point.Y)) * height;
            return new Vector2(x, y);
        }
    }
}
