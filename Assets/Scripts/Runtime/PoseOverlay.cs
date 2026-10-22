using UnityEngine;
using UnityEngine.UI;

namespace Handline
{
    public sealed class PoseOverlay : MonoBehaviour
    {
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
            (int)JointId.RightKnee, (int)JointId.RightAnkle,
            (int)JointId.LeftEar, (int)JointId.LeftEye,
            (int)JointId.LeftEye, (int)JointId.Nose,
            (int)JointId.Nose, (int)JointId.RightEye,
            (int)JointId.RightEye, (int)JointId.RightEar
        };

        const int MaxPeople = 6;
        Image[][] lines;
        Image[][] dots;
        RectTransform rect;

        public void Build()
        {
            Sprite sprite = White();
            rect = GetComponent<RectTransform>();
            int boneCount = Bones.Length / 2;
            lines = new Image[MaxPeople][];
            dots = new Image[MaxPeople][];
            for (int person = 0; person < MaxPeople; person++)
            {
                Color bone = person == 0
                    ? new Color(0.98f, 0.74f, 0.28f, 0.95f)
                    : new Color(0.45f, 0.82f, 0.78f, 0.75f);
                Color joint = person == 0
                    ? new Color(0.96f, 0.38f, 0.24f, 0.95f)
                    : new Color(0.85f, 0.9f, 0.88f, 0.75f);
                lines[person] = new Image[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    lines[person][i] = Mark("Bone", sprite, bone);
                }

                dots[person] = new Image[(int)JointId.Count];
                for (int i = 0; i < dots[person].Length; i++)
                {
                    dots[person][i] = Mark("Joint", sprite, joint);
                    dots[person][i].rectTransform.sizeDelta = new Vector2(person == 0 ? 8f : 6f, person == 0 ? 8f : 6f);
                }
            }
        }

        public void Redraw(PoseFrame pose)
        {
            PoseFrame[] one = pose == null ? null : new PoseFrame[] { pose };
            Redraw(one, pose == null ? 0 : 1, 0);
        }

        public void Redraw(PoseFrame[] poses, int count, int active)
        {
            float width = rect.sizeDelta.x;
            float height = rect.sizeDelta.y;
            for (int person = 0; person < MaxPeople; person++)
            {
                bool show = poses != null && person < count && person < poses.Length
                    && poses[person] != null && poses[person].Joints != null
                    && poses[person].Joints.Length >= (int)JointId.Count;
                DrawPerson(person, show ? poses[person] : null, show, person == active, width, height);
            }
        }

        void DrawPerson(int person, PoseFrame pose, bool show, bool active, float width, float height)
        {
            Image[] bones = lines[person];
            for (int i = 0; i < bones.Length; i++)
            {
                if (!show)
                {
                    bones[i].enabled = false;
                    continue;
                }

                Joint a = pose.Joints[Bones[i * 2]];
                Joint b = pose.Joints[Bones[i * 2 + 1]];
                if (a.Score < 0.25f || b.Score < 0.25f)
                {
                    bones[i].enabled = false;
                    continue;
                }

                Vector2 pa = ToPoint(a, width, height);
                Vector2 pb = ToPoint(b, width, height);
                Vector2 delta = pb - pa;
                float length = delta.magnitude;
                if (length < 1f)
                {
                    bones[i].enabled = false;
                    continue;
                }

                RectTransform rt = bones[i].rectTransform;
                rt.anchoredPosition = (pa + pb) * 0.5f;
                rt.sizeDelta = new Vector2(length, active ? 3f : 2f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                bones[i].enabled = true;
            }

            Image[] joints = dots[person];
            for (int i = 0; i < joints.Length; i++)
            {
                if (!show || pose.Joints[i].Score < 0.25f)
                {
                    joints[i].enabled = false;
                    continue;
                }

                joints[i].enabled = true;
                joints[i].rectTransform.anchoredPosition = ToPoint(pose.Joints[i], width, height);
            }
        }

        static Vector2 ToPoint(Joint joint, float width, float height)
        {
            return new Vector2(joint.X * width, (1f - joint.Y) * height);
        }

        static Sprite White()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }

        Image Mark(string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }
    }
}
