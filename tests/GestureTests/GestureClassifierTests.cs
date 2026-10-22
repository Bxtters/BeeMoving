using System;
using Xunit;

namespace Handline.Tests
{
    public class GestureClassifierTests
    {
        [Fact]
        public void BothHandsAboveTheNose_HoldsJump()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = classifier.Evaluate(HandsUp(), 0f);
            Assert.True(signal.Tracking);
            Assert.True(signal.JumpHeld);
            Assert.False(signal.DuckHeld);
            Assert.Equal(0, signal.LaneStep);
            Assert.Equal("Jump", signal.Label);
        }

        [Fact]
        public void ArmsAtTheSides_AreReady()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = classifier.Evaluate(Stand(), 0f);
            Assert.True(signal.Tracking);
            Assert.False(signal.JumpHeld);
            Assert.False(signal.DuckHeld);
            Assert.Equal(0, signal.LaneStep);
            Assert.Equal("Ready", signal.Label);
        }

        [Fact]
        public void ClaspedHands_DoNotSlide()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = classifier.Evaluate(Clasped(), 0f);
            Assert.False(signal.DuckHeld);
        }

        [Fact]
        public void HandsApartAndDown_Slide()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = classifier.Evaluate(HandsWideDown(), 0f);
            Assert.True(signal.DuckHeld);
            Assert.False(signal.JumpHeld);
            Assert.Equal("Slide", signal.Label);
        }

        [Fact]
        public void ReachLeft_StepsOnceUntilTheHandReturns()
        {
            GestureClassifier classifier = new GestureClassifier();
            int steps = CountSteps(classifier, ReachLeft(), -1, 0f, 0.8f);
            Assert.Equal(1, steps);

            float time = 1f;
            for (int i = 0; i < 8; i++)
            {
                classifier.Evaluate(Stand(), time);
                time += 0.05f;
            }

            steps = CountSteps(classifier, ReachLeft(), -1, time, time + 0.8f);
            Assert.Equal(1, steps);
        }

        [Fact]
        public void ReachRight_StepsRight()
        {
            GestureClassifier classifier = new GestureClassifier();
            int steps = CountSteps(classifier, ReachRight(), 1, 0f, 0.8f);
            Assert.Equal(1, steps);
        }

        [Fact]
        public void HandLeavingTheFrame_DoesNotMove()
        {
            GestureClassifier classifier = new GestureClassifier();
            int steps = CountSteps(classifier, HandOffTheLeftEdge(), -1, 0f, 0.8f);
            Assert.Equal(0, steps);
        }

        [Fact]
        public void HoldingOneHandOut_DoesNotStepTwice()
        {
            GestureClassifier classifier = new GestureClassifier();
            int steps = CountSteps(classifier, ReachLeft(), -1, 0f, 1.2f);
            Assert.Equal(1, steps);
        }

        [Fact]
        public void AlternatingArms_BuildASprint()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = default(GestureSignal);
            float time = 0f;
            const float step = 1f / 30f;
            while (time < 1.3f)
            {
                signal = classifier.Evaluate(Pump(time), time);
                time += step;
            }

            Assert.True(signal.Tracking);
            Assert.True(signal.Run > 0.6f);
            Assert.Equal("Sprint", signal.Label);
        }

        [Fact]
        public void StillArms_DoNotSprint()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = default(GestureSignal);
            float time = 0f;
            while (time < 1f)
            {
                signal = classifier.Evaluate(Stand(), time);
                time += 1f / 30f;
            }

            Assert.True(signal.Run < 0.05f);
        }

        [Fact]
        public void LowConfidence_AsksThePlayerToStepIn()
        {
            GestureClassifier classifier = new GestureClassifier();
            GestureSignal signal = classifier.Evaluate(Faint(), 0.2f);
            Assert.False(signal.Tracking);
            Assert.False(signal.JumpHeld);
            Assert.Equal("Step into frame", signal.Label);
        }

        static PoseFrame Stand()
        {
            return Build(delegate(Joint[] joints)
            {
                Set(joints, JointId.Nose, 0.50f, 0.22f, 0.95f);
                Set(joints, JointId.LeftEye, 0.47f, 0.20f, 0.9f);
                Set(joints, JointId.RightEye, 0.53f, 0.20f, 0.9f);
                Set(joints, JointId.LeftShoulder, 0.38f, 0.36f, 0.95f);
                Set(joints, JointId.RightShoulder, 0.62f, 0.36f, 0.95f);
                Set(joints, JointId.LeftElbow, 0.34f, 0.50f, 0.8f);
                Set(joints, JointId.RightElbow, 0.66f, 0.50f, 0.8f);
                Set(joints, JointId.LeftWrist, 0.36f, 0.66f, 0.95f);
                Set(joints, JointId.RightWrist, 0.64f, 0.66f, 0.95f);
                Set(joints, JointId.LeftHip, 0.42f, 0.58f, 0.95f);
                Set(joints, JointId.RightHip, 0.58f, 0.58f, 0.95f);
                Set(joints, JointId.LeftKnee, 0.42f, 0.76f, 0.8f);
                Set(joints, JointId.RightKnee, 0.58f, 0.76f, 0.8f);
                Set(joints, JointId.LeftAnkle, 0.42f, 0.94f, 0.7f);
                Set(joints, JointId.RightAnkle, 0.58f, 0.94f, 0.7f);
            });
        }

        static PoseFrame HandsUp()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.40f, 0.08f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.60f, 0.08f, 0.95f);
            return pose;
        }

        static int CountSteps(GestureClassifier classifier, PoseFrame pose, int direction, float start, float end)
        {
            int steps = 0;
            float time = start;
            while (time < end)
            {
                GestureSignal signal = classifier.Evaluate(pose, time);
                if (signal.LaneStep != 0)
                {
                    Assert.Equal(direction, signal.LaneStep);
                    steps++;
                }

                time += 0.05f;
            }

            return steps;
        }

        static PoseFrame Clasped()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.48f, 0.80f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.52f, 0.80f, 0.95f);
            return pose;
        }

        static PoseFrame HandsWideDown()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.18f, 0.82f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.82f, 0.82f, 0.95f);
            return pose;
        }

        static PoseFrame HandOffTheLeftEdge()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.01f, 0.40f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.55f, 0.64f, 0.95f);
            return pose;
        }

        static PoseFrame ReachLeft()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.10f, 0.40f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.55f, 0.64f, 0.95f);
            return pose;
        }

        static PoseFrame ReachRight()
        {
            PoseFrame pose = Stand();
            Set(pose.Joints, JointId.LeftWrist, 0.45f, 0.64f, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.90f, 0.40f, 0.95f);
            return pose;
        }

        static PoseFrame Pump(float time)
        {
            PoseFrame pose = Stand();
            float wave = (float)Math.Sin(time * 2.2 * Math.PI * 2.0);
            Set(pose.Joints, JointId.LeftWrist, 0.36f, 0.55f + 0.12f * wave, 0.95f);
            Set(pose.Joints, JointId.RightWrist, 0.64f, 0.55f - 0.12f * wave, 0.95f);
            return pose;
        }

        static PoseFrame Faint()
        {
            PoseFrame pose = Stand();
            for (int i = 0; i < pose.Joints.Length; i++)
            {
                Joint joint = pose.Joints[i];
                joint.Score = 0.08f;
                pose.Joints[i] = joint;
            }

            return pose;
        }

        static PoseFrame Build(Action<Joint[]> fill)
        {
            PoseFrame pose = new PoseFrame();
            for (int i = 0; i < pose.Joints.Length; i++)
            {
                pose.Joints[i] = new Joint(0.5f, 0.5f, 0.1f);
            }

            fill(pose.Joints);
            return pose;
        }

        static void Set(Joint[] joints, JointId id, float x, float y, float score)
        {
            joints[(int)id] = new Joint(x, y, score);
        }
    }
}
