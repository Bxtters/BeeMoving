using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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
    /// The settings screens live in Camera_MoCap.Ui.
    /// Saved choices and the phone steps live in Camera_MoCap.Guides.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class Camera_MoCap : MonoBehaviour
    {
        /// <summary>
        /// Creates the camera stage when Play starts and the open scene does not already contain it.
        /// </summary>
        public const bool OpenInsteadOfLane = true;

        /// <summary>MoveNet MultiPose Lightning publishes at most six people.</summary>
        public const int MaxPeople = 6;

        const int CropSize = 192;
        // 384 is the crop sent to MoveNet. The model then scales that square to 192 or 256.
        const int HandlineSize = 384;
        // How long a lost person keeps a color before the slot is cleared.
        const float HoldSeconds = 0.7f;
        // Minimum score for a joint to be drawn on the corner card.
        const float DrawScore = 0.25f;
        const string MultiModelFile = "movenet_multipose.tflite";
        const string SingleModelFile = "movenet_lightning.tflite";
        const string PrivacyNotice = "Shown in this window only. The picture is not saved or uploaded. Faces are not drawn.";
        const string HandNotice = "Each hand is the wrist. MoveNet Lightning has no finger joints, so none are drawn.";
        // PlayerPrefs keys. Added so resolution, mirror, people, and the chosen camera survive the next Play.
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
            // Pairs of joints for the corner skeleton. The face is omitted, so nose, eyes, and ears are not drawn.
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
        bool stagePlaced;
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
        Color32[] cameraPixels;
        Texture2D previewTex;
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

            if (webcam != null && webcam.isPlaying && webcam.didUpdateThisFrame && webcam.width > 16)
            {
                CaptureAndEstimate();
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
        /// Loads the single-person Lightning file first, then the six-person file for groups.
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
        /// Lightning for one person. MultiPose when the dropdown asks for more.
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
            bool single = active == engine;
            modelLabel = single ? "MoveNet Lightning" : "MoveNet MultiPose Lightning";
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
                int pixels = sw * sh;
                if (cameraPixels == null || cameraPixels.Length != pixels) cameraPixels = new Color32[pixels];
                Color32[] filled = webcam.GetPixels32(cameraPixels);
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
                    if (i < crowdCount && crowd[i] != null && crowd[i].Joints != null && puppet != null)
                    {
                        puppet.NoteHead(i, crowd[i].Joints[(int)JointId.Nose]);
                    }

                    if (i < crowdCount) DropFace(crowd[i]);
                    else Blank(crowd[i]);
                }

                ApplyRawPoses();
                for (int i = 0; i < MaxPeople; i++) Blank(crowd[i]);
                crowdCount = 0;
                poseUntil = Time.unscaledTime + 0.45f;
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
            KeepLegsOffArms(dest);
        }

        /// <summary>
        /// Drops a knee or ankle that is sitting on an arm. A raised arm stays an arm,
        /// and the leg eases down instead of climbing to the head.
        /// </summary>
        static void KeepLegsOffArms(PoseFrame pose)
        {
            if (pose == null || pose.Joints == null) return;
            DropLegOnArm(pose, JointId.LeftKnee, JointId.LeftHip);
            DropLegOnArm(pose, JointId.RightKnee, JointId.RightHip);
            DropLegOnArm(pose, JointId.LeftAnkle, JointId.LeftHip);
            DropLegOnArm(pose, JointId.RightAnkle, JointId.RightHip);
        }

        /// <summary>
        /// Clears one leg joint when it is above the shoulders, or in the upper body and closer to an arm than to its hip.
        /// </summary>
        static void DropLegOnArm(PoseFrame pose, JointId legId, JointId hipId)
        {
            int leg = (int)legId;
            int hip = (int)hipId;
            if (leg >= pose.Joints.Length || hip >= pose.Joints.Length) return;
            Joint point = pose.Joints[leg];
            if (point.Score < 0.1f) return;

            float shoulderY;
            bool hasShoulder = TopShoulder(pose, out shoulderY);
            if (hasShoulder && point.Y < shoulderY + 0.04f)
            {
                pose.Joints[leg] = default(Joint);
                return;
            }

            Joint hipJoint = pose.Joints[hip];
            if (hipJoint.Score < 0.2f) return;
            float chestY = hasShoulder ? (shoulderY + hipJoint.Y) * 0.5f : hipJoint.Y - 0.08f;
            if (point.Y > chestY) return;

            float arm = NearestArm(pose, point);
            float dx = point.X - hipJoint.X;
            float dy = point.Y - hipJoint.Y;
            float hipDist = (dx * dx) + (dy * dy);
            if (arm < hipDist * 0.64f) pose.Joints[leg] = default(Joint);
        }

        /// <summary>
        /// The higher shoulder in the picture. Y grows downward, so the smaller value is higher.
        /// </summary>
        static bool TopShoulder(PoseFrame pose, out float y)
        {
            y = 1f;
            bool any = false;
            Joint left = pose.Joints[(int)JointId.LeftShoulder];
            Joint right = pose.Joints[(int)JointId.RightShoulder];
            if (left.Score >= 0.2f)
            {
                y = left.Y;
                any = true;
            }

            if (right.Score >= 0.2f && (!any || right.Y < y))
            {
                y = right.Y;
                any = true;
            }

            return any;
        }

        /// <summary>
        /// Squared distance from a point to the nearest elbow or wrist. A missing arm returns a huge distance.
        /// </summary>
        static float NearestArm(PoseFrame pose, Joint point)
        {
            float best = float.MaxValue;
            int[] arms =
            {
                (int)JointId.LeftElbow,
                (int)JointId.RightElbow,
                (int)JointId.LeftWrist,
                (int)JointId.RightWrist
            };
            for (int i = 0; i < arms.Length; i++)
            {
                int id = arms[i];
                if (id >= pose.Joints.Length) continue;
                Joint arm = pose.Joints[id];
                if (arm.Score < 0.2f) continue;
                float dx = point.X - arm.X;
                float dy = point.Y - arm.Y;
                float dist = (dx * dx) + (dy * dy);
                if (dist < best) best = dist;
            }

            return best;
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
        /// Copies the MoveNet read straight onto the stage. No extra smoothing or guesses.
        /// </summary>
        void ApplyRawPoses()
        {
            for (int i = 0; i < MaxPeople; i++)
            {
                if (i < crowdCount && crowd[i] != null && crowd[i].Joints != null)
                {
                    CopyBody(crowd[i], stable[i]);
                    slotUsed[i] = HasBody(stable[i]);
                }
                else
                {
                    Blank(stable[i]);
                    slotUsed[i] = false;
                }
            }
        }

        /// <summary>
        /// Follows a joint only while it is inside the picture. An off-screen joint stays
        /// at the last place it was seen and does not move again until the camera picks it up.
        /// A sudden jump inside the frame is ignored, so a lost point is not pasted back on screen.
        /// </summary>
        void BlendSlot(int slot, PoseFrame raw)
        {
            float now = Time.unscaledTime;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            float follow = 1f - Mathf.Exp(-dt / 0.05f);
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
                if (raw != null && raw.Joints != null && i < raw.Joints.Length && InFrame(raw.Joints[i]) && Steady(point, raw.Joints[i]))
                {
                    Joint sample = raw.Joints[i];
                    if (point.Seen <= 0f)
                    {
                        point.X = sample.X;
                        point.Y = sample.Y;
                    }
                    else
                    {
                        float gain = follow * Mathf.Clamp01(0.55f + sample.Score);
                        point.X = Mathf.Lerp(point.X, sample.X, gain);
                        point.Y = Mathf.Lerp(point.Y, sample.Y, gain);
                    }

                    point.Score = sample.Score;
                    point.Seen = now;
                }

                track[slot][i] = point;
                if (point.Seen <= 0f)
                {
                    dest.Joints[i] = default(Joint);
                    continue;
                }

                dest.Joints[i] = new Joint(point.X, point.Y, point.Score < DrawScore ? DrawScore : point.Score);
                any = true;
            }

            slotUsed[slot] = any;
        }

        /// <summary>
        /// True when MoveNet still sees the joint inside the picture, not clamped to the border.
        /// </summary>
        bool InFrame(Joint joint)
        {
            if (joint.Score < jointThreshold) return false;
            if (joint.X <= 0.04f || joint.X >= 0.96f) return false;
            if (joint.Y <= 0.04f || joint.Y >= 0.96f) return false;
            return true;
        }

        /// <summary>
        /// Rejects a reading that leaps across the frame. That reading is not used until a closer one arrives.
        /// </summary>
        static bool Steady(TrackPoint point, Joint sample)
        {
            if (point.Seen <= 0f) return true;
            float dx = sample.X - point.X;
            float dy = sample.Y - point.Y;
            return (dx * dx) + (dy * dy) <= 0.28f * 0.28f;
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
                videoView.texture = previewTex;
                videoView.uvRect = new Rect(0f, 1f, 1f, -1f);
            }

            previewTex.SetPixels32(square);
            previewTex.Apply(false, false);
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
            if (stagePlaced || stage == null) return false;
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

            stagePlaced = true;
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
                puppet.Follow(stable, slotUsed, peopleLimit, jointThreshold);
                puppet.Frame(stageCamera, peopleLimit);
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
                boneRect.sizeDelta = new Vector2(length, 8f);
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
                float size = hand ? 22f : 14f;
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
        /// One smoothed body joint. Seen is the last time MoveNet measured it.
        /// </summary>
        struct TrackPoint
        {
            public float X;
            public float Y;
            public float Score;
            public float Seen;
        }

    }
}
