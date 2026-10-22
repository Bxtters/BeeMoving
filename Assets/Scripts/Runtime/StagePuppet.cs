using UnityEngine;

namespace Handline
{
    /// <summary>
    /// A blocky polygon character on a Unity plane, built the same way as the Handline runner.
    /// Limbs are cubes between the joints that are still on screen. An off-screen joint
    /// keeps the last cube pose and does not move until the camera sees it again.
    /// No face is drawn.
    /// </summary>
    public sealed class StagePuppet : MonoBehaviour
    {
        const int MaxPeople = 6;

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
        Vector3[][] heldA;
        Vector3[][] heldB;
        bool[][] heldLimb;
        Vector3[] heldHead;
        Vector3[] heldChestA;
        Vector3[] heldChestB;
        Vector3[] heldHipA;
        Vector3[] heldHipB;
        bool[] heldBody;
        Material bodyMaterial;
        Material limbMaterial;
        Material headMaterial;
        Material floorMaterial;
        PoseFrame neutral;

        /// <summary>
        /// Builds the plane, a light, and one polygon figure for each person slot.
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
            floor.localScale = new Vector3(0.28f, 1f, 0.2f);

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            int limbCount = LimbJoints.Length / 2;
            roots = new Transform[MaxPeople];
            heads = new Transform[MaxPeople];
            chests = new Transform[MaxPeople];
            hips = new Transform[MaxPeople];
            limbs = new Transform[MaxPeople][];
            heldA = new Vector3[MaxPeople][];
            heldB = new Vector3[MaxPeople][];
            heldLimb = new bool[MaxPeople][];
            heldHead = new Vector3[MaxPeople];
            heldChestA = new Vector3[MaxPeople];
            heldChestB = new Vector3[MaxPeople];
            heldHipA = new Vector3[MaxPeople];
            heldHipB = new Vector3[MaxPeople];
            heldBody = new bool[MaxPeople];
            for (int person = 0; person < MaxPeople; person++)
            {
                GameObject root = new GameObject("Character " + (person + 1));
                root.transform.SetParent(transform, false);
                roots[person] = root.transform;
                chests[person] = Box("Chest", bodyMaterial, root.transform);
                hips[person] = Box("Hips", limbMaterial, root.transform);
                heads[person] = Box("Head", headMaterial, root.transform);
                limbs[person] = new Transform[limbCount];
                heldA[person] = new Vector3[limbCount];
                heldB[person] = new Vector3[limbCount];
                heldLimb[person] = new bool[limbCount];
                for (int i = 0; i < limbCount; i++) limbs[person][i] = Box("Limb", limbMaterial, root.transform);
                root.SetActive(false);
            }

            neutral = NeutralPose();
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
                floor.localScale = new Vector3(width / 10f, 1f, 0.22f);
                floor.position = new Vector3(0f, 0f, 0f);
            }

            float dist = (span * 0.72f) / Mathf.Tan(18f * Mathf.Deg2Rad);
            if (dist < 4.2f) dist = 4.2f;
            Vector3 look = new Vector3(0f, 1.05f, 0.4f);
            camera.orthographic = false;
            camera.fieldOfView = 36f;
            camera.transform.position = new Vector3(0f, 2.35f, -dist);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.953f, 0.835f, 1f);
        }

        /// <summary>
        /// Moves each visible person. When nobody is tracked, one character stands in a rest pose.
        /// </summary>
        public void Follow(PoseFrame[] poses, bool[] used, int limit)
        {
            if (roots == null) return;
            int visible = 0;
            for (int i = 0; i < MaxPeople && i < limit; i++)
            {
                if (used != null && used[i] && poses != null && poses[i] != null) visible++;
            }

            if (visible == 0)
            {
                Place(0, neutral, 0f);
                for (int i = 1; i < MaxPeople; i++) roots[i].gameObject.SetActive(false);
                return;
            }

            int shown = 0;
            for (int i = 0; i < MaxPeople; i++)
            {
                bool live = i < limit && used != null && used[i] && poses != null && poses[i] != null;
                if (!live)
                {
                    roots[i].gameObject.SetActive(false);
                    continue;
                }

                float x = (shown - ((visible - 1) * 0.5f)) * 1.15f;
                Place(i, poses[i], x);
                shown++;
            }
        }

        void Place(int person, PoseFrame pose, float originX)
        {
            if (pose == null || pose.Joints == null) return;
            roots[person].gameObject.SetActive(true);
            bool shoulders = Live(pose, JointId.LeftShoulder) && Live(pose, JointId.RightShoulder);
            bool hipJoints = Live(pose, JointId.LeftHip) && Live(pose, JointId.RightHip);
            if (shoulders && hipJoints)
            {
                Vector3 hip = (Map(pose, JointId.LeftHip, originX) + Map(pose, JointId.RightHip, originX)) * 0.5f;
                Vector3 shoulder = (Map(pose, JointId.LeftShoulder, originX) + Map(pose, JointId.RightShoulder, originX)) * 0.5f;
                heldChestA[person] = hip;
                heldChestB[person] = shoulder;
                heldHipA[person] = Map(pose, JointId.LeftHip, originX);
                heldHipB[person] = Map(pose, JointId.RightHip, originX);
                Vector3 up = shoulder - hip;
                if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
                heldHead[person] = shoulder + (up.normalized * 0.2f);
                heldBody[person] = true;
            }

            if (shoulders && hipJoints)
            {
                PlaceBox(chests[person], heldChestA[person], heldChestB[person], 0.42f, 0.28f);
                PlaceBox(hips[person], heldHipA[person], heldHipB[person], 0.36f, 0.24f);
                heads[person].gameObject.SetActive(true);
                heads[person].position = heldHead[person];
                heads[person].rotation = Quaternion.identity;
                heads[person].localScale = new Vector3(0.32f, 0.3f, 0.3f);
            }
            else
            {
                chests[person].gameObject.SetActive(false);
                hips[person].gameObject.SetActive(false);
                heads[person].gameObject.SetActive(false);
            }

            int limbCount = LimbJoints.Length / 2;
            for (int i = 0; i < limbCount; i++)
            {
                int aId = LimbJoints[i * 2];
                int bId = LimbJoints[i * 2 + 1];
                bool live = pose.Joints[aId].Score >= 0.25f && pose.Joints[bId].Score >= 0.25f;
                if (!live)
                {
                    limbs[person][i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 worldA = Map(pose.Joints[aId], originX);
                Vector3 worldB = Map(pose.Joints[bId], originX);
                heldA[person][i] = worldA;
                heldB[person][i] = worldB;
                heldLimb[person][i] = true;
                PlaceBox(limbs[person][i], worldA, worldB, LimbThickness[i], LimbThickness[i]);
            }
        }

        static bool Live(PoseFrame pose, JointId id)
        {
            return pose.Joints[(int)id].Score >= 0.25f;
        }

        static Vector3 Map(PoseFrame pose, JointId id, float originX)
        {
            return Map(pose.Joints[(int)id], originX);
        }

        static Vector3 Map(Joint joint, float originX)
        {
            float x = ((joint.X - 0.5f) * 1.35f) + originX;
            float y = (1f - joint.Y) * 1.95f;
            return new Vector3(x, y, 0f);
        }

        static void PlaceBox(Transform box, Vector3 a, Vector3 b, float width, float depth)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.02f)
            {
                box.gameObject.SetActive(false);
                return;
            }

            box.gameObject.SetActive(true);
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
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.GetComponent<Renderer>().receiveShadows = false;
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
