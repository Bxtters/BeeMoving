using UnityEngine;

namespace Handline
{
    /// <summary>
    /// A blocky polygon character on a Unity plane, in the same kind of scene as the Handline runner.
    /// The figure is standing from the start. A joint only moves when the camera sees it.
    /// Joints that are missing keep the pose they already have. Nothing is removed.
    /// Arms sit in front of the chest and the hips sit slightly back, so front and back read in the view.
    /// No face is drawn.
    /// </summary>
    public sealed class StagePuppet : MonoBehaviour
    {
        const int MaxPeople = 6;
        const float SeenScore = 0.25f;

        static readonly int[] LimbJoints =
        {
            (int)JointId.LeftShoulder, (int)JointId.LeftElbow,
            (int)JointId.LeftElbow, (int)JointId.LeftWrist,
            (int)JointId.RightShoulder, (int)JointId.RightElbow,
            (int)JointId.RightElbow, (int)JointId.RightWrist,
            (int)JointId.LeftHip, (int)JointId.LeftKnee,
            (int)JointId.LeftKnee, (int)JointId.LeftAnkle,
            (int)JointId.RightHip, (int)JointId.RightKnee,
            (int)JointId.RightKnee, (int)JointId.RightAnkle
        };

        static readonly float[] LimbThickness =
        {
            0.16f, 0.13f,
            0.16f, 0.13f,
            0.18f, 0.15f,
            0.18f, 0.15f
        };

        Transform floor;
        Transform[] roots;
        Transform[] heads;
        Transform[] chests;
        Transform[] hips;
        Transform[][] limbs;
        Vector3[][] jointPos;
        float[][] poseU;
        float[][] poseV;
        bool[][] tracked;
        float[] noseU;
        float[] noseV;
        bool[] hasNose;
        bool[] posed;
        float[] restLen;
        float[] anchorX;
        float[] anchorY;
        bool[] anchorSet;
        Material bodyMaterial;
        Material limbMaterial;
        Material headMaterial;
        Material floorMaterial;
        PoseFrame neutral;

        /// <summary>
        /// Builds the plane, a light, and one polygon figure already standing in a rest pose.
        /// </summary>
        public void Build()
        {
            bodyMaterial = MakeMaterial(new Color(0.95f, 0.34f, 0.22f, 1f), 0.05f);
            limbMaterial = MakeMaterial(new Color(0.55f, 0.18f, 0.13f, 1f), 0f);
            headMaterial = MakeMaterial(new Color(0.95f, 0.34f, 0.22f, 1f), 0.05f);
            floorMaterial = MakeMaterial(new Color(0.21f, 0.19f, 0.16f, 1f), 0.04f);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Floor";
            plane.transform.SetParent(transform, false);
            Strip(plane);
            plane.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            floor = plane.transform;
            floor.localScale = new Vector3(0.32f, 1f, 0.22f);
            floor.position = Vector3.zero;

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            int limbCount = LimbJoints.Length / 2;
            int jointCount = (int)JointId.Count;
            roots = new Transform[MaxPeople];
            heads = new Transform[MaxPeople];
            chests = new Transform[MaxPeople];
            hips = new Transform[MaxPeople];
            limbs = new Transform[MaxPeople][];
            jointPos = new Vector3[MaxPeople][];
            poseU = new float[MaxPeople][];
            poseV = new float[MaxPeople][];
            tracked = new bool[MaxPeople][];
            noseU = new float[MaxPeople];
            noseV = new float[MaxPeople];
            hasNose = new bool[MaxPeople];
            posed = new bool[MaxPeople];
            anchorX = new float[MaxPeople];
            anchorY = new float[MaxPeople];
            anchorSet = new bool[MaxPeople];
            restLen = new float[limbCount];
            neutral = NeutralPose();
            for (int person = 0; person < MaxPeople; person++)
            {
                GameObject root = new GameObject("Character " + (person + 1));
                root.transform.SetParent(transform, false);
                roots[person] = root.transform;
                chests[person] = Box("Chest", bodyMaterial, root.transform);
                hips[person] = Box("Hips", limbMaterial, root.transform);
                heads[person] = Box("Head", headMaterial, root.transform);
                limbs[person] = new Transform[limbCount];
                jointPos[person] = new Vector3[jointCount];
                poseU[person] = new float[jointCount];
                poseV[person] = new float[jointCount];
                tracked[person] = new bool[jointCount];
                for (int i = 0; i < limbCount; i++) limbs[person][i] = Box("Limb", limbMaterial, root.transform);
                Seed(person);
                Rebuild(person, 0f);
                root.SetActive(person == 0);
            }

            MeasureRest();
        }

        /// <summary>
        /// Points the camera along the plane the way the Handline runner camera does.
        /// </summary>
        public void Frame(Camera camera, int visible)
        {
            if (camera == null) return;
            if (visible < 1) visible = 1;
            float span = Mathf.Max(1.8f, ((visible - 1) * 1.15f) + 1.4f);
            if (floor != null)
            {
                float width = Mathf.Max(3.2f, span + 1.4f);
                floor.localScale = new Vector3(width / 10f, 1f, 0.24f);
                floor.position = Vector3.zero;
            }

            float dist = (span * 0.72f) / Mathf.Tan(18f * Mathf.Deg2Rad);
            if (dist < 4.6f) dist = 4.6f;
            Vector3 look = new Vector3(0f, 1.05f, 0.15f);
            camera.orthographic = false;
            camera.fieldOfView = 36f;
            camera.transform.position = new Vector3(0.35f, 1.85f, -dist);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.953f, 0.835f, 1f);
        }

        /// <summary>
        /// Keeps every character standing. Only joints the camera currently sees are moved.
        /// </summary>
        public void Follow(PoseFrame[] poses, bool[] used, int limit)
        {
            if (roots == null) return;
            if (limit < 1) limit = 1;
            if (limit > MaxPeople) limit = MaxPeople;
            for (int i = 0; i < MaxPeople; i++) roots[i].gameObject.SetActive(i < limit);

            for (int i = 0; i < limit; i++)
            {
                float originX = (i - ((limit - 1) * 0.5f)) * 1.15f;
                if (!posed[i]) Seed(i);
                if (poses != null && used != null && used[i] && i < poses.Length && poses[i] != null)
                {
                    Absorb(i, poses[i]);
                }

                Rebuild(i, originX);
            }
        }

        /// <summary>
        /// Remembers a nose point for the head. The skeleton does not draw the face.
        /// </summary>
        public void NoteHead(int person, Joint nose)
        {
            if (person < 0 || person >= MaxPeople || noseU == null) return;
            if (!Usable(nose)) return;
            if (hasNose[person])
            {
                float dx = nose.X - noseU[person];
                float dy = nose.Y - noseV[person];
                if ((dx * dx) + (dy * dy) > 0.2f * 0.2f) return;
            }

            noseU[person] = nose.X;
            noseV[person] = nose.Y;
            hasNose[person] = true;
        }

        void Seed(int person)
        {
            int end = (int)JointId.Count;
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                poseU[person][id] = neutral.Joints[id].X;
                poseV[person][id] = neutral.Joints[id].Y;
            }

            posed[person] = true;
        }

        /// <summary>
        /// Moves a joint only when this frame sees it inside the picture.
        /// The other joints keep the picture position they already have.
        /// </summary>
        void Absorb(int person, PoseFrame pose)
        {
            if (pose.Joints == null) return;
            int end = pose.Joints.Length;
            if (end > (int)JointId.Count) end = (int)JointId.Count;
            if (Usable(pose.Joints[(int)JointId.LeftHip]) && Usable(pose.Joints[(int)JointId.RightHip]))
            {
                anchorX[person] = (pose.Joints[(int)JointId.LeftHip].X + pose.Joints[(int)JointId.RightHip].X) * 0.5f;
                anchorY[person] = (pose.Joints[(int)JointId.LeftHip].Y + pose.Joints[(int)JointId.RightHip].Y) * 0.5f;
                anchorSet[person] = true;
            }

            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                Joint joint = pose.Joints[id];
                if (!Usable(joint)) continue;
                if (tracked[person][id])
                {
                    float dx = joint.X - poseU[person][id];
                    float dy = joint.Y - poseV[person][id];
                    if ((dx * dx) + (dy * dy) > 0.2f * 0.2f) continue;
                }

                poseU[person][id] = joint.X;
                poseV[person][id] = joint.Y;
                tracked[person][id] = true;
            }
        }

        static bool Usable(Joint joint)
        {
            if (joint.Score < SeenScore) return false;
            if (joint.X < 0.05f || joint.X > 0.95f) return false;
            if (joint.Y < 0.05f || joint.Y > 0.95f) return false;
            return true;
        }

        void MeasureRest()
        {
            int end = (int)JointId.Count;
            Vector3[] world = new Vector3[end];
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                world[id] = Plant(neutral.Joints[id].X, neutral.Joints[id].Y, id, 0f, 0.5f, 0.56f);
            }

            int limbCount = LimbJoints.Length / 2;
            for (int i = 0; i < limbCount; i++)
            {
                restLen[i] = Vector3.Distance(world[LimbJoints[i * 2]], world[LimbJoints[i * 2 + 1]]);
                if (restLen[i] < 0.05f) restLen[i] = 0.35f;
            }
        }

        void Rebuild(int person, float originX)
        {
            float au = anchorSet[person] ? anchorX[person] : 0.5f;
            float av = anchorSet[person] ? anchorY[person] : 0.56f;
            int end = (int)JointId.Count;
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                jointPos[person][id] = Plant(poseU[person][id], poseV[person][id], id, originX, au, av);
            }

            int limbCount = LimbJoints.Length / 2;
            if (restLen != null)
            {
                for (int i = 0; i < limbCount; i++)
                {
                    int aId = LimbJoints[i * 2];
                    int bId = LimbJoints[i * 2 + 1];
                    Vector3 delta = jointPos[person][bId] - jointPos[person][aId];
                    float length = delta.magnitude;
                    float limit = restLen[i] * 1.15f;
                    if (length > limit && length > 0.001f)
                    {
                        jointPos[person][bId] = jointPos[person][aId] + (delta / length) * limit;
                    }
                }
            }

            Vector3 leftHip = jointPos[person][(int)JointId.LeftHip];
            Vector3 rightHip = jointPos[person][(int)JointId.RightHip];
            Vector3 leftShoulder = jointPos[person][(int)JointId.LeftShoulder];
            Vector3 rightShoulder = jointPos[person][(int)JointId.RightShoulder];
            Vector3 hip = (leftHip + rightHip) * 0.5f;
            Vector3 shoulder = (leftShoulder + rightShoulder) * 0.5f;
            float height = shoulder.y - hip.y;
            if (height < 0.42f) height = 0.48f;
            float midX = (hip.x + shoulder.x) * 0.5f;
            float midZ = Mathf.Lerp(hip.z, shoulder.z, 0.35f);
            Vector3 chestBottom = new Vector3(midX, hip.y, hip.z);
            Vector3 chestTop = new Vector3(midX, hip.y + height, midZ);
            float shoulderWidth = Vector3.Distance(leftShoulder, rightShoulder);
            if (shoulderWidth < 0.28f) shoulderWidth = 0.42f;
            if (shoulderWidth > 0.7f) shoulderWidth = 0.55f;
            PlaceBox(chests[person], chestBottom, chestTop, shoulderWidth, 0.3f);
            PlaceBox(hips[person], leftHip, rightHip, 0.2f, 0.24f);

            Vector3 headPos;
            if (hasNose[person]) headPos = Plant(noseU[person], noseV[person], (int)JointId.Nose, originX, au, av);
            else headPos = chestTop + (Vector3.up * 0.16f);
            heads[person].gameObject.SetActive(true);
            heads[person].position = headPos + new Vector3(0f, 0.06f, -0.06f);
            heads[person].rotation = Quaternion.identity;
            heads[person].localScale = new Vector3(0.32f, 0.3f, 0.3f);

            for (int i = 0; i < limbCount; i++)
            {
                Vector3 a = jointPos[person][LimbJoints[i * 2]];
                Vector3 b = jointPos[person][LimbJoints[i * 2 + 1]];
                PlaceBox(limbs[person][i], a, b, LimbThickness[i], LimbThickness[i]);
            }
        }

        /// <summary>
        /// Maps one stored picture point into the scene with the same hip anchor as the rest of the body.
        /// Negative Z is toward the camera.
        /// </summary>
        static Vector3 Plant(float u, float v, int jointId, float originX, float anchorU, float anchorV)
        {
            float x = originX + ((u - anchorU) * 1.35f);
            float y = 0.9f + ((anchorV - v) * 1.7f);
            float front = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.5f) / 0.42f);
            return new Vector3(x, y, Depth(jointId, front));
        }

        /// <summary>
        /// Puts the arms in front of the torso and the hips slightly behind. A hand over the body comes closer to the camera.
        /// </summary>
        static float Depth(int jointId, float front)
        {
            float side = IsLeft(jointId) ? -0.04f : 0.04f;
            switch ((JointId)jointId)
            {
                case JointId.LeftWrist:
                case JointId.RightWrist:
                    return Mathf.Lerp(-0.1f, -0.28f, front) + side;
                case JointId.LeftElbow:
                case JointId.RightElbow:
                    return Mathf.Lerp(-0.06f, -0.18f, front) + side;
                case JointId.LeftKnee:
                case JointId.RightKnee:
                    return Mathf.Lerp(0.04f, -0.18f, front) + (side * 0.5f);
                case JointId.LeftAnkle:
                case JointId.RightAnkle:
                    return 0.06f + (side * 0.5f);
                case JointId.Nose:
                    return -0.12f;
                case JointId.LeftShoulder:
                case JointId.RightShoulder:
                    return -0.06f + side;
                default:
                    return 0.1f + (side * 0.5f);
            }
        }

        static bool IsLeft(int jointId)
        {
            return jointId == (int)JointId.LeftShoulder
                || jointId == (int)JointId.LeftElbow
                || jointId == (int)JointId.LeftWrist
                || jointId == (int)JointId.LeftHip
                || jointId == (int)JointId.LeftKnee
                || jointId == (int)JointId.LeftAnkle;
        }

        static void PlaceBox(Transform box, Vector3 a, Vector3 b, float width, float depth)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            box.gameObject.SetActive(true);
            if (length < 0.02f) return;

            box.position = (a + b) * 0.5f;
            box.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            box.localScale = new Vector3(width, length, depth);
        }

        static Transform Box(string name, Material material, Transform parent)
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

        static PoseFrame NeutralPose()
        {
            PoseFrame pose = new PoseFrame();
            Set(pose, JointId.LeftShoulder, 0.42f, 0.32f);
            Set(pose, JointId.RightShoulder, 0.58f, 0.32f);
            Set(pose, JointId.LeftElbow, 0.34f, 0.48f);
            Set(pose, JointId.RightElbow, 0.66f, 0.48f);
            Set(pose, JointId.LeftWrist, 0.32f, 0.64f);
            Set(pose, JointId.RightWrist, 0.68f, 0.64f);
            Set(pose, JointId.LeftHip, 0.46f, 0.56f);
            Set(pose, JointId.RightHip, 0.54f, 0.56f);
            Set(pose, JointId.LeftKnee, 0.45f, 0.74f);
            Set(pose, JointId.RightKnee, 0.55f, 0.74f);
            Set(pose, JointId.LeftAnkle, 0.44f, 0.92f);
            Set(pose, JointId.RightAnkle, 0.56f, 0.92f);
            return pose;
        }

        static void Set(PoseFrame pose, JointId id, float x, float y)
        {
            pose.Joints[(int)id] = new Joint(x, y, 1f);
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
