using System;
using System.Collections;
using System.Collections.Generic;
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
    /// is shown in this window only. It is never written to disk and never sent off the device.
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
        const int HandlineSize = 384;
        const float HoldSeconds = 0.7f;
        const float DrawScore = 0.18f;
        const string MultiModelFile = "movenet_multipose.tflite";
        const string SingleModelFile = "movenet_lightning.tflite";
        const string PrivacyNotice = "Shown in this window only. The picture is not saved or uploaded. Faces are not drawn.";
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
            "This app only uses a webcam the computer already lists. It does not install a driver. The picture stays in this window and is not saved.";
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
            "The picture stays in this window. It is not saved.";
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

        static readonly Color Paper = new Color(1f, 0.953f, 0.835f, 1f);
        static readonly Color Ink = new Color(0.22f, 0.16f, 0.05f, 1f);
        static readonly Color StageColor = new Color(1f, 0.988f, 0.94f, 1f);
        static readonly Color Card = new Color(1f, 0.988f, 0.94f, 1f);
        static readonly Color Cream = new Color(0.22f, 0.16f, 0.05f, 1f);
        static readonly Color Yellow = new Color(1f, 0.855f, 0.42f, 1f);
        static readonly Color YellowDeep = new Color(0.62f, 0.42f, 0.05f, 1f);
        static readonly Color Muted = new Color(0.48f, 0.37f, 0.16f, 1f);
        static readonly Color Line = new Color(0.86f, 0.70f, 0.32f, 1f);
        static readonly Color Border = new Color(0.72f, 0.50f, 0.08f, 1f);
        static readonly Color Drop = new Color(0.45f, 0.30f, 0.05f, 0.22f);

        readonly PoseFrame[] crowd = new PoseFrame[MaxPeople];
        readonly PoseFrame[] stable = new PoseFrame[MaxPeople];
        readonly bool[] slotUsed = new bool[MaxPeople];
        readonly float[] slotX = new float[MaxPeople];
        readonly float[] slotY = new float[MaxPeople];
        readonly float[] slotSeen = new float[MaxPeople];
        readonly TrackPoint[][] track = new TrackPoint[MaxPeople][];

        PoseEngine engine;
        PoseEngine multiEngine;
        WebCamTexture webcam;
        WebCamDevice[] cachedDevices = new WebCamDevice[0];
        readonly int[] tryW = new int[3];
        readonly int[] tryH = new int[3];
        readonly int[] tryF = new int[3];
        readonly bool[] slotTaken = new bool[MaxPeople];
        readonly int[] slotAssigned = new int[MaxPeople];
        RectTransform canvasRect;
        RectTransform stage;
        RectTransform stageFrame;
        RawImage videoView;
        StagePuppet puppet;
        Camera stageCamera;
        GameObject startRoot;
        bool sessionStarted;
        bool modelPrepared;
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
        RectTransform settingsBorder;
        int settingsSeenW = -1;
        int settingsSeenH = -1;
        Color32[] crop;
        Texture2D previewTex;
        Color32[] previewPixels;
        int[] sampleIndex;
        int sampleWidth;
        int sampleHeight;
        int sampleRotation = int.MinValue;
        int sampleSize;
        bool sampleMirror;
        bool sampleVertical;
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
        bool reportedGiveUp;
        int tryIndex;
        int tryCount;
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
        string deviceSig = "";
        Dropdown cameraDropdown;
        Dropdown resolutionDropdown;
        Dropdown fpsDropdown;
        Dropdown mirrorDropdown;
        Dropdown rotationDropdown;
        Dropdown peopleDropdown;
        Dropdown detailDropdown;
        Dropdown powerDropdown;
        bool suppressDropdown;

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
                    track[i] = new TrackPoint[(int)JointId.Count];
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
            if (!ModelReady() || cameraHeldOff) yield break;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until && ReadDevices(true).Length == 0)
                yield return new WaitForSecondsRealtime(0.4f);
            modelPrepared = true;
            if (sessionStarted) OpenCamera(ChooseStartupCamera());
        }

        /// <summary>
        /// Polls keys, estimates the current frame, and redraws body skeletons.
        /// </summary>
        void Update()
        {
            if (!ready) return;
            LayoutStage();
            LayoutSettingsCard();
            PollKeys();
            WatchCamera();
            if (!cameraHeldOff && ModelReady()) ScanDevices();

            if (webcam != null && webcam.isPlaying && webcam.width > 16 && (webcam.didUpdateThisFrame || Time.unscaledTime >= nextPose))
            {
                CaptureAndEstimate();
                nextPose = Time.unscaledTime + 0.05f;
            }
            else if (webcam == null && poseUntil > 0f && Time.unscaledTime > poseUntil)
            {
                poseUntil = 0f;
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
                if (!sessionStarted)
                {
                    BeginSession();
                    return;
                }

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
            if (!ModelReady()) yield return LoadModel();
            resuming = false;
            if (!ModelReady() || cameraHeldOff) yield break;
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
        /// Loads the Handline Lightning file first, then the six-person file for groups.
        /// One person uses Lightning. More than one person uses MultiPose.
        /// </summary>
        IEnumerator LoadModelFiles()
        {
            SetStatus("Loading MoveNet Lightning…");
            modelError = null;
            yield return ReadModelFile(SingleModelFile);
            if (loadedBytes != null)
            {
                string singleError;
                engine = TryCreateEngine(loadedBytes, out singleError);
                loadedBytes = null;
                if (engine == null) modelError = singleError;
            }
            else if (!string.IsNullOrEmpty(loadError))
            {
                modelError = loadError;
            }

            yield return ReadModelFile(MultiModelFile);
            if (loadedBytes != null)
            {
                string multiError;
                multiEngine = TryCreateEngine(loadedBytes, out multiError);
                loadedBytes = null;
                if (multiEngine == null && !string.IsNullOrEmpty(multiError)) Debug.LogWarning(multiError);
            }

            if (!ModelReady())
            {
                if (string.IsNullOrEmpty(modelError)) modelError = "MoveNet Lightning did not load.";
                SetStatus(modelError);
                yield break;
            }

            ApplyModelLabel();
            SetStatus(modelLabel);
        }

        /// <summary>
        /// Lightning for one person, the same file Handline uses. MultiPose when the dropdown asks for more.
        /// </summary>
        PoseEngine ActiveEngine()
        {
            if (peopleLimit <= 1 && engine != null) return engine;
            if (multiEngine != null) return multiEngine;
            return engine;
        }

        /// <summary>
        /// True when at least one MoveNet file started.
        /// </summary>
        bool ModelReady()
        {
            return engine != null || multiEngine != null;
        }

        /// <summary>
        /// Names the model that is tracking right now.
        /// </summary>
        void ApplyModelLabel()
        {
            PoseEngine active = ActiveEngine();
            if (active == null) return;
            bool handline = active == engine;
            modelLabel = handline ? "MoveNet Lightning" : "MoveNet MultiPose Lightning";
            if (!string.IsNullOrEmpty(active.Summary)) modelLabel += "  ·  " + active.Summary;
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
            if (engine != null) engine.Dispose();
            if (multiEngine != null) multiEngine.Dispose();
            engine = null;
            multiEngine = null;
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
                Color32[] filled = webcam.GetPixels32();
                if (filled == null || filled.Length == 0) return;
                FillCrop(filled, sw, sh, HandlineSize);
                ShowCrop(crop, HandlineSize);
                PoseEngine active = ActiveEngine();
                if (active != null)
                {
                    crowdCount = active.TryRead(crop, HandlineSize, crowd, peopleLimit);
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
                if (active != null && !string.IsNullOrEmpty(active.Failure)) NoteModelFailure();
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
        /// Center-crops the sensor image into the upright square MoveNet reads.
        /// The square is the model input. It is not copied onto a texture.
        /// </summary>
        void FillCrop(Color32[] source, int srcWidth, int srcHeight, int size)
        {
            if (size < 32) size = CropSize;
            int count = size * size;
            if (crop == null || crop.Length != count) crop = new Color32[count];

            int rotation = webcam.videoRotationAngle + extraRotation;
            bool vertical = webcam.videoVerticallyMirrored;
            if (sampleIndex == null || sampleSize != size || sampleWidth != srcWidth || sampleHeight != srcHeight
                || sampleRotation != rotation || sampleMirror != mirror || sampleVertical != vertical)
            {
                sampleIndex = new int[count];
                FrameOrient.FillIndices(sampleIndex, size, srcWidth, srcHeight, rotation, vertical, mirror);
                sampleSize = size;
                sampleWidth = srcWidth;
                sampleHeight = srcHeight;
                sampleRotation = rotation;
                sampleMirror = mirror;
                sampleVertical = vertical;
            }

            int limit = source.Length;
            for (int i = 0; i < count; i++)
            {
                int index = sampleIndex[i];
                crop[i] = (uint)index < (uint)limit ? source[index] : default(Color32);
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
                if (pose.Joints[i].Score >= DrawScore) count++;
            }

            return count;
        }

        /// <summary>
        /// True when enough of the torso and limbs are visible to draw a person.
        /// </summary>
        bool HasBody(PoseFrame pose)
        {
            return BodyPoints(pose) >= 1;
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
                if (joint.Score < DrawScore) continue;
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
                    if (joint.Score < DrawScore) continue;
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
                    float bestDist = 0.6f;
                for (int n = 0; n < crowdCount && n < MaxPeople; n++)
                {
                    if (slotTaken[n] || !Detectable(crowd[n])) continue;
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
                if (slotTaken[n] || !Detectable(crowd[n])) continue;
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
                    if (slotSeen[slot] > 0f && Time.unscaledTime - slotSeen[slot] < HoldSeconds)
                    {
                        BlendSlot(slot, null);
                        continue;
                    }

                    Blank(stable[slot]);
                    ClearTrack(slot);
                    slotUsed[slot] = false;
                    slotX[slot] = 0f;
                    slotY[slot] = 0f;
                    slotSeen[slot] = 0f;
                    continue;
                }

                BlendSlot(slot, crowd[source]);
                float x;
                float y;
                if (Torso(stable[slot], out x, out y))
                {
                    slotX[slot] = x;
                    slotY[slot] = y;
                }

                slotSeen[slot] = Time.unscaledTime;
            }
        }

        /// <summary>
        /// True when a new sample has at least one body joint worth matching.
        /// </summary>
        bool Detectable(PoseFrame pose)
        {
            if (pose == null || pose.Joints == null) return false;
            int start = (int)JointId.LeftShoulder;
            int end = pose.Joints.Length;
            if (end > (int)JointId.Count) end = (int)JointId.Count;
            for (int i = start; i < end; i++)
            {
                if (pose.Joints[i].Score >= 0.15f) return true;
            }

            return false;
        }

        /// <summary>
        /// Follows a new MoveNet sample and keeps a covered joint in place for a short hold.
        /// The hold is what stops a limb from vanishing when it leaves the frame.
        /// </summary>
        void BlendSlot(int slot, PoseFrame raw)
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            float follow = 1f - Mathf.Exp(-dt / 0.045f);
            PoseFrame dest = stable[slot];
            int start = (int)JointId.LeftShoulder;
            int end = (int)JointId.Count;
            bool any = false;
            for (int i = 0; i < end; i++)
            {
                if (i < start || dest.Joints == null)
                {
                    if (dest.Joints != null && i < dest.Joints.Length) dest.Joints[i] = default(Joint);
                    continue;
                }

                TrackPoint point = track[slot][i];
                if (raw != null && raw.Joints != null && i < raw.Joints.Length && raw.Joints[i].Score >= jointThreshold)
                {
                    Joint sample = raw.Joints[i];
                    if (point.Seen <= 0f)
                    {
                        point.X = sample.X;
                        point.Y = sample.Y;
                    }
                    else
                    {
                        float gain = follow * Mathf.Clamp01(0.45f + sample.Score);
                        point.X = Mathf.Lerp(point.X, sample.X, gain);
                        point.Y = Mathf.Lerp(point.Y, sample.Y, gain);
                    }

                    point.Score = sample.Score;
                    point.Seen = now;
                }

                float age = point.Seen > 0f ? now - point.Seen : 99f;
                if (point.Seen > 0f && age < HoldSeconds)
                {
                    float shown = point.Score;
                    if (age > HoldSeconds * 0.75f)
                    {
                        float fade = 1f - ((age - HoldSeconds * 0.75f) / (HoldSeconds * 0.25f));
                        shown = DrawScore * Mathf.Clamp01(fade);
                    }
                    else if (shown < DrawScore)
                    {
                        shown = DrawScore;
                    }

                    dest.Joints[i] = new Joint(point.X, point.Y, shown);
                    any = true;
                }
                else
                {
                    dest.Joints[i] = default(Joint);
                    if (age >= HoldSeconds) point.Seen = 0f;
                }

                track[slot][i] = point;
            }

            slotUsed[slot] = any;
        }

        /// <summary>
        /// Forgets the smoothed joints for one person.
        /// </summary>
        void ClearTrack(int slot)
        {
            if (track[slot] == null) return;
            for (int i = 0; i < track[slot].Length; i++) track[slot][i] = default(TrackPoint);
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
                ClearTrack(i);
                slotUsed[i] = false;
                slotX[i] = 0f;
                slotY[i] = 0f;
                slotSeen[i] = 0f;
            }
        }

        /// <summary>
        /// Fills the camera buffers with empty pixels after each estimate.
        /// </summary>
        void WipePixels()
        {
            if (crop != null) Array.Clear(crop, 0, crop.Length);
        }

        /// <summary>
        /// Surfaces a model failure on the status line. The line does not include the picture.
        /// </summary>
        void NoteModelFailure()
        {
            PoseEngine active = ActiveEngine();
            if (active == null || string.IsNullOrEmpty(active.Failure)) return;
            SetStatus("Pose tracking stopped. " + active.Failure);
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
        /// Loads the camera list. Pass true to ask the operating system again.
        /// </summary>
        void EnsureDeviceCache(bool force)
        {
            ReadDevices(force);
        }

        /// <summary>
        /// Reads the operating-system camera list. A playing camera is not queried again, because that read drops the stream on Windows.
        /// </summary>
        WebCamDevice[] ReadDevices(bool force)
        {
            if (!force && cachedDevices != null && cachedDevices.Length > 0) return cachedDevices;
            WebCamDevice[] found = WebCamTexture.devices;
            if (found == null) found = new WebCamDevice[0];
            if (!SameDevices(cachedDevices, found)) deviceSig = "";
            cachedDevices = found;
            knownDeviceCount = cachedDevices.Length;
            return cachedDevices;
        }

        /// <summary>
        /// True when two camera lists have the same names in the same order.
        /// </summary>
        static bool SameDevices(WebCamDevice[] left, WebCamDevice[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i].name != right[i].name) return false;
            }

            return true;
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
            HidePreview();
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
            scaler.dynamicPixelsPerUnit = 4f;
            canvasRect = canvasObject.GetComponent<RectTransform>();

            Image background = MakeImage("Background", canvasRect, sprite, new Color(Paper.r, Paper.g, Paper.b, 0f));
            Stretch(background.rectTransform);

            Image frameImage = MakeImage("Stage Frame", canvasRect, roundSprite, Border);
            frameImage.type = Image.Type.Sliced;
            stageFrame = frameImage.rectTransform;
            stageFrame.anchorMin = new Vector2(0.5f, 0.5f);
            stageFrame.anchorMax = new Vector2(0.5f, 0.5f);
            stageFrame.pivot = new Vector2(0.5f, 0.5f);
            stageFrame.anchoredPosition = new Vector2(0f, -12f);

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

            GameObject videoObject = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            videoObject.transform.SetParent(stage, false);
            Stretch(videoObject.GetComponent<RectTransform>());
            videoView = videoObject.GetComponent<RawImage>();
            videoView.raycastTarget = false;
            videoView.color = Color.white;
            videoView.uvRect = new Rect(0f, 1f, 1f, -1f);
            videoView.enabled = false;

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
                    AddInkEdge(lines[person][i]);
                }

                dots[person] = new Image[bodyCount];
                Color jointColor = Color.Lerp(color, Ink, 0.2f);
                for (int i = 0; i < bodyCount; i++)
                {
                    dots[person][i] = MakeImage("Joint", stage, sprite, jointColor);
                    dots[person][i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    dots[person][i].enabled = false;
                    AddInkEdge(dots[person][i]);
                }
            }

            Text title = MakeText("Title", canvasRect, font, "Camera", 32, TextAnchor.UpperLeft, Ink);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(520f, 40f));
            Text privacy = MakeText("Privacy", canvasRect, font, PrivacyNotice, 18, TextAnchor.UpperLeft, Ink);
            privacy.horizontalOverflow = HorizontalWrapMode.Wrap;
            privacy.verticalOverflow = VerticalWrapMode.Overflow;
            Place(privacy.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -68f), new Vector2(900f, 28f));
            Text hands = MakeText("Hands", canvasRect, font, HandNotice, 18, TextAnchor.UpperLeft, Ink);
            hands.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(hands.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -100f), new Vector2(980f, 28f));

            countText = MakeText("Count", canvasRect, font, "No one in frame", 28, TextAnchor.UpperRight, Ink);
            countText.fontStyle = FontStyle.Bold;
            Place(countText.rectTransform, new Vector2(1f, 1f), new Vector2(-176f, -24f), new Vector2(420f, 40f));

            statusText = MakeText("Status", canvasRect, font, "Starting…", 20, TextAnchor.LowerLeft, Ink);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(36f, 56f), new Vector2(1200f, 32f));
            Text help = MakeText("Help", canvasRect, font, "Tab settings    F mirror    Q rotate    Esc stop    Enter start", 18, TextAnchor.LowerLeft, Muted);
            Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(340f, 36f), new Vector2(900f, 28f));
            Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(340f, 72f), new Vector2(900f, 32f));
            BuildStartScreen(font, sprite);
            BuildPuppet();
            BuildSettings(font, sprite);
        }

        /// <summary>
        /// Shows the title card until the player starts. The character is already standing behind it.
        /// </summary>
        void BuildStartScreen(Font font, Sprite sprite)
        {
            startRoot = new GameObject("Start Screen", typeof(RectTransform));
            startRoot.transform.SetParent(canvasRect, false);
            Stretch(startRoot.GetComponent<RectTransform>());

            Image dim = MakeImage("Start Dim", startRoot.transform, sprite, new Color(0.35f, 0.26f, 0.08f, 0.28f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            Image card = MakeImage("Start Card", startRoot.transform, roundSprite, Card);
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 150f);
            cardRect.sizeDelta = new Vector2(560f, 340f);

            Image rim = MakeImage("Start Rim", startRoot.transform, roundSprite, Border);
            rim.type = Image.Type.Sliced;
            rim.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rim.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            rim.rectTransform.sizeDelta = new Vector2(572f, 352f);
            rim.rectTransform.SetSiblingIndex(card.transform.GetSiblingIndex());

            Text heading = MakeText("Start Title", card.transform, font, "Camera MoCap", 40, TextAnchor.MiddleCenter, Ink);
            heading.fontStyle = FontStyle.Bold;
            Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(480f, 52f));
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);

            Text body = MakeText("Start Body", card.transform, font, "A character stands on the floor and follows your camera pose. The picture stays in the corner and is not saved.", 20, TextAnchor.UpperCenter, Ink);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(460f, 90f));
            body.rectTransform.pivot = new Vector2(0.5f, 1f);

            Button start = MakeButton("Start", card.transform, roundSprite, font, "Start", BeginSession, Btn.Filled);
            Text startLabel = start.GetComponentInChildren<Text>();
            startLabel.fontSize = 22;
            startLabel.fontStyle = FontStyle.Bold;
            RectTransform startRect = start.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 36f);
            startRect.sizeDelta = new Vector2(220f, 56f);
        }

        /// <summary>
        /// Leaves the title card and opens the camera so the character can follow.
        /// </summary>
        void BeginSession()
        {
            if (sessionStarted) return;
            sessionStarted = true;
            if (startRoot != null) startRoot.SetActive(false);
            if (modelPrepared && ModelReady() && webcam == null && !cameraHeldOff)
            {
                OpenCamera(ChooseStartupCamera());
            }
        }

        /// <summary>
        /// Creates the floor and the characters the camera pose will move.
        /// </summary>
        void BuildPuppet()
        {
            GameObject go = new GameObject("Standing Area");
            go.transform.SetParent(transform, false);
            puppet = go.AddComponent<StagePuppet>();
            puppet.Build();
            if (stageCamera != null) puppet.Frame(stageCamera, 1);
        }

        /// <summary>
        /// Draws the same upright center crop MoveNet reads, so the skeleton sits on the picture.
        /// The texture lives on the GPU for this window. It is not written to a file.
        /// </summary>
        void ShowCrop(Color32[] square, int size)
        {
            if (videoView == null || square == null || size < 32) return;
            if (previewTex == null || previewTex.width != size)
            {
                if (previewTex != null) Destroy(previewTex);
                previewTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                previewTex.wrapMode = TextureWrapMode.Clamp;
                previewTex.filterMode = FilterMode.Bilinear;
                previewTex.name = "Camera preview";
            }

            int count = size * size;
            if (previewPixels == null || previewPixels.Length != count) previewPixels = new Color32[count];
            for (int y = 0; y < size; y++)
            {
                int src = y * size;
                int dst = (size - 1 - y) * size;
                for (int x = 0; x < size; x++) previewPixels[dst + x] = square[src + x];
            }

            previewTex.SetPixels32(previewPixels);
            previewTex.Apply(false, false);
            Array.Clear(previewPixels, 0, previewPixels.Length);
            videoView.uvRect = new Rect(0f, 0f, 1f, 1f);
            videoView.texture = previewTex;
            videoView.enabled = true;
        }

        /// <summary>
        /// Drops the on-screen picture when the camera stops.
        /// </summary>
        void HidePreview()
        {
            if (previewTex != null)
            {
                Destroy(previewTex);
                previewTex = null;
            }

            previewPixels = null;
            if (videoView == null) return;
            videoView.texture = null;
            videoView.enabled = false;
        }

        /// <summary>
        /// Gives the Game view a display to render. The webcam is drawn by the stage canvas, not by this camera.
        /// </summary>
        void EnsureStageCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera existing = cameras[i];
                if (existing == null || !existing.enabled || existing.targetDisplay != 0) continue;
                stageCamera = existing;
                FrameStageCamera(existing);
                return;
            }

            GameObject go = new GameObject("Stage Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            Camera cam = go.AddComponent<Camera>();
            cam.depth = -1;
            cam.targetDisplay = 0;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            stageCamera = cam;
            FrameStageCamera(cam);
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Looks at the standing area. The picture itself is a canvas card, not this camera.
        /// </summary>
        void FrameStageCamera(Camera cam)
        {
            cam.orthographic = false;
            cam.fieldOfView = 32f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Paper;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.transform.position = new Vector3(0f, 1.2f, -4.2f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.05f, 0f) - cam.transform.position, Vector3.up);
        }

        /// <summary>
        /// Sizes the square stage inside the screen margins.
        /// </summary>
        bool LayoutStage()
        {
            if (stage == null) return false;
            const float side = 280f;
            stage.anchorMin = Vector2.zero;
            stage.anchorMax = Vector2.zero;
            stage.pivot = Vector2.zero;
            stage.anchoredPosition = new Vector2(28f, 28f);
            stage.sizeDelta = new Vector2(side, side);
            if (stageFrame != null)
            {
                stageFrame.anchorMin = Vector2.zero;
                stageFrame.anchorMax = Vector2.zero;
                stageFrame.pivot = Vector2.zero;
                stageFrame.anchoredPosition = new Vector2(23f, 23f);
                stageFrame.sizeDelta = new Vector2(side + 10f, side + 10f);
            }

            return false;
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

            if (puppet != null)
            {
                puppet.Follow(stable, slotUsed, peopleLimit);
                int visible = VisiblePeople();
                puppet.Frame(stageCamera, visible > 0 ? visible : 1);
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
                if (a.Score < DrawScore || b.Score < DrawScore)
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
                boneRect.SetAsLastSibling();
                boneRect.sizeDelta = new Vector2(length, 8f);
                boneRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                bones[i].enabled = true;
            }

            Image[] joints = dots[person];
            int start = (int)JointId.LeftShoulder;
            for (int i = 0; i < joints.Length; i++)
            {
                int jointId = start + i;
                if (!show || jointId >= pose.Joints.Length || pose.Joints[jointId].Score < DrawScore)
                {
                    joints[i].enabled = false;
                    continue;
                }

                bool hand = IsMeasuredHand(jointId);
                float size = hand ? 22f : 14f;
                RectTransform jointRect = joints[i].rectTransform;
                jointRect.SetAsLastSibling();
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
            if (!ModelReady())
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
            peopleLimit = Mathf.Clamp(PlayerPrefs.GetInt(PrefPeople, 1), 1, MaxPeople);
            jointThreshold = PlayerPrefs.GetFloat(PrefThreshold, 0.2f);
            if (jointThreshold < 0.1f || jointThreshold > 0.6f) jointThreshold = 0.2f;
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
        /// Builds the settings card on the stage canvas.
        /// The card is light yellow with a gold rim, and it scrolls so every row can be reached.
        /// </summary>
        void BuildSettings(Font font, Sprite sprite)
        {
            int u = 1;
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

            Image dim = MakeImage("Dim", settingsRoot.transform, sprite, new Color(0.35f, 0.26f, 0.08f, 0.35f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(delegate { SetSettingsOpen(false); });

            settingsShadow = MakeImage("Shadow", settingsRoot.transform, roundSprite, Drop).rectTransform;
            settingsShadow.GetComponent<Image>().type = Image.Type.Sliced;
            settingsShadow.anchorMin = new Vector2(0.5f, 0.5f);
            settingsShadow.anchorMax = new Vector2(0.5f, 0.5f);
            settingsShadow.pivot = new Vector2(0.5f, 0.5f);

            settingsBorder = MakeImage("Border", settingsRoot.transform, roundSprite, Border).rectTransform;
            settingsBorder.GetComponent<Image>().type = Image.Type.Sliced;
            settingsBorder.anchorMin = new Vector2(0.5f, 0.5f);
            settingsBorder.anchorMax = new Vector2(0.5f, 0.5f);
            settingsBorder.pivot = new Vector2(0.5f, 0.5f);

            Image panel = MakeImage("Panel", settingsRoot.transform, roundSprite, Card);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            settingsCard = panel.rectTransform;
            settingsCard.anchorMin = new Vector2(0.5f, 0.5f);
            settingsCard.anchorMax = new Vector2(0.5f, 0.5f);
            settingsCard.pivot = new Vector2(0.5f, 0.5f);
            settingsCard.sizeDelta = new Vector2(520f, 640f);
            FitCardChrome(520f, 640f);

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
            Text heading = MakeText("Heading", settingsMain.transform, font, "Settings", 26 * u, TextAnchor.MiddleLeft, Ink);
            heading.fontStyle = FontStyle.Bold;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(0f, 1f);
            headingRect.pivot = new Vector2(0f, 1f);
            headingRect.anchoredPosition = new Vector2(24f * u, -16f * u);
            headingRect.sizeDelta = new Vector2(220f * u, 32f * u);

            Transform content = MakeScroll(settingsMain.transform, sprite, u, 64f * u, false);
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
            float w = Mathf.Clamp(area.width - 80f, 360f, 520f);
            float h = Mathf.Clamp(area.height - 96f, 460f, 700f);
            settingsCard.sizeDelta = new Vector2(w, h);
            FitCardChrome(w, h);
        }

        /// <summary>
        /// Sizes the gold rim and the soft drop behind the settings card.
        /// </summary>
        void FitCardChrome(float width, float height)
        {
            if (settingsBorder != null)
            {
                settingsBorder.sizeDelta = new Vector2(width + 10f, height + 10f);
                settingsBorder.anchoredPosition = Vector2.zero;
            }

            if (settingsShadow == null) return;
            settingsShadow.sizeDelta = new Vector2(width + 10f, height + 10f);
            settingsShadow.anchoredPosition = new Vector2(0f, -12f);
        }

        /// <summary>
        /// Lays out the camera and capture rows inside the scrolling page.
        /// </summary>
        void BuildSettingsMain(Transform page, Font font, Sprite sprite, int u)
        {
            SectionLabel(page, font, "Camera", 14 * u, 24 * u);
            cameraDropdown = AddDropdown(page, roundSprite, font, "No camera yet", PickCamera);

            Transform actions = ActionRow(page, 40f * u);
            FlexButton(actions, roundSprite, font, "Refresh", 120f * u, 40f * u, 16 * u, Btn.Text, RefreshCameras);

            Transform links = ActionRow(page, 44f * u);
            FlexButton(links, roundSprite, font, "Pair phone", 140f * u, 40f * u, 16 * u, Btn.Filled, ShowPairing);
            FlexButton(links, roundSprite, font, "USB cable", 140f * u, 40f * u, 16 * u, Btn.Filled, ShowCable);

            Spacer(page, 10f * u);
            SectionLabel(page, font, "Picture", 14 * u, 24 * u);
            resolutionDropdown = AddDropdown(page, roundSprite, font, "Auto", PickResolution);
            FillFixedDropdown(resolutionDropdown, new string[] { "Auto", "640 × 480", "1280 × 720", "1920 × 1080" }, ResolutionIndex());
            fpsDropdown = AddDropdown(page, roundSprite, font, "30", PickFrameRate);
            FillFixedDropdown(fpsDropdown, new string[] { "15", "30", "60" }, FpsIndex());
            mirrorDropdown = AddDropdown(page, roundSprite, font, "On", PickMirror);
            FillFixedDropdown(mirrorDropdown, new string[] { "Mirror on", "Mirror off" }, mirror ? 0 : 1);
            rotationDropdown = AddDropdown(page, roundSprite, font, "0°", PickRotation);
            FillFixedDropdown(rotationDropdown, new string[] { "0°", "90°", "180°", "270°" }, extraRotation / 90);

            Spacer(page, 10f * u);
            SectionLabel(page, font, "Tracking", 14 * u, 24 * u);
            peopleDropdown = AddDropdown(page, roundSprite, font, "1 person", PickPeople);
            FillFixedDropdown(peopleDropdown, new string[] { "1 person", "2 people", "3 people", "4 people", "5 people", "6 people" }, peopleLimit - 1);
            detailDropdown = AddDropdown(page, roundSprite, font, "Normal", PickDetail);
            FillFixedDropdown(detailDropdown, new string[] { "Relaxed", "Normal", "Firm" }, DetailIndex());
            powerDropdown = AddDropdown(page, roundSprite, font, "On", PickPower);
            FillFixedDropdown(powerDropdown, new string[] { "Camera on", "Camera off" }, cameraHeldOff ? 1 : 0);
        }

        /// <summary>
        /// Adds a scroll view that fills the parent below a top inset, so the card can be shorter than its contents.
        /// </summary>
        static Transform MakeScroll(Transform parent, Sprite sprite, int u, float top, bool clip)
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

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
            if (clip) viewport.AddComponent<RectMask2D>();
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
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

            PlaceCenteredBack(settingsPairing.transform, roundSprite, font, HidePairing);
            Text title = MakeText("Pair Title", settingsPairing.transform, font, "Pair phone", 24 * u, TextAnchor.MiddleCenter, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceHeaderLine(title.rectTransform, 72f * u, 36f * u);
            pairingStatus = MakeText("Pair Status", settingsPairing.transform, font, "", 18 * u, TextAnchor.MiddleCenter, Ink);
            pairingStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            pairingStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceHeaderLine(pairingStatus.rectTransform, 114f * u, 40f * u);
            AddTextButton(settingsPairing.transform, roundSprite, font, "Refresh", 20f * u, 162f * u, 140f * u, 40f * u, 18 * u, RefreshCameras);

            GameObject scrollObject = new GameObject("Steps", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(settingsPairing.transform, false);
            RectTransform stepsRect = scrollObject.GetComponent<RectTransform>();
            stepsRect.anchorMin = Vector2.zero;
            stepsRect.anchorMax = Vector2.one;
            stepsRect.offsetMin = new Vector2(20f * u, 16f * u);
            stepsRect.offsetMax = new Vector2(-16f * u, -214f * u);
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f * u;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, PairingGuide, 18 * u, TextAnchor.UpperLeft, Ink);
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

            PlaceCenteredBack(settingsCable.transform, roundSprite, font, HideCable);
            Text title = MakeText("Cable Title", settingsCable.transform, font, "USB cable", 24 * u, TextAnchor.MiddleCenter, Ink);
            title.fontStyle = FontStyle.Bold;
            PlaceHeaderLine(title.rectTransform, 72f * u, 36f * u);
            cableStatus = MakeText("Cable Status", settingsCable.transform, font, "", 18 * u, TextAnchor.MiddleCenter, Ink);
            cableStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            cableStatus.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceHeaderLine(cableStatus.rectTransform, 114f * u, 40f * u);
            Transform cableActions = ActionRow(settingsCable.transform, 40f * u);
            RectTransform cableRow = cableActions.GetComponent<RectTransform>();
            cableRow.anchorMin = new Vector2(0f, 1f);
            cableRow.anchorMax = new Vector2(1f, 1f);
            cableRow.pivot = new Vector2(0.5f, 1f);
            cableRow.anchoredPosition = new Vector2(0f, -162f * u);
            cableRow.sizeDelta = new Vector2(0f, 40f * u);
            HorizontalLayoutGroup cableLayout = cableActions.GetComponent<HorizontalLayoutGroup>();
            cableLayout.padding = new RectOffset(16 * u, 16 * u, 0, 0);
            cableLayout.childForceExpandWidth = true;
            FlexButton(cableActions, roundSprite, font, "Check cable", 0f, 36f * u, 13 * u, Btn.Text, RefreshCameras);
            FlexButton(cableActions, roundSprite, font, "Apple driver", 0f, 36f * u, 13 * u, Btn.Filled, OpenAppleDriver);
            FlexButton(cableActions, roundSprite, font, "Iriun", 0f, 36f * u, 13 * u, Btn.Text, OpenIriun);
            BuildGuideScroll(settingsCable.transform, font, sprite, u, CableGuide, 214f * u);
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

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = sprite;
            viewportImage.color = Card;
            viewportImage.raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            Text guide = MakeText("Guide", content.transform, font, body, 18 * u, TextAnchor.UpperLeft, Ink);
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
            Image line = MakeImage("Rule", parent, sprite, Line);
            PlaceTop(line.rectTransform, x, y, width, 2f);
        }

        /// <summary>
        /// Puts Back in the middle of the top of a settings page, with the label centered in the button.
        /// </summary>
        void PlaceCenteredBack(Transform parent, Sprite sprite, Font font, UnityAction action)
        {
            Button button = MakeButton("Back", parent, sprite, font, "Back", action, Btn.Filled);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -16f);
            rect.sizeDelta = new Vector2(160f, 44f);
        }

        /// <summary>
        /// Stretches a header line across the card, measured down from the top.
        /// </summary>
        static void PlaceHeaderLine(RectTransform rect, float down, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -down);
            rect.sizeDelta = new Vector2(-48f, height);
        }

        /// <summary>
        /// Adds a text button with the label centered in its box.
        /// </summary>
        static void AddTextButton(Transform parent, Sprite sprite, Font font, string label, float x, float y, float width, float height, int fontSize, UnityAction action)
        {
            Button button = MakeButton(label, parent, sprite, font, label, action, Btn.Text);
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleCenter;
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
            Text name = MakeText("Name", button.transform, font, caption, 17 * u, TextAnchor.MiddleLeft, Ink);
            PlaceTop(name.rectTransform, 12f * u, 0f, 150f * u, 48f * u);
            Text value = MakeText("Value", button.transform, font, "", 17 * u, TextAnchor.MiddleRight, YellowDeep);
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
            if (!ModelReady())
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
        /// Builds one settings dropdown. The list opens downward and is not clipped by the card.
        /// </summary>
        Dropdown AddDropdown(Transform parent, Sprite sprite, Font font, string caption, UnityAction<int> onPick)
        {
            Dropdown dropdown = BuildDropdown(parent, sprite, font, caption);
            LayoutElement element = dropdown.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 44f;
            element.preferredHeight = 44f;
            element.flexibleWidth = 1f;
            dropdown.onValueChanged.AddListener(delegate (int index)
            {
                if (suppressDropdown || onPick == null) return;
                onPick(index);
            });
            return dropdown;
        }

        /// <summary>
        /// Creates the dropdown control Unity's template expects: caption, scroll list, and one item.
        /// </summary>
        Dropdown BuildDropdown(Transform parent, Sprite sprite, Font font, string caption)
        {
            GameObject root = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = Card;
            Outline rim = root.AddComponent<Outline>();
            rim.effectColor = Border;
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            Dropdown dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = background;

            Text label = MakeText("Label", root.transform, font, caption, 17, TextAnchor.MiddleLeft, Ink);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(14f, 4f);
            label.rectTransform.offsetMax = new Vector2(-28f, -4f);
            dropdown.captionText = label;

            Text arrow = MakeText("Arrow", root.transform, font, "▾", 16, TextAnchor.MiddleCenter, YellowDeep);
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-8f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 0f);

            GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            Image templateImage = template.GetComponent<Image>();
            templateImage.sprite = sprite;
            templateImage.type = Image.Type.Sliced;
            templateImage.color = Card;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Card;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 36f);
            VerticalLayoutGroup listLayout = content.GetComponent<VerticalLayoutGroup>();
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter listFit = content.GetComponent<ContentSizeFitter>();
            listFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<LayoutElement>().preferredHeight = 36f;
            Toggle toggle = item.GetComponent<Toggle>();
            Image itemImage = item.AddComponent<Image>();
            itemImage.color = new Color(1f, 0.95f, 0.8f, 1f);
            toggle.targetGraphic = itemImage;
            ColorBlock colors = toggle.colors;
            colors.normalColor = Card;
            colors.highlightedColor = Yellow;
            colors.pressedColor = Yellow;
            colors.selectedColor = Yellow;
            toggle.colors = colors;
            Text itemLabel = MakeText("Item Label", item.transform, font, "Option", 17, TextAnchor.MiddleLeft, Ink);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            ScrollRect scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
            return dropdown;
        }

        /// <summary>
        /// Replaces a dropdown's choices without treating that as a user click.
        /// </summary>
        void FillFixedDropdown(Dropdown dropdown, string[] options, int value)
        {
            if (dropdown == null || options == null) return;
            suppressDropdown = true;
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            if (value < 0) value = 0;
            if (value >= options.Length) value = options.Length - 1;
            dropdown.value = value;
            dropdown.RefreshShownValue();
            suppressDropdown = false;
        }

        /// <summary>
        /// Shows the current choice. A refresh does not count as the user picking it.
        /// </summary>
        void SetDropdown(Dropdown dropdown, int value)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0) return;
            if (value < 0) value = 0;
            if (value >= dropdown.options.Count) value = dropdown.options.Count - 1;
            suppressDropdown = true;
            dropdown.value = value;
            dropdown.RefreshShownValue();
            suppressDropdown = false;
        }

        /// <summary>
        /// Index of the saved resolution in the dropdown.
        /// </summary>
        int ResolutionIndex()
        {
            if (requestWidth == 640) return 1;
            if (requestWidth == 1280) return 2;
            if (requestWidth == 1920) return 3;
            return 0;
        }

        /// <summary>
        /// Index of the saved frame rate in the dropdown.
        /// </summary>
        int FpsIndex()
        {
            if (requestFps == 15) return 0;
            if (requestFps == 60) return 2;
            return 1;
        }

        /// <summary>
        /// Index of the tracking strictness in the dropdown.
        /// </summary>
        int DetailIndex()
        {
            if (jointThreshold < 0.16f) return 0;
            if (jointThreshold < 0.28f) return 1;
            return 2;
        }

        /// <summary>
        /// Opens the camera chosen in the dropdown.
        /// </summary>
        void PickCamera(int index)
        {
            if (cachedDevices == null || index < 0 || index >= cachedDevices.Length) return;
            SelectListedCamera(index);
        }

        /// <summary>
        /// Applies a resolution from the dropdown and reopens the camera.
        /// </summary>
        void PickResolution(int index)
        {
            if (index == 1)
            {
                requestWidth = 640;
                requestHeight = 480;
            }
            else if (index == 2)
            {
                requestWidth = 1280;
                requestHeight = 720;
            }
            else if (index == 3)
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
        /// Applies a frame rate from the dropdown and reopens the camera.
        /// </summary>
        void PickFrameRate(int index)
        {
            requestFps = index == 0 ? 15 : index == 2 ? 60 : 30;
            SaveSettings();
            ReopenCamera();
        }

        /// <summary>
        /// Turns the selfie mirror on or off.
        /// </summary>
        void PickMirror(int index)
        {
            mirror = index == 0;
            SaveSettings();
        }

        /// <summary>
        /// Sets the extra rotation from the dropdown.
        /// </summary>
        void PickRotation(int index)
        {
            extraRotation = index * 90;
            if (extraRotation < 0 || extraRotation > 270) extraRotation = 0;
            SaveSettings();
        }

        /// <summary>
        /// Sets how many people to track. One person uses the Handline Lightning model.
        /// </summary>
        void PickPeople(int index)
        {
            peopleLimit = Mathf.Clamp(index + 1, 1, MaxPeople);
            for (int i = peopleLimit; i < MaxPeople; i++)
            {
                Blank(stable[i]);
                ClearTrack(i);
                slotUsed[i] = false;
                slotSeen[i] = 0f;
            }

            ApplyModelLabel();
            SetStatus(modelLabel);
            SaveSettings();
        }

        /// <summary>
        /// Sets how sure a new joint must be before it moves the skeleton.
        /// </summary>
        void PickDetail(int index)
        {
            if (index == 0) jointThreshold = 0.12f;
            else if (index == 2) jointThreshold = 0.35f;
            else jointThreshold = 0.2f;
            SaveSettings();
        }

        /// <summary>
        /// Turns the camera on or off from the dropdown.
        /// </summary>
        void PickPower(int index)
        {
            if (index == 0)
            {
                if (cameraHeldOff && !resuming) StartCoroutine(Resume());
                return;
            }

            if (!cameraHeldOff) StopCapture("Camera off. Turn it on in Settings, or press Enter.");
            RefreshSettingLabels();
        }

        /// <summary>
        /// Rewrites the settings labels from the current choices.
        /// </summary>
        void RefreshSettingLabels()
        {
            SyncCameraDropdown();
            SetDropdown(resolutionDropdown, ResolutionIndex());
            SetDropdown(fpsDropdown, FpsIndex());
            SetDropdown(mirrorDropdown, mirror ? 0 : 1);
            SetDropdown(rotationDropdown, extraRotation / 90);
            SetDropdown(peopleDropdown, peopleLimit - 1);
            SetDropdown(detailDropdown, DetailIndex());
            SetDropdown(powerDropdown, cameraHeldOff ? 1 : 0);
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
            if (!ModelReady())
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
        /// Fills the camera dropdown when the devices or the selection change.
        /// </summary>
        void SyncCameraDropdown()
        {
            if (cameraDropdown == null) return;
            WebCamDevice[] devices = cachedDevices ?? new WebCamDevice[0];
            string sig = cameraIndex.ToString();
            for (int i = 0; i < devices.Length; i++) sig += "|" + devices[i].name;
            if (sig != deviceSig || cameraDropdown.options.Count != Mathf.Max(1, devices.Length))
            {
                deviceSig = sig;
                List<string> names = new List<string>();
                if (devices.Length == 0) names.Add("No camera yet");
                else
                {
                    for (int i = 0; i < devices.Length; i++)
                    {
                        string label = ShortName(devices[i].name);
                        if (devices[i].isFrontFacing) label += "  ·  front";
                        names.Add(label);
                    }
                }

                suppressDropdown = true;
                cameraDropdown.ClearOptions();
                cameraDropdown.AddOptions(names);
                suppressDropdown = false;
            }

            SetDropdown(cameraDropdown, devices.Length == 0 ? 0 : cameraIndex);
        }

        /// <summary>
        /// Opens the camera the user picked in the settings list.
        /// </summary>
        void SelectListedCamera(int index)
        {
            waitingForPhone = false;
            waitingForCable = false;
            cameraHeldOff = false;
            if (!ModelReady())
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
            if (cameraHeldOff || !ModelReady()) return;
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
            rect.anchoredPosition = new Vector2(x, -y);
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
            if (sprite != null)
            {
                Image face = MakeImage("Face", go.transform, sprite, kind == Btn.Filled ? Yellow : Card);
                face.type = Image.Type.Sliced;
                face.raycastTarget = false;
                RectTransform faceRect = face.rectTransform;
                faceRect.anchorMin = Vector2.zero;
                faceRect.anchorMax = Vector2.one;
                faceRect.offsetMin = new Vector2(3f, 3f);
                faceRect.offsetMax = new Vector2(-3f, -3f);
            }

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            Color labelColor = kind == Btn.Filled ? Ink : YellowDeep;
            colors.normalColor = Border;
            colors.highlightedColor = YellowDeep;
            colors.pressedColor = new Color(0.42f, 0.28f, 0.04f, 1f);
            colors.selectedColor = colors.highlightedColor;

            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            Text text = MakeText("Label", go.transform, font, label, 17, TextAnchor.MiddleCenter, labelColor);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>
        /// Adds a dark rim so a joint or bone stays visible on the camera picture.
        /// </summary>
        static void AddInkEdge(Image image)
        {
            Outline edge = image.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.1f, 0.07f, 0.02f, 0.92f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = true;
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
            text.alignByGeometry = true;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
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
        /// One smoothed body joint. Seen is the last time MoveNet measured it.
        /// </summary>
        struct TrackPoint
        {
            public float X;
            public float Y;
            public float Score;
            public float Seen;
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
