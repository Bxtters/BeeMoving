using System;

namespace Handline
{
    /// <summary>
    /// Turns MoveNet wrists into game actions.
    ///
    /// Both hands above the nose: jump.
    /// Both hands down and apart: slide.
    /// One hand out to the side: one lane step, after it stays there briefly.
    /// Arms pumping in opposite directions: sprint.
    ///
    /// Reach uses the preview after the selfie mirror, so the hand on the
    /// left side of the picture moves the runner left.
    /// </summary>
    public sealed class GestureClassifier
    {
        public const float MinScore = 0.35f;
        public const float JumpClearance = 0.02f;
        public const float DuckDrop = 0.12f;
        public const float ApartRatio = 1.6f;
        public const float ReachPastShoulder = 0.45f;
        public const float RunWindow = 0.75f;
        public const float RunAmplitude = 0.16f;
        public const float LaneCooldown = 0.45f;
        public const float HoldToStep = 0.16f;
        public const float FrameEdge = 0.05f;

        struct Sample
        {
            public float Time;
            public float Diff;
        }

        readonly Sample[] samples = new Sample[48];
        int head;
        int count;
        float lastTime = -1f;
        float run;
        float laneLock;
        float leftHold;
        float rightHold;
        bool leftArmed = true;
        bool rightArmed = true;

        public void Reset()
        {
            head = 0;
            count = 0;
            lastTime = -1f;
            run = 0f;
            laneLock = 0f;
            leftHold = 0f;
            rightHold = 0f;
            leftArmed = true;
            rightArmed = true;
        }

        public GestureSignal Evaluate(PoseFrame pose, float time)
        {
            float dt = 1f / 30f;
            if (lastTime >= 0f)
            {
                dt = time - lastTime;
            }

            if (dt < 0f || dt > 0.5f)
            {
                count = 0;
                dt = 0.033f;
            }
            else if (dt < 0.001f)
            {
                dt = 0.001f;
            }

            lastTime = time;
            if (laneLock > 0f)
            {
                laneLock -= dt;
            }

            GestureSignal signal = default(GestureSignal);
            signal.Label = "Step into frame";

            if (pose == null || pose.Joints == null || pose.Joints.Length < (int)JointId.Count)
            {
                run = Mathx.MoveTowards(run, 0f, dt * 1.6f);
                signal.Run = run;
                return signal;
            }

            Joint nose = pose.Get(JointId.Nose);
            Joint ls = pose.Get(JointId.LeftShoulder);
            Joint rs = pose.Get(JointId.RightShoulder);
            Joint lw = pose.Get(JointId.LeftWrist);
            Joint rw = pose.Get(JointId.RightWrist);
            Joint lh = pose.Get(JointId.LeftHip);
            Joint rh = pose.Get(JointId.RightHip);

            float confidence = (nose.Score + ls.Score + rs.Score + lw.Score + rw.Score + lh.Score + rh.Score) / 7f;
            signal.Confidence = confidence;

            int visible = 0;
            if (Visible(nose)) visible++;
            if (Visible(ls)) visible++;
            if (Visible(rs)) visible++;
            if (Visible(lw)) visible++;
            if (Visible(rw)) visible++;
            if (Visible(lh)) visible++;
            if (Visible(rh)) visible++;

            float shoulderWidth = Math.Abs(ls.X - rs.X);
            float torso = ((lh.Y + rh.Y) * 0.5f) - ((ls.Y + rs.Y) * 0.5f);
            bool tracking = visible >= 5 && confidence >= 0.3f && shoulderWidth > 0.05f && torso > 0.06f;
            signal.Tracking = tracking;
            if (!tracking)
            {
                run = Mathx.MoveTowards(run, 0f, dt * 1.6f);
                signal.Run = run;
                leftHold = 0f;
                rightHold = 0f;
                leftArmed = true;
                rightArmed = true;
                return signal;
            }

            float hipY = (lh.Y + rh.Y) * 0.5f;
            bool leftHand = InFrame(lw);
            bool rightHand = InFrame(rw);
            bool shoulders = InFrame(ls) && InFrame(rs);
            bool noseSeen = InFrame(nose);

            bool jump = leftHand && rightHand && noseSeen
                && lw.Y < nose.Y - JumpClearance
                && rw.Y < nose.Y - JumpClearance;
            float handGap = Math.Abs(lw.X - rw.X);
            bool duck = leftHand && rightHand && !jump
                && lw.Y > hipY + DuckDrop
                && rw.Y > hipY + DuckDrop
                && handGap > shoulderWidth * ApartRatio;

            float past = shoulderWidth * ReachPastShoulder;
            bool reachLeft = shoulders && !jump && !duck && (
                (leftHand && lw.X < ls.X - past) || (rightHand && rw.X < ls.X - past));
            bool reachRight = shoulders && !jump && !duck && (
                (leftHand && lw.X > rs.X + past) || (rightHand && rw.X > rs.X + past));
            if (reachLeft && reachRight)
            {
                reachLeft = false;
                reachRight = false;
            }

            if (reachLeft) leftHold += dt;
            else leftHold = 0f;
            if (reachRight) rightHold += dt;
            else rightHold = 0f;
            if (!reachLeft) leftArmed = true;
            if (!reachRight) rightArmed = true;

            int laneStep = 0;
            if (reachLeft && leftArmed && leftHold >= HoldToStep && laneLock <= 0f)
            {
                laneStep = -1;
                leftArmed = false;
                laneLock = LaneCooldown;
            }
            else if (reachRight && rightArmed && rightHold >= HoldToStep && laneLock <= 0f)
            {
                laneStep = 1;
                rightArmed = false;
                laneLock = LaneCooldown;
            }

            if (leftHand && rightHand)
            {
                Push(time, lw.Y - rw.Y);
            }

            float amplitude;
            int crossings;
            Measure(time, out amplitude, out crossings);
            float target = 0f;
            if (amplitude >= RunAmplitude && crossings >= 2)
            {
                target = Mathx.Clamp01((amplitude - RunAmplitude) / 0.20f);
            }

            if (duck)
            {
                target = 0f;
            }

            float rate = target > run ? 3.4f : 1.8f;
            run = Mathx.MoveTowards(run, target, rate * dt);

            signal.JumpHeld = jump;
            signal.DuckHeld = duck;
            signal.LaneStep = laneStep;
            signal.Run = run;
            signal.Label = LabelFor(jump, duck, laneStep, reachLeft, reachRight, run);
            return signal;
        }

        static string LabelFor(bool jump, bool duck, int laneStep, bool reachLeft, bool reachRight, float run)
        {
            if (jump) return "Jump";
            if (duck) return "Slide";
            if (laneStep < 0 || reachLeft) return "Left";
            if (laneStep > 0 || reachRight) return "Right";
            if (run > 0.55f) return "Sprint";
            if (run > 0.2f) return "Run";
            return "Ready";
        }

        void Push(float time, float diff)
        {
            samples[head].Time = time;
            samples[head].Diff = diff;
            head = (head + 1) % samples.Length;
            if (count < samples.Length) count++;
        }

        void Measure(float time, out float amplitude, out int crossings)
        {
            amplitude = 0f;
            crossings = 0;
            if (count < 3) return;

            float min = float.MaxValue;
            float max = float.MinValue;
            bool havePrev = false;
            float prev = 0f;
            float cutoff = time - RunWindow;
            int start = head - count;
            if (start < 0) start += samples.Length;

            for (int i = 0; i < count; i++)
            {
                Sample sample = samples[(start + i) % samples.Length];
                if (sample.Time < cutoff) continue;
                if (sample.Diff < min) min = sample.Diff;
                if (sample.Diff > max) max = sample.Diff;
                if (havePrev)
                {
                    bool cross = (prev < 0f && sample.Diff >= 0f) || (prev > 0f && sample.Diff <= 0f);
                    if (cross && Math.Abs(sample.Diff - prev) > 0.02f)
                    {
                        crossings++;
                    }
                }

                prev = sample.Diff;
                havePrev = true;
            }

            if (min <= max)
            {
                amplitude = max - min;
            }
        }

        static bool Visible(Joint joint)
        {
            return joint.Score >= MinScore;
        }

        static bool InFrame(Joint joint)
        {
            return joint.Score >= MinScore
                && joint.X > FrameEdge
                && joint.X < 1f - FrameEdge
                && joint.Y > FrameEdge
                && joint.Y < 1f - FrameEdge;
        }
    }
}
