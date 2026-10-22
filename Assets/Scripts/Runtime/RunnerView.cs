using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Handline
{
    public sealed class RunnerView : MonoBehaviour
    {
        static readonly Color Sky = new Color(0.09f, 0.075f, 0.06f);
        static readonly Color Asphalt = new Color(0.15f, 0.14f, 0.12f);
        static readonly Color LaneDim = new Color(0.21f, 0.19f, 0.16f);
        static readonly Color LaneHot = new Color(0.46f, 0.30f, 0.12f);
        static readonly Color Paint = new Color(0.86f, 0.62f, 0.22f);
        static readonly Color Coral = new Color(0.95f, 0.34f, 0.22f);
        static readonly Color CoralDeep = new Color(0.55f, 0.18f, 0.13f);
        static readonly Color Amber = new Color(0.98f, 0.74f, 0.28f);
        static readonly Color CrateColor = new Color(0.70f, 0.34f, 0.16f);
        static readonly Color BeamColor = new Color(0.22f, 0.66f, 0.64f);
        static readonly Color BlockerColor = new Color(0.36f, 0.40f, 0.44f);
        static readonly Color CoinColor = new Color(1f, 0.80f, 0.26f);
        static readonly Color Rail = new Color(0.11f, 0.10f, 0.09f);
        static readonly Color Arch = new Color(0.20f, 0.17f, 0.14f);

        public Camera CameraView;

        Transform player;
        Transform avatar;
        Transform armL;
        Transform armR;
        Transform legL;
        Transform legR;
        Transform shadow;
        Transform[] dashes;
        Transform[] posts;
        Transform[] arches;
        Material[] laneMaterials;
        Renderer[] avatarRenderers;
        readonly Dictionary<int, Transform> shown = new Dictionary<int, Transform>();
        readonly List<int> stale = new List<int>();
        Material crateMat;
        Material beamMat;
        Material blockerMat;
        Material coinMat;
        float phase;
        float duck;

        public static RunnerView Create()
        {
            GameObject root = new GameObject("World");
            RunnerView view = root.AddComponent<RunnerView>();
            view.Build();
            return view;
        }

        public void Apply(RunnerSim sim, float dt)
        {
            if (dt < 0f) dt = 0f;
            phase += dt * (2.2f + sim.RunAmount * 8.5f + (sim.Grounded ? sim.Speed * 0.08f : 0f));
            duck = Mathf.MoveTowards(duck, sim.Ducking ? 1f : 0f, dt * 9f);

            float x = sim.LanePosition * RunnerSim.LaneSpacing;
            player.position = new Vector3(x, sim.FeetY, 0f);
            float lean = (sim.Lane - sim.LanePosition) * -24f;
            float pitch = sim.Grounded ? 6f * sim.RunAmount : -14f;
            float height = Mathf.Lerp(1f, 0.6f, duck);
            if (!sim.Grounded) height *= 1.05f;
            avatar.localScale = new Vector3(1f, height, 1f);
            avatar.localRotation = Quaternion.Euler(pitch, 0f, lean);
            avatar.localPosition = new Vector3(0f, 0f, 0.18f * duck);

            float swing = sim.Grounded ? Mathf.Lerp(14f, 62f, sim.RunAmount) : 22f;
            float s = Mathf.Sin(phase);
            armL.localRotation = Quaternion.Euler(s * swing, 0f, 12f);
            armR.localRotation = Quaternion.Euler(-s * swing, 0f, -12f);
            float stride = sim.Grounded ? Mathf.Lerp(18f, 50f, Mathf.Max(0.25f, sim.RunAmount)) : 12f;
            legL.localRotation = Quaternion.Euler(-s * stride, 0f, 0f);
            legR.localRotation = Quaternion.Euler(s * stride, 0f, 0f);

            bool visible = sim.HurtTimer <= 0f || Mathf.Repeat(Time.time * 16f, 1f) > 0.42f;
            for (int i = 0; i < avatarRenderers.Length; i++)
            {
                avatarRenderers[i].enabled = visible;
            }

            float lift = Mathf.Clamp01(sim.FeetY / 1.5f);
            shadow.localScale = new Vector3(Mathf.Lerp(0.9f, 0.4f, lift), 0.03f, Mathf.Lerp(0.7f, 0.35f, lift));
            shadow.position = new Vector3(x, 0.02f, 0.08f);

            for (int lane = 0; lane < 3; lane++)
            {
                bool hot = sim.Lane == lane - 1;
                laneMaterials[lane].color = hot ? LaneHot : LaneDim;
            }

            float dashSpacing = 4.2f;
            float scroll = sim.Distance % dashSpacing;
            for (int i = 0; i < dashes.Length; i++)
            {
                int row = i % 2;
                int index = i / 2;
                float z = -10f + index * dashSpacing - scroll;
                float side = row == 0 ? -0.92f : 0.92f;
                dashes[i].position = new Vector3(side, 0.045f, z);
            }

            float postSpacing = 7.5f;
            float postScroll = sim.Distance % postSpacing;
            for (int i = 0; i < posts.Length; i++)
            {
                int row = i % 2;
                int index = i / 2;
                float z = -8f + index * postSpacing - postScroll;
                float side = row == 0 ? -3.15f : 3.15f;
                posts[i].position = new Vector3(side, 0.55f, z);
            }

            float archSpacing = 26f;
            float archScroll = sim.Distance % archSpacing;
            for (int i = 0; i < arches.Length; i++)
            {
                float z = 6f + i * archSpacing - archScroll;
                if (z < -8f) z += arches.Length * archSpacing;
                arches[i].position = new Vector3(0f, 0f, z);
            }

            stale.Clear();
            foreach (KeyValuePair<int, Transform> pair in shown)
            {
                stale.Add(pair.Key);
            }

            IReadOnlyList<Obstacle> obstacles = sim.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle obstacle = obstacles[i];
                stale.Remove(obstacle.Id);
                Transform body;
                if (!shown.TryGetValue(obstacle.Id, out body))
                {
                    body = BuildObstacle(obstacle);
                    shown.Add(obstacle.Id, body);
                }

                body.position = new Vector3(obstacle.Lane * RunnerSim.LaneSpacing, 0f, obstacle.Z);
                if (obstacle.Kind == ObstacleKind.Coin)
                {
                    body.Rotate(0f, 140f * dt, 0f, Space.World);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                Transform body;
                if (shown.TryGetValue(stale[i], out body) && body != null)
                {
                    Destroy(body.gameObject);
                }

                shown.Remove(stale[i]);
            }
        }

        void Build()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 58f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.30f, 0.26f);

            GameObject cameraObject = new GameObject("Run Camera");
            cameraObject.transform.SetParent(transform, false);
            CameraView = cameraObject.AddComponent<Camera>();
            CameraView.tag = "MainCamera";
            CameraView.clearFlags = CameraClearFlags.SolidColor;
            CameraView.backgroundColor = Sky;
            CameraView.fieldOfView = 40f;
            CameraView.nearClipPlane = 0.08f;
            CameraView.farClipPlane = 80f;
            CameraView.transform.position = new Vector3(0f, 3.25f, -7.4f);
            CameraView.transform.LookAt(new Vector3(0f, 1.15f, 9f));
            cameraObject.AddComponent<AudioListener>();

            Light sun = MakeLight("Sun", new Color(1f, 0.94f, 0.84f), 1.25f, LightShadows.Soft);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light fill = MakeLight("Fill", new Color(0.55f, 0.68f, 0.82f), 0.32f, LightShadows.None);
            fill.transform.rotation = Quaternion.Euler(16f, 150f, 0f);

            Material asphalt = Make(Asphalt, 0f);
            Material paint = Make(Paint, 0.15f);
            Material railMat = Make(Rail, 0f);
            Material archMat = Make(Arch, 0f);
            Material amberMat = Make(Amber, 0.4f);
            crateMat = Make(CrateColor, 0f);
            beamMat = Make(BeamColor, 0.25f);
            blockerMat = Make(BlockerColor, 0f);
            coinMat = Make(CoinColor, 0.55f);
            Material body = Make(Coral, 0.05f);
            Material deep = Make(CoralDeep, 0f);
            Material shadowMat = Make(new Color(0.04f, 0.03f, 0.03f), 0f);

            Cube(transform, "Ground", new Vector3(0f, -0.08f, 32f), new Vector3(14f, 0.16f, 96f), asphalt);
            Cube(transform, "Rail Left", new Vector3(-3.15f, 0.18f, 32f), new Vector3(0.22f, 0.36f, 96f), railMat);
            Cube(transform, "Rail Right", new Vector3(3.15f, 0.18f, 32f), new Vector3(0.22f, 0.36f, 96f), railMat);

            laneMaterials = new Material[3];
            for (int lane = 0; lane < 3; lane++)
            {
                laneMaterials[lane] = Make(LaneDim, 0.08f);
                Cube(transform, "Lane", new Vector3((lane - 1) * RunnerSim.LaneSpacing, 0.02f, 30f), new Vector3(1.28f, 0.02f, 90f), laneMaterials[lane]);
            }

            dashes = new Transform[36];
            for (int i = 0; i < dashes.Length; i++)
            {
                dashes[i] = Cube(transform, "Dash", Vector3.zero, new Vector3(0.08f, 0.02f, 1.3f), paint);
            }

            posts = new Transform[20];
            for (int i = 0; i < posts.Length; i++)
            {
                posts[i] = Cube(transform, "Post", Vector3.zero, new Vector3(0.16f, 1.1f, 0.16f), railMat);
            }

            arches = new Transform[4];
            for (int i = 0; i < arches.Length; i++)
            {
                GameObject arch = new GameObject("Arch");
                arch.transform.SetParent(transform, false);
                Cube(arch.transform, "Pillar", new Vector3(-2.55f, 1.45f, 0f), new Vector3(0.18f, 2.9f, 0.18f), archMat);
                Cube(arch.transform, "Pillar", new Vector3(2.55f, 1.45f, 0f), new Vector3(0.18f, 2.9f, 0.18f), archMat);
                Cube(arch.transform, "Bar", new Vector3(0f, 2.95f, 0f), new Vector3(5.3f, 0.16f, 0.22f), amberMat);
                arches[i] = arch.transform;
            }

            player = new GameObject("Runner").transform;
            player.SetParent(transform, false);
            shadow = Cube(player, "Shadow", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.85f, 0.03f, 0.65f), shadowMat);

            GameObject avatarObject = new GameObject("Avatar");
            avatar = avatarObject.transform;
            avatar.SetParent(player, false);
            Cube(avatar, "Hips", new Vector3(0f, 0.78f, 0f), new Vector3(0.42f, 0.28f, 0.26f), deep);
            Cube(avatar, "Chest", new Vector3(0f, 1.16f, 0f), new Vector3(0.5f, 0.48f, 0.3f), body);
            Cube(avatar, "Stripe", new Vector3(0f, 1.22f, 0.16f), new Vector3(0.36f, 0.08f, 0.04f), amberMat);
            Cube(avatar, "Head", new Vector3(0f, 1.62f, 0f), new Vector3(0.34f, 0.3f, 0.32f), body);
            Cube(avatar, "Visor", new Vector3(0f, 1.64f, 0.18f), new Vector3(0.36f, 0.08f, 0.06f), amberMat);

            armL = Limb(avatar, "Arm L", new Vector3(-0.34f, 1.28f, 0f), deep);
            armR = Limb(avatar, "Arm R", new Vector3(0.34f, 1.28f, 0f), deep);
            legL = Limb(avatar, "Leg L", new Vector3(-0.14f, 0.7f, 0f), deep);
            legR = Limb(avatar, "Leg R", new Vector3(0.14f, 0.7f, 0f), deep);
            legL.GetChild(0).localScale = new Vector3(0.16f, 0.66f, 0.16f);
            legR.GetChild(0).localScale = new Vector3(0.16f, 0.66f, 0.16f);
            legL.GetChild(0).localPosition = new Vector3(0f, -0.33f, 0f);
            legR.GetChild(0).localPosition = new Vector3(0f, -0.33f, 0f);

            avatarRenderers = avatar.GetComponentsInChildren<Renderer>();
        }

        Transform BuildObstacle(Obstacle obstacle)
        {
            GameObject root = new GameObject(obstacle.Kind.ToString());
            root.transform.SetParent(transform, false);
            if (obstacle.Kind == ObstacleKind.Crate)
            {
                float h = RunnerSim.CrateClearance;
                Cube(root.transform, "Box", new Vector3(0f, h * 0.5f, 0f), new Vector3(1.18f, h, 1.18f), crateMat);
                Cube(root.transform, "Band", new Vector3(0f, h * 0.72f, 0f), new Vector3(1.22f, 0.08f, 1.22f), Make(Amber, 0.2f));
            }
            else if (obstacle.Kind == ObstacleKind.Beam)
            {
                Cube(root.transform, "Bar", new Vector3(0f, 1.58f, 0f), new Vector3(1.7f, 0.34f, 0.55f), beamMat);
                Cube(root.transform, "Cap", new Vector3(-0.78f, 1.58f, 0f), new Vector3(0.16f, 0.5f, 0.16f), beamMat);
                Cube(root.transform, "Cap", new Vector3(0.78f, 1.58f, 0f), new Vector3(0.16f, 0.5f, 0.16f), beamMat);
            }
            else if (obstacle.Kind == ObstacleKind.Blocker)
            {
                Cube(root.transform, "Wall", new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 2.1f, 1.05f), blockerMat);
                Cube(root.transform, "Band", new Vector3(0f, 1.15f, 0.54f), new Vector3(1.22f, 0.16f, 0.04f), Make(new Color(0.75f, 0.28f, 0.2f), 0.1f));
            }
            else
            {
                GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coin.name = "Disc";
                coin.transform.SetParent(root.transform, false);
                coin.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                coin.transform.localScale = new Vector3(0.42f, 0.42f, 0.12f);
                Strip(coin);
                coin.GetComponent<Renderer>().sharedMaterial = coinMat;
            }

            return root.transform;
        }

        static Transform Limb(Transform parent, string name, Vector3 pivot, Material material)
        {
            GameObject joint = new GameObject(name);
            joint.transform.SetParent(parent, false);
            joint.transform.localPosition = pivot;
            Cube(joint.transform, "Bone", new Vector3(0f, -0.22f, 0f), new Vector3(0.13f, 0.44f, 0.13f), material);
            return joint.transform;
        }

        Light MakeLight(string name, Color color, float intensity, LightShadows shadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            return light;
        }

        static Material Make(Color color, float emission)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader == null) throw new InvalidOperationException("Unity did not provide a shader, so the track could not be drawn.");
            Material material = new Material(shader);
            material.color = color;
            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }

            return material;
        }

        static Transform Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Strip(go);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        static void Strip(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
