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

        const int CropSize = 192;
        const float PoseInterval = 0.07f;
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
            new Color(0.89f, 0.45f, 0.00f, 1f),
            new Color(0.76f, 0.52f, 0.04f, 1f),
            new Color(0.58f, 0.38f, 0.02f, 1f),
            new Color(0.93f, 0.60f, 0.10f, 1f),
            new Color(0.50f, 0.36f, 0.06f, 1f),
            new Color(0.78f, 0.34f, 0.04f, 1f)
        };

        static readonly Color Paper = new Color(0.973f, 0.976f, 0.980f, 1f);
        static readonly Color Ink = new Color(0.125f, 0.129f, 0.141f, 1f);
        static readonly Color StageColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color Cream = new Color(0.125f, 0.129f, 0.141f, 1f);
        static readonly Color Yellow = new Color(0.976f, 0.671f, 0.000f, 1f);
        static readonly Color YellowDeep = new Color(0.722f, 0.408f, 0.000f, 1f);
        static readonly Color Muted = new Color(0.373f, 0.388f, 0.408f, 1f);
        static readonly Color Line = new Color(0.886f, 0.910f, 0.929f, 1f);
        static readonly Color Hover = new Color(0.945f, 0.949f, 0.957f, 1f);

        readonly PoseFrame[] crowd = new PoseFrame[MaxPeople];
        readonly PoseFrame[] stable = new PoseFrame[MaxPeople];
        readonly bool[] slotUsed = new bool[MaxPeople];
        readonly float[] slotX = new float[MaxPeople];
        readonly float[] slotY = new float[MaxPeople];

        PoseEngine engine;
        WebCamTexture webcam;
        WebCamDevice[] cachedDevices = new WebCamDevice[0];
        readonly int[] tryW = new int[3];
        readonly int[] tryH = new int[3];
        readonly int[] tryF = new int[3];
        readonly bool[] slotTaken = new bool[MaxPeople];
        readonly int[] slotAssigned = new int[MaxPeople];
        RectTransform canvasRect;
        RectTransform stage;
        Image[][] lines;
        Image[][] dots;
        Text countText;
        Text statusText;
        Texture2D whiteTex;
        Sprite whiteSprite;
        Texture2D roundTex;
        Sprite roundSprite;
        RectTransform settingsCard;
        RectTransform settingsShadow;
        int settingsSeenW = -1;
        int settingsSeenH = -1;
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
        float nextPose;
        bool mirror = true;
        bool cameraReady;
        bool poseDirty = true;
        bool reportedGiveUp;
        int tryIndex;
        int tryCount;
        int modelInputSize;
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
        Transform deviceList;
        string deviceSig = "";
        Font settingsFont;
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
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until && ReadDevices(true).Length == 0)
                yield return new WaitForSecondsRealtime(0.4f);
            OpenCamera(ChooseStartupCamera());
        }

        /// <summary>
        /// Polls keys, estimates the current frame, and redraws body skeletons.
        /// </summary>
        void Update()
        {
            if (!ready) return;
            if (LayoutStage()) poseDirty = true;
            LayoutSettingsCard();
            PollKeys();
            WatchCamera();
            if (!cameraHeldOff && engine != null) ScanDevices();

            if (webcam != null && webcam.isPlaying && webcam.width > 16 && Time.unscaledTime >= nextPose)
            {
                CaptureAndEstimate();
                nextPose = Time.unscaledTime + PoseInterval;
                poseDirty = true;
            }
            else if (webcam == null && poseUntil > 0f && Time.unscaledTime > poseUntil)
            {
                poseUntil = 0f;
                ClearCrowd();
                ShowCount(0);
                poseDirty = true;
            }

            if (!poseDirty) return;
            poseDirty = false;
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
            if (roundSprite != null) Destroy(roundSprite);
            if (roundTex != null) Destroy(roundTex);
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
                if (cachedDevices == null || cachedDevices.Length == 0) ReadDevices(true);
                int count = cachedDevices == null ? 0 : cachedDevices.Length;
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
            modelInputSize = 0;
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
                int modelSize = ModelSize();
                FillCrop(sensorPixels, sw, sh, modelSize);
                if (engine != null)
                {
                    crowdCount = engine.TryRead(crop, modelSize, crowd, peopleLimit);
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
                if (crop != null) Array.Clear(crop, 0, crop.Length);
            }
        }

        /// <summary>
        /// Uses the model edge when MoveNet has reported one. 192 is Lightning's usual input.
        /// </summary>
        int ModelSize()
        {
            if (modelInputSize >= 96) return modelInputSize;
            int size = engine == null ? 0 : engine.InputSize;
            if (size < 96 || size > 320) size = CropSize;
            modelInputSize = size;
            return size;
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
        void FillCrop(Color32[] source, int srcWidth, int srcHeight, int size)
        {
            if (size < 32) size = CropSize;
            if (crop == null || crop.Length != size * size)
            {
                crop = new Color32[size * size];
            }

            int rotation = webcam.videoRotationAngle + extraRotation;
            bool vertical = webcam.videoVerticallyMirrored;
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    int bx;
                    int by;
                    FrameOrient.Map(x, y, size, srcWidth, srcHeight, rotation, vertical, mirror, out bx, out by);
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
            for (int i = 0; i < MaxPeople; i++)
            {
                slotTaken[i] = false;
                slotAssigned[i] = -1;
            }

            for (int slot = 0; slot < MaxPeople; slot++)
            {
                if (!slotUsed[slot]) continue;
                int best = -1;
                float bestDist = SlotMatchDistance;
                for (int n = 0; n < crowdCount && n < MaxPeople; n++)
                {
                    if (slotTaken[n] || !HasBody(crowd[n])) continue;
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
                    slotTaken[best] = true;
                    slotAssigned[slot] = best;
                }
            }

            for (int n = 0; n < crowdCount && n < MaxPeople; n++)
            {
                if (slotTaken[n] || !HasBody(crowd[n])) continue;
                for (int slot = 0; slot < MaxPeople; slot++)
                {
                    if (slotAssigned[slot] >= 0) continue;
                    slotAssigned[slot] = n;
                    slotTaken[n] = true;
                    break;
                }
            }

            for (int slot = 0; slot < MaxPeople; slot++)
            {
                int source = slotAssigned[slot];
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
        /// Opens one camera. The picture is read for MoveNet and is not assigned to a view.
        /// A size the device rejects is followed by 640×480, then by the device's own size.
        /// </summary>
        void OpenCamera(int index)
        {
            EnsureDeviceCache(cachedDevices == null || cachedDevices.Length == 0);
            if (cachedDevices.Length == 0)
            {
                ReleaseCamera();
                cameraReady = false;
                RefreshSettingLabels();
                SetStatus("No camera is listed. On Windows, allow Unity under Privacy and Camera, then press Refresh.");
                return;
            }

            if (index < 0 || index >= cachedDevices.Length) index = 0;
            cameraIndex = index;
            cameraHeldOff = false;
            BuildTries();
            tryIndex = 0;
            PlayTry();
            RefreshSettingLabels();
        }

        /// <summary>
        /// Starts the current fallback size. Asking WebCamTexture for an unsupported mode never throws. It just sends no frames.
        /// </summary>
        void PlayTry()
        {
            reportedGiveUp = false;
            cameraReady = false;
            ReleaseCamera();
            if (cachedDevices == null || cameraIndex < 0 || cameraIndex >= cachedDevices.Length) return;
            string name = cachedDevices[cameraIndex].name;
            int w = tryW[tryIndex];
            int h = tryH[tryIndex];
            int fps = tryF[tryIndex];
            try
            {
                webcam = w <= 0 ? new WebCamTexture(name) : new WebCamTexture(name, w, h, fps);
                webcam.Play();
            }
            catch (Exception ex)
            {
                ReleaseCamera();
                SetStatus("The camera did not start. " + ex.Message);
                RefreshSettingLabels();
                return;
            }

            cameraOpenedAt = Time.unscaledTime;
            SetStatus(w <= 0 ? "Starting camera at its own size…" : "Starting camera at " + w + "×" + h + "…");
        }

        /// <summary>
        /// Records the sizes to try, skipping duplicates.
        /// </summary>
        void BuildTries()
        {
            tryCount = 0;
            AddTry(requestWidth, requestHeight, requestWidth <= 0 ? 0 : requestFps);
            if (requestWidth != 640 || requestHeight != 480) AddTry(640, 480, 30);
            if (requestWidth != 0 || requestHeight != 0) AddTry(0, 0, 0);
            if (tryCount == 0) AddTry(0, 0, 0);
        }

        /// <summary>
        /// Appends one camera mode when it is not already in the list.
        /// </summary>
        void AddTry(int width, int height, int fps)
        {
            if (tryCount >= tryW.Length) return;
            for (int i = 0; i < tryCount; i++)
            {
                if (tryW[i] == width && tryH[i] == height) return;
            }

            tryW[tryCount] = width;
            tryH[tryCount] = height;
            tryF[tryCount] = fps;
            tryCount++;
        }

        /// <summary>
        /// Advances to the next size when the current one stays blank. A live camera is left alone.
        /// </summary>
        void WatchCamera()
        {
            if (cameraHeldOff || webcam == null) return;
            if (webcam.width > 16 && webcam.height > 16)
            {
                if (cameraReady) return;
                cameraReady = true;
                if (cachedDevices != null && cameraIndex >= 0 && cameraIndex < cachedDevices.Length)
                    savedCameraName = cachedDevices[cameraIndex].name;
                SaveSettings();
                RefreshStatus();
                RefreshSettingLabels();
                return;
            }

            if (Time.unscaledTime < cameraOpenedAt + 2.2f) return;
            if (tryIndex + 1 < tryCount)
            {
                tryIndex++;
                PlayTry();
                return;
            }

            if (reportedGiveUp) return;
            reportedGiveUp = true;
            SetStatus("The camera is listed, but no frames arrived. Set Resolution to Auto, allow Unity under Privacy and Camera, then press Refresh.");
        }

        /// <summary>
        /// Reads the operating-system camera list. A playing camera is not queried again, because that read drops the stream on Windows.
        /// </summary>
        WebCamDevice[] ReadDevices(bool force)
        {
            if (!force && cachedDevices != null && cachedDevices.Length > 0) return cachedDevices;
            WebCamDevice[] found = WebCamTexture.devices;
            cachedDevices = found ?? new WebCamDevice[0];
            knownDeviceCount = cachedDevices.Length;
            deviceSig = "";
            return cachedDevices;
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
            WebCamDevice[] devices = ReadDevices(false);
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
        /// Notices a camera that appears while none is open. A playing camera is not polled, because that drops the stream on Windows.
        /// </summary>
        void ScanDevices()
        {
            if (webcam != null) return;
            if (Time.unscaledTime < nextDeviceScan) return;
            nextDeviceScan = Time.unscaledTime + 2f;
            int previous = knownDeviceCount;
            WebCamDevice[] devices = ReadDevices(true);
            int count = devices.Length;
            if (count == previous) return;
            RefreshSettingLabels();
            if (count == 0) return;

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
            RoundedSprite();

            EnsureEventSystem();
            GameObject canvasObject = new GameObject("Camera_MoCap Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;
            canvas.targetDisplay = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            Image background = MakeImage("Background", canvasRect, sprite, Paper);
            Stretch(background.rectTransform);

            GameObject stageObject = new GameObject("Stage", typeof(RectTransform), typeof(Image));
            stageObject.transform.SetParent(canvasRect, false);
            stage = stageObject.GetComponent<RectTransform>();
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = new Vector2(0f, -12f);
            Image stageImage = stageObject.GetComponent<Image>();
            stageImage.sprite = roundSprite;
            stageImage.type = Image.Type.Sliced;
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
                Color jointColor = Color.Lerp(color, Ink, 0.2f);
                for (int i = 0; i < bodyCount; i++)
                {
                    dots[person][i] = MakeImage("Joint", stage, sprite, jointColor);
                    dots[person][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    dots[person][i].enabled = false;
                }
            }

            Text title = MakeText("Title", canvasRect, font, "Camera", 28, TextAnchor.UpperLeft, Ink);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -28f), new Vector2(520f, 36f));
            Text privacy = MakeText("Privacy", canvasRect, font, PrivacyNotice, 16, TextAnchor.UpperLeft, Muted);
            privacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            privacy.verticalOverflow = VerticalWrapMode.Overflow;
            Place(privacy.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -68f), new Vector2(760f, 24f));
            Text hands = MakeText("Hands", canvasRect, font, HandNotice, 16, TextAnchor.UpperLeft, Muted);
            hands.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(hands.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -96f), new Vector2(860f, 24f));

            countText = MakeText("Count", canvasRect, font, "No one in frame", 28, TextAnchor.UpperRight, Cream);
            Place(countText.rectTransform, new Vector2(1f, 1f), new Vector2(-176f, -28f), new Vector2(360f, 36f));

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
            cam.backgroundColor = Paper;
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
        bool LayoutStage()
        {
            if (stage == null || canvasRect == null) return false;
            Rect area = canvasRect.rect;
            float side = Mathf.Min(area.width - 64f, area.height - 280f);
            if (side < 64f) side = 64f;
            if (Mathf.Abs(stage.sizeDelta.x - side) < 0.5f) return false;
            stage.sizeDelta = new Vector2(side, side);
            return true;
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
        /// Builds the rounded rectangle used by cards and buttons. The border keeps the corners even when the image is sliced.
        /// </summary>
        Sprite RoundedSprite()
        {
            const int size = 64;
            const float radius = 18f;
            roundTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            roundTex.filterMode = FilterMode.Bilinear;
            roundTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundAlpha(x + 0.5f, y + 0.5f, size, radius);
                    byte channel = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, channel);
                }
            }

            roundTex.SetPixels32(pixels);
            roundTex.Apply(false, false);
            roundSprite = Sprite.Create(roundTex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return roundSprite;
        }

        /// <summary>
        /// Signed coverage of one pixel in a rounded rectangle, 1 inside and 0 outside.
        /// </summary>
        static float RoundAlpha(float x, float y, float size, float radius)
        {
            float half = size * 0.5f;
            float dx = Mathf.Abs(x - half) - (half - radius);
            float dy = Mathf.Abs(y - half) - (half - radius);
            float outside = Mathf.Sqrt((Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)) + (Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Mathf.Clamp01(0.5f - (outside + inside - radius));
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
                requestWidth = 0;
                requestHeight = 0;
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
            return (width == 0 && height == 0) || (width == 640 && height == 480) || (width == 1280 && height == 720) || (width == 1920 && height == 1080);
        }

        /// <summary>
        /// Creates the event system the settings buttons need.
        /// A module created from code has no pointer actions until AssignDefaultActions runs, so clicks never reach the card.
        /// </summary>
        static void EnsureEventSystem()
        {
            EventSystem existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (existing != null)
            {
                InputSystemUIInputModule current = existing.GetComponent<InputSystemUIInputModule>();
                if (current == null)
                {
                    StandaloneInputModule legacy = existing.GetComponent<StandaloneInputModule>();
                    if (legacy != null) Destroy(legacy);
                    current = existing.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                current.AssignDefaultActions();
                return;
            }

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            InputSystemUIInputModule created = events.AddComponent<InputSystemUIInputModule>();
            created.AssignDefaultActions();
        }

        /// <summary>
        /// Builds the settings card on its own overlay so it stays above the stage.
        /// The card is white, sized to the screen, and scrolled so every row can be reached.
        /// </summary>
        void BuildSettings(Font font, Sprite sprite)
        {
            int u = 1;
            settingsFont = font;
            RectTransform root = canvasRect;

            Button open = MakeButton("Open Settings", root, roundSprite, font, "Settings", delegate { SetSettingsOpen(true); }, Btn.Filled);
            Text openLabel = open.GetComponentInChildren<Text>();
            openLabel.fontSize = 14 * u;
            openLabel.fontStyle = FontStyle.Bold;
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.anchoredPosition = new Vector2(-24f * u, -20f * u);
            openRect.sizeDelta = new Vector2(132f * u, 40f * u);

            settingsRoot = new GameObject("Settings", typeof(RectTransform));
            settingsRoot.transform.SetParent(root, false);
            Stretch(settingsRoot.GetComponent<RectTransform>());

            Image dim = MakeImage("Dim", settingsRoot.transform, sprite, new Color(0.125f, 0.129f, 0.141f, 0.32f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(delegate { SetSettingsOpen(false); });

            settingsShadow = MakeImage("Shadow", settingsRoot.transform, roundSprite, new Color(0.125f, 0.129f, 0.141f, 0.16f)).rectTransform;
            settingsShadow.GetComponent<Image>().type = Image.Type.Sliced;
            settingsShadow.anchorMin = new Vector2(0.5f, 0.5f);
            settingsShadow.anchorMax = new Vector2(0.5f, 0.5f);
            settingsShadow.pivot = new Vector2(0.5f, 0.5f);

            Image panel = MakeImage("Panel", settingsRoot.transform, roundSprite, Color.white);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            settingsCard = panel.rectTransform;
            settingsCard.anchorMin = new Vector2(0.5f, 0.5f);
            settingsCard.anchorMax = new Vector2(0.5f, 0.5f);
            settingsCard.pivot = new Vector2(0.5f, 0.5f);
            settingsCard.sizeDelta = new Vector2(520f, 640f);
            if (settingsShadow != null)
            {
                settingsShadow.sizeDelta = settingsCard.sizeDelta;
                settingsShadow.anchoredPosition = new Vector2(0f, -4f);
            }

            Button close = MakeButton("Close", panel.transform, roundSprite, font, "Close", delegate { SetSettingsOpen(false); }, Btn.Text);
            Text closeLabel = close.GetComponentInChildren<Text>();
            closeLabel.fontSize = 14 * u;
            closeLabel.color = Muted;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-12f * u, -12f * u);
            closeRect.sizeDelta = new Vector2(72f * u, 36f * u);

            settingsMain = new GameObject("Main", typeof(RectTransform));
            settingsMain.transform.SetParent(panel.transform, false);
            Stretch(settingsMain.GetComponent<RectTransform>());
            Text heading = MakeText("Heading", settingsMain.transform, font, "Settings", 22 * u, TextAnchor.MiddleLeft, Ink);
            heading.fontStyle = FontStyle.Bold;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(0f, 1f);
            headingRect.pivot = new Vector2(0f, 1f);
            headingRect.anchoredPosition = new Vector2(24f * u, -16f * u);
            headingRect.sizeDelta = new Vector2(220f * u, 32f * u);

            Transform content = MakeScroll(settingsMain.transform, sprite, u, 64f * u);
            BuildSettingsMain(content, font, sprite, u);
            BuildPairingPage(panel.transform, font, sprite, u);
            BuildCablePage(panel.transform, font, sprite, u);

            settingsRoot.transform.SetAsLastSibling();
            settingsRoot.SetActive(false);
            LayoutSettingsCard();
            RefreshSettingLabels();
        }

        /// <summary>
        /// Keeps the settings card inside the window. A card taller than the view was drawing off screen.
        /// </summary>
        void LayoutSettingsCard()
        {
            if (settingsCard == null || canvasRect == null) return;
            Rect area = canvasRect.rect;
            if (area.width < 16f || area.height < 16f) return;
            int seenW = Mathf.RoundToInt(area.width);
            int seenH = Mathf.RoundToInt(area.height);
            if (seenW == settingsSeenW && seenH == settingsSeenH) return;
            settingsSeenW = seenW;
            settingsSeenH = seenH;
            int u = 1;
            float w = Mathf.Clamp(area.width - (80f * u), 360f, 520f);
            float h = Mathf.Clamp(area.height - (96f * u), 460f, 700f);
            settingsCard.sizeDelta = new Vector2(w, h);
            if (settingsShadow == null) return;
            settingsShadow.sizeDelta = new Vector2(w, h);
            settingsShadow.anchoredPosition = new Vector2(0f, -4f * u);
        }

        /// <summary>
        /// Lays out the camera and capture rows inside the scrolling page.
        /// </summary>
        void BuildSettingsMain(Transform page, Font font, Sprite sprite, int u)
        {
            SectionLabel(page, font, "Camera", 12 * u, 22 * u);
            settingsCameraText = BlockLabel(page, font, "No camera yet", 16 * u, 28 * u, Ink);
            settingsCameraText.horizontalOverflow = HorizontalWrapMode.Wrap;

            deviceList = new GameObject("Devices", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).transform;
            deviceList.SetParent(page, false);
            VerticalLayoutGroup deviceLayout = deviceList.GetComponent<VerticalLayoutGroup>();
            deviceLayout.spacing = 4f;
            deviceLayout.childAlignment = TextAnchor.UpperLeft;
            deviceLayout.childControlWidth = true;
            deviceLayout.childControlHeight = true;
            deviceLayout.childForceExpandWidth = true;
            deviceLayout.childForceExpandHeight = false;
            ContentSizeFitter deviceFit = deviceList.GetComponent<ContentSizeFitter>();
            deviceFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            deviceFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform actions = ActionRow(page, 40f * u);
            FlexButton(actions, roundSprite, font, "Previous", 104f * u, 36f * u, 14 * u, Btn.Text, delegate { StepCamera(-1); });
            FlexButton(actions, roundSprite, font, "Next", 72f * u, 36f * u, 14 * u, Btn.Text, delegate { StepCamera(1); });
            FlexButton(actions, roundSprite, font, "Refresh", 96f * u, 36f * u, 14 * u, Btn.Text, RefreshCameras);

            Transform links = ActionRow(page, 44f * u);
            FlexButton(links, roundSprite, font, "Pair phone", 140f * u, 40f * u, 14 * u, Btn.Filled, ShowPairing);
            FlexButton(links, roundSprite, font, "USB cable", 140f * u, 40f * u, 14 * u, Btn.Filled, ShowCable);

            Spacer(page, 8f * u);
            settingsResolutionText = AddSettingRow(page, roundSprite, font, "Resolution", u, CycleResolution);
            settingsFpsText = AddSettingRow(page, roundSprite, font, "Frame rate", u, CycleFrameRate);
            settingsMirrorText = AddSettingRow(page, roundSprite, font, "Mirror", u, ToggleMirror);
            settingsRotationText = AddSettingRow(page, roundSprite, font, "Rotate", u, CycleRotation);
            settingsPeopleText = AddSettingRow(page, roundSprite, font, "People", u, CyclePeople);
            settingsDetailText = AddSettingRow(page, roundSprite, font, "Tracking", u, CycleDetail);
            settingsPowerText = AddSettingRow(page, roundSprite, font, "Camera", u, TogglePower);
        }

        /// <summary>
        /// Adds a scroll view that fills the parent below a top inset, so the card can be shorter than its contents.
        /// </summary>
        static Transform MakeScroll(Transform parent, Sprite sprite, int u, float top)
        {
            GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(12f * u, 12f * u);
            scrollRect.offsetMax = new Vector2(-12f * u, -top);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8 * u, 8 * u, 4 * u, 12 * u);
            layout.spacing = 2f * u;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            return content.transform;
        }

        /// <summary>
        /// Builds the pairing page. It stays hidden until Pair phone is clicked.
        /// </summary>
        void BuildPairingPage(Transform panel, Font font, Sprite sprite, int u)
        {
            settingsPairing = new GameObject("Pairing", typeof(RectTransform));
            settingsPairing.transform.SetParent(panel, false);
            Stretch(settingsPairing.GetComponent<RectTransform>());

            AddTextButton(settingsPairing.transform, roundSprite, font, "Back", 20f * u, 16f * u, 72f * u, 36f * u, 15 * u, HidePairing);
            Text title = MakeText("Pair Title", settingsPairing.transform, font, "Pair phone", 22 * u, TextAnchor.MiddleLeft, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceTop(title.rectTransform, 112f * u, 18f * u, 240f * u, 32f * u);
            pairingStatus = MakeText("Pair Status", settingsPairing.transform, font, "", 14 * u, TextAnchor.UpperLeft, Muted);
            pairingStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            pairingStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(pairingStatus.rectTransform, 28f * u, 58f * u, 404f * u, 36f * u);
            AddTextButton(settingsPairing.transform, roundSprite, font, "Refresh", 20f * u, 98f * u, 120f * u, 36f * u, 15 * u, RefreshCameras);

            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(settingsPairing.transform, false);
            RectTransform stepsRect = scrollObject.GetComponent<RectTransform>();
            stepsRect.anchorMin = Vector2.zero;
            stepsRect.anchorMax = Vector2.one;
            stepsRect.offsetMin = new Vector2(20f * u, 16f * u);
            stepsRect.offsetMax = new Vector2(-16f * u, -142f * u);
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

            AddTextButton(settingsCable.transform, roundSprite, font, "Back", 20f * u, 16f * u, 72f * u, 36f * u, 15 * u, HideCable);
            Text title = MakeText("Cable Title", settingsCable.transform, font, "USB cable", 22 * u, TextAnchor.MiddleLeft, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceTop(title.rectTransform, 112f * u, 18f * u, 240f * u, 32f * u);
            cableStatus = MakeText("Cable Status", settingsCable.transform, font, "", 14 * u, TextAnchor.UpperLeft, Muted);
            cableStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            cableStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(cableStatus.rectTransform, 28f * u, 58f * u, 404f * u, 36f * u);
            Transform cableActions = ActionRow(settingsCable.transform, 40f * u);
            RectTransform cableRow = cableActions.GetComponent<RectTransform>();
            cableRow.anchorMin = new Vector2(0f, 1f);
            cableRow.anchorMax = new Vector2(1f, 1f);
            cableRow.pivot = new Vector2(0.5f, 1f);
            cableRow.anchoredPosition = new Vector2(0f, -96f * u);
            cableRow.sizeDelta = new Vector2(0f, 40f * u);
            HorizontalLayoutGroup cableLayout = cableActions.GetComponent<HorizontalLayoutGroup>();
            cableLayout.padding = new RectOffset(16 * u, 16 * u, 0, 0);
            cableLayout.childForceExpandWidth = true;
            FlexButton(cableActions, roundSprite, font, "Check cable", 0f, 36f * u, 13 * u, Btn.Text, RefreshCameras);
            FlexButton(cableActions, roundSprite, font, "Apple driver", 0f, 36f * u, 13 * u, Btn.Filled, OpenAppleDriver);
            FlexButton(cableActions, roundSprite, font, "Iriun", 0f, 36f * u, 13 * u, Btn.Text, OpenIriun);
            BuildGuideScroll(settingsCable.transform, font, sprite, u, CableGuide, 148f * u);
            settingsCable.SetActive(false);
        }

        /// <summary>
        /// Adds the scrolling steps used by the pairing and cable pages.
        /// </summary>
        static void BuildGuideScroll(Transform parent, Font font, Sprite sprite, int u, string body, float top)
        {
            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(20f * u, 16f * u);
            scrollRect.offsetMax = new Vector2(-16f * u, -top);
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
            Button button = MakeButton(label, parent, sprite, font, label, action, Btn.Text);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.color = YellowDeep;
            text.alignment = TextAnchor.MiddleLeft;
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
        }

        /// <summary>
        /// Adds one settings row and returns the value label.
        /// </summary>
        static Text AddSettingRow(Transform parent, Sprite sprite, Font font, string caption, int u, UnityAction action)
        {
            Button button = MakeButton(caption, parent, sprite, font, "", action, Btn.Row);
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 48f * u;
            element.preferredHeight = 48f * u;
            Text name = MakeText("Name", button.transform, font, caption, 15 * u, TextAnchor.MiddleLeft, Ink);
            PlaceTop(name.rectTransform, 12f * u, 0f, 150f * u, 48f * u);
            Text value = MakeText("Value", button.transform, font, "", 15 * u, TextAnchor.MiddleRight, YellowDeep);
            RectTransform valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = new Vector2(-12f * u, 0f);
            valueRect.sizeDelta = new Vector2(140f * u, 40f * u);
            return value;
        }

        /// <summary>
        /// A short gray section name inside the settings list.
        /// </summary>
        static void SectionLabel(Transform parent, Font font, string message, int size, float height)
        {
            Text text = MakeText("Section", parent, font, message, size, TextAnchor.MiddleLeft, Muted);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        /// <summary>
        /// A single line of primary text inside the settings list.
        /// </summary>
        static Text BlockLabel(Transform parent, Font font, string message, int size, float height, Color color)
        {
            Text text = MakeText("Block", parent, font, message, size, TextAnchor.MiddleLeft, color);
            LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return text;
        }

        /// <summary>
        /// A horizontal row of buttons inside the settings list.
        /// </summary>
        static Transform ActionRow(Transform parent, float height)
        {
            GameObject go = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return go.transform;
        }

        /// <summary>
        /// A button that takes part in a layout row.
        /// </summary>
        static void FlexButton(Transform parent, Sprite sprite, Font font, string label, float width, float height, int fontSize, Btn kind, UnityAction action)
        {
            Button button = MakeButton(label, parent, sprite, font, label, action, kind);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (kind == Btn.Filled) text.color = Ink;
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.flexibleWidth = width <= 0f ? 1f : 0f;
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>
        /// Empty vertical space in the settings list.
        /// </summary>
        static void Spacer(Transform parent, float height)
        {
            GameObject go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            LayoutElement element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
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
            bool live = webcam != null && webcam.isPlaying;
            WebCamDevice[] devices = ReadDevices(!live);
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
            bool live = webcam != null && webcam.isPlaying;
            WebCamDevice[] devices = ReadDevices(!live);
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
            if (devices != null) cachedDevices = devices;
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
            settingsResolutionText.text = requestWidth <= 0 ? "Auto" : requestWidth + " × " + requestHeight;
            RebuildDeviceList();
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
            WebCamDevice[] devices = cachedDevices;
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
            WebCamDevice[] devices = ReadDevices(cachedDevices == null || cachedDevices.Length == 0);
            int count = devices == null ? 0 : devices.Length;
            if (count == 0)
            {
                SetStatus("No camera is listed. Allow Unity under Privacy and Camera, then press Refresh.");
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
            WebCamDevice[] devices = ReadDevices(true);
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
        /// Rebuilds the clickable camera list when the devices or the selection change.
        /// </summary>
        void RebuildDeviceList()
        {
            if (deviceList == null || settingsFont == null) return;
            WebCamDevice[] devices = cachedDevices ?? new WebCamDevice[0];
            string sig = cameraIndex.ToString();
            for (int i = 0; i < devices.Length; i++) sig += "|" + devices[i].name;
            if (sig == deviceSig) return;
            deviceSig = sig;
            for (int i = deviceList.childCount - 1; i >= 0; i--) Destroy(deviceList.GetChild(i).gameObject);
            if (devices.Length == 0)
            {
                BlockLabel(deviceList, settingsFont, "None listed. Press Refresh.", 14, 32f, Muted);
                return;
            }

            for (int i = 0; i < devices.Length; i++)
            {
                int picked = i;
                string label = ShortName(devices[i].name);
                if (devices[i].isFrontFacing) label += "  ·  front";
                Btn kind = i == cameraIndex ? Btn.Filled : Btn.Text;
                FlexButton(deviceList, roundSprite, settingsFont, label, 0f, 40f, 14, kind, delegate { SelectListedCamera(picked); });
            }
        }

        /// <summary>
        /// Opens the camera the user picked in the settings list.
        /// </summary>
        void SelectListedCamera(int index)
        {
            waitingForPhone = false;
            waitingForCable = false;
            cameraHeldOff = false;
            if (engine == null)
            {
                pendingCamera = index;
                if (!resuming) StartCoroutine(Resume());
                return;
            }

            OpenCamera(index);
        }

        /// <summary>
        /// Switches Auto, 640×480, 1280×720, and 1920×1080, then reopens the camera.
        /// </summary>
        void CycleResolution()
        {
            if (requestWidth <= 0)
            {
                requestWidth = 640;
                requestHeight = 480;
            }
            else if (requestWidth == 640)
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
                requestWidth = 0;
                requestHeight = 0;
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
        /// Visual weight of a settings button. Filled is the yellow primary action.
        /// </summary>
        enum Btn
        {
            Text,
            Filled,
            Row
        }

        /// <summary>
        /// Creates a clickable settings button. The graphic stays visible so the raycast is not culled.
        /// </summary>
        static Button MakeButton(string name, Transform parent, Sprite sprite, Font font, string label, UnityAction action, Btn kind)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = true;
            image.canvasRenderer.cullTransparentMesh = false;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            Color labelColor = Ink;
            if (kind == Btn.Filled)
            {
                colors.normalColor = Yellow;
                colors.highlightedColor = new Color(0.93f, 0.62f, 0.00f, 1f);
                colors.pressedColor = YellowDeep;
                colors.selectedColor = colors.highlightedColor;
                labelColor = Ink;
            }
            else if (kind == Btn.Row)
            {
                colors.normalColor = Color.white;
                colors.highlightedColor = Hover;
                colors.pressedColor = Line;
                colors.selectedColor = colors.highlightedColor;
                labelColor = Ink;
            }
            else
            {
                colors.normalColor = new Color(1f, 1f, 1f, 0.04f);
                colors.highlightedColor = Hover;
                colors.pressedColor = Line;
                colors.selectedColor = colors.highlightedColor;
                labelColor = YellowDeep;
            }

            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            Text text = MakeText("Label", go.transform, font, label, 16, TextAnchor.MiddleCenter, labelColor);
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
