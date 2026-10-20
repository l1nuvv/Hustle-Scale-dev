using Inventory;
using UnityEngine;

namespace Interactables
{
    public enum FirstDealSound { Purchase, Contact, TestPassed, TestFailed, Repair, Sale, Warning }

    // Local presentation only. Clips are original synthesized PCM, generated once per scene.
    // Attached by WorkshopView at runtime so existing scenes/builders need no YAML changes.
    [DisallowMultipleComponent]
    public sealed class FirstDealAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.12f;
        private const int SampleRate = 22050;
        private WorkshopSession session;
        private AudioSource world, confirmations, ambience;
        private AudioClip ambienceClip;
        private readonly AudioClip[] clips = new AudioClip[7];
        private readonly double[] nextAllowed = new double[7];
        private readonly DealOrderState[] orderStates = new DealOrderState[3];
        private bool focused = true;

        private void Awake()
        {
            session = GetComponent<WorkshopView>().Session;
            for (var i = 0; i < clips.Length; i++)
                clips[i] = CreateCue((FirstDealSound)i);
            world = Source("Actions", effectsVolume);
            confirmations = Source("Confirmations", effectsVolume);
            ambience = Source("Workshop and yard", ambienceVolume);
            ambienceClip = CreateAmbience();
            ambience.clip = ambienceClip;
            ambience.loop = true;
            for (var i = 0; i < 3; i++) orderStates[i] = session.Order((DealOrderId)i).State;
        }

        private AudioSource Source(string label, float volume)
        {
            var child = new GameObject("F1 Audio - " + label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 0; // Quiet location bed and readable feedback anywhere in this compact scene.
            source.dopplerLevel = 0;
            return source;
        }

        private void OnEnable()
        {
            session.Changed += Refresh;
            ambience.Play();
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            world.Stop();
            confirmations.Stop();
            ambience.Stop();
        }

        private void OnDestroy()
        {
            foreach (var clip in clips) if (clip != null) Destroy(clip);
            if (ambienceClip != null) Destroy(ambienceClip);
        }

        private void OnApplicationFocus(bool value) { focused = value; Refresh(); }
        private void OnApplicationPause(bool value) { focused = !value; Refresh(); }

        private void Refresh()
        {
            var expired = false;
            for (var i = 0; i < 3; i++)
            {
                var state = session.Order((DealOrderId)i).State;
                if (state != orderStates[i] && (state == DealOrderState.Expired || state == DealOrderState.Failed))
                    expired = true;
                orderStates[i] = state;
            }
            if (!focused || session.IsPaused || session.ShiftEnded)
            {
                world.Pause();
                ambience.Pause();
            }
            else { world.UnPause(); ambience.UnPause(); }
            if (!focused || session.ShiftEnded) confirmations.Stop();
            if (expired) Play(FirstDealSound.Warning);
        }

        // Menu confirmations are allowed while the trade screen pauses the world.
        public void Play(FirstDealSound sound, bool menuConfirmation = false)
        {
            if (!isActiveAndEnabled || !focused || session.ShiftEnded ||
                (session.IsPaused && !menuConfirmation)) return;
            var index = (int)sound;
            if (Time.unscaledTimeAsDouble < nextAllowed[index]) return;
            nextAllowed[index] = Time.unscaledTimeAsDouble + (sound == FirstDealSound.Warning ? 0.6 : 0.1);
            var source = menuConfirmation || sound == FirstDealSound.Warning ? confirmations : world;
            source.clip = clips[index];
            source.Play(); // One voice per channel: repeated input cannot build an overlapping stack.
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create("F1 " + name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Tone(float time, float frequency) => Mathf.Sin(2 * Mathf.PI * frequency * time);

        private static float Pulse(float time, float start, float length, float frequency)
        {
            var t = time - start;
            if (t < 0 || t >= length) return 0;
            return Tone(t, frequency) * Mathf.Min(t / 0.006f, 1f) * Mathf.Pow(1 - t / length, 2);
        }

        private static AudioClip CreateCue(FirstDealSound sound)
        {
            var samples = new float[(int)(SampleRate * 0.65f)];
            var random = new System.Random(170 + (int)sound);
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                var noise = (float)(random.NextDouble() * 2 - 1);
                float value;
                switch (sound)
                {
                    case FirstDealSound.Purchase:
                        value = Pulse(t, 0, 0.17f, 1450) + 0.55f * Pulse(t, 0.07f, 0.25f, 2100); break;
                    case FirstDealSound.Contact:
                        value = 0.8f * Pulse(t, 0, 0.14f, 130) + noise * Mathf.Exp(-t * 65) * Mathf.Min(t * 1000, 1); break;
                    case FirstDealSound.TestPassed:
                        value = Pulse(t, 0, 0.13f, 880) + Pulse(t, 0.14f, 0.23f, 1320); break;
                    case FirstDealSound.TestFailed:
                        value = Pulse(t, 0, 0.12f, 330) + Pulse(t, 0.15f, 0.22f, 220); break;
                    case FirstDealSound.Repair:
                        value = noise * (Mathf.Abs(Pulse(t, 0, 0.1f, 65)) + Mathf.Abs(Pulse(t, 0.14f, 0.12f, 65))) +
                            Pulse(t, 0.32f, 0.2f, 620); break;
                    case FirstDealSound.Sale:
                        value = Pulse(t, 0, 0.16f, 660) + Pulse(t, 0.1f, 0.18f, 880) + Pulse(t, 0.22f, 0.3f, 1100); break;
                    default:
                        value = Pulse(t, 0, 0.18f, 440) + Pulse(t, 0.23f, 0.22f, 440); break;
                }
                samples[i] = value * 0.32f;
            }
            return Clip(sound.ToString(), samples);
        }

        private static AudioClip CreateAmbience()
        {
            // Integer cycles over eight seconds keep the ventilation/distant yard bed seamless.
            var samples = new float[SampleRate * 8];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = (float)i / SampleRate;
                samples[i] = 0.16f * Tone(t, 60) + 0.06f * Tone(t, 120) +
                    0.055f * Tone(t, 173.125f) * (0.6f + 0.4f * Tone(t, 0.25f)) +
                    0.035f * Tone(t, 287.5f) * (0.6f + 0.4f * Tone(t, 0.375f));
            }
            return Clip("Location ambience", samples);
        }
    }
}
