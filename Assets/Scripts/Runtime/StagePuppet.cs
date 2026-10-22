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
        float[][] seenX;
        float[][] seenY;
        bool[][] seenJoint;
        float[] anchorX;
        float[] anchorY;
        bool[] anchorSet;
        float[] slotX;
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
            seenX = new float[MaxPeople][];
            seenY = new float[MaxPeople][];
            seenJoint = new bool[MaxPeople][];
            anchorX = new float[MaxPeople];
            anchorY = new float[MaxPeople];
            anchorSet = new bool[MaxPeople];
            slotX = new float[MaxPeople];
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
                seenX[person] = new float[jointCount];
                seenY[person] = new float[jointCount];
                seenJoint[person] = new bool[jointCount];
                for (int i = 0; i < limbCount; i++) limbs[person][i] = Box("Limb", limbMaterial, root.transform);
                Seed(person, 0f);
                Rebuild(person);
                root.SetActive(person == 0);
            }
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
                if (!HasSeed(i)) Seed(i, originX);
                else ShiftSlot(i, originX);
                if (poses != null && used != null && used[i] && i < poses.Length && poses[i] != null)
                {
                    Absorb(i, poses[i], originX);
                }

                Rebuild(i);
            }
        }

        bool HasSeed(int person)
        {
            return jointPos[person][(int)JointId.LeftShoulder].sqrMagnitude > 0.0001f
                || jointPos[person][(int)JointId.RightHip].sqrMagnitude > 0.0001f;
        }

        void Seed(int person, float originX)
        {
            int end = (int)JointId.Count;
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                jointPos[person][id] = Map(neutral.Joints[id], id, originX, 0.5f, 0.56f);
            }

            slotX[person] = originX;
        }

        void ShiftSlot(int person, float originX)
        {
            float dx = originX - slotX[person];
            if (Mathf.Abs(dx) < 0.001f) return;
            int end = (int)JointId.Count;
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                Vector3 point = jointPos[person][id];
                point.x += dx;
                jointPos[person][id] = point;
            }

            slotX[person] = originX;
        }

        /// <summary>
        /// Moves a joint only when this frame actually sees it inside the picture.
        /// Anything else keeps the position it already has, so that part stays on the model.
        /// </summary>
        void Absorb(int person, PoseFrame pose, float originX)
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

            float au = anchorSet[person] ? anchorX[person] : 0.5f;
            float av = anchorSet[person] ? anchorY[person] : 0.56f;
            for (int id = (int)JointId.LeftShoulder; id < end; id++)
            {
                Joint joint = pose.Joints[id];
                if (!Usable(joint)) continue;
                if (seenJoint[person][id])
                {
                    float dx = joint.X - seenX[person][id];
                    float dy = joint.Y - seenY[person][id];
                    if ((dx * dx) + (dy * dy) > 0.22f * 0.22f) continue;
                }

                seenX[person][id] = joint.X;
                seenY[person][id] = joint.Y;
                seenJoint[person][id] = true;
                jointPos[person][id] = Map(joint, id, originX, au, av);
            }
        }

        /// <summary>
        /// A joint counts only when MoveNet sees it inside the picture, not on the border.
        /// </summary>
        static bool Usable(Joint joint)
        {
            if (joint.Score < SeenScore) return false;
            if (joint.X < 0.05f || joint.X > 0.95f) return false;
            if (joint.Y < 0.05f || joint.Y > 0.95f) return false;
            return true;
        }

        void Rebuild(int person)
        {
            Vector3 leftHip = jointPos[person][(int)JointId.LeftHip];
            Vector3 rightHip = jointPos[person][(int)JointId.RightHip];
            Vector3 leftShoulder = jointPos[person][(int)JointId.LeftShoulder];
            Vector3 rightShoulder = jointPos[person][(int)JointId.RightShoulder];
            Vector3 hip = (leftHip + rightHip) * 0.5f;
            Vector3 shoulder = (leftShoulder + rightShoulder) * 0.5f;
            Vector3 up = shoulder - hip;
            if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
            PlaceBox(chests[person], hip, shoulder, 0.42f, 0.32f);
            PlaceBox(hips[person], leftHip, rightHip, 0.36f, 0.26f);
            heads[person].gameObject.SetActive(true);
            heads[person].position = shoulder + (up.normalized * 0.22f) + new Vector3(0f, 0f, -0.04f);
            heads[person].rotation = Quaternion.identity;
            heads[person].localScale = new Vector3(0.32f, 0.3f, 0.3f);

            int limbCount = LimbJoints.Length / 2;
            for (int i = 0; i < limbCount; i++)
            {
                Vector3 a = jointPos[person][LimbJoints[i * 2]];
                Vector3 b = jointPos[person][LimbJoints[i * 2 + 1]];
                PlaceBox(limbs[person][i], a, b, LimbThickness[i], LimbThickness[i]);
            }
        }

        /// <summary>
        /// Maps a picture joint into the scene. Negative Z is toward the camera, so wrists read in front of the chest.
        /// </summary>
        static Vector3 Map(Joint joint, int jointId, float originX, float anchorX, float anchorY)
        {
            float x = originX + ((joint.X - anchorX) * 1.45f);
            float y = 0.92f + ((anchorY - joint.Y) * 1.85f);
            float front = 1f - Mathf.Clamp01(Mathf.Abs(joint.X - 0.5f) / 0.42f);
            float z = Depth(jointId, front);
            return new Vector3(x, y, z);
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
                    return Mathf.Lerp(-0.16f, -0.48f, front) + side;
                case JointId.LeftElbow:
                case JointId.RightElbow:
                    return Mathf.Lerp(-0.08f, -0.3f, front) + side;
                case JointId.LeftKnee:
                case JointId.RightKnee:
                    return Mathf.Lerp(0.04f, -0.18f, front) + (side * 0.5f);
                case JointId.LeftAnkle:
                case JointId.RightAnkle:
                    return 0.06f + (side * 0.5f);
                case JointId.LeftShoulder:
                case JointId.RightShoulder:
                    return -0.08f + side;
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
