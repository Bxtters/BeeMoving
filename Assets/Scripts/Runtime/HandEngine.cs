using System;
using System.Reflection;
using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Talks to the hand-landmark model when TensorFlow Lite imported.
    /// Body tracking still runs when that model is missing.
    /// </summary>
    public sealed class HandEngine : IDisposable
    {
        readonly object inner;
        readonly MethodInfo tryRead;
        readonly PropertyInfo summary;
        readonly PropertyInfo failure;
        readonly PropertyInfo inputSize;
        readonly object[] args = new object[3];

        public string Summary
        {
            get { return summary == null ? "" : (string)summary.GetValue(inner, null); }
        }

        public string Failure
        {
            get { return failure == null ? "" : (string)failure.GetValue(inner, null); }
        }

        public int InputSize
        {
            get
            {
                if (inputSize == null || inner == null) return 0;
                object value = inputSize.GetValue(inner, null);
                return value is int ? (int)value : 0;
            }
        }

        HandEngine(object inner, Type type)
        {
            this.inner = inner;
            tryRead = type.GetMethod("TryRead");
            summary = type.GetProperty("Summary");
            failure = type.GetProperty("Failure");
            inputSize = type.GetProperty("InputSize");
        }

        public static HandEngine Create(byte[] model)
        {
            Type type = Type.GetType("Handline.HandLandmarkEstimator, Handline.Vision");
            if (type == null) return null;
            object inner = Activator.CreateInstance(type, model);
            return new HandEngine(inner, type);
        }

        /// <summary>
        /// Measures one hand crop. Points come back in that crop's 0-1 space.
        /// </summary>
        public bool TryRead(Color32[] square, int size, HandPose pose)
        {
            if (tryRead == null || inner == null) return false;
            args[0] = square;
            args[1] = size;
            args[2] = pose;
            return (bool)tryRead.Invoke(inner, args);
        }

        public void Dispose()
        {
            IDisposable disposable = inner as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }
}
