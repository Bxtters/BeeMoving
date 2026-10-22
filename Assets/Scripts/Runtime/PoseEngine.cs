using System;
using System.Reflection;
using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Talks to MoveNet if the TensorFlow Lite package imported.
    /// The camera stage still opens when that package is missing and shows the reason on the status line.
    /// </summary>
    public sealed class PoseEngine : IDisposable
    {
        readonly object inner;
        readonly MethodInfo tryInfer;
        readonly MethodInfo tryPeople;
        readonly PropertyInfo summary;
        readonly PropertyInfo failure;
        readonly PropertyInfo inputSize;
        readonly object[] peopleArgs = new object[4];
        readonly object[] oneArgs = new object[3];

        /// <summary>One-line description of the loaded model. Empty when the property is missing.</summary>
        public string Summary
        {
            get { return summary == null ? "" : (string)summary.GetValue(inner, null); }
        }

        /// <summary>The last inference error. Empty when the model is running.</summary>
        public string Failure
        {
            get { return failure == null ? "" : (string)failure.GetValue(inner, null); }
        }

        /// <summary>
        /// Caches the reflected methods once. Added so each frame does not search the type again.
        /// peopleArgs and oneArgs are reused so inference does not allocate a new array every frame.
        /// </summary>
        PoseEngine(object inner, Type type)
        {
            this.inner = inner;
            tryInfer = type.GetMethod("TryInfer");
            tryPeople = type.GetMethod("TryInferPeople");
            summary = type.GetProperty("Summary");
            failure = type.GetProperty("Failure");
            inputSize = type.GetProperty("InputSize");
        }

        /// <summary>
        /// Model edge length in pixels. The stage crops to this so it does not scale the frame twice.
        /// </summary>
        public int InputSize
        {
            get
            {
                if (inputSize == null || inner == null) return 0;
                object value = inputSize.GetValue(inner, null);
                return value is int ? (int)value : 0;
            }
        }

        /// <summary>
        /// Builds an engine from the model bytes. Returns null when TensorFlow Lite is not imported yet.
        /// Added so the stage can keep running and show a status line instead of a missing-type crash.
        /// </summary>
        public static PoseEngine Create(byte[] model)
        {
            Type type = Type.GetType("Handline.MoveNetEstimator, Handline.Vision");
            if (type == null) return null;
            object inner = Activator.CreateInstance(type, model);
            return new PoseEngine(inner, type);
        }

        /// <summary>Runs one person. Added for the single-pose Lightning file.</summary>
        public bool TryInfer(Color32[] upright, int size, PoseFrame pose)
        {
            if (tryInfer == null || inner == null) return false;
            oneArgs[0] = upright;
            oneArgs[1] = size;
            oneArgs[2] = pose;
            return (bool)tryInfer.Invoke(inner, oneArgs);
        }

        /// <summary>
        /// Runs up to maxPeople. Falls back to one person when the model has no multi-person method.
        /// Added so the same call site works for Lightning and MultiPose.
        /// </summary>
        public int TryRead(Color32[] upright, int size, PoseFrame[] poses, int maxPeople)
        {
            if (tryPeople != null && inner != null)
            {
                peopleArgs[0] = upright;
                peopleArgs[1] = size;
                peopleArgs[2] = poses;
                peopleArgs[3] = maxPeople;
                return (int)tryPeople.Invoke(inner, peopleArgs);
            }

            if (poses == null || poses.Length == 0 || maxPeople <= 0) return 0;
            if (poses[0] == null) poses[0] = new PoseFrame();
            return TryInfer(upright, size, poses[0]) ? 1 : 0;
        }

        /// <summary>Releases the interpreter. Added so stopping the camera drops the last frame it held.</summary>
        public void Dispose()
        {
            IDisposable disposable = inner as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }
}
