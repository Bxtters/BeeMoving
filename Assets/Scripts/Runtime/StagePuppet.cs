using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Polygon character on a Unity plane, built from fixed-size boxes like the Handline runner.
    /// The figure is standing when Play starts. A bone rotates only when both of its joints
    /// are inside the picture. Otherwise that bone stays as it is. Lengths never change.
    /// The head follows the nose. The face is not drawn.
    /// </summary>
    public sealed class StagePuppet : MonoBehaviour
    {
        const int MaxPeople = 6;
        const float Seen = 0.25f;

        Transform floor;
        Figure[] figures;
        Material bodyMaterial;
        Material limbMaterial;
        Material headMaterial;
        Material floorMaterial;

        class Figure
        {
            public Transform Root;
            public Transform Torso;
            public Transform Head;
            public Transform UpperArmL;
            public Transform ForeArmL;
            public Transform UpperArmR;
            public Transform ForeArmR;
            public Transform ThighL;
            public Transform ShinL;
            public Transform ThighR;
            public Transform ShinR;
            public float NoseX;
            public float NoseY;
            public int NoseFrame;
        }

        /// <summary>
        /// Builds the plane, a light, and the standing figures.
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
            floor.localScale = new Vector3(0.36f, 1f, 0.24f);

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            figures = new Figure[MaxPeople];
            for (int i = 0; i < MaxPeople; i++)
            {
                figures[i] = CreateFigure(i);
                figures[i].Root.gameObject.SetActive(i == 0);
            }
        }

        /// <summary>
        /// Looks at the figures from the front-side so the boxes have visible depth.
        /// </summary>
        public void Frame(Camera camera, int visible)
        {
            if (camera == null) return;
            if (visible < 1) visible = 1;
            float span = Mathf.Max(2.2f, ((visible - 1) * 1.2f) + 1.6f);
            if (floor != null)
            {
                floor.localScale = new Vector3(Mathf.Max(0.36f, (span + 1.6f) / 10f), 1f, 0.26f);
                floor.position = Vector3.zero;
            }

            float dist = Mathf.Max(5.2f, span * 2.15f);
            Vector3 look = new Vector3(0f, 1.05f, 0f);
            camera.orthographic = false;
            camera.fieldOfView = 34f;
            camera.transform.position = new Vector3(dist * 0.22f, 1.75f, -dist);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0.953f, 0.835f, 1f);
        }

        /// <summary>
        /// Keeps every figure on the plane. Only bones whose joints are visible this frame rotate.
        /// </summary>
        public void Follow(PoseFrame[] poses, bool[] used, int limit)
        {
            if (figures == null) return;
            if (limit < 1) limit = 1;
            if (limit > MaxPeople) limit = MaxPeople;
            for (int i = 0; i < MaxPeople; i++)
            {
                figures[i].Root.gameObject.SetActive(i < limit);
                if (i >= limit) continue;
                figures[i].Root.localPosition = new Vector3((i - ((limit - 1) * 0.5f)) * 1.25f, 0f, 0f);
                if (poses != null && used != null && used[i] && i < poses.Length && poses[i] != null)
                {
                    Apply(figures[i], poses[i]);
                }
            }
        }

        /// <summary>
        /// Stores a nose sample for the head. It is not drawn as a face.
        /// </summary>
        public void NoteHead(int person, Joint nose)
        {
            if (figures == null || person < 0 || person >= MaxPeople) return;
            if (!Inside(nose)) return;
            Figure figure = figures[person];
            if (figure.NoseFrame == Time.frameCount - 1 || figure.NoseFrame == Time.frameCount)
            {
                float dx = nose.X - figure.NoseX;
                float dy = nose.Y - figure.NoseY;
                if ((dx * dx) + (dy * dy) > 0.18f * 0.18f) return;
            }

            figure.NoseX = nose.X;
            figure.NoseY = nose.Y;
            figure.NoseFrame = Time.frameCount;
        }

        void Apply(Figure figure, PoseFrame pose)
        {
            if (pose.Joints == null) return;
            Joint leftHip = pose.Joints[(int)JointId.LeftHip];
            Joint rightHip = pose.Joints[(int)JointId.RightHip];
            Joint leftShoulder = pose.Joints[(int)JointId.LeftShoulder];
            Joint rightShoulder = pose.Joints[(int)JointId.RightShoulder];
            if (Inside(leftHip) && Inside(rightHip) && Inside(leftShoulder) && Inside(rightShoulder))
            {
                float hipX = (leftHip.X + rightHip.X) * 0.5f;
                float hipY = (leftHip.Y + rightHip.Y) * 0.5f;
                float shoulderX = (leftShoulder.X + rightShoulder.X) * 0.5f;
                float shoulderY = (leftShoulder.Y + rightShoulder.Y) * 0.5f;
                Vector3 spine = new Vector3(shoulderX - hipX, hipY - shoulderY, 0f);
                if (spine.y > 0.04f && spine.y > Mathf.Abs(spine.x))
                {
                    Aim(figure.Torso, figure.Root, Vector3.up, spine);
                }

                if (figure.NoseFrame == Time.frameCount)
                {
                    Vector3 look = new Vector3(figure.NoseX - shoulderX, shoulderY - figure.NoseY, 0f);
                    if (look.y > 0.01f) Aim(figure.Head, figure.Torso, Vector3.up, look);
                }
            }

            AimBone(figure.UpperArmL, figure.Torso, pose, JointId.LeftShoulder, JointId.LeftElbow);
            AimBone(figure.ForeArmL, figure.UpperArmL, pose, JointId.LeftElbow, JointId.LeftWrist);
            AimBone(figure.UpperArmR, figure.Torso, pose, JointId.RightShoulder, JointId.RightElbow);
            AimBone(figure.ForeArmR, figure.UpperArmR, pose, JointId.RightElbow, JointId.RightWrist);
            AimBone(figure.ThighL, figure.Root, pose, JointId.LeftHip, JointId.LeftKnee);
            AimBone(figure.ShinL, figure.ThighL, pose, JointId.LeftKnee, JointId.LeftAnkle);
            AimBone(figure.ThighR, figure.Root, pose, JointId.RightHip, JointId.RightKnee);
            AimBone(figure.ShinR, figure.ThighR, pose, JointId.RightKnee, JointId.RightAnkle);
        }

        static void AimBone(Transform bone, Transform parent, PoseFrame pose, JointId from, JointId to)
        {
            Joint a = pose.Joints[(int)from];
            Joint b = pose.Joints[(int)to];
            if (!Inside(a) || !Inside(b)) return;
            Vector3 dir = new Vector3(b.X - a.X, a.Y - b.Y, 0f);
            if (dir.sqrMagnitude < 0.0004f) return;
            Aim(bone, parent, Vector3.down, dir);
        }

        static void Aim(Transform bone, Transform parent, Vector3 restAxis, Vector3 direction)
        {
            Quaternion world = Quaternion.FromToRotation(restAxis, direction.normalized);
            Quaternion local = Quaternion.Inverse(parent.rotation) * world;
            bone.localRotation = Quaternion.Slerp(bone.localRotation, local, 0.55f);
        }

        static bool Inside(Joint joint)
        {
            if (joint.Score < Seen) return false;
            if (joint.X < 0.06f || joint.X > 0.94f) return false;
            if (joint.Y < 0.06f || joint.Y > 0.94f) return false;
            return true;
        }

        Figure CreateFigure(int index)
        {
            Figure figure = new Figure();
            GameObject rootObject = new GameObject("Character " + (index + 1));
            rootObject.transform.SetParent(transform, false);
            figure.Root = rootObject.transform;

            Transform hips = Pivot("Hips", figure.Root, new Vector3(0f, 0.92f, 0f));
            Cube(hips, "Pelvis", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.38f, 0.2f, 0.24f), limbMaterial);

            figure.Torso = Pivot("Torso", hips, Vector3.zero);
            Cube(figure.Torso, "Chest", new Vector3(0f, 0.28f, -0.02f), new Vector3(0.46f, 0.48f, 0.28f), bodyMaterial);
            Cube(figure.Torso, "Stripe", new Vector3(0f, 0.34f, -0.16f), new Vector3(0.28f, 0.06f, 0.04f), headMaterial);

            figure.Head = Pivot("Head", figure.Torso, new Vector3(0f, 0.56f, -0.02f));
            Cube(figure.Head, "Skull", new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.3f, 0.28f), bodyMaterial);

            figure.UpperArmL = Limb(figure.Torso, "Upper Arm L", new Vector3(-0.28f, 0.46f, -0.12f), 0.34f, 0.12f, limbMaterial, out figure.ForeArmL, 0.3f);
            figure.UpperArmR = Limb(figure.Torso, "Upper Arm R", new Vector3(0.28f, 0.46f, -0.12f), 0.34f, 0.12f, limbMaterial, out figure.ForeArmR, 0.3f);
            Cube(figure.ForeArmL, "Hand", new Vector3(0f, -0.34f, -0.02f), new Vector3(0.1f, 0.1f, 0.08f), headMaterial);
            Cube(figure.ForeArmR, "Hand", new Vector3(0f, -0.34f, -0.02f), new Vector3(0.1f, 0.1f, 0.08f), headMaterial);
            figure.ThighL = Limb(figure.Root, "Thigh L", new Vector3(-0.11f, 0.92f, 0.04f), 0.42f, 0.15f, limbMaterial, out figure.ShinL, 0.42f);
            figure.ThighR = Limb(figure.Root, "Thigh R", new Vector3(0.11f, 0.92f, 0.04f), 0.42f, 0.15f, limbMaterial, out figure.ShinR, 0.42f);
            Cube(figure.ShinL, "Foot", new Vector3(0f, -0.44f, -0.06f), new Vector3(0.14f, 0.08f, 0.22f), limbMaterial);
            Cube(figure.ShinR, "Foot", new Vector3(0f, -0.44f, -0.06f), new Vector3(0.14f, 0.08f, 0.22f), limbMaterial);
            return figure;
        }

        static Transform Limb(Transform parent, string name, Vector3 pivot, float upper, float thick, Material material, out Transform next, float lower)
        {
            Transform upperPivot = Pivot(name, parent, pivot);
            Cube(upperPivot, "Upper", new Vector3(0f, -upper * 0.5f, 0f), new Vector3(thick, upper, thick), material);
            next = Pivot("Lower", upperPivot, new Vector3(0f, -upper, 0f));
            Cube(next, "Lower", new Vector3(0f, -lower * 0.5f, 0f), new Vector3(thick * 0.9f, lower, thick * 0.9f), material);
            return upperPivot;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        static void Cube(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            Strip(go);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
