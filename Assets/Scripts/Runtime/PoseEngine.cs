using System;
using System.Reflection;
using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Talks to MoveNet if the TensorFlow Lite package imported.
    /// The lane still runs when that package is missing.
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

        public string Summary
        {
            get { return summary == null ? "" : (string)summary.GetValue(inner, null); }
        }

        public string Failure
        {
            get { return failure == null ? "" : (string)failure.GetValue(inner, null); }
        }

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

        public static PoseEngine Create(byte[] model)
        {
            Type type = Type.GetType("Handline.MoveNetEstimator, Handline.Vision");
            if (type == null) return null;
            object inner = Activator.CreateInstance(type, model);
            return new PoseEngine(inner, type);
        }

        public bool TryInfer(Color32[] upright, int size, PoseFrame pose)
        {
            if (tryInfer == null || inner == null) return false;
            oneArgs[0] = upright;
            oneArgs[1] = size;
            oneArgs[2] = pose;
            return (bool)tryInfer.Invoke(inner, oneArgs);
        }

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

        public void Dispose()
        {
            IDisposable disposable = inner as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }
}
