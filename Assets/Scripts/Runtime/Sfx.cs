using UnityEngine;

namespace Handline
{
    public sealed class Sfx
    {
        readonly AudioSource source;
        readonly AudioClip jump;
        readonly AudioClip coin;
        readonly AudioClip hit;
        readonly AudioClip go;
        readonly AudioClip over;
        readonly AudioClip lane;

        public Sfx(AudioSource source)
        {
            this.source = source;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            jump = Tone("jump", 620f, 0.11f, 16f, false);
            coin = Tone("coin", 980f, 0.09f, 10f, false);
            hit = Tone("hit", 90f, 0.20f, 8f, true);
            go = Tone("go", 523f, 0.14f, 9f, false);
            over = Tone("over", 150f, 0.36f, 4f, false);
            lane = Tone("lane", 340f, 0.05f, 28f, false);
        }

        public void Jump() { source.PlayOneShot(jump, 0.32f); }
        public void Coin() { source.PlayOneShot(coin, 0.28f); }
        public void Hit() { source.PlayOneShot(hit, 0.4f); }
        public void Go() { source.PlayOneShot(go, 0.3f); }
        public void Over() { source.PlayOneShot(over, 0.35f); }
        public void Lane() { source.PlayOneShot(lane, 0.22f); }

        static AudioClip Tone(string name, float freq, float seconds, float decay, bool noise)
        {
            const int rate = 44100;
            int count = Mathf.Max(1, (int)(rate * seconds));
            float[] data = new float[count];
            float phase = 0f;
            float step = (Mathf.PI * 2f * freq) / rate;
            System.Random rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float env = Mathf.Exp(-decay * (i / (float)rate));
                float sample = noise ? ((float)rng.NextDouble() * 2f - 1f) : Mathf.Sin(phase);
                data[i] = sample * env * 0.28f;
                phase += step;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
