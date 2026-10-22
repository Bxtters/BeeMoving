using System;
using TensorFlowLite;
using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Runs MediaPipe's hand-landmark model on one square crop.
    /// The crop is a hand found from a MoveNet wrist. The model returns 21 points.
    /// It does not invent a hand when the crop does not look like one.
    /// </summary>
    public sealed class HandLandmarkEstimator : IDisposable
    {
        const float PresentThreshold = 0.5f;

        readonly Interpreter interpreter;
        readonly bool floatInput;
        readonly float[] floatPixels;
        readonly byte[] bytePixels;
        readonly float[][] outputs;
        readonly int landmarkOutput;
        readonly int presenceOutput;
        bool broken;

        public int InputSize { get; private set; }
        public string Summary { get; private set; }
        public string Failure { get; private set; }

        public HandLandmarkEstimator(byte[] modelBytes)
        {
            if (modelBytes == null || modelBytes.Length < 16)
            {
                throw new ArgumentException("Hand landmark model file is empty.");
            }

            InterpreterOptions options = new InterpreterOptions();
            options.threads = 2;
            try
            {
                interpreter = new Interpreter(modelBytes, options);
                Interpreter.TensorInfo inputInfo = interpreter.GetInputTensorInfo(0);
                if (inputInfo.shape == null || inputInfo.shape.Length != 4 || inputInfo.shape[3] != 3)
                {
                    throw new InvalidOperationException("Hand model input is not an RGB image.");
                }

                InputSize = inputInfo.shape[1];
                if (InputSize < 32 || inputInfo.shape[2] != InputSize)
                {
                    InputSize = 224;
                    interpreter.ResizeInputTensor(0, new int[] { 1, InputSize, InputSize, 3 });
                    interpreter.AllocateTensors();
                    inputInfo = interpreter.GetInputTensorInfo(0);
                }

                floatInput = inputInfo.type == Interpreter.DataType.Float32;
                if (!floatInput && inputInfo.type != Interpreter.DataType.UInt8)
                {
                    throw new InvalidOperationException("Hand model input type is " + inputInfo.type + ".");
                }

                int pixels = InputSize * InputSize * 3;
                if (floatInput) floatPixels = new float[pixels];
                else bytePixels = new byte[pixels];

                int count = interpreter.GetOutputTensorCount();
                outputs = new float[count][];
                landmarkOutput = -1;
                presenceOutput = -1;
                int landmarksFound = 0;
                for (int i = 0; i < count; i++)
                {
                    Interpreter.TensorInfo info = interpreter.GetOutputTensorInfo(i);
                    int length = 1;
                    if (info.shape != null)
                    {
                        for (int s = 0; s < info.shape.Length; s++)
                        {
                            if (info.shape[s] > 0) length *= info.shape[s];
                        }
                    }

                    if (length < 1) length = 1;
                    outputs[i] = new float[length];
                    if (length >= 63 && landmarksFound == 0)
                    {
                        landmarkOutput = i;
                        landmarksFound++;
                    }
                    else if (length == 1 && presenceOutput < 0)
                    {
                        presenceOutput = i;
                    }
                }

                if (landmarkOutput < 0)
                {
                    throw new InvalidOperationException("Hand model did not publish 21 landmarks.");
                }

                Summary = (floatInput ? "float " : "uint8 ") + InputSize;
            }
            catch
            {
                if (interpreter != null) interpreter.Dispose();
                else options.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Writes 21 points into the pose, in the same 0-1 space as the square crop.
        /// Returns false when the crop does not contain a hand.
        /// </summary>
        public bool TryRead(Color32[] square, int size, HandPose pose)
        {
            if (broken || interpreter == null || square == null || pose == null || pose.Points == null || size <= 0)
            {
                return false;
            }

            try
            {
                Fill(square, size);
                if (floatInput) interpreter.SetInputTensorData(0, floatPixels);
                else interpreter.SetInputTensorData(0, bytePixels);
                interpreter.Invoke();
                for (int i = 0; i < outputs.Length; i++)
                {
                    interpreter.GetOutputTensorData(i, outputs[i]);
                }

                float presence = presenceOutput >= 0 ? outputs[presenceOutput][0] : 1f;
                if (presence < PresentThreshold) return false;

                float[] marks = outputs[landmarkOutput];
                float score = presence > 1f ? 1f : presence;
                int points = pose.Points.Length;
                if (points > HandPose.Count) points = HandPose.Count;
                for (int i = 0; i < points; i++)
                {
                    int at = i * 3;
                    if (at + 1 >= marks.Length) break;
                    float z = at + 2 < marks.Length ? marks[at + 2] : 0f;
                    pose.Points[i] = new HandPoint(marks[at], marks[at + 1], z, score);
                }

                pose.Presence = score;
                return true;
            }
            catch (Exception ex)
            {
                broken = true;
                Failure = ex.Message;
                return false;
            }
            finally
            {
                if (floatPixels != null) Array.Clear(floatPixels, 0, floatPixels.Length);
                if (bytePixels != null) Array.Clear(bytePixels, 0, bytePixels.Length);
            }
        }

        public void Dispose()
        {
            if (interpreter != null) interpreter.Dispose();
        }

        void Fill(Color32[] square, int size)
        {
            int target = InputSize;
            for (int y = 0; y < target; y++)
            {
                int sy = y * size / target;
                if (sy >= size) sy = size - 1;
                for (int x = 0; x < target; x++)
                {
                    int sx = x * size / target;
                    if (sx >= size) sx = size - 1;
                    Color32 pixel = square[sy * size + sx];
                    int index = (y * target + x) * 3;
                    if (floatInput)
                    {
                        floatPixels[index] = pixel.r / 255f;
                        floatPixels[index + 1] = pixel.g / 255f;
                        floatPixels[index + 2] = pixel.b / 255f;
                    }
                    else
                    {
                        bytePixels[index] = pixel.r;
                        bytePixels[index + 1] = pixel.g;
                        bytePixels[index + 2] = pixel.b;
                    }
                }
            }
        }
    }
}
