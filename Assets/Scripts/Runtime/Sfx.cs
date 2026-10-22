using UnityEngine;

namespace Handline
{
    /// <summary>
    /// Short tones for the lane. Added so the runner has feedback without shipping audio files.
    /// Each clip is generated once in the constructor and played as a one-shot.
    /// </summary>
    public sealed class Sfx
    {
        readonly AudioSource source;
        readonly AudioClip jump;
        readonly AudioClip coin;
        readonly AudioClip hit;
        readonly AudioClip go;
        readonly AudioClip over;
        readonly AudioClip lane;

        /// <summary>
        /// Builds the six tones on the runner's audio source.
        /// playOnAwake is cleared so nothing sounds at spawn. spatialBlend 0 keeps the tone in stereo, not in the 3D world.
        /// </summary>
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

        /// <summary>Plays the jump tone. The volume argument keeps it under the rest of the mix.</summary>
        public void Jump() { source.PlayOneShot(jump, 0.32f); }

        /// <summary>Plays the coin tone.</summary>
        public void Coin() { source.PlayOneShot(coin, 0.28f); }

        /// <summary>Plays the hit tone. Noise is used so a collision sounds rough.</summary>
        public void Hit() { source.PlayOneShot(hit, 0.4f); }

        /// <summary>Plays the start tone.</summary>
        public void Go() { source.PlayOneShot(go, 0.3f); }

        /// <summary>Plays the game-over tone.</summary>
        public void Over() { source.PlayOneShot(over, 0.35f); }

        /// <summary>Plays the lane-change click.</summary>
        public void Lane() { source.PlayOneShot(lane, 0.22f); }

        /// <summary>
        /// Writes a decaying sine, or noise when noise is true.
        /// Added so each event has its own pitch and length without an imported clip.
        /// The envelope makes the tone fade instead of clicking off.
        /// </summary>
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
