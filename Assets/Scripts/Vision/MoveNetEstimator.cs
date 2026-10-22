using System;
using TensorFlowLite;
using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Runs MoveNet SinglePose Lightning. The float16 TFLite file takes a
    /// uint8 192x192 RGB image and returns 17 keypoints as (y, x, score).
    /// </summary>
    public sealed class MoveNetEstimator : IDisposable
    {
        readonly byte[] model;
        Interpreter interpreter;
        byte[] input;
        float[] output;
        int inputSize;
        bool multi;
        bool broken;

        public int InputSize { get { return inputSize; } }
        public string Summary { get; private set; }
        public string Failure { get; private set; }

        public MoveNetEstimator(byte[] modelBytes)
        {
            if (modelBytes == null || modelBytes.Length < 16)
            {
                throw new ArgumentException("MoveNet model file is empty.");
            }

            model = modelBytes;
            InterpreterOptions options = new InterpreterOptions();
            options.threads = Mathf.Clamp(SystemInfo.processorCount / 2, 1, 4);
            try
            {
                interpreter = new Interpreter(model, options);
                Interpreter.TensorInfo inputInfo = interpreter.GetInputTensorInfo(0);
                interpreter.ResizeInputTensor(0, inputInfo.shape);
                interpreter.AllocateTensors();
                inputInfo = interpreter.GetInputTensorInfo(0);
                Interpreter.TensorInfo outputInfo = interpreter.GetOutputTensorInfo(0);

                if (inputInfo.shape == null || inputInfo.shape.Length != 4 || inputInfo.shape[3] != 3)
                {
                    throw new InvalidOperationException("MoveNet input is not an RGB image tensor.");
                }

                if (inputInfo.type != Interpreter.DataType.UInt8)
                {
                    throw new InvalidOperationException("MoveNet Lightning expects a uint8 RGB image, got " + inputInfo.type + ".");
                }

                int outputCount = Count(outputInfo.shape);
                multi = outputCount == 6 * 56;
                if (!multi && !IsSinglePose(outputInfo.shape))
                {
                    throw new InvalidOperationException("Expected single-pose or 6-person MoveNet output. Got shape " + Join(outputInfo.shape) + ".");
                }

                inputSize = inputInfo.shape[1];
                if (inputSize < 32 || inputInfo.shape[2] != inputSize)
                {
                    inputSize = multi ? 256 : 192;
                    interpreter.ResizeInputTensor(0, new int[] { 1, inputSize, inputSize, 3 });
                    interpreter.AllocateTensors();
                }

                input = new byte[inputSize * inputSize * 3];
                output = new float[multi ? 6 * 56 : 51];
                Summary = "uint8 " + inputSize + (multi ? " · 6 people" : " · 1 person");
                Debug.Log("Handline pose model: " + Summary + " output " + Join(outputInfo.shape));
            }
            catch
            {
                if (interpreter != null) interpreter.Dispose();
                else options.Dispose();
                throw;
            }
        }

        public bool TryInfer(Color32[] upright, int size, PoseFrame pose)
        {
            PoseFrame[] one = new PoseFrame[] { pose };
            return TryInferPeople(upright, size, one, 1) > 0;
        }

        public int TryInferPeople(Color32[] upright, int size, PoseFrame[] poses, int maxPeople)
        {
            if (broken || upright == null || poses == null || size <= 0 || maxPeople <= 0)
            {
                return 0;
            }

            int count = 0;
            try
            {
                Fill(upright, size);
                interpreter.SetInputTensorData(0, input);
                interpreter.Invoke();
                interpreter.GetOutputTensorData(0, output);
                count = multi ? ReadPeople(poses, maxPeople) : ReadSingle(poses);
                return count;
            }
            catch (Exception ex)
            {
                broken = true;
                Failure = ex.Message;
                Debug.LogError("MoveNet inference stopped. " + ex.Message);
                return 0;
            }
            finally
            {
                // Drop the camera image and the raw output, including any facial points,
                // once the joints have been copied. The copied pose is what callers keep.
                WipeFrame();
            }
        }

        /// <summary>
        /// Clears the managed camera buffer and the raw output after the joints have been copied.
        /// The next frame overwrites the interpreter input. Stopping capture disposes the interpreter.
        /// </summary>
        void WipeFrame()
        {
            if (input != null) Array.Clear(input, 0, input.Length);
            if (output != null) Array.Clear(output, 0, output.Length);
        }

        int ReadSingle(PoseFrame[] poses)
        {
            if (poses[0] == null) poses[0] = new PoseFrame();
            for (int i = 0; i < 17; i++)
            {
                poses[0].Joints[i] = new Joint(output[i * 3 + 1], output[i * 3], output[i * 3 + 2]);
            }

            return 1;
        }

        int ReadPeople(PoseFrame[] poses, int maxPeople)
        {
            if (maxPeople > 6) maxPeople = 6;
            int[] order = new int[] { 0, 1, 2, 3, 4, 5 };
            for (int a = 0; a < 6; a++)
            {
                for (int b = a + 1; b < 6; b++)
                {
                    if (PersonScore(order[b]) > PersonScore(order[a]))
                    {
                        int swap = order[a];
                        order[a] = order[b];
                        order[b] = swap;
                    }
                }
            }

            int written = 0;
            for (int n = 0; n < 6 && written < maxPeople; n++)
            {
                int person = order[n];
                if (PersonScore(person) < 0.2f) continue;
                if (poses[written] == null) poses[written] = new PoseFrame();
                int origin = person * 56;
                for (int k = 0; k < 17; k++)
                {
                    int at = origin + k * 3;
                    poses[written].Joints[k] = new Joint(output[at + 1], output[at], output[at + 2]);
                }

                written++;
            }

            return written;
        }

        float PersonScore(int person)
        {
            return output[person * 56 + 55];
        }

        public void Dispose()
        {
            if (interpreter != null)
            {
                interpreter.Dispose();
            }
        }

        void Fill(Color32[] upright, int size)
        {
            int block = size / inputSize;
            if (block >= 2 && size == block * inputSize)
            {
                int area = block * block;
                for (int y = 0; y < inputSize; y++)
                {
                    for (int x = 0; x < inputSize; x++)
                    {
                        int r = 0;
                        int g = 0;
                        int b = 0;
                        int origin = (y * block) * size + (x * block);
                        for (int oy = 0; oy < block; oy++)
                        {
                            int row = origin + oy * size;
                            for (int ox = 0; ox < block; ox++)
                            {
                                Color32 pixel = upright[row + ox];
                                r += pixel.r;
                                g += pixel.g;
                                b += pixel.b;
                            }
                        }

                        WritePixel(y, x, (byte)(r / area), (byte)(g / area), (byte)(b / area));
                    }
                }

                return;
            }

            for (int y = 0; y < inputSize; y++)
            {
                int sy = y * size / inputSize;
                if (sy >= size) sy = size - 1;
                for (int x = 0; x < inputSize; x++)
                {
                    int sx = x * size / inputSize;
                    if (sx >= size) sx = size - 1;
                    Color32 pixel = upright[sy * size + sx];
                    WritePixel(y, x, pixel.r, pixel.g, pixel.b);
                }
            }
        }

        void WritePixel(int y, int x, byte r, byte g, byte b)
        {
            int index = (y * inputSize + x) * 3;
            input[index] = r;
            input[index + 1] = g;
            input[index + 2] = b;
        }

        static int Count(int[] shape)
        {
            if (shape == null || shape.Length == 0) return 0;
            int count = 1;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i] <= 0) return 0;
                count *= shape[i];
            }

            return count;
        }

        static bool IsSinglePose(int[] shape)
        {
            if (shape == null) return false;
            int joints = 0;
            int dims = 0;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i] == 17) joints++;
                if (shape[i] == 3) dims++;
            }

            int count = 1;
            for (int i = 0; i < shape.Length; i++) count *= shape[i];
            return joints >= 1 && dims >= 1 && count == 51;
        }

        static string Join(int[] shape)
        {
            if (shape == null) return "(none)";
            return "[" + string.Join(",", shape) + "]";
        }
    }
}
