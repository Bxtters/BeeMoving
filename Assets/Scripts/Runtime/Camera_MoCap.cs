using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Handline
{
    /// <summary>
    /// Camera stage for Unity 6000.0.82f1. Play opens this display.
    /// A local camera feeds MoveNet MultiPose Lightning, which returns up to six
    /// people. The stage draws the body from the shoulders to the ankles.
    /// MoveNet measures each hand at the wrist and has no finger joints, so the
    /// hand marker is that wrist and no fingers are invented.
    /// Nose, eye, and ear points are erased before drawing. The camera picture
    /// is never shown, never written to disk, and never sent off the device.
    /// The Unity scene is Assets/Scenes/Camera_MoCap.unity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Camera_MoCap : MonoBehaviour
    {
        /// <summary>
        /// Play opens this stage instead of the lane while this is true.
        /// Set it to false to boot the runner again. The two displays are not
        /// meant to share one camera.
        /// </summary>
        public const bool OpenInsteadOfLane = true;

        /// <summary>MoveNet MultiPose Lightning publishes at most six people.</summary>
        public const int MaxPeople = 6;

        const int CropSize = 384;
        const int MinBodyPoints = 3;
        const float SlotMatchDistance = 0.35f;
        const string MultiModelFile = "movenet_multipose.tflite";
        const string SingleModelFile = "movenet_lightning.tflite";
        const string PrivacyNotice = "On this device only. The camera picture is not shown or saved. Faces are not kept.";
        const string HandNotice = "Each hand is the wrist. MoveNet Lightning has no finger joints, so none are drawn.";
        const string PairingGuide =
            "IPHONE ON A MAC\n\n" +
            "1. On the iPhone, open Settings, then General, then AirPlay & Continuity.\n" +
            "2. Turn Continuity Camera on.\n" +
            "3. Sign the iPhone and the Mac into the same Apple ID.\n" +
            "4. Turn Wi-Fi and Bluetooth on for both. Unlock the iPhone, leave it near the Mac, and do not let it sleep.\n" +
            "5. Press Refresh. Choose the iPhone in the camera row.\n\n" +
            "IPHONE ON WINDOWS\n\n" +
            "Continuity Camera is a Mac feature. Windows does not see an iPhone until a webcam app publishes it.\n\n" +
            "1. On the PC, install Iriun Webcam from iriun.com and open it. If Windows asks to install a camera driver, allow it.\n" +
            "2. On the iPhone, install Iriun Webcam from the App Store.\n" +
            "3. Join the same Wi-Fi on the phone and the PC. Or plug the iPhone in with a data cable and tap Trust.\n" +
            "4. Open Iriun on the iPhone and allow the camera.\n" +
            "5. Leave Iriun open on Windows so the phone is registered as a webcam.\n" +
            "6. Press Refresh here and select the camera named Iriun.\n\n" +
            "ANDROID\n\n" +
            "1. Install Iriun Webcam or DroidCam on the phone and on this computer.\n" +
            "2. Open the app on both. Allow the camera on the phone.\n" +
            "3. Use the same Wi-Fi, or USB when the app offers it.\n" +
            "4. Press Refresh here and select that camera.\n\n" +
            "A plugged-in iPhone is a different setup. Press Back, then USB cable.\n\n" +
            "This app only uses a webcam the computer already lists. It does not install a driver and it does not show the picture.";
        const string CableGuide =
            "Use the charging cable that came with the iPhone, or another data cable. A charge-only cable powers the phone and never becomes a camera.\n\n" +
            "MAC\n\n" +
            "1. Plug the iPhone into the Mac.\n" +
            "2. Unlock the iPhone. If it asks to Trust This Computer, tap Trust and enter the passcode.\n" +
            "3. Leave the phone unlocked. Continuity Camera uses the cable while it is plugged in.\n" +
            "4. Press Check cable. This page selects the iPhone when the Mac lists it.\n\n" +
            "WINDOWS\n\n" +
            "Windows does not speak iPhone USB until Apple's phone service is installed. Until then the cable only charges the phone.\n\n" +
            "1. Press Apple driver and install Apple Devices. Open it once. iTunes from Apple does the same job if you already have it.\n" +
            "2. Plug the iPhone into a USB port on the PC. Unlock it, tap Trust, and enter the passcode.\n" +
            "3. If the PC still does not see the phone, press Windows+R, type services.msc, and start Apple Mobile Device Service. Set its startup type to Automatic.\n" +
            "4. Press Iriun and install Iriun Webcam on the PC. Open the PC app first and leave it open.\n" +
            "5. Install Iriun Webcam on the iPhone from the App Store. Open it, allow the camera, and leave the cable plugged in. Iriun uses the cable when it is connected.\n" +
            "6. Press Check cable. This page selects the camera named Iriun when Windows lists it.\n\n" +
            "The picture stays on this computer. It is not shown and it is not saved.";
        const string AppleDriverUrl = "https://apps.microsoft.com/detail/9np83lwlpz9k";
        const string IriunUrl = "https://iriun.com/";
        const string PrefMirror = "CameraMoCap.Mirror";
        const string PrefRotation = "CameraMoCap.Rotation";
        const string PrefWidth = "CameraMoCap.Width";
        const string PrefHeight = "CameraMoCap.Height";
        const string PrefFps = "CameraMoCap.Fps";
        const string PrefPeople = "CameraMoCap.People";
        const string PrefThreshold = "CameraMoCap.Threshold";
        const string PrefCamera = "CameraMoCap.Camera";

        static readonly int[] Bones =
        {
            (int)JointId.LeftShoulder, (int)JointId.RightShoulder,
            (int)JointId.LeftShoulder, (int)JointId.LeftElbow,
            (int)JointId.LeftElbow, (int)JointId.LeftWrist,
            (int)JointId.RightShoulder, (int)JointId.RightElbow,
            (int)JointId.RightElbow, (int)JointId.RightWrist,
            (int)JointId.LeftShoulder, (int)JointId.LeftHip,
            (int)JointId.RightShoulder, (int)JointId.RightHip,
            (int)JointId.LeftHip, (int)JointId.RightHip,
            (int)JointId.LeftHip, (int)JointId.LeftKnee,
            (int)JointId.LeftKnee, (int)JointId.LeftAnkle,
            (int)JointId.RightHip, (int)JointId.RightKnee,
            (int)JointId.RightKnee, (int)JointId.RightAnkle
        };

        static readonly Color[] PersonColors =
        {
            new Color(1f, 0.84f, 0.12f, 1f),
            new Color(1f, 0.93f, 0.55f, 1f),
            new Color(0.86f, 0.68f, 0.04f, 1f),
            new Color(1f, 0.78f, 0.20f, 1f),
            new Color(0.96f, 0.90f, 0.62f, 1f),
            new Color(0.72f, 0.58f, 0.06f, 1f)
        };

        static readonly Color Ink = new Color(0f, 0f, 0f, 1f);
        static readonly Color StageColor = new Color(0.05f, 0.05f, 0.05f, 1f);
        static readonly Color Cream = new Color(0.96f, 0.96f, 0.94f, 1f);
        static readonly Color Yellow = new Color(1f, 0.82f, 0.12f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.62f, 0.58f, 1f);

        readonly PoseFrame[] crowd = new PoseFrame[MaxPeople];
        readonly PoseFrame[] stable = new PoseFrame[MaxPeople];
        readonly bool[] slotUsed = new bool[MaxPeople];
        readonly float[] slotX = new float[MaxPeople];
        readonly float[] slotY = new float[MaxPeople];

        PoseEngine engine;
        WebCamTexture webcam;
        RectTransform canvasRect;
        RectTransform stage;
        Image[][] lines;
        Image[][] dots;
        Text countText;
        Text statusText;
        Texture2D whiteTex;
        Sprite whiteSprite;
        Color32[] sensorPixels;
        Color32[] crop;
        byte[] loadedBytes;

        string modelLabel = "";
        string modelError;
        string loadError;
        string setupError;
        bool loadingModel;
        int cameraIndex;
        int knownDeviceCount = -1;
        int crowdCount;
        int extraRotation;
        int requestWidth = 640;
        int requestHeight = 480;
        int requestFps = 30;
        int peopleLimit = MaxPeople;
        int pendingCamera = -1;
        float jointThreshold = 0.3f;
        float nextDeviceScan;
        float poseUntil;
        float cameraOpenedAt;
        bool mirror = true;
        bool ready;
        bool cameraHeldOff;
        bool resuming;
        bool waitingForPhone;
        bool waitingForCable;
        string savedCameraName = "";
        GameObject settingsRoot;
        GameObject settingsMain;
        GameObject settingsPairing;
        GameObject settingsCable;
        Text pairingStatus;
        Text cableStatus;
        Text settingsCameraText;
        Text settingsResolutionText;
        Text settingsFpsText;
        Text settingsMirrorText;
        Text settingsRotationText;
        Text settingsPeopleText;
        Text settingsDetailText;
        Text settingsPowerText;

        /// <summary>
        /// Creates the camera stage when the open scene does not already contain it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!OpenInsteadOfLane) return;
            if (UnityEngine.Object.FindAnyObjectByType<Camera_MoCap>() != null) return;
            GameObject stageObject = new GameObject("Camera_MoCap");
            stageObject.AddComponent<Camera_MoCap>();
        }

        /// <summary>
        /// Allocates the six pose slots and builds the skeleton canvas.
        /// </summary>
        void Awake()
        {
            try
            {
                for (int i = 0; i < MaxPeople; i++)
                {
                    crowd[i] = new PoseFrame();
                    stable[i] = new PoseFrame();
                }

                LoadSettings();
                BuildStage();
                ready = true;
            }
            catch (Exception ex)
            {
                setupError = ex.Message;
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Loads the local MoveNet file, then opens a camera only if that load succeeded.
        /// </summary>
        IEnumerator Start()
        {
            if (!ready) yield break;
            yield return LoadModel();
            if (engine == null || cameraHeldOff) yield break;
            OpenCamera(ChooseStartupCamera());
        }

        /// <summary>
        /// Polls keys, estimates the current frame, and redraws body skeletons.
        /// </summary>
        void Update()
        {
            if (!ready) return;
            LayoutStage();
            PollKeys();
            if (!cameraHeldOff && engine != null) ScanDevices();

            if (webcam != null && webcam.isPlaying && webcam.didUpdateThisFrame && webcam.width > 16)
            {
                CaptureAndEstimate();
            }
            else if (!cameraHeldOff && webcam != null && webcam.isPlaying && webcam.width <= 16 && Time.unscaledTime > cameraOpenedAt + 3f)
            {
                SetStatus("The camera did not start. Allow the camera for Unity, then press C.");
            }
            else if (Time.unscaledTime > poseUntil)
            {
                ClearCrowd();
                ShowCount(0);
            }

            Redraw();
        }

        /// <summary>
        /// Stops the camera when the app pauses so it does not keep capturing in the background.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (!paused || !ready) return;
            StopCapture("Camera off while the app is paused. Press Enter to start it again.");
        }

        /// <summary>
        /// Releases the camera, the interpreter, and every pixel buffer this stage allocated.
        /// </summary>
        void OnDestroy()
        {
            StopAllCoroutines();
            ReleaseCamera();
            ReleaseEngine();
            ClearCrowd();
            WipePixels();
            if (whiteSprite != null) Destroy(whiteSprite);
            if (whiteTex != null) Destroy(whiteTex);
        }

        /// <summary>
        /// Shows a setup failure if the canvas could not be built.
        /// </summary>
        void OnGUI()
        {
            if (string.IsNullOrEmpty(setupError)) return;
            GUI.Label(new Rect(16f, 16f, Screen.width - 32f, 140f), setupError);
        }

        /// <summary>
        /// Handles mirror, rotate, camera cycle, and the keys that start or stop capture.
        /// </summary>
        void PollKeys()
        {
            if (Keys.Pressed(Key.Tab))
            {
                SetSettingsOpen(settingsRoot == null || !settingsRoot.activeSelf);
                return;
            }

            if (Keys.Pressed(Key.Escape))
            {
                if (settingsRoot != null && settingsRoot.activeSelf)
                {
                    SetSettingsOpen(false);
                    return;
                }

                StopCapture("Camera off. Press Enter to start it again.");
                return;
            }

            if (Keys.Pressed(Key.Enter) || Keys.Pressed(Key.Space))
            {
                if (settingsRoot != null && settingsRoot.activeSelf) return;
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            if (cameraHeldOff || webcam == null) return;
            if (Keys.Pressed(Key.F)) ToggleMirror();

            if (Keys.Pressed(Key.Q)) CycleRotation();

            if (Keys.Pressed(Key.C))
            {
                int count = WebCamTexture.devices.Length;
                if (count > 0) OpenCamera((cameraIndex + 1) % count);
            }
        }

        /// <summary>
        /// Loads MoveNet again if it was released, then starts the camera.
        /// </summary>
        IEnumerator Resume()
        {
            resuming = true;
            cameraHeldOff = false;
            if (engine == null) yield return LoadModel();
            resuming = false;
            if (engine == null || cameraHeldOff) yield break;
            int index = pendingCamera >= 0 ? pendingCamera : ChooseStartupCamera();
            pendingCamera = -1;
            OpenCamera(index);
        }

        /// <summary>
        /// Turns capture off, drops the current poses, and disposes the interpreter.
        /// </summary>
        void StopCapture(string message)
        {
            cameraHeldOff = true;
            ReleaseCamera();
            ReleaseEngine();
            ClearCrowd();
            WipePixels();
            ShowCount(0);
            SetStatus(message);
        }

        /// <summary>
        /// Reads the multi-person Lightning model, then the single-person model if that file is missing.
        /// </summary>
        IEnumerator LoadModel()
        {
            if (loadingModel) yield break;
            loadingModel = true;
            try
            {
                yield return LoadModelFiles();
            }
            finally
            {
                loadingModel = false;
            }
        }

        /// <summary>
        /// Tries the six-person model first and keeps the single-person Lightning file as the fallback.
        /// </summary>
        IEnumerator LoadModelFiles()
        {
            SetStatus("Loading MoveNet Lightning…");
            modelError = null;
            yield return ReadModelFile(MultiModelFile);
            if (loadedBytes != null)
            {
                string error;
                engine = TryCreateEngine(loadedBytes, out error);
                loadedBytes = null;
                if (engine != null)
                {
                    modelLabel = "MoveNet MultiPose Lightning · up to 6 people";
                    if (!string.IsNullOrEmpty(engine.Summary)) modelLabel += "  ·  " + engine.Summary;
                    SetStatus(modelLabel);
                    yield break;
                }

                modelError = error;
            }
            else if (!string.IsNullOrEmpty(loadError))
            {
                modelError = loadError;
            }

            yield return ReadModelFile(SingleModelFile);
            if (loadedBytes == null)
            {
                if (string.IsNullOrEmpty(modelError)) modelError = loadError;
                if (string.IsNullOrEmpty(modelError)) modelError = "MoveNet Lightning did not load.";
                SetStatus(modelError);
                yield break;
            }

            string singleError;
            engine = TryCreateEngine(loadedBytes, out singleError);
            loadedBytes = null;
            if (engine == null)
            {
                modelError = string.IsNullOrEmpty(singleError) ? modelError : singleError;
                SetStatus(modelError);
                yield break;
            }

            modelLabel = "MoveNet Lightning · 1 person";
            if (!string.IsNullOrEmpty(engine.Summary)) modelLabel += "  ·  " + engine.Summary;
            if (!string.IsNullOrEmpty(modelError)) Debug.LogWarning("Multi-person MoveNet did not start. " + modelError);
            SetStatus(modelLabel);
        }

        /// <summary>
        /// Reads one model file from StreamingAssets into memory. The bytes are the published model, not a camera frame.
        /// </summary>
        IEnumerator ReadModelFile(string fileName)
        {
            loadedBytes = null;
            loadError = null;
            string path = Path.Combine(Application.streamingAssetsPath, fileName);
            if (path.Contains("://") || path.Contains("jar:"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        loadError = "Could not read " + fileName + ".";
                        yield break;
                    }

                    loadedBytes = request.downloadHandler.data;
                }

                yield break;
            }

            if (!File.Exists(path))
            {
                loadError = "Missing " + fileName + " in StreamingAssets.";
                yield break;
            }

            try
            {
                loadedBytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                loadError = "Could not read " + fileName + ". " + ex.Message;
            }
        }

        /// <summary>
        /// Starts the local TensorFlow Lite interpreter for one MoveNet file.
        /// </summary>
        static PoseEngine TryCreateEngine(byte[] bytes, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length < 16)
            {
                error = "The MoveNet file was empty.";
                return null;
            }

            try
            {
                PoseEngine created = PoseEngine.Create(bytes);
                if (created == null)
                {
                    error = "TensorFlow Lite is not imported yet. Let Package Manager finish, then press Play again.";
                    return null;
                }

                return created;
            }
            catch (Exception ex)
            {
                Exception root = ex;
                TargetInvocationException wrapped = ex as TargetInvocationException;
                if (wrapped != null && wrapped.InnerException != null) root = wrapped.InnerException;
                if (root is DllNotFoundException)
                {
                    error = "TensorFlow Lite did not load. Finish importing com.github.asus4.tflite, then press Play again.";
                }
                else
                {
                    error = "MoveNet did not start. " + root.Message;
                }

                return null;
            }
        }

        /// <summary>
        /// Disposes the interpreter so its copy of the last frame can be freed.
        /// </summary>
        void ReleaseEngine()
        {
            if (engine == null) return;
            engine.Dispose();
            engine = null;
        }

        /// <summary>
        /// Copies one camera frame into a square, estimates poses, then clears the pixels.
        /// </summary>
        void CaptureAndEstimate()
        {
            int sw = webcam.width;
            int sh = webcam.height;
            if (sw <= 16 || sh <= 16) return;

            try
            {
                EnsureSensorBuffer(sw, sh);
                Color32[] filled = webcam.GetPixels32(sensorPixels);
                if (filled == null) return;
                if (filled != sensorPixels) sensorPixels = filled;
                FillCrop(sensorPixels, sw, sh);
                if (engine != null)
                {
                    crowdCount = engine.TryRead(crop, CropSize, crowd, peopleLimit);
                    if (crowdCount < 0) crowdCount = 0;
                    if (crowdCount > MaxPeople) crowdCount = MaxPeople;
                }
                else
                {
                    crowdCount = 0;
                }

                for (int i = 0; i < MaxPeople; i++)
                {
                    if (i < crowdCount) DropFace(crowd[i]);
                    else Blank(crowd[i]);
                }

                Stabilize();
                for (int i = 0; i < MaxPeople; i++) Blank(crowd[i]);
                crowdCount = 0;
                poseUntil = Time.unscaledTime + 0.4f;
                ShowCount(VisiblePeople());
                if (engine != null && !string.IsNullOrEmpty(engine.Failure)) NoteModelFailure();
                else RefreshStatus();
            }
            catch (Exception ex)
            {
                crowdCount = 0;
                ClearCrowd();
                SetStatus("Pose tracking stopped. " + ex.Message);
            }
            finally
            {
                WipePixels();
            }
        }

        /// <summary>
        /// Grows the reusable camera buffer when the device changes resolution.
        /// </summary>
        void EnsureSensorBuffer(int width, int height)
        {
            int needed = width * height;
            if (sensorPixels != null && sensorPixels.Length == needed) return;
            sensorPixels = new Color32[needed];
        }

        /// <summary>
        /// Center-crops the sensor image into the upright square MoveNet reads.
        /// The square is the model input. It is not copied onto a texture.
        /// </summary>
        void FillCrop(Color32[] source, int srcWidth, int srcHeight)
        {
            if (crop == null || crop.Length != CropSize * CropSize)
            {
                crop = new Color32[CropSize * CropSize];
            }

            int rotation = webcam.videoRotationAngle + extraRotation;
            bool vertical = webcam.videoVerticallyMirrored;
            for (int y = 0; y < CropSize; y++)
            {
                int row = y * CropSize;
                for (int x = 0; x < CropSize; x++)
                {
                    int bx;
                    int by;
                    FrameOrient.Map(x, y, CropSize, srcWidth, srcHeight, rotation, vertical, mirror, out bx, out by);
                    int index = by * srcWidth + bx;
                    crop[row + x] = index >= 0 && index < source.Length ? source[index] : default(Color32);
                }
            }
        }

        /// <summary>
        /// Overwrites nose, eye, and ear points so this pose cannot be turned back into a face.
        /// </summary>
        static void DropFace(PoseFrame pose)
        {
            if (pose == null || pose.Joints == null) return;
            int lastFace = (int)JointId.RightEar;
            for (int i = 0; i <= lastFace && i < pose.Joints.Length; i++)
            {
                pose.Joints[i] = default(Joint);
            }
        }

        /// <summary>
        /// Sets every joint on a pose back to empty.
        /// </summary>
        static void Blank(PoseFrame pose)
        {
            if (pose == null || pose.Joints == null) return;
            for (int i = 0; i < pose.Joints.Length; i++)
            {
                pose.Joints[i] = default(Joint);
            }
        }

        /// <summary>
        /// Copies shoulder-to-ankle joints and leaves the face slots empty.
        /// </summary>
        static void CopyBody(PoseFrame source, PoseFrame dest)
        {
            Blank(dest);
            if (source == null || source.Joints == null || dest == null || dest.Joints == null) return;
            int start = (int)JointId.LeftShoulder;
            int end = source.Joints.Length;
            if (end > dest.Joints.Length) end = dest.Joints.Length;
            if (end > (int)JointId.Count) end = (int)JointId.Count;
            for (int i = start; i < end; i++) dest.Joints[i] = source.Joints[i];
        }

        /// <summary>
        /// Counts body joints that cleared the drawing threshold.
        /// </summary>
        int BodyPoints(PoseFrame pose)
        {
            if (pose == null || pose.Joints == null) return 0;
            int count = 0;
            int start = (int)JointId.LeftShoulder;
            int end = pose.Joints.Length;
            if (end > (int)JointId.Count) end = (int)JointId.Count;
            for (int i = start; i < end; i++)
            {
                if (pose.Joints[i].Score >= jointThreshold) count++;
            }

            return count;
        }

        /// <summary>
        /// True when enough of the torso and limbs are visible to draw a person.
        /// </summary>
        bool HasBody(PoseFrame pose)
        {
            return BodyPoints(pose) >= MinBodyPoints;
        }

        /// <summary>
        /// Averages the visible shoulders and hips. If those are hidden, it uses the visible limbs.
        /// Facial points are not part of the average.
        /// </summary>
        bool Torso(PoseFrame pose, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (pose == null || pose.Joints == null) return false;
            int[] ids =
            {
                (int)JointId.LeftShoulder,
                (int)JointId.RightShoulder,
                (int)JointId.LeftHip,
                (int)JointId.RightHip
            };

            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] >= pose.Joints.Length) continue;
                Joint joint = pose.Joints[ids[i]];
                if (joint.Score < jointThreshold) continue;
                x += joint.X;
                y += joint.Y;
                count++;
            }

            if (count == 0)
            {
                int start = (int)JointId.LeftShoulder;
                int end = pose.Joints.Length;
                if (end > (int)JointId.Count) end = (int)JointId.Count;
                for (int i = start; i < end; i++)
                {
                    Joint joint = pose.Joints[i];
                    if (joint.Score < jointThreshold) continue;
                    x += joint.X;
                    y += joint.Y;
                    count++;
                }
            }

            if (count == 0) return false;
            x /= count;
            y /= count;
            return true;
        }

        /// <summary>
        /// Keeps each skeleton in the same color while that person stays in frame.
        /// Matching uses the torso center for this camera session only. The centers
        /// are cleared when the camera stops. They are not names and they are not saved.
        /// </summary>
        void Stabilize()
        {
            bool[] taken = new bool[MaxPeople];
            int[] assigned = new int[MaxPeople];
            for (int i = 0; i < MaxPeople; i++) assigned[i] = -1;

            for (int slot = 0; slot < MaxPeople; slot++)
            {
                if (!slotUsed[slot]) continue;
                int best = -1;
                float bestDist = SlotMatchDistance;
                for (int n = 0; n < crowdCount && n < MaxPeople; n++)
                {
                    if (taken[n] || !HasBody(crowd[n])) continue;
                    float x;
                    float y;
                    if (!Torso(crowd[n], out x, out y)) continue;
                    float dx = x - slotX[slot];
                    float dy = y - slotY[slot];
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    best = n;
                }

                if (best >= 0)
                {
                    taken[best] = true;
                    assigned[slot] = best;
                }
            }

            for (int n = 0; n < crowdCount && n < MaxPeople; n++)
            {
                if (taken[n] || !HasBody(crowd[n])) continue;
                for (int slot = 0; slot < MaxPeople; slot++)
                {
                    if (assigned[slot] >= 0) continue;
                    assigned[slot] = n;
                    taken[n] = true;
                    break;
                }
            }

            for (int slot = 0; slot < MaxPeople; slot++)
            {
                int source = assigned[slot];
                if (source < 0)
                {
                    Blank(stable[slot]);
                    slotUsed[slot] = false;
                    slotX[slot] = 0f;
                    slotY[slot] = 0f;
                    continue;
                }

                CopyBody(crowd[source], stable[slot]);
                float x;
                float y;
                slotUsed[slot] = Torso(stable[slot], out x, out y);
                slotX[slot] = x;
                slotY[slot] = y;
            }
        }

        /// <summary>
        /// Counts skeletons that currently have a body on screen.
        /// </summary>
        int VisiblePeople()
        {
            int count = 0;
            for (int i = 0; i < MaxPeople; i++)
            {
                if (slotUsed[i] && HasBody(stable[i])) count++;
            }

            return count;
        }

        /// <summary>
        /// Clears poses and the in-memory torso match from the current session.
        /// </summary>
        void ClearCrowd()
        {
            crowdCount = 0;
            for (int i = 0; i < MaxPeople; i++)
            {
                Blank(crowd[i]);
                Blank(stable[i]);
                slotUsed[i] = false;
                slotX[i] = 0f;
                slotY[i] = 0f;
            }
        }

        /// <summary>
        /// Fills the camera buffers with empty pixels after each estimate.
        /// </summary>
        void WipePixels()
        {
            if (sensorPixels != null) Array.Clear(sensorPixels, 0, sensorPixels.Length);
            if (crop != null) Array.Clear(crop, 0, crop.Length);
        }

        /// <summary>
        /// Surfaces a model failure on the status line. The line does not include the picture.
        /// </summary>
        void NoteModelFailure()
        {
            if (engine == null || string.IsNullOrEmpty(engine.Failure)) return;
            SetStatus("Pose tracking stopped. " + engine.Failure);
        }

        /// <summary>
        /// Opens one camera at the size chosen in settings. The picture is read for MoveNet and is not assigned to a view.
        /// </summary>
        void OpenCamera(int index)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                knownDeviceCount = 0;
                ReleaseCamera();
                RefreshSettingLabels();
                SetStatus("No camera detected.");
                return;
            }

            if (index < 0 || index >= devices.Length) index = 0;
            cameraIndex = index;
            knownDeviceCount = devices.Length;
            savedCameraName = devices[index].name;
            SaveSettings();
            ReleaseCamera();
            try
            {
                webcam = new WebCamTexture(devices[index].name, requestWidth, requestHeight, requestFps);
                webcam.Play();
            }
            catch (Exception)
            {
                ReleaseCamera();
                SetStatus("The camera did not start. Allow the camera for Unity, then choose another one in Settings.");
                RefreshSettingLabels();
                return;
            }

            cameraOpenedAt = Time.unscaledTime;
            RefreshSettingLabels();
            RefreshStatus();
        }

        /// <summary>
        /// Stops the camera device and drops Unity's handle to it.
        /// </summary>
        void ReleaseCamera()
        {
            if (webcam == null) return;
            if (webcam.isPlaying) webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }

        /// <summary>
        /// Uses the camera saved in settings, or a front-facing camera when nothing is saved.
        /// </summary>
        int ChooseStartupCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return 0;
            if (!string.IsNullOrEmpty(savedCameraName))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name == savedCameraName) return i;
                }
            }

            return FrontCamera(devices);
        }

        /// <summary>
        /// Picks a front-facing camera when the device reports one.
        /// </summary>
        static int FrontCamera(WebCamDevice[] devices)
        {
            if (devices == null) return 0;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].isFrontFacing) return i;
            }

            return 0;
        }

        /// <summary>
        /// Notices a newly connected camera. A phone that appears while pairing is selected on its own.
        /// </summary>
        void ScanDevices()
        {
            if (Time.unscaledTime < nextDeviceScan) return;
            nextDeviceScan = Time.unscaledTime + 1.2f;
            WebCamDevice[] devices = WebCamTexture.devices;
            int count = devices == null ? 0 : devices.Length;
            if (count == knownDeviceCount) return;
            knownDeviceCount = count;
            RefreshSettingLabels();
            if (count == 0)
            {
                ReleaseCamera();
                return;
            }

            if (waitingForPhone || waitingForCable)
            {
                string connected = waitingForCable
                    ? "Cable camera connected. The picture stays on this device."
                    : "Phone camera connected. The picture stays on this device.";
                if (TryConnectPhone(devices, connected)) return;
                if (pairingStatus != null && settingsPairing != null && settingsPairing.activeSelf)
                    pairingStatus.text = "No phone camera yet. Follow the steps, then press Refresh.";
                if (cableStatus != null && settingsCable != null && settingsCable.activeSelf)
                    cableStatus.text = "Plug in the data cable and tap Trust. This page connects the iPhone when the computer lists it.";
            }

            if (webcam == null && !cameraHeldOff) OpenCamera(ChooseStartupCamera());
        }

        /// <summary>
        /// Builds the full-screen canvas, the six skeleton layers, and the privacy text.
        /// </summary>
        void BuildStage()
        {
            EnsureStageCamera();
            Font font = BuiltinFont();
            Sprite sprite = WhiteSprite();

            EnsureEventSystem();
            GameObject canvasObject = new GameObject("Camera_MoCap Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 100;
            canvas.targetDisplay = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            Image background = MakeImage("Background", canvasRect, sprite, Ink);
            Stretch(background.rectTransform);

            GameObject stageObject = new GameObject("Stage", typeof(RectTransform), typeof(Image));
            stageObject.transform.SetParent(canvasRect, false);
            stage = stageObject.GetComponent<RectTransform>();
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = new Vector2(0f, -12f);
            Image stageImage = stageObject.GetComponent<Image>();
            stageImage.sprite = sprite;
            stageImage.color = StageColor;
            stageImage.raycastTarget = false;

            int boneCount = Bones.Length / 2;
            int bodyCount = (int)JointId.Count - (int)JointId.LeftShoulder;
            lines = new Image[MaxPeople][];
            dots = new Image[MaxPeople][];
            for (int person = 0; person < MaxPeople; person++)
            {
                Color color = PersonColors[person];
                lines[person] = new Image[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    lines[person][i] = MakeImage("Bone", stage, sprite, color);
                    lines[person][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    lines[person][i].enabled = false;
                }

                dots[person] = new Image[bodyCount];
                Color jointColor = Color.Lerp(color, Color.white, 0.35f);
                for (int i = 0; i < bodyCount; i++)
                {
                    dots[person][i] = MakeImage("Joint", stage, sprite, jointColor);
                    dots[person][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    dots[person][i].enabled = false;
                }
            }

            Text title = MakeText("Title", canvasRect, font, "Camera", 28, TextAnchor.UpperLeft, Yellow);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(520f, 36f));
            Text privacy = MakeText("Privacy", canvasRect, font, PrivacyNotice, 16, TextAnchor.UpperLeft, Muted);
            privacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            privacy.verticalOverflow = VerticalWrapMode.Overflow;
            Place(privacy.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -68f), new Vector2(760f, 24f));
            Text hands = MakeText("Hands", canvasRect, font, HandNotice, 16, TextAnchor.UpperLeft, Muted);
            hands.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(hands.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -96f), new Vector2(860f, 24f));

            countText = MakeText("Count", canvasRect, font, "No one in frame", 28, TextAnchor.UpperRight, Cream);
            Place(countText.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(360f, 36f));

            statusText = MakeText("Status", canvasRect, font, "Starting…", 18, TextAnchor.LowerLeft, Cream);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 58f), new Vector2(1100f, 28f));
            Text help = MakeText("Help", canvasRect, font, "Tab settings    F mirror    Q rotate    Esc stop    Enter start", 16, TextAnchor.LowerLeft, Muted);
            Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 28f), new Vector2(1100f, 24f));
            BuildSettings(font, sprite);
        }

        /// <summary>
        /// Gives the Game view a display to render. The camera clears to black and never receives the webcam picture.
        /// </summary>
        void EnsureStageCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera existing = cameras[i];
                if (existing != null && existing.enabled && existing.targetDisplay == 0) return;
            }

            GameObject go = new GameObject("Stage Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.depth = -1;
            cam.targetDisplay = 0;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Sizes the square stage inside the screen margins.
        /// </summary>
        void LayoutStage()
        {
            if (stage == null || canvasRect == null) return;
            Rect area = canvasRect.rect;
            float side = Mathf.Min(area.width - 64f, area.height - 280f);
            if (side < 64f) side = 64f;
            if (Mathf.Abs(stage.sizeDelta.x - side) < 0.5f) return;
            stage.sizeDelta = new Vector2(side, side);
        }

        /// <summary>
        /// Draws every occupied body slot and hides the empty ones.
        /// </summary>
        void Redraw()
        {
            if (lines == null || stage == null) return;
            float width = stage.sizeDelta.x;
            float height = stage.sizeDelta.y;
            for (int person = 0; person < MaxPeople; person++)
            {
                bool show = slotUsed[person] && HasBody(stable[person]) && width > 2f && height > 2f;
                DrawPerson(person, show ? stable[person] : null, show, width, height);
            }
        }

        /// <summary>
        /// Draws one person's limbs. A bone that touches a facial index is skipped.
        /// </summary>
        void DrawPerson(int person, PoseFrame pose, bool show, float width, float height)
        {
            Image[] bones = lines[person];
            for (int i = 0; i < bones.Length; i++)
            {
                int aId = Bones[i * 2];
                int bId = Bones[i * 2 + 1];
                if (!show || aId <= (int)JointId.RightEar || bId <= (int)JointId.RightEar)
                {
                    bones[i].enabled = false;
                    continue;
                }

                Joint a = pose.Joints[aId];
                Joint b = pose.Joints[bId];
                if (a.Score < jointThreshold || b.Score < jointThreshold)
                {
                    bones[i].enabled = false;
                    continue;
                }

                Vector2 pa = ToPoint(a, width, height);
                Vector2 pb = ToPoint(b, width, height);
                Vector2 delta = pb - pa;
                float length = delta.magnitude;
                if (length < 2f)
                {
                    bones[i].enabled = false;
                    continue;
                }

                RectTransform boneRect = bones[i].rectTransform;
                boneRect.anchoredPosition = (pa + pb) * 0.5f;
                boneRect.sizeDelta = new Vector2(length, 4f);
                boneRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                bones[i].enabled = true;
            }

            Image[] joints = dots[person];
            int start = (int)JointId.LeftShoulder;
            for (int i = 0; i < joints.Length; i++)
            {
                int jointId = start + i;
                if (!show || jointId >= pose.Joints.Length || pose.Joints[jointId].Score < jointThreshold)
                {
                    joints[i].enabled = false;
                    continue;
                }

                bool hand = IsMeasuredHand(jointId);
                float size = hand ? 16f : 9f;
                RectTransform jointRect = joints[i].rectTransform;
                jointRect.sizeDelta = new Vector2(size, size);
                Vector2 point = ToPoint(pose.Joints[jointId], width, height);
                jointRect.anchoredPosition = point;
                joints[i].enabled = true;
            }
        }

        /// <summary>
        /// Maps a normalized joint into the stage, measured from the stage's lower left.
        /// </summary>
        static Vector2 ToPoint(Joint joint, float width, float height)
        {
            float x = Mathf.Clamp01(joint.X) * width;
            float y = (1f - Mathf.Clamp01(joint.Y)) * height;
            return new Vector2(x, y);
        }

        /// <summary>
        /// True for the wrist joints. Those are the hand positions MoveNet Lightning actually measures.
        /// </summary>
        static bool IsMeasuredHand(int jointId)
        {
            return jointId == (int)JointId.LeftWrist || jointId == (int)JointId.RightWrist;
        }

        /// <summary>
        /// Writes the status line under the stage.
        /// </summary>
        void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? "";
        }

        /// <summary>
        /// Writes how many bodies are on screen. The number is a count, not a name.
        /// </summary>
        void ShowCount(int visible)
        {
            if (countText == null) return;
            if (visible <= 0)
            {
                countText.text = "No one in frame";
                return;
            }

            countText.text = visible == 1 ? "1 person" : visible + " people";
        }

        /// <summary>
        /// Refreshes the model and camera state without including a device name.
        /// </summary>
        void RefreshStatus()
        {
            if (cameraHeldOff) return;
            if (engine == null)
            {
                if (!string.IsNullOrEmpty(modelError)) SetStatus(modelError);
                return;
            }

            if (webcam == null)
            {
                SetStatus("No camera detected.");
                return;
            }

            string line = modelLabel;
            if (knownDeviceCount > 0) line += "  ·  camera " + (cameraIndex + 1) + " of " + knownDeviceCount;
            if (!mirror) line += "  ·  mirror off";
            if (extraRotation != 0) line += "  ·  rotated " + extraRotation + "°";
            SetStatus(line);
        }

        /// <summary>
        /// Returns Unity's built-in UI font.
        /// </summary>
        static Font BuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        /// <summary>
        /// Creates the small white sprite used by the skeleton bars and the stage background.
        /// </summary>
        Sprite WhiteSprite()
        {
            whiteTex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            whiteTex.filterMode = FilterMode.Point;
            whiteTex.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            whiteTex.SetPixels(pixels);
            whiteTex.Apply(false, false);
            whiteSprite = Sprite.Create(whiteTex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            return whiteSprite;
        }

        /// <summary>
        /// Reads display choices saved on this machine. Nothing here is uploaded.
        /// </summary>
        void LoadSettings()
        {
            mirror = PlayerPrefs.GetInt(PrefMirror, 1) == 1;
            extraRotation = PlayerPrefs.GetInt(PrefRotation, 0);
            if (extraRotation != 0 && extraRotation != 90 && extraRotation != 180 && extraRotation != 270) extraRotation = 0;
            requestWidth = PlayerPrefs.GetInt(PrefWidth, 640);
            requestHeight = PlayerPrefs.GetInt(PrefHeight, 480);
            if (!KnownResolution(requestWidth, requestHeight))
            {
                requestWidth = 640;
                requestHeight = 480;
            }

            requestFps = PlayerPrefs.GetInt(PrefFps, 30);
            if (requestFps != 15 && requestFps != 30 && requestFps != 60) requestFps = 30;
            peopleLimit = Mathf.Clamp(PlayerPrefs.GetInt(PrefPeople, MaxPeople), 1, MaxPeople);
            jointThreshold = PlayerPrefs.GetFloat(PrefThreshold, 0.3f);
            if (jointThreshold < 0.15f || jointThreshold > 0.6f) jointThreshold = 0.3f;
            savedCameraName = PlayerPrefs.GetString(PrefCamera, "");
        }

        /// <summary>
        /// Stores the current display choices in Unity PlayerPrefs on this machine.
        /// </summary>
        void SaveSettings()
        {
            PlayerPrefs.SetInt(PrefMirror, mirror ? 1 : 0);
            PlayerPrefs.SetInt(PrefRotation, extraRotation);
            PlayerPrefs.SetInt(PrefWidth, requestWidth);
            PlayerPrefs.SetInt(PrefHeight, requestHeight);
            PlayerPrefs.SetInt(PrefFps, requestFps);
            PlayerPrefs.SetInt(PrefPeople, peopleLimit);
            PlayerPrefs.SetFloat(PrefThreshold, jointThreshold);
            PlayerPrefs.SetString(PrefCamera, savedCameraName ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// True for the three resolutions the settings menu offers.
        /// </summary>
        static bool KnownResolution(int width, int height)
        {
            return (width == 640 && height == 480) || (width == 1280 && height == 720) || (width == 1920 && height == 1080);
        }

        /// <summary>
        /// Creates the event system the settings buttons need.
        /// </summary>
        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// Builds a sharp black-and-yellow settings card on its own pixel-perfect canvas.
        /// </summary>
        void BuildSettings(Font font, Sprite sprite)
        {
            int u = Screen.height >= 1700 ? 2 : 1;
            GameObject canvasObject = new GameObject("Settings Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 200;
            canvas.targetDisplay = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            Button open = MakeButton("Open Settings", root, sprite, font, "Settings", delegate { SetSettingsOpen(true); });
            Text openLabel = open.GetComponentInChildren<Text>();
            openLabel.fontSize = 16 * u;
            openLabel.color = Yellow;
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-28f * u, -28f * u);
            openRect.sizeDelta = new Vector2(120f * u, 36f * u);

            settingsRoot = new GameObject("Settings", typeof(RectTransform));
            settingsRoot.transform.SetParent(root, false);
            Stretch(settingsRoot.GetComponent<RectTransform>());

            Image dim = MakeImage("Dim", settingsRoot.transform, sprite, new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(delegate { SetSettingsOpen(false); });

            float panelW = 460f * u;
            float panelH = 600f * u;
            Image frame = MakeImage("Frame", settingsRoot.transform, sprite, Yellow);
            frame.raycastTarget = false;
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(panelW + (2f * u), panelH + (2f * u));

            Image panel = MakeImage("Panel", settingsRoot.transform, sprite, Ink);
            panel.raycastTarget = true;
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(panelW, panelH);

            Button close = MakeButton("Close", panel.transform, sprite, font, "Close", delegate { SetSettingsOpen(false); });
            Text closeLabel = close.GetComponentInChildren<Text>();
            closeLabel.fontSize = 15 * u;
            closeLabel.color = Muted;
            closeLabel.alignment = TextAnchor.MiddleRight;
            PlaceTop(close.GetComponent<RectTransform>(), panelW - (108f * u), 16f * u, 84f * u, 32f * u);

            settingsMain = new GameObject("Main", typeof(RectTransform));
            settingsMain.transform.SetParent(panel.transform, false);
            Stretch(settingsMain.GetComponent<RectTransform>());
            BuildSettingsMain(settingsMain.transform, font, sprite, u);
            BuildPairingPage(panel.transform, font, sprite, u);
            BuildCablePage(panel.transform, font, sprite, u);

            settingsRoot.SetActive(false);
            RefreshSettingLabels();
        }

        /// <summary>
        /// Lays out the camera and capture rows. Pairing steps are not on this page.
        /// </summary>
        void BuildSettingsMain(Transform page, Font font, Sprite sprite, int u)
        {
            Text heading = MakeText("Heading", page, font, "Settings", 22 * u, TextAnchor.MiddleLeft, Yellow);
            PlaceTop(heading.rectTransform, 28f * u, 18f * u, 240f * u, 32f * u);
            Rule(page, sprite, 28f * u, 58f * u, 404f * u);

            Text cameraHeading = MakeText("Camera Heading", page, font, "CAMERA", 12 * u, TextAnchor.MiddleLeft, Muted);
            PlaceTop(cameraHeading.rectTransform, 28f * u, 72f * u, 404f * u, 18f * u);
            settingsCameraText = MakeText("Camera Value", page, font, "No camera yet", 18 * u, TextAnchor.MiddleLeft, Cream);
            PlaceTop(settingsCameraText.rectTransform, 28f * u, 94f * u, 404f * u, 28f * u);

            AddTextButton(page, sprite, font, "Previous", 28f * u, 136f * u, 110f * u, 32f * u, 15 * u, delegate { StepCamera(-1); });
            AddTextButton(page, sprite, font, "Next", 150f * u, 136f * u, 80f * u, 32f * u, 15 * u, delegate { StepCamera(1); });
            AddTextButton(page, sprite, font, "Refresh", 250f * u, 136f * u, 110f * u, 32f * u, 15 * u, RefreshCameras);

            AddTextButton(page, sprite, font, "Pair phone", 28f * u, 180f * u, 150f * u, 32f * u, 16 * u, ShowPairing);
            AddTextButton(page, sprite, font, "USB cable", 190f * u, 180f * u, 150f * u, 32f * u, 16 * u, ShowCable);
            Rule(page, sprite, 28f * u, 224f * u, 404f * u);

            settingsResolutionText = AddSettingRow(page, sprite, font, "Resolution", 240f * u, u, CycleResolution);
            settingsFpsText = AddSettingRow(page, sprite, font, "Frame rate", 288f * u, u, CycleFrameRate);
            settingsMirrorText = AddSettingRow(page, sprite, font, "Mirror", 336f * u, u, ToggleMirror);
            settingsRotationText = AddSettingRow(page, sprite, font, "Rotate", 384f * u, u, CycleRotation);
            settingsPeopleText = AddSettingRow(page, sprite, font, "People", 432f * u, u, CyclePeople);
            settingsDetailText = AddSettingRow(page, sprite, font, "Tracking", 480f * u, u, CycleDetail);
            settingsPowerText = AddSettingRow(page, sprite, font, "Camera", 528f * u, u, TogglePower);
        }

        /// <summary>
        /// Builds the pairing page. It stays hidden until Pair phone is clicked.
        /// </summary>
        void BuildPairingPage(Transform panel, Font font, Sprite sprite, int u)
        {
            settingsPairing = new GameObject("Pairing", typeof(RectTransform));
            settingsPairing.transform.SetParent(panel, false);
            Stretch(settingsPairing.GetComponent<RectTransform>());

            AddTextButton(settingsPairing.transform, sprite, font, "Back", 28f * u, 18f * u, 72f * u, 32f * u, 15 * u, HidePairing);
            Text title = MakeText("Pair Title", settingsPairing.transform, font, "Pair phone", 22 * u, TextAnchor.MiddleLeft, Yellow);
            PlaceTop(title.rectTransform, 112f * u, 18f * u, 240f * u, 32f * u);
            pairingStatus = MakeText("Pair Status", settingsPairing.transform, font, "", 14 * u, TextAnchor.UpperLeft, Muted);
            pairingStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            pairingStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(pairingStatus.rectTransform, 28f * u, 58f * u, 404f * u, 36f * u);
            AddTextButton(settingsPairing.transform, sprite, font, "Refresh", 28f * u, 98f * u, 120f * u, 32f * u, 15 * u, RefreshCameras);

            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(settingsPairing.transform, false);
            PlaceTop(scrollObject.GetComponent<RectTransform>(), 28f * u, 142f * u, 404f * u, 420f * u);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, PairingGuide, 15 * u, TextAnchor.UpperLeft, Cream);
            guide.horizontalOverflow = HorizontalWrapMode.Wrap;
            guide.verticalOverflow = VerticalWrapMode.Overflow;
            guide.lineSpacing = 1.08f;
            RectTransform guideRect = guide.rectTransform;
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(1f, 1f);
            guideRect.pivot = new Vector2(0.5f, 1f);
            guideRect.anchoredPosition = Vector2.zero;
            guideRect.sizeDelta = new Vector2(-4f * u, 0f);
            ContentSizeFitter fitter = guide.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = guideRect;
            settingsPairing.SetActive(false);
        }

        /// <summary>
        /// Builds the USB setup. It stays hidden until USB cable is clicked.
        /// </summary>
        void BuildCablePage(Transform panel, Font font, Sprite sprite, int u)
        {
            settingsCable = new GameObject("Cable", typeof(RectTransform));
            settingsCable.transform.SetParent(panel, false);
            Stretch(settingsCable.GetComponent<RectTransform>());

            AddTextButton(settingsCable.transform, sprite, font, "Back", 28f * u, 18f * u, 72f * u, 32f * u, 15 * u, HideCable);
            Text title = MakeText("Cable Title", settingsCable.transform, font, "USB cable", 22 * u, TextAnchor.MiddleLeft, Yellow);
            PlaceTop(title.rectTransform, 112f * u, 18f * u, 240f * u, 32f * u);
            cableStatus = MakeText("Cable Status", settingsCable.transform, font, "", 14 * u, TextAnchor.UpperLeft, Muted);
            cableStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            cableStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(cableStatus.rectTransform, 28f * u, 58f * u, 404f * u, 36f * u);
            AddTextButton(settingsCable.transform, sprite, font, "Check cable", 28f * u, 98f * u, 140f * u, 32f * u, 15 * u, RefreshCameras);
            AddTextButton(settingsCable.transform, sprite, font, "Apple driver", 176f * u, 98f * u, 140f * u, 32f * u, 15 * u, OpenAppleDriver);
            AddTextButton(settingsCable.transform, sprite, font, "Iriun", 324f * u, 98f * u, 90f * u, 32f * u, 15 * u, OpenIriun);
            BuildGuideScroll(settingsCable.transform, font, sprite, u, CableGuide, 142f * u, 420f * u);
            settingsCable.SetActive(false);
        }

        /// <summary>
        /// Adds the scrolling steps used by the pairing and cable pages.
        /// </summary>
        static void BuildGuideScroll(Transform parent, Font font, Sprite sprite, int u, string body, float y, float height)
        {
            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            PlaceTop(scrollObject.GetComponent<RectTransform>(), 28f * u, y, 404f * u, height);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, body, 15 * u, TextAnchor.UpperLeft, Cream);
            guide.horizontalOverflow = HorizontalWrapMode.Wrap;
            guide.verticalOverflow = VerticalWrapMode.Overflow;
            guide.lineSpacing = 1.08f;
            RectTransform guideRect = guide.rectTransform;
            guideRect.anchorMin = new Vector2(0f, 1f);
            guideRect.anchorMax = new Vector2(1f, 1f);
            guideRect.pivot = new Vector2(0.5f, 1f);
            guideRect.anchoredPosition = Vector2.zero;
            guideRect.sizeDelta = new Vector2(-4f * u, 0f);
            ContentSizeFitter fitter = guide.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = guideRect;
        }

        /// <summary>
        /// Draws a one-pixel yellow hairline.
        /// </summary>
        static void Rule(Transform parent, Sprite sprite, float x, float y, float width)
        {
            Image line = MakeImage("Rule", parent, sprite, new Color(1f, 0.82f, 0.12f, 0.45f));
            PlaceTop(line.rectTransform, x, y, width, 1f);
        }

        /// <summary>
        /// Adds a text button with no filled background.
        /// </summary>
        static void AddTextButton(Transform parent, Sprite sprite, Font font, string label, float x, float y, float width, float height, int fontSize, UnityAction action)
        {
            Button button = MakeButton(label, parent, sprite, font, label, action);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.color = Yellow;
            text.alignment = TextAnchor.MiddleLeft;
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
        }

        /// <summary>
        /// Adds one settings row and returns the yellow value label.
        /// </summary>
        static Text AddSettingRow(Transform parent, Sprite sprite, Font font, string caption, float y, int u, UnityAction action)
        {
            Button button = MakeButton(caption, parent, sprite, font, "", action);
            PlaceTop(button.GetComponent<RectTransform>(), 20f * u, y, 420f * u, 40f * u);
            Text name = MakeText("Name", button.transform, font, caption, 16 * u, TextAnchor.MiddleLeft, Cream);
            PlaceTop(name.rectTransform, 8f * u, 0f, 200f * u, 40f * u);
            Text value = MakeText("Value", button.transform, font, "", 16 * u, TextAnchor.MiddleRight, Yellow);
            PlaceTop(value.rectTransform, 180f * u, 0f, 228f * u, 40f * u);
            return value;
        }

        /// <summary>
        /// Shows or hides the settings card.
        /// </summary>
        void SetSettingsOpen(bool open)
        {
            if (settingsRoot == null) return;
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            settingsRoot.SetActive(open);
            if (open) RefreshSettingLabels();
        }

        /// <summary>
        /// Opens the pairing steps and watches for a phone webcam.
        /// </summary>
        void ShowPairing()
        {
            if (settingsMain != null) settingsMain.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsPairing != null) settingsPairing.SetActive(true);
            waitingForPhone = true;
            waitingForCable = false;
            WebCamDevice[] devices = WebCamTexture.devices;
            knownDeviceCount = devices == null ? 0 : devices.Length;
            if (TryConnectPhone(devices, "Phone camera connected. The picture stays on this device.")) return;
            if (pairingStatus != null) pairingStatus.text = "No phone camera yet. Follow the steps, then press Refresh.";
            SetStatus("Waiting for the phone. Follow the pairing steps, then press Refresh.");
        }

        /// <summary>
        /// Opens the cable setup and watches for an iPhone plugged in with a data cable.
        /// </summary>
        void ShowCable()
        {
            if (settingsMain != null) settingsMain.SetActive(false);
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsCable != null) settingsCable.SetActive(true);
            waitingForCable = true;
            waitingForPhone = false;
            WebCamDevice[] devices = WebCamTexture.devices;
            knownDeviceCount = devices == null ? 0 : devices.Length;
            if (TryConnectPhone(devices, "Cable camera connected. The picture stays on this device.")) return;
            if (cableStatus != null) cableStatus.text = "Plug in the data cable and tap Trust. This page connects the iPhone when the computer lists it.";
            SetStatus("Waiting for the USB camera.");
        }

        /// <summary>
        /// Returns from the cable setup to the settings rows.
        /// </summary>
        void HideCable()
        {
            waitingForCable = false;
            if (settingsCable != null) settingsCable.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            RefreshSettingLabels();
        }

        /// <summary>
        /// Selects a phone webcam and writes the connected line. Returns false when none is listed.
        /// </summary>
        bool TryConnectPhone(WebCamDevice[] devices, string connectedLine)
        {
            int phone = FindPhone(devices);
            if (phone < 0) return false;
            waitingForPhone = false;
            waitingForCable = false;
            cameraHeldOff = false;
            if (pairingStatus != null) pairingStatus.text = connectedLine;
            if (cableStatus != null) cableStatus.text = connectedLine;
            if (engine == null)
            {
                pendingCamera = phone;
                if (!resuming) StartCoroutine(Resume());
                return true;
            }

            OpenCamera(phone);
            SetStatus(connectedLine);
            return true;
        }

        /// <summary>
        /// Opens the Apple Devices install page. Windows needs it before an iPhone USB cable is more than power.
        /// </summary>
        void OpenAppleDriver()
        {
            Application.OpenURL(AppleDriverUrl);
        }

        /// <summary>
        /// Opens the Iriun Webcam site. The Windows app is what publishes the plugged-in iPhone as a webcam.
        /// </summary>
        void OpenIriun()
        {
            Application.OpenURL(IriunUrl);
        }

        /// <summary>
        /// Returns from the pairing steps to the settings rows.
        /// </summary>
        void HidePairing()
        {
            if (settingsPairing != null) settingsPairing.SetActive(false);
            if (settingsMain != null) settingsMain.SetActive(true);
            RefreshSettingLabels();
        }

        /// <summary>
        /// Rewrites the settings labels from the current choices.
        /// </summary>
        void RefreshSettingLabels()
        {
            if (settingsCameraText == null) return;
            settingsCameraText.text = CurrentCameraLabel();
            settingsResolutionText.text = requestWidth + " × " + requestHeight;
            settingsFpsText.text = requestFps.ToString();
            settingsMirrorText.text = mirror ? "On" : "Off";
            settingsRotationText.text = extraRotation + "°";
            settingsPeopleText.text = peopleLimit.ToString();
            settingsDetailText.text = DetailLabel();
            settingsPowerText.text = cameraHeldOff ? "Off" : "On";
        }

        /// <summary>
        /// Names the camera selected in settings. The main stage still hides the device name.
        /// </summary>
        string CurrentCameraLabel()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0) return "No camera yet";
            int index = cameraIndex;
            if (index < 0 || index >= devices.Length) index = 0;
            string name = ShortName(devices[index].name);
            if (devices[index].isFrontFacing) name += "  ·  front";
            return name + "   " + (index + 1) + " of " + devices.Length;
        }

        /// <summary>
        /// Keeps a long device name inside the settings card.
        /// </summary>
        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Camera";
            if (name.Length <= 42) return name;
            return name.Substring(0, 39) + "...";
        }

        /// <summary>
        /// Describes the joint-confidence choice.
        /// </summary>
        string DetailLabel()
        {
            if (jointThreshold < 0.25f) return "Relaxed";
            if (jointThreshold < 0.4f) return "Normal";
            return "Firm";
        }

        /// <summary>
        /// Moves to the previous or next camera the operating system exposes.
        /// </summary>
        void StepCamera(int delta)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            int count = devices == null ? 0 : devices.Length;
            if (count == 0)
            {
                SetStatus("No camera yet. Connect a phone, then press Refresh.");
                RefreshSettingLabels();
                return;
            }

            int next = cameraIndex + delta;
            if (next < 0) next = count - 1;
            if (next >= count) next = 0;
            cameraHeldOff = false;
            waitingForPhone = false;
            waitingForCable = false;
            if (engine == null)
            {
                pendingCamera = next;
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            OpenCamera(next);
        }

        /// <summary>
        /// Asks the operating system for cameras again and updates the settings card.
        /// </summary>
        void RefreshCameras()
        {
            knownDeviceCount = -1;
            WebCamDevice[] devices = WebCamTexture.devices;
            knownDeviceCount = devices == null ? 0 : devices.Length;
            RefreshSettingLabels();
            bool pairingOpen = settingsPairing != null && settingsPairing.activeSelf;
            bool cableOpen = settingsCable != null && settingsCable.activeSelf;
            if (pairingOpen) waitingForPhone = true;
            if (cableOpen) waitingForCable = true;
            if (!waitingForPhone && !waitingForCable)
            {
                SetStatus(knownDeviceCount == 0 ? "No camera yet." : "Camera list refreshed.");
                return;
            }

            string connected = (cableOpen || waitingForCable)
                ? "Cable camera connected. The picture stays on this device."
                : "Phone camera connected. The picture stays on this device.";
            if (TryConnectPhone(devices, connected)) return;

            if (cableOpen || waitingForCable)
            {
                if (cableStatus != null) cableStatus.text = "No cable camera yet. Plug in the data cable, tap Trust, then press Check cable.";
                SetStatus("Waiting for the USB camera. Leave the cable connected, then press Check cable.");
            }

            if (pairingOpen || waitingForPhone)
            {
                waitingForPhone = true;
                if (pairingStatus != null) pairingStatus.text = "No phone camera yet. Follow the steps, then press Refresh.";
                if (!cableOpen) SetStatus("Still waiting for the phone. Leave the webcam app running, then press Refresh.");
            }
        }

        /// <summary>
        /// Finds a device whose name looks like a phone or a phone webcam app.
        /// </summary>
        static int FindPhone(WebCamDevice[] devices)
        {
            if (devices == null) return -1;
            for (int i = 0; i < devices.Length; i++)
            {
                if (LooksLikePhone(devices[i])) return i;
            }

            return -1;
        }

        /// <summary>
        /// True when a device name matches a phone, Continuity Camera, or a common phone webcam app.
        /// </summary>
        static bool LooksLikePhone(WebCamDevice device)
        {
            string name = device.name == null ? "" : device.name.ToLowerInvariant();
            if (name.IndexOf("iphone") >= 0 || name.IndexOf("ipad") >= 0 || name.IndexOf("continuity") >= 0) return true;
            if (name.IndexOf("android") >= 0 || name.IndexOf("droidcam") >= 0 || name.IndexOf("iriun") >= 0) return true;
            if (name.IndexOf("epoccam") >= 0 || name.IndexOf("camo") >= 0 || name.IndexOf("galaxy") >= 0) return true;
            if (name.IndexOf("pixel") >= 0 || name.IndexOf("phone") >= 0) return true;
            return false;
        }

        /// <summary>
        /// Switches between 640×480, 1280×720, and 1920×1080, then reopens the camera.
        /// </summary>
        void CycleResolution()
        {
            if (requestWidth == 640)
            {
                requestWidth = 1280;
                requestHeight = 720;
            }
            else if (requestWidth == 1280)
            {
                requestWidth = 1920;
                requestHeight = 1080;
            }
            else
            {
                requestWidth = 640;
                requestHeight = 480;
            }

            SaveSettings();
            ReopenCamera();
        }

        /// <summary>
        /// Switches the requested frame rate between 15, 30, and 60, then reopens the camera.
        /// </summary>
        void CycleFrameRate()
        {
            if (requestFps == 15) requestFps = 30;
            else if (requestFps == 30) requestFps = 60;
            else requestFps = 15;
            SaveSettings();
            ReopenCamera();
        }

        /// <summary>
        /// Flips the selfie mirror and keeps that choice.
        /// </summary>
        void ToggleMirror()
        {
            mirror = !mirror;
            SaveSettings();
            RefreshSettingLabels();
            RefreshStatus();
        }

        /// <summary>
        /// Turns the camera crop by another 90 degrees and keeps that choice.
        /// </summary>
        void CycleRotation()
        {
            extraRotation = (extraRotation + 90) % 360;
            SaveSettings();
            RefreshSettingLabels();
            RefreshStatus();
        }

        /// <summary>
        /// Cycles how many people MoveNet is asked to return, from 1 through 6.
        /// </summary>
        void CyclePeople()
        {
            peopleLimit++;
            if (peopleLimit > MaxPeople) peopleLimit = 1;
            for (int i = peopleLimit; i < MaxPeople; i++)
            {
                Blank(stable[i]);
                slotUsed[i] = false;
            }

            SaveSettings();
            RefreshSettingLabels();
        }

        /// <summary>
        /// Cycles how confident a joint must be before it is drawn.
        /// </summary>
        void CycleDetail()
        {
            if (jointThreshold < 0.25f) jointThreshold = 0.3f;
            else if (jointThreshold < 0.4f) jointThreshold = 0.45f;
            else jointThreshold = 0.2f;
            SaveSettings();
            RefreshSettingLabels();
        }

        /// <summary>
        /// Turns capture off, or starts it again with the current settings.
        /// </summary>
        void TogglePower()
        {
            if (cameraHeldOff)
            {
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            StopCapture("Camera off. Turn it on in Settings, or press Enter.");
            RefreshSettingLabels();
        }

        /// <summary>
        /// Applies a resolution or frame-rate change to the open camera.
        /// </summary>
        void ReopenCamera()
        {
            RefreshSettingLabels();
            if (cameraHeldOff || engine == null) return;
            OpenCamera(cameraIndex);
        }

        /// <summary>
        /// Places a control from the top-left of its parent.
        /// </summary>
        static void PlaceTop(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// Creates a clickable settings button. The label does not take the click.
        /// </summary>
        static Button MakeButton(string name, Transform parent, Sprite sprite, Font font, string label, UnityAction action)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = true;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 0.82f, 0.12f, 0.12f);
            colors.pressedColor = new Color(1f, 0.82f, 0.12f, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            Text text = MakeText("Label", go.transform, font, label, 16, TextAnchor.MiddleCenter, Yellow);
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>
        /// Creates a non-interactive image parented to the stage or the canvas.
        /// </summary>
        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        /// <summary>
        /// Creates a non-interactive text label.
        /// </summary>
        static Text MakeText(string name, Transform parent, Font font, string message, int size, TextAnchor anchor, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.text = message;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Stretches a rect over its parent.
        /// </summary>
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Places a label by a single anchor, a pixel position, and a size.
        /// </summary>
        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
