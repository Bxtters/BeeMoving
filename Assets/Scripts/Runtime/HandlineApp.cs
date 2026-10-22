using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace Handline
{
    /// <summary>
    /// Builds the lane, reads whichever camera the OS exposes, and turns
    /// MoveNet Lightning wrists into run, jump, slide, and lane changes.
    /// </summary>
    public sealed class HandlineApp : MonoBehaviour
    {
        const int PreviewSize = 384;
        const string ModelFile = "movenet_lightning.tflite";

        enum Mode
        {
            Attract,
            Play,
            Pause,
            Over
        }

        static HandlineApp live;

        Camera backdrop;
        HudView hud;
        PoseOverlay overlay;
        Sfx sfx;
        RunnerSim sim;
        GestureClassifier classifier;
        PoseFrame pose;
        PoseEngine estimator;
        PoseEngine multiEstimator;
        readonly PoseFrame[] crowd = new PoseFrame[6];
        int crowdCount;
        int peopleLimit = 1;
        string setupError;
        WebCamTexture webcam;

        Color32[] upright;
        Color32[] texPixels;
        Texture2D previewTex;

        Mode mode = Mode.Attract;
        int cameraIndex;
        int knownDeviceCount = -1;
        int extraRotation;
        bool mirror = true;
        bool ready;
        bool waitForHandsDown;
        bool restartNeedsRelease;
        float restartHold;
        float nextDeviceScan;
        float poseUntil;
        string cameraStatus = "Looking for a camera";
        string modelError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindOne() != null) return;
            // The motion-capture stage uses the camera while it is the display Play opens.
            if (Camera_MoCap.OpenInsteadOfLane) return;
            GameObject go = new GameObject("Handline");
            go.AddComponent<HandlineApp>();
        }

        void Awake()
        {
            if (Camera_MoCap.OpenInsteadOfLane)
            {
                Destroy(gameObject);
                return;
            }

            if (live != null && live != this)
            {
                Destroy(gameObject);
                return;
            }

            live = this;
            try
            {
                hud = HudView.Create();
                hud.HideRunner();
                overlay = hud.OverlayRoot.gameObject.AddComponent<PoseOverlay>();
                overlay.Build();
                classifier = new GestureClassifier();
                EnsureBackdrop();
                for (int i = 0; i < crowd.Length; i++) crowd[i] = new PoseFrame();
                pose = crowd[0];
                hud.Bind(OpenCamera);
                if (hud.People != null)
                {
                    hud.People.onValueChanged.AddListener(delegate (int value)
                    {
                        peopleLimit = value + 1;
                    });
                }
                ClaimCameraAndLights();
                ready = true;
                Debug.Log("Handline motion capture is running. The camera preview is in the corner.");
            }
            catch (Exception ex)
            {
                setupError = ex.Message;
                Debug.LogException(ex);
            }
        }

        IEnumerator Start()
        {
            if (!ready) yield break;
            yield return LoadModel();
            OpenCamera(PreferredCamera());
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(setupError)) return;
            GUI.Label(new Rect(16f, 16f, Screen.width - 32f, 160f), setupError);
        }

        void Update()
        {
            if (!ready) return;

            ScanDevices();
            PollKeys();
            if (webcam != null && webcam.isPlaying && webcam.didUpdateThisFrame && webcam.width > 16)
            {
                BlitAndInfer();
            }
            else if (Time.unscaledTime > poseUntil)
            {
                BlankPose();
            }

            GestureSignal signal = classifier.Evaluate(pose, Time.timeSinceLevelLoad);
            if (estimator != null && !string.IsNullOrEmpty(estimator.Failure))
            {
                modelError = "Pose tracking stopped. " + estimator.Failure;
            }

            int shown = webcam != null && webcam.isPlaying ? crowdCount : 0;
            overlay.Redraw(crowd, shown, 0);
            hud.ShowCapture(signal, StatusLine());
        }

        void OnDestroy()
        {
            if (live == this) live = null;
            if (webcam != null) webcam.Stop();
            if (estimator != null) estimator.Dispose();
            if (multiEstimator != null) multiEstimator.Dispose();
        }

        void Begin(bool fromGesture)
        {
            sim.Reset();
            classifier.Reset();
            mode = Mode.Play;
            waitForHandsDown = fromGesture;
            restartHold = 0f;
            sfx.Go();
        }

        RunnerInput ReadInput(GestureSignal signal)
        {
            RunnerInput input = default(RunnerInput);
            int lane = 0;
            if (Keys.Pressed(Key.LeftArrow) || Keys.Pressed(Key.A)) lane = -1;
            if (Keys.Pressed(Key.RightArrow) || Keys.Pressed(Key.D)) lane = 1;
            if (lane == 0) lane = signal.LaneStep;

            input.LaneStep = lane;
            input.JumpHeld = signal.JumpHeld || KeyJump();
            input.DuckHeld = signal.DuckHeld || Keys.Held(Key.DownArrow) || Keys.Held(Key.S) || Keys.Held(Key.LeftCtrl);
            input.Run = signal.Run;
            if (Keys.Held(Key.LeftShift) || Keys.Held(Key.RightShift)) input.Run = 1f;
            return input;
        }

        static bool KeyJump()
        {
            return Keys.Held(Key.Space) || Keys.Held(Key.W) || Keys.Held(Key.UpArrow);
        }

        void PollKeys()
        {
            if (Keys.Pressed(Key.F)) mirror = !mirror;
            if (Keys.Pressed(Key.Q)) extraRotation = (extraRotation + 90) % 360;
            if (Keys.Pressed(Key.C))
            {
                int count = WebCamTexture.devices.Length;
                if (count > 0) OpenCamera((cameraIndex + 1) % count);
            }
        }

        void BlitAndInfer()
        {
            int sw = webcam.width;
            int sh = webcam.height;
            Color32[] source = webcam.GetPixels32();
            if (source == null || source.Length < sw * sh) return;

            if (upright == null)
            {
                upright = new Color32[PreviewSize * PreviewSize];
                texPixels = new Color32[PreviewSize * PreviewSize];
                previewTex = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false);
                previewTex.wrapMode = TextureWrapMode.Clamp;
                previewTex.filterMode = FilterMode.Bilinear;
                hud.Preview.texture = previewTex;
            }

            int rotation = webcam.videoRotationAngle + extraRotation;
            bool vertical = webcam.videoVerticallyMirrored;
            for (int y = 0; y < PreviewSize; y++)
            {
                int row = y * PreviewSize;
                for (int x = 0; x < PreviewSize; x++)
                {
                    int bx;
                    int by;
                    FrameOrient.Map(x, y, PreviewSize, sw, sh, rotation, vertical, mirror, out bx, out by);
                    upright[row + x] = source[by * sw + bx];
                }
            }

            for (int y = 0; y < PreviewSize; y++)
            {
                int src = y * PreviewSize;
                int dst = (PreviewSize - 1 - y) * PreviewSize;
                for (int x = 0; x < PreviewSize; x++)
                {
                    texPixels[dst + x] = upright[src + x];
                }
            }

            previewTex.SetPixels32(texPixels);
            previewTex.Apply(false);

            PoseEngine engine = peopleLimit > 1 && multiEstimator != null ? multiEstimator : estimator;
            if (engine != null)
            {
                crowdCount = engine.TryRead(upright, PreviewSize, crowd, peopleLimit);
                int active = BestPerson();
                if (active >= 0)
                {
                    if (active != 0 && crowd[0] != null && crowd[active] != null)
                    {
                        PoseFrame swap = crowd[0];
                        crowd[0] = crowd[active];
                        crowd[active] = swap;
                    }

                    pose = crowd[0];
                }
                else
                {
                    BlankPose();
                }
            }

            cameraStatus = webcam.deviceName + "  ·  " + sw + "×" + sh;
            if (estimator != null && string.IsNullOrEmpty(estimator.Failure))
            {
                cameraStatus += "  ·  " + estimator.Summary;
            }

            if (!mirror) cameraStatus += "  ·  unmirrored";
            if (extraRotation != 0) cameraStatus += "  ·  rotated " + extraRotation + "°";
            poseUntil = Time.unscaledTime + 0.45f;
        }

        int BestPerson()
        {
            int best = -1;
            float bestScore = 0.2f;
            for (int i = 0; i < crowdCount && i < crowd.Length; i++)
            {
                if (crowd[i] == null || crowd[i].Joints == null) continue;
                float score = 0f;
                int n = 0;
                for (int k = 0; k < crowd[i].Joints.Length; k++)
                {
                    score += crowd[i].Joints[k].Score;
                    n++;
                }

                if (n == 0) continue;
                score /= n;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        void BlankPose()
        {
            crowdCount = 0;
            if (pose == null || pose.Joints == null) return;
            for (int i = 0; i < pose.Joints.Length; i++)
            {
                Joint joint = pose.Joints[i];
                joint.Score = 0f;
                pose.Joints[i] = joint;
            }
        }

        IEnumerator LoadModel()
        {
            string path = Path.Combine(Application.streamingAssetsPath, ModelFile);
            byte[] bytes = null;
            if (path.Contains("://") || path.Contains("jar:"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        modelError = "Could not read the MoveNet model. " + request.error;
                        yield break;
                    }

                    bytes = request.downloadHandler.data;
                }
            }
            else if (!File.Exists(path))
            {
                modelError = "Missing " + path;
                yield break;
            }
            else
            {
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception ex)
                {
                    modelError = ex.Message;
                    yield break;
                }
            }

            try
            {
                estimator = PoseEngine.Create(bytes);
                if (estimator == null)
                {
                    modelError = "TensorFlow Lite is not imported yet. The lane still runs from the keyboard. Let Package Manager finish, then press Play again.";
                }
            }
            catch (DllNotFoundException ex)
            {
                modelError = "TensorFlow Lite did not load (" + ex.Message + "). Finish importing com.github.asus4.tflite, then press Play again.";
            }
            catch (Exception ex)
            {
                modelError = "MoveNet did not start. " + ex.Message;
            }

            string multiPath = Path.Combine(Application.streamingAssetsPath, "movenet_multipose.tflite");
            if (multiPath.Contains("://") || multiPath.Contains("jar:")) yield break;
            if (!File.Exists(multiPath)) yield break;
            try
            {
                multiEstimator = PoseEngine.Create(File.ReadAllBytes(multiPath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Multi-person MoveNet did not start. " + ex.Message);
            }
        }

        void OpenCamera(int index)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            PushCameraList(devices);
            if (devices == null || devices.Length == 0)
            {
                cameraStatus = "No camera detected.";
                if (webcam != null)
                {
                    webcam.Stop();
                    Destroy(webcam);
                    webcam = null;
                }

                return;
            }

            if (index < 0 || index >= devices.Length) index = 0;
            cameraIndex = index;
            if (webcam != null)
            {
                webcam.Stop();
                Destroy(webcam);
                webcam = null;
            }

            webcam = new WebCamTexture(devices[index].name, 640, 480, 30);
            webcam.Play();
            cameraStatus = "Starting " + devices[index].name;
        }

        int PreferredCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].isFrontFacing) return i;
            }

            return 0;
        }

        void ScanDevices()
        {
            if (Time.unscaledTime < nextDeviceScan) return;
            nextDeviceScan = Time.unscaledTime + 1.2f;
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == knownDeviceCount) return;
            knownDeviceCount = devices.Length;
            PushCameraList(devices);
            if (webcam == null && devices.Length > 0) OpenCamera(PreferredCamera());
        }

        void PushCameraList(WebCamDevice[] devices)
        {
            List<string> names = new List<string>();
            if (devices != null)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    string name = devices[i].name;
                    if (devices[i].isFrontFacing) name += " (front)";
                    names.Add(name);
                }
            }

            knownDeviceCount = names.Count;
            hud.SetCameras(names, cameraIndex);
        }

        string StatusLine()
        {
            if (!string.IsNullOrEmpty(modelError)) return modelError;
            if (webcam != null && webcam.isPlaying && webcam.width <= 16 && Time.unscaledTime > 3f)
            {
                return "Camera did not start. Allow Unity to use the camera, then press C.";
            }

            return cameraStatus;
        }

        /// <summary>
        /// Gives the Game view a clear camera. The motion capture itself is the HUD preview.
        /// </summary>
        void EnsureBackdrop()
        {
            GameObject go = new GameObject("Handline Camera");
            go.tag = "MainCamera";
            backdrop = go.AddComponent<Camera>();
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = new Color(0.09f, 0.075f, 0.06f, 1f);
            backdrop.fieldOfView = 40f;
            backdrop.nearClipPlane = 0.08f;
            backdrop.farClipPlane = 80f;
            backdrop.transform.position = new Vector3(0f, 1.6f, -4f);
            go.AddComponent<AudioListener>();
        }

        void ClaimCameraAndLights()
        {
            Camera[] cameras = FindAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != backdrop) cameras[i].gameObject.SetActive(false);
            }

            AudioListener[] listeners = FindAll<AudioListener>();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].GetComponent<Camera>() != backdrop)
                {
                    Destroy(listeners[i]);
                }
            }
        }

        static HudMode ToHud(Mode mode)
        {
            if (mode == Mode.Play) return HudMode.Play;
            if (mode == Mode.Pause) return HudMode.Pause;
            if (mode == Mode.Over) return HudMode.Over;
            return HudMode.Attract;
        }

        static HandlineApp FindOne()
        {
            return UnityEngine.Object.FindAnyObjectByType<HandlineApp>();
        }

        static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }
    }
}
