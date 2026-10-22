using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Polygon character on a Unity plane. The body is standing when Play starts.
    /// Shoulders, elbows, wrists, hips, knees, and ankles follow the camera.
    /// A limb keeps a fixed reach, and any reach the picture does not show comes
    /// forward, toward the camera, so the arm can move in every direction.
    /// A joint that disappears eases back to the standing pose. It is not left stuck.
    /// Every joint carries speed into the next frame, so the body glides and the hands
    /// and feet trail the limbs. The face is not drawn. The head follows the nose.
    /// </summary>
    public sealed class StagePuppet : MonoBehaviour
    {
        const int MaxPeople = 6;
        const float UpperArm = 0.30f;
        const float ForeArm = 0.27f;
        const float Thigh = 0.42f;
        const float Shin = 0.40f;
        const float Neck = 0.28f;
        const float Torso = 0.48f;
        const float HipY = 0.92f;
        const float GlideBody = 0.11f;
        const float GlideElbow = 0.09f;
        const float GlideWrist = 0.15f;
        const float GlideKnee = 0.10f;
        const float GlideAnkle = 0.16f;
        const float GlideHead = 0.13f;
        const float GlideRest = 0.22f;

        static readonly Vector3[] Rest = BuildRest();

        Transform floor;
        Figure[] figures;
        Material bodyMaterial;
        Material limbMaterial;
        Material headMaterial;
        Material floorMaterial;
        int framedFor = -1;

        class Figure
        {
            public Transform Root;
            public Transform Chest;
            public Transform Pelvis;
            public Transform NeckBone;
            public Transform Head;
            public Transform UpperArmL;
            public Transform ForeArmL;
            public Transform UpperArmR;
            public Transform ForeArmR;
            public Transform ThighL;
            public Transform ShinL;
            public Transform ThighR;
            public Transform ShinR;
            public Transform HandL;
            public Transform HandR;
            public Transform FootL;
            public Transform FootR;
            public Vector3[] Pos;
            public Vector3[] Vel;
            public float[] Reach;
            public bool[] Held;
            public float[] HeldX;
            public float[] HeldY;
            public bool[] Jump;
            public float[] JumpX;
            public float[] JumpY;
            public float Scale = 1.8f;
            public bool Sized;
            public float AnchorU = 0.5f;
            public float AnchorV = 0.62f;
            public float AnchorUVel;
            public float AnchorVVel;
            public float ScaleVel;
            public bool AnchorReady;
            public float Plant;
            public float PlantVel;
            public float Shift;
            public float ShiftVel;
            public float NoseX;
            public float NoseY;
            public float NoseUntil;
        }

        /// <summary>
        /// Builds the plane, a light, and one standing figure for each person slot.
        /// </summary>
        public void Build()
        {
            bodyMaterial = MakeMaterial(new Color(0.95f, 0.34f, 0.22f, 1f), 0.05f);
            limbMaterial = MakeMaterial(new Color(0.55f, 0.18f, 0.13f, 1f), 0f);
            headMaterial = MakeMaterial(new Color(0.98f, 0.74f, 0.28f, 1f), 0.08f);
            floorMaterial = MakeMaterial(new Color(0.21f, 0.19f, 0.16f, 1f), 0.04f);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Floor";
            plane.transform.SetParent(transform, false);
            Strip(plane);
            plane.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            floor = plane.transform;
            floor.localScale = new Vector3(0.42f, 1f, 0.28f);

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            figures = new Figure[MaxPeople];
            for (int i = 0; i < MaxPeople; i++)
            {
                figures[i] = CreateFigure(i);
                figures[i].Root.gameObject.SetActive(i == 0);
                Draw(figures[i]);
            }
        }

        /// <summary>
        /// Frames the standing area from the front-side so a reach toward the camera is visible.
        /// </summary>
        public void Frame(Camera camera, int visible)
        {
            if (camera == null) return;
            if (visible < 1) visible = 1;
            if (visible > MaxPeople) visible = MaxPeople;
            float span = Mathf.Max(2.4f, ((visible - 1) * 1.35f) + 1.8f);
            if (floor != null)
            {
                floor.localScale = new Vector3(Mathf.Max(0.42f, (span + 2.2f) / 10f), 1f, 0.30f);
                floor.position = Vector3.zero;
            }

            if (framedFor == visible && camera.fieldOfView == 32f) return;
            framedFor = visible;
            float dist = Mathf.Max(4.6f, span * 1.85f);
            Vector3 look = new Vector3(0f, 1.0f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.transform.position = new Vector3(dist * 0.36f, 1.55f, -dist);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.953f, 0.835f, 1f);
        }

        /// <summary>
        /// Moves every active figure. A visible joint goes to the camera point.
        /// A joint that drops out eases back to the standing pose instead of freezing.
        /// </summary>
        public void Follow(PoseFrame[] poses, bool[] used, int limit, float score)
        {
            if (figures == null) return;
            if (limit < 1) limit = 1;
            if (limit > MaxPeople) limit = MaxPeople;
            if (score < 0.1f) score = 0.1f;
            if (score > 0.6f) score = 0.6f;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
            for (int i = 0; i < MaxPeople; i++)
            {
                Figure figure = figures[i];
                bool show = i < limit;
                if (figure.Root.gameObject.activeSelf != show) figure.Root.gameObject.SetActive(show);
                if (!show) continue;
                float lane = (i - ((limit - 1) * 0.5f)) * 1.4f;
                float targetShift = figure.AnchorReady ? Mathf.Clamp((figure.AnchorU - 0.5f) * 0.85f, -0.7f, 0.7f) : 0f;
                figure.Shift = Mathf.SmoothDamp(figure.Shift, targetShift, ref figure.ShiftVel, 0.18f, 1.8f, dt);
                figure.Root.localPosition = new Vector3(lane + figure.Shift, 0f, 0f);
                PoseFrame pose = null;
                if (poses != null && used != null && used[i] && i < poses.Length) pose = poses[i];
                Track(figure, pose, dt, score);
                Draw(figure);
            }
        }

        /// <summary>
        /// Stores a nose sample for the head. The nose is not drawn as a face.
        /// A missing nose clears the hold so the head returns to the shoulders.
        /// </summary>
        public void NoteHead(int person, Joint nose)
        {
            if (figures == null || person < 0 || person >= MaxPeople) return;
            Figure figure = figures[person];
            if (!Live(nose, 0.2f))
            {
                figure.NoseUntil = 0f;
                return;
            }

            if (figure.NoseUntil > Time.unscaledTime)
            {
                float dx = nose.X - figure.NoseX;
                float dy = nose.Y - figure.NoseY;
                if ((dx * dx) + (dy * dy) > 0.35f * 0.35f) return;
            }

            figure.NoseX = nose.X;
            figure.NoseY = nose.Y;
            figure.NoseUntil = Time.unscaledTime + 0.25f;
        }

        /// <summary>
        /// Solves one figure from the current pose, then relaxes any joint that is not in frame.
        /// </summary>
        void Track(Figure figure, PoseFrame pose, float dt, float score)
        {
            bool tracking = pose != null && pose.Joints != null;
            if (!tracking) figure.NoseUntil = 0f;
            bool leftHip = tracking && Accept(figure, JointId.LeftHip, pose, score);
            bool rightHip = tracking && Accept(figure, JointId.RightHip, pose, score);
            if (leftHip && rightHip)
            {
                Joint l = pose.Joints[(int)JointId.LeftHip];
                Joint r = pose.Joints[(int)JointId.RightHip];
                float u = (l.X + r.X) * 0.5f;
                float v = (l.Y + r.Y) * 0.5f;
                if (!figure.AnchorReady)
                {
                    figure.AnchorU = u;
                    figure.AnchorV = v;
                    figure.AnchorReady = true;
                }
                else
                {
                    figure.AnchorU = Mathf.SmoothDamp(figure.AnchorU, u, ref figure.AnchorUVel, 0.14f, 2.2f, dt);
                    figure.AnchorV = Mathf.SmoothDamp(figure.AnchorV, v, ref figure.AnchorVVel, 0.14f, 2.2f, dt);
                }

                UpdateScale(figure, pose, dt, score);
            }

            Vector3 hipL = PlaceHips(figure, pose, leftHip, rightHip, dt);
            Vector3 hipR = figure.Pos[(int)JointId.RightHip];
            Vector3 hipMid = (hipL + hipR) * 0.5f;

            bool leftShoulder = tracking && figure.Sized && Accept(figure, JointId.LeftShoulder, pose, score);
            bool rightShoulder = tracking && figure.Sized && Accept(figure, JointId.RightShoulder, pose, score);
            PlaceShoulders(figure, pose, leftShoulder, rightShoulder, hipMid, dt);

            bool ready = tracking && figure.Sized;
            PlaceArm(figure, pose, true, ready, dt, score);
            PlaceArm(figure, pose, false, ready, dt, score);
            PlaceLimb(figure, pose, JointId.LeftKnee, JointId.LeftHip, ready && Accept(figure, JointId.LeftKnee, pose, score), Thigh, dt);
            PlaceLimb(figure, pose, JointId.RightKnee, JointId.RightHip, ready && Accept(figure, JointId.RightKnee, pose, score), Thigh, dt);
            PlaceLimb(figure, pose, JointId.LeftAnkle, JointId.LeftKnee, ready && Accept(figure, JointId.LeftAnkle, pose, score), Shin, dt);
            PlaceLimb(figure, pose, JointId.RightAnkle, JointId.RightKnee, ready && Accept(figure, JointId.RightAnkle, pose, score), Shin, dt);
            PlaceHead(figure, dt);

            float lowest = Mathf.Min(figure.Pos[(int)JointId.LeftAnkle].y, figure.Pos[(int)JointId.RightAnkle].y);
            float targetPlant = Mathf.Clamp(lowest - 0.05f, -0.08f, 0.62f);
            figure.Plant = Mathf.SmoothDamp(figure.Plant, targetPlant, ref figure.PlantVel, 0.18f, 1.4f, dt);
        }

        /// <summary>
        /// Learns how many meters one camera unit covers from the shoulders and the torso.
        /// A spiked sample is ignored so the body does not pulse.
        /// </summary>
        static void UpdateScale(Figure figure, PoseFrame pose, float dt, float score)
        {
            Joint ls = pose.Joints[(int)JointId.LeftShoulder];
            Joint rs = pose.Joints[(int)JointId.RightShoulder];
            Joint lh = pose.Joints[(int)JointId.LeftHip];
            Joint rh = pose.Joints[(int)JointId.RightHip];
            float sum = 0f;
            int count = 0;
            if (Live(ls, score) && Live(rs, score))
            {
                float width = Mathf.Abs(ls.X - rs.X);
                if (width > 0.05f && width < 0.8f)
                {
                    sum += 0.40f / width;
                    count++;
                }
            }

            if (count > 0 && Live(lh, score) && Live(rh, score) && Live(ls, score) && Live(rs, score))
            {
                float sx = (ls.X + rs.X) * 0.5f;
                float sy = (ls.Y + rs.Y) * 0.5f;
                float hx = (lh.X + rh.X) * 0.5f;
                float hy = (lh.Y + rh.Y) * 0.5f;
                float dx = sx - hx;
                float dy = sy - hy;
                float torso = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (torso > 0.06f && torso < 0.85f)
                {
                    sum += Torso / torso;
                    count++;
                }
            }

            if (count == 0) return;
            float target = Mathf.Clamp(sum / count, 0.6f, 5.5f);
            if (!figure.Sized)
            {
                figure.Scale = target;
                figure.Sized = true;
                return;
            }

            if (target < figure.Scale * 0.65f || target > figure.Scale * 1.45f) return;
            figure.Scale = Mathf.SmoothDamp(figure.Scale, target, ref figure.ScaleVel, 0.4f, 2.5f, dt);
        }

        /// <summary>
        /// Puts both hips on the camera points and keeps the pelvis level.
        /// A hip that is missing moves back beside the other hip.
        /// </summary>
        Vector3 PlaceHips(Figure figure, PoseFrame pose, bool liveL, bool liveR, float dt)
        {
            int left = (int)JointId.LeftHip;
            int right = (int)JointId.RightHip;
            Vector3 wantL = Rest[left];
            Vector3 wantR = Rest[right];
            if (liveL && liveR)
            {
                FitHips(Map(figure, pose.Joints[left]), Map(figure, pose.Joints[right]), out wantL, out wantR);
            }
            else if (liveL)
            {
                Vector3 mapped = Map(figure, pose.Joints[left]);
                FitHips(mapped, mapped + (Rest[right] - Rest[left]), out wantL, out wantR);
            }
            else if (liveR)
            {
                Vector3 mapped = Map(figure, pose.Joints[right]);
                FitHips(mapped + (Rest[left] - Rest[right]), mapped, out wantL, out wantR);
            }

            figure.Pos[left] = Glide(figure, left, wantL, GlideBody, liveL, dt);
            figure.Pos[right] = Glide(figure, right, wantR, GlideBody, liveR, dt);
            if (liveL || liveR)
            {
                Vector3 fittedL;
                Vector3 fittedR;
                FitHips(figure.Pos[left], figure.Pos[right], out fittedL, out fittedR);
                figure.Pos[left] = fittedL;
                figure.Pos[right] = fittedR;
            }

            return figure.Pos[left];
        }

        /// <summary>
        /// Moves each shoulder to its camera point. The pair stays on a real torso,
        /// and a foreshortened torso comes forward instead of shrinking.
        /// A missing shoulder returns to the standing offset. It does not stay shrugged.
        /// </summary>
        void PlaceShoulders(Figure figure, PoseFrame pose, bool liveL, bool liveR, Vector3 hipMid, float dt)
        {
            int left = (int)JointId.LeftShoulder;
            int right = (int)JointId.RightShoulder;
            Vector3 hipRest = (Rest[(int)JointId.LeftHip] + Rest[(int)JointId.RightHip]) * 0.5f;
            Vector3 wantL = hipMid + (Rest[left] - hipRest);
            Vector3 wantR = hipMid + (Rest[right] - hipRest);
            if (liveL && liveR)
            {
                FitShoulders(hipMid, Map(figure, pose.Joints[left]), Map(figure, pose.Joints[right]), out wantL, out wantR);
            }
            else if (liveL)
            {
                Vector3 mapped = Map(figure, pose.Joints[left]);
                Vector3 other = new Vector3((hipMid.x * 2f) - mapped.x, mapped.y, mapped.z);
                FitShoulders(hipMid, mapped, other, out wantL, out wantR);
            }
            else if (liveR)
            {
                Vector3 mapped = Map(figure, pose.Joints[right]);
                Vector3 other = new Vector3((hipMid.x * 2f) - mapped.x, mapped.y, mapped.z);
                FitShoulders(hipMid, other, mapped, out wantL, out wantR);
            }

            figure.Pos[left] = Glide(figure, left, wantL, GlideBody, liveL, dt);
            figure.Pos[right] = Glide(figure, right, wantR, GlideBody, liveR, dt);
        }

        /// <summary>
        /// Bends the arm at the elbow from the shoulder and the wrist.
        /// A straight arm keeps the elbow on that line, so a noisy elbow point cannot kink it.
        /// A curl uses the elbow the camera sees. Depth fades out as the arm straightens.
        /// </summary>
        void PlaceArm(Figure figure, PoseFrame pose, bool left, bool ready, float dt, float score)
        {
            JointId elbowId = left ? JointId.LeftElbow : JointId.RightElbow;
            JointId wristId = left ? JointId.LeftWrist : JointId.RightWrist;
            int shoulder = left ? (int)JointId.LeftShoulder : (int)JointId.RightShoulder;
            int otherShoulder = left ? (int)JointId.RightShoulder : (int)JointId.LeftShoulder;
            int hip = left ? (int)JointId.LeftHip : (int)JointId.RightHip;
            int elbow = (int)elbowId;
            int wrist = (int)wristId;
            Vector3 origin = figure.Pos[shoulder];
            Vector3 across = figure.Pos[otherShoulder];
            Vector3 hipPoint = figure.Pos[hip];
            bool seeElbow = ready && Accept(figure, elbowId, pose, score);
            bool seeWrist = ready && Accept(figure, wristId, pose, score);

            Vector3 desiredElbow;
            Vector3 desiredWrist;
            if (seeElbow)
            {
                Vector3 elbowImage = Map(figure, pose.Joints[elbow]);
                desiredElbow = SoftBone(origin, elbowImage, UpperArm);
                BringInFront(ref desiredElbow, elbowImage, origin, across, hipPoint);
            }
            else
            {
                desiredElbow = Project(origin, origin + (Rest[elbow] - Rest[shoulder]), UpperArm);
            }

            if (seeWrist)
            {
                Vector3 wristImage = Map(figure, pose.Joints[wrist]);
                float dx = wristImage.x - origin.x;
                float dy = wristImage.y - origin.y;
                float imageReach = Mathf.Sqrt((dx * dx) + (dy * dy));
                float straight = imageReach / (UpperArm + ForeArm);
                if (straight > 0.9f && imageReach > 0.001f)
                {
                    Vector3 pinned = origin + new Vector3(dx, dy, 0f) * (UpperArm / imageReach);
                    float pin = Mathf.InverseLerp(0.9f, 0.98f, straight);
                    desiredElbow = Vector3.Lerp(desiredElbow, pinned, pin);
                }

                desiredWrist = SoftBone(desiredElbow, wristImage, ForeArm);
                float before = desiredWrist.z;
                BringInFront(ref desiredWrist, wristImage, origin, across, hipPoint);
                if (desiredWrist.z < before - 0.04f)
                {
                    desiredElbow.z = Mathf.Min(desiredElbow.z, desiredWrist.z + 0.1f);
                }
            }
            else
            {
                desiredWrist = Project(desiredElbow, desiredElbow + (Rest[wrist] - Rest[elbow]), ForeArm);
            }

            bool armLive = seeElbow || seeWrist;
            Vector3 nextElbow = Glide(figure, elbow, desiredElbow, GlideElbow, armLive, dt);
            figure.Pos[elbow] = Project(origin, nextElbow, UpperArm);
            Vector3 nextWrist = Glide(figure, wrist, desiredWrist, GlideWrist, seeWrist, dt);
            figure.Pos[wrist] = Project(figure.Pos[elbow], nextWrist, ForeArm);
        }

        /// <summary>
        /// Moves a hand or elbow toward the camera when it sits over the chest or the face.
        /// Reaching behind the body is left as it is.
        /// </summary>
        static void BringInFront(ref Vector3 joint, Vector3 image, Vector3 shoulder, Vector3 otherShoulder, Vector3 hip)
        {
            float amount;
            bool byHead;
            if (!InFrontOfBody(image, shoulder, otherShoulder, hip, out amount, out byHead)) return;
            Vector3 crown = (shoulder + otherShoulder) * 0.5f;
            crown.y += 0.24f;
            crown.z -= 0.04f;
            float ahead = byHead ? crown.z - 0.26f : shoulder.z - 0.32f;
            joint.z = Mathf.Lerp(joint.z, Mathf.Min(joint.z, ahead), amount);
        }

        /// <summary>
        /// How far a camera point lies over the torso or the face. One means it is in front of the body.
        /// </summary>
        static bool InFrontOfBody(Vector3 image, Vector3 shoulder, Vector3 otherShoulder, Vector3 hip, out float amount, out bool byHead)
        {
            amount = 0f;
            byHead = false;
            float midX = (shoulder.x + otherShoulder.x) * 0.5f;
            float torsoHalf = Mathf.Abs(otherShoulder.x - shoulder.x) * 0.5f + 0.06f;
            if (torsoHalf < 0.16f) torsoHalf = 0.16f;
            float dx = Mathf.Abs(image.x - midX);

            if (image.y > shoulder.y - 0.04f)
            {
                const float faceHalf = 0.22f;
                float across = 1f - Mathf.InverseLerp(faceHalf * 0.3f, faceHalf, dx);
                if (across <= 0f) return false;
                byHead = true;
                amount = across;
                return true;
            }

            if (image.y < hip.y - 0.06f) return false;
            float overChest = 1f - Mathf.InverseLerp(torsoHalf * 0.4f, torsoHalf, dx);
            if (overChest <= 0f) return false;
            amount = overChest;
            return true;
        }

        /// <summary>
        /// Places a child joint one bone-length from its parent.
        /// The camera point is matched on screen, and the leftover length points at the camera.
        /// When the joint is not seen, the bone swings back to its standing direction.
        /// </summary>
        void PlaceLimb(Figure figure, PoseFrame pose, JointId childId, JointId parentId, bool seen, float fallback, float dt)
        {
            int child = (int)childId;
            int parent = (int)parentId;
            Vector3 origin = figure.Pos[parent];
            Vector3 restPoint = origin + (Rest[child] - Rest[parent]);
            Vector3 desired;
            if (seen && pose != null)
            {
                Vector3 target = Map(figure, pose.Joints[child]);
                float dx = target.x - origin.x;
                float dy = target.y - origin.y;
                float planar = Mathf.Sqrt((dx * dx) + (dy * dy));
                float sample = planar;
                float cap = fallback * 1.4f;
                if (sample > cap) sample = cap;
                if (sample > figure.Reach[child]) figure.Reach[child] = Mathf.Lerp(figure.Reach[child], sample, 0.35f);
                float length = Mathf.Clamp(figure.Reach[child], fallback * 0.85f, fallback * 1.4f);
                desired = OnBone(origin, target, length);
            }
            else
            {
                float length = Mathf.Clamp(figure.Reach[child], fallback * 0.85f, fallback * 1.4f);
                desired = Project(origin, restPoint, length);
            }

            bool ankle = childId == JointId.LeftAnkle || childId == JointId.RightAnkle;
            float smooth = ankle ? GlideAnkle : GlideKnee;
            Vector3 next = Glide(figure, child, desired, smooth, seen, dt);
            float keep = Mathf.Clamp(figure.Reach[child], fallback * 0.85f, fallback * 1.4f);
            figure.Pos[child] = Project(origin, next, keep);
        }

        /// <summary>
        /// Sits the head on the shoulders and leans it toward the nose while the nose is visible.
        /// </summary>
        void PlaceHead(Figure figure, float dt)
        {
            int nose = (int)JointId.Nose;
            Vector3 shoulderMid = (figure.Pos[(int)JointId.LeftShoulder] + figure.Pos[(int)JointId.RightShoulder]) * 0.5f;
            bool seen = figure.NoseUntil >= Time.unscaledTime && figure.Sized;
            Vector3 restPoint = shoulderMid + new Vector3(0f, Neck, -0.02f);
            Vector3 desired = restPoint;
            if (seen)
            {
                Vector3 target = Map(figure, figure.NoseX, figure.NoseY);
                if (target.y < shoulderMid.y + 0.04f) target = restPoint;
                desired = OnBone(shoulderMid, target, Neck);
            }

            Vector3 next = Glide(figure, nose, desired, GlideHead, seen, dt);
            figure.Pos[nose] = Project(shoulderMid, next, Neck);
        }

        /// <summary>
        /// Maps a camera point into the figure. X is right, Y is up, and the hips sit near standing height.
        /// </summary>
        static Vector3 Map(Figure figure, Joint joint)
        {
            return Map(figure, joint.X, joint.Y);
        }

        static Vector3 Map(Figure figure, float u, float v)
        {
            return new Vector3((u - figure.AnchorU) * figure.Scale, ((figure.AnchorV - v) * figure.Scale) + HipY, 0f);
        }

        /// <summary>
        /// Keeps the pelvis a normal width and level, so one noisy hip cannot kick a leg sideways.
        /// </summary>
        static void FitHips(Vector3 left, Vector3 right, out Vector3 fittedL, out Vector3 fittedR)
        {
            float y = (left.y + right.y) * 0.5f;
            float mid = (left.x + right.x) * 0.5f;
            float half = Mathf.Abs(right.x - left.x) * 0.5f;
            if (half < 0.08f) half = 0.08f;
            if (half > 0.2f) half = 0.2f;
            float order = right.x >= left.x ? 1f : -1f;
            fittedL = new Vector3(mid - (half * order), y, 0.02f);
            fittedR = new Vector3(mid + (half * order), y, 0.02f);
        }

        /// <summary>
        /// Keeps the shoulders on a torso-length spine. A short spine in the picture
        /// is a lean toward the camera, not a shorter body.
        /// </summary>
        static void FitShoulders(Vector3 hipMid, Vector3 left, Vector3 right, out Vector3 fittedL, out Vector3 fittedR)
        {
            float yL = left.y;
            float yR = right.y;
            float yMid = (yL + yR) * 0.5f;
            yL = Mathf.Clamp(yL, yMid - 0.18f, yMid + 0.18f);
            yR = Mathf.Clamp(yR, yMid - 0.18f, yMid + 0.18f);
            yMid = (yL + yR) * 0.5f;

            float midX = (left.x + right.x) * 0.5f;
            float half = Mathf.Abs(right.x - left.x) * 0.5f;
            if (half < 0.12f) half = 0.12f;
            if (half > 0.32f) half = 0.32f;
            float order = right.x >= left.x ? 1f : -1f;
            float xL = midX - (half * order);
            float xR = midX + (half * order);

            float dx = midX - hipMid.x;
            float dy = yMid - hipMid.y;
            float planar = Mathf.Sqrt((dx * dx) + (dy * dy));
            bool upright = dy > 0.05f && dy > Mathf.Abs(dx);
            float x;
            float y;
            float z;
            if (planar < 0.04f)
            {
                x = hipMid.x;
                y = hipMid.y + Torso;
                z = hipMid.z - 0.04f;
            }
            else if (!upright || planar >= Torso * 0.82f)
            {
                float span = planar > Torso ? Torso / planar : 1f;
                x = hipMid.x + (dx * span);
                y = hipMid.y + (dy * span);
                z = hipMid.z - 0.04f;
            }
            else
            {
                x = hipMid.x + dx;
                y = hipMid.y + dy;
                z = hipMid.z - Mathf.Sqrt((Torso * Torso) - (planar * planar));
            }

            if (y < hipMid.y + 0.12f && z > hipMid.z - 0.16f) y = hipMid.y + 0.12f;
            float horiz = Mathf.Abs(x - hipMid.x);
            float vert = Mathf.Max(0.16f, y - hipMid.y);
            if (horiz > vert * 0.9f)
            {
                float sign = x < hipMid.x ? -1f : 1f;
                x = hipMid.x + (sign * vert * 0.9f);
            }

            float shiftX = x - midX;
            xL += shiftX;
            xR += shiftX;
            float shrugL = yL - yMid;
            float shrugR = yR - yMid;
            fittedL = new Vector3(xL, y + shrugL, z);
            fittedR = new Vector3(xR, y + shrugR, z);
        }

        /// <summary>
        /// Puts a point on the bone sphere. Screen position is kept when the bone can reach it.
        /// The part of the bone the camera cannot see points toward the camera.
        /// </summary>
        static Vector3 OnBone(Vector3 parent, Vector3 target, float length)
        {
            float dx = target.x - parent.x;
            float dy = target.y - parent.y;
            float planar = Mathf.Sqrt((dx * dx) + (dy * dy));
            if (planar < 0.0008f) return new Vector3(parent.x, parent.y, parent.z - length);
            if (planar >= length)
            {
                float span = length / planar;
                return new Vector3(parent.x + (dx * span), parent.y + (dy * span), parent.z);
            }

            float depth = -Mathf.Sqrt((length * length) - (planar * planar));
            return new Vector3(target.x, target.y, parent.z + depth);
        }

        /// <summary>
        /// Same reach as OnBone, but the forward pop fades out as the limb straightens.
        /// A nearly straight elbow stays on the arm instead of jumping at the camera.
        /// </summary>
        static Vector3 SoftBone(Vector3 parent, Vector3 target, float length)
        {
            float dx = target.x - parent.x;
            float dy = target.y - parent.y;
            float planar = Mathf.Sqrt((dx * dx) + (dy * dy));
            if (planar < 0.0008f) return new Vector3(parent.x, parent.y, parent.z - (length * 0.85f));
            if (planar >= length)
            {
                float span = length / planar;
                return new Vector3(parent.x + (dx * span), parent.y + (dy * span), parent.z);
            }

            float ratio = planar / length;
            float weight = 1f - Mathf.SmoothStep(0.8f, 0.98f, ratio);
            float depth = -Mathf.Sqrt((length * length) - (planar * planar)) * weight;
            return new Vector3(target.x, target.y, parent.z + depth);
        }

        /// <summary>
        /// Keeps a point exactly one bone-length from its parent, in the direction it already has.
        /// </summary>
        static Vector3 Project(Vector3 parent, Vector3 point, float length)
        {
            Vector3 delta = point - parent;
            float mag = delta.magnitude;
            if (mag < 0.0001f) return parent + new Vector3(0f, -length, 0f);
            return parent + (delta * (length / mag));
        }

        /// <summary>
        /// Accepts a joint that is inside the picture. One wild jump is skipped.
        /// The next steady sample is used, so a lost point is not glued in place.
        /// </summary>
        static bool Accept(Figure figure, JointId id, PoseFrame pose, float score)
        {
            int index = (int)id;
            if (pose == null || pose.Joints == null || index >= pose.Joints.Length) return false;
            Joint joint = pose.Joints[index];
            if (!Live(joint, score))
            {
                figure.Jump[index] = false;
                return false;
            }

            if (!figure.Held[index])
            {
                figure.Held[index] = true;
                figure.HeldX[index] = joint.X;
                figure.HeldY[index] = joint.Y;
                figure.Jump[index] = false;
                return true;
            }

            float dx = joint.X - figure.HeldX[index];
            float dy = joint.Y - figure.HeldY[index];
            if ((dx * dx) + (dy * dy) > 0.35f * 0.35f)
            {
                if (figure.Jump[index])
                {
                    float jx = joint.X - figure.JumpX[index];
                    float jy = joint.Y - figure.JumpY[index];
                    if ((jx * jx) + (jy * jy) < 0.12f * 0.12f)
                    {
                        figure.HeldX[index] = joint.X;
                        figure.HeldY[index] = joint.Y;
                        figure.Jump[index] = false;
                        return true;
                    }
                }

                figure.Jump[index] = true;
                figure.JumpX[index] = joint.X;
                figure.JumpY[index] = joint.Y;
                return false;
            }

            figure.HeldX[index] = joint.X;
            figure.HeldY[index] = joint.Y;
            figure.Jump[index] = false;
            return true;
        }

        static bool Live(Joint joint, float score)
        {
            if (joint.Score < score) return false;
            if (joint.X <= 0.01f || joint.X >= 0.99f) return false;
            if (joint.Y <= 0.01f || joint.Y >= 0.99f) return false;
            return true;
        }

        /// <summary>
        /// Eases a joint toward its pose. Speed carries into the next frame, so motion
        /// accelerates and settles instead of stepping. Hands and feet use a longer time
        /// and trail the limb. A lost joint glides back more slowly.
        /// </summary>
        static Vector3 Glide(Figure figure, int index, Vector3 target, float smoothTime, bool live, float dt)
        {
            float time = live ? smoothTime : GlideRest;
            float maxSpeed = live ? 7f : 2.4f;
            Vector3 velocity = figure.Vel[index];
            Vector3 next = Vector3.SmoothDamp(figure.Pos[index], target, ref velocity, time, maxSpeed, dt);
            figure.Vel[index] = velocity;
            return next;
        }

        /// <summary>
        /// Writes the boxes from the solved joints. Length is the distance between those joints.
        /// </summary>
        void Draw(Figure figure)
        {
            float plant = figure.Plant;
            Vector3 hipL = Shown(figure.Pos[(int)JointId.LeftHip], plant);
            Vector3 hipR = Shown(figure.Pos[(int)JointId.RightHip], plant);
            Vector3 shL = Shown(figure.Pos[(int)JointId.LeftShoulder], plant);
            Vector3 shR = Shown(figure.Pos[(int)JointId.RightShoulder], plant);
            Vector3 elL = Shown(figure.Pos[(int)JointId.LeftElbow], plant);
            Vector3 elR = Shown(figure.Pos[(int)JointId.RightElbow], plant);
            Vector3 wrL = Shown(figure.Pos[(int)JointId.LeftWrist], plant);
            Vector3 wrR = Shown(figure.Pos[(int)JointId.RightWrist], plant);
            Vector3 knL = Shown(figure.Pos[(int)JointId.LeftKnee], plant);
            Vector3 knR = Shown(figure.Pos[(int)JointId.RightKnee], plant);
            Vector3 anL = Shown(figure.Pos[(int)JointId.LeftAnkle], plant);
            Vector3 anR = Shown(figure.Pos[(int)JointId.RightAnkle], plant);
            Vector3 head = Shown(figure.Pos[(int)JointId.Nose], plant);
            Vector3 hipMid = (hipL + hipR) * 0.5f;
            Vector3 shoulderMid = (shL + shR) * 0.5f;

            Orient(figure.Pelvis, hipL, hipR, 0.18f, 0.22f, true);
            Orient(figure.Chest, hipMid, shoulderMid, Mathf.Clamp(Mathf.Abs(shR.x - shL.x) + 0.08f, 0.28f, 0.72f), 0.26f, false);
            Orient(figure.NeckBone, shoulderMid, head, 0.1f, 0.1f, false);
            figure.Head.localPosition = head;
            figure.Head.localRotation = Quaternion.identity;
            figure.Head.localScale = new Vector3(0.28f, 0.3f, 0.26f);

            Orient(figure.UpperArmL, shL, elL, 0.1f, 0.1f, false);
            Orient(figure.ForeArmL, elL, wrL, 0.09f, 0.09f, false);
            Orient(figure.UpperArmR, shR, elR, 0.1f, 0.1f, false);
            Orient(figure.ForeArmR, elR, wrR, 0.09f, 0.09f, false);
            Orient(figure.ThighL, hipL, knL, 0.14f, 0.14f, false);
            Orient(figure.ShinL, knL, anL, 0.12f, 0.12f, false);
            Orient(figure.ThighR, hipR, knR, 0.14f, 0.14f, false);
            Orient(figure.ShinR, knR, anR, 0.12f, 0.12f, false);
            PlaceEnd(figure.HandL, elL, wrL, new Vector3(0.1f, 0.1f, 0.08f));
            PlaceEnd(figure.HandR, elR, wrR, new Vector3(0.1f, 0.1f, 0.08f));
            PlaceEnd(figure.FootL, knL, anL, new Vector3(0.12f, 0.1f, 0.18f));
            PlaceEnd(figure.FootR, knR, anR, new Vector3(0.12f, 0.1f, 0.18f));
        }

        static Vector3 Shown(Vector3 point, float plant)
        {
            point.y -= plant;
            return point;
        }

        /// <summary>
        /// Stretches one box between two joints. The long axis follows the bone without flipping.
        /// </summary>
        static void Orient(Transform bone, Vector3 from, Vector3 to, float thickX, float thickZ, bool alongX)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.02f)
            {
                bone.localPosition = (from + to) * 0.5f;
                bone.localScale = new Vector3(thickX, 0.02f, thickZ);
                return;
            }

            bone.localPosition = (from + to) * 0.5f;
            if (alongX)
            {
                bone.localRotation = LookAlongX(delta / length);
                bone.localScale = new Vector3(length, thickX, thickZ);
            }
            else
            {
                bone.localRotation = LookAlongY(delta / length);
                bone.localScale = new Vector3(thickX, length, thickZ);
            }
        }

        /// <summary>
        /// Puts the hand or foot just past the end joint, lined up with that limb.
        /// </summary>
        static void PlaceEnd(Transform bone, Vector3 from, Vector3 to, Vector3 scale)
        {
            Vector3 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.down;
            else dir.Normalize();
            bone.localPosition = to + (dir * (scale.y * 0.35f));
            bone.localRotation = LookAlongY(dir);
            bone.localScale = scale;
        }

        static Quaternion LookAlongY(Vector3 direction)
        {
            Vector3 y = direction.normalized;
            Vector3 x = Vector3.Cross(Vector3.forward, y);
            if (x.sqrMagnitude < 0.0001f) x = Vector3.Cross(Vector3.right, y);
            x.Normalize();
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }

        static Quaternion LookAlongX(Vector3 direction)
        {
            Vector3 x = direction.normalized;
            Vector3 y = Vector3.Cross(x, Vector3.forward);
            if (y.sqrMagnitude < 0.0001f) y = Vector3.Cross(x, Vector3.up);
            y.Normalize();
            Vector3 z = Vector3.Cross(x, y);
            return Quaternion.LookRotation(z, y);
        }

        static Vector3[] BuildRest()
        {
            Vector3[] rest = new Vector3[(int)JointId.Count];
            rest[(int)JointId.LeftHip] = new Vector3(-0.11f, HipY, 0.02f);
            rest[(int)JointId.RightHip] = new Vector3(0.11f, HipY, 0.02f);
            rest[(int)JointId.LeftShoulder] = new Vector3(-0.24f, 1.40f, -0.04f);
            rest[(int)JointId.RightShoulder] = new Vector3(0.24f, 1.40f, -0.04f);
            rest[(int)JointId.LeftElbow] = rest[(int)JointId.LeftShoulder] + new Vector3(-0.06f, -0.294f, 0f);
            rest[(int)JointId.RightElbow] = rest[(int)JointId.RightShoulder] + new Vector3(0.06f, -0.294f, 0f);
            rest[(int)JointId.LeftWrist] = rest[(int)JointId.LeftElbow] + new Vector3(0f, -ForeArm, 0f);
            rest[(int)JointId.RightWrist] = rest[(int)JointId.RightElbow] + new Vector3(0f, -ForeArm, 0f);
            rest[(int)JointId.LeftKnee] = rest[(int)JointId.LeftHip] + new Vector3(0f, -Thigh, 0f);
            rest[(int)JointId.RightKnee] = rest[(int)JointId.RightHip] + new Vector3(0f, -Thigh, 0f);
            rest[(int)JointId.LeftAnkle] = rest[(int)JointId.LeftKnee] + new Vector3(0f, -Shin, 0f);
            rest[(int)JointId.RightAnkle] = rest[(int)JointId.RightKnee] + new Vector3(0f, -Shin, 0f);
            Vector3 shoulderMid = (rest[(int)JointId.LeftShoulder] + rest[(int)JointId.RightShoulder]) * 0.5f;
            rest[(int)JointId.Nose] = shoulderMid + new Vector3(0f, Neck, -0.02f);
            return rest;
        }

        Figure CreateFigure(int index)
        {
            Figure figure = new Figure();
            int count = (int)JointId.Count;
            figure.Pos = new Vector3[count];
            figure.Vel = new Vector3[count];
            figure.Reach = new float[count];
            figure.Held = new bool[count];
            figure.HeldX = new float[count];
            figure.HeldY = new float[count];
            figure.Jump = new bool[count];
            figure.JumpX = new float[count];
            figure.JumpY = new float[count];
            for (int i = 0; i < count; i++) figure.Pos[i] = Rest[i];
            figure.Reach[(int)JointId.LeftElbow] = UpperArm;
            figure.Reach[(int)JointId.RightElbow] = UpperArm;
            figure.Reach[(int)JointId.LeftWrist] = ForeArm;
            figure.Reach[(int)JointId.RightWrist] = ForeArm;
            figure.Reach[(int)JointId.LeftKnee] = Thigh;
            figure.Reach[(int)JointId.RightKnee] = Thigh;
            figure.Reach[(int)JointId.LeftAnkle] = Shin;
            figure.Reach[(int)JointId.RightAnkle] = Shin;

            GameObject rootObject = new GameObject("Character " + (index + 1));
            rootObject.transform.SetParent(transform, false);
            figure.Root = rootObject.transform;
            figure.Pelvis = Box(figure.Root, "Pelvis", limbMaterial);
            figure.Chest = Box(figure.Root, "Chest", bodyMaterial);
            figure.NeckBone = Box(figure.Root, "Neck", limbMaterial);
            figure.Head = Box(figure.Root, "Head", headMaterial);
            figure.UpperArmL = Box(figure.Root, "Upper Arm L", limbMaterial);
            figure.ForeArmL = Box(figure.Root, "Forearm L", limbMaterial);
            figure.UpperArmR = Box(figure.Root, "Upper Arm R", limbMaterial);
            figure.ForeArmR = Box(figure.Root, "Forearm R", limbMaterial);
            figure.ThighL = Box(figure.Root, "Thigh L", limbMaterial);
            figure.ShinL = Box(figure.Root, "Shin L", limbMaterial);
            figure.ThighR = Box(figure.Root, "Thigh R", limbMaterial);
            figure.ShinR = Box(figure.Root, "Shin R", limbMaterial);
            figure.HandL = Box(figure.Root, "Hand L", headMaterial);
            figure.HandR = Box(figure.Root, "Hand R", headMaterial);
            figure.FootL = Box(figure.Root, "Foot L", limbMaterial);
            figure.FootR = Box(figure.Root, "Foot R", limbMaterial);
            return figure;
        }

        static Transform Box(Transform parent, string name, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            Strip(go);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        static void Strip(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        static Material MakeMaterial(Color color, float emission)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;
            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }

            return material;
        }

        void OnDestroy()
        {
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (limbMaterial != null) Destroy(limbMaterial);
            if (headMaterial != null) Destroy(headMaterial);
            if (floorMaterial != null) Destroy(floorMaterial);
        }
    }
}
