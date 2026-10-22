using UnityEngine;

namespace Handline
{
    /// <summary>
    /// A simple standing character made of capsules. The body stays on a small floor
    /// and the limbs follow a MoveNet pose. No face is drawn.
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
            (int)JointId.RightKnee, (int)JointId.RightAnkle,
            (int)JointId.LeftShoulder, (int)JointId.LeftHip,
            (int)JointId.RightShoulder, (int)JointId.RightHip,
            (int)JointId.LeftHip, (int)JointId.RightHip
        };

        Transform floor;
        Transform[] roots;
        Transform[] heads;
        Transform[] torsos;
        Transform[][] limbs;
        Vector3[][] heldA;
        Vector3[][] heldB;
        bool[][] heldLimb;
        Vector3[] heldHead;
        Vector3[] heldHip;
        Vector3[] heldShoulder;
        bool[] heldBody;
        Material bodyMaterial;
        Material limbMaterial;
        Material headMaterial;
        Material floorMaterial;
        PoseFrame neutral;

        /// <summary>
        /// Builds the floor and one puppet for each person slot.
        /// </summary>
        public void Build()
        {
            bodyMaterial = MakeMaterial(new Color(0.96f, 0.78f, 0.22f, 1f));
            limbMaterial = MakeMaterial(new Color(0.55f, 0.36f, 0.08f, 1f));
            headMaterial = MakeMaterial(new Color(0.99f, 0.93f, 0.72f, 1f));
            floorMaterial = MakeMaterial(new Color(0.86f, 0.68f, 0.22f, 1f));

            floor = Shape("Floor", PrimitiveType.Cube, floorMaterial, transform);
            floor.localScale = new Vector3(2.4f, 0.12f, 1.7f);
            floor.position = new Vector3(0f, -0.06f, 0f);

            int limbCount = LimbJoints.Length / 2;
            roots = new Transform[MaxPeople];
            heads = new Transform[MaxPeople];
            torsos = new Transform[MaxPeople];
            limbs = new Transform[MaxPeople][];
            heldA = new Vector3[MaxPeople][];
            heldB = new Vector3[MaxPeople][];
            heldLimb = new bool[MaxPeople][];
            heldHead = new Vector3[MaxPeople];
            heldHip = new Vector3[MaxPeople];
            heldShoulder = new Vector3[MaxPeople];
            heldBody = new bool[MaxPeople];
            for (int person = 0; person < MaxPeople; person++)
            {
                GameObject root = new GameObject("Character " + (person + 1));
                root.transform.SetParent(transform, false);
                roots[person] = root.transform;
                torsos[person] = Shape("Torso", PrimitiveType.Capsule, bodyMaterial, root.transform);
                heads[person] = Shape("Head", PrimitiveType.Sphere, headMaterial, root.transform);
                limbs[person] = new Transform[limbCount];
                heldA[person] = new Vector3[limbCount];
                heldB[person] = new Vector3[limbCount];
                heldLimb[person] = new bool[limbCount];
                for (int i = 0; i < limbCount; i++)
                {
                    limbs[person][i] = Shape("Limb", PrimitiveType.Capsule, limbMaterial, root.transform);
                }

                root.SetActive(false);
            }

            neutral = NeutralPose();
        }

        /// <summary>
        /// Points the stage camera at the floor so the characters stay in view.
        /// </summary>
        public void Frame(Camera camera, int visible)
        {
            if (camera == null) return;
            if (visible < 1) visible = 1;
            float span = Mathf.Max(1.8f, ((visible - 1) * 1.15f) + 1.4f);
            ResizeFloor(span);
            float dist = (span * 0.62f) / Mathf.Tan(16f * Mathf.Deg2Rad);
            if (dist < 3.4f) dist = 3.4f;
            Vector3 look = new Vector3(0f, 1.05f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.transform.position = new Vector3(0f, 1.2f, -dist);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 40f;
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
            Vector3 Hip(int id)
            {
                return Map(pose.Joints[id], originX);
            }

            Vector3 leftHip = Hip((int)JointId.LeftHip);
            Vector3 rightHip = Hip((int)JointId.RightHip);
            Vector3 leftShoulder = Hip((int)JointId.LeftShoulder);
            Vector3 rightShoulder = Hip((int)JointId.RightShoulder);
            bool torsoLive = pose.Joints[(int)JointId.LeftHip].Score >= 0.15f
                && pose.Joints[(int)JointId.RightHip].Score >= 0.15f
                && pose.Joints[(int)JointId.LeftShoulder].Score >= 0.15f
                && pose.Joints[(int)JointId.RightShoulder].Score >= 0.15f;
            Vector3 hip = torsoLive ? (leftHip + rightHip) * 0.5f : heldHip[person];
            Vector3 shoulder = torsoLive ? (leftShoulder + rightShoulder) * 0.5f : heldShoulder[person];
            if (torsoLive)
            {
                heldHip[person] = hip;
                heldShoulder[person] = shoulder;
                heldBody[person] = true;
            }

            if (torsoLive || heldBody[person])
            {
                Vector3 up = shoulder - hip;
                if (up.sqrMagnitude < 0.0001f) up = Vector3.up;
                PlaceLimb(torsos[person], hip, shoulder, 0.34f);
                heads[person].gameObject.SetActive(true);
                Vector3 head = shoulder + (up.normalized * 0.22f);
                if (torsoLive) heldHead[person] = head;
                heads[person].position = torsoLive ? head : heldHead[person];
                heads[person].localScale = new Vector3(0.36f, 0.4f, 0.36f);
            }

            int limbCount = LimbJoints.Length / 2;
            for (int i = 0; i < limbCount; i++)
            {
                Joint a = pose.Joints[LimbJoints[i * 2]];
                Joint b = pose.Joints[LimbJoints[i * 2 + 1]];
                bool live = a.Score >= 0.15f && b.Score >= 0.15f;
                if (!live)
                {
                    if (heldLimb[person][i]) PlaceLimb(limbs[person][i], heldA[person][i], heldB[person][i], 0.12f);
                    else limbs[person][i].gameObject.SetActive(false);
                    continue;
                }

                Vector3 worldA = Map(a, originX);
                Vector3 worldB = Map(b, originX);
                heldA[person][i] = worldA;
                heldB[person][i] = worldB;
                heldLimb[person][i] = true;
                PlaceLimb(limbs[person][i], worldA, worldB, 0.12f);
            }
        }

        static Vector3 Map(Joint joint, float originX)
        {
            float x = ((joint.X - 0.5f) * 1.35f) + originX;
            float y = (1f - joint.Y) * 1.95f;
            return new Vector3(x, y, 0f);
        }

        static void PlaceLimb(Transform limb, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.02f)
            {
                limb.gameObject.SetActive(false);
                return;
            }

            limb.gameObject.SetActive(true);
            limb.position = (a + b) * 0.5f;
            limb.rotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            limb.localScale = new Vector3(thickness, length * 0.5f, thickness);
        }

        void ResizeFloor(float span)
        {
            if (floor == null) return;
            float width = Mathf.Max(2.2f, span + 0.8f);
            floor.localScale = new Vector3(width, 0.12f, 1.8f);
            floor.position = new Vector3(0f, -0.06f, 0f);
        }

        static Transform Shape(string name, PrimitiveType type, Material material, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;
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
