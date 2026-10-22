using System;
using UnityEngine;

namespace Interactables
{
    // Bounded E5 presentation; it never writes to the trading model.
    public sealed partial class CityTradeView
    {
        private AudioSource feedbackAudio;
        private AudioClip actionTone, successTone, refusalTone;

        private void BindSceneFeedback()
        {
            foreach (var site in sceneBindings.Sites)
            {
                var reaction = site.Reaction;
                if (reaction == null) continue;
                reaction.Bind(this);
            }
            feedbackAudio = root.gameObject.AddComponent<AudioSource>();
            feedbackAudio.playOnAwake = false; feedbackAudio.spatialBlend = 0; feedbackAudio.volume = 0.12f;
            feedbackAudio.ignoreListenerPause = true;
            actionTone = Tone("Interaction", 440, 0.07f);
            successTone = Tone("Result", 660, 0.18f);
            refusalTone = Tone("Unavailable", 220, 0.12f);
        }

        private static AudioClip Tone(string name, float frequency, float seconds)
        {
            const int rate = 22050;
            var samples = new float[(int)(rate * seconds)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Sin(Mathf.PI * i / samples.Length) * 0.5f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        private void ActionFeedback(string command, bool success)
        {
            if (feedbackAudio != null)
                feedbackAudio.PlayOneShot(!success ? refusalTone : command == "test" || command == "repair" || command == "deliver" || command == "purchase" ? successTone : actionTone);
            if (current == null) return;
            var reaction = current.GetComponentInParent<CityGameplaySite>()?.Reaction;
            if (reaction != null) reaction.React(success);
        }

        private void DisposeFeedback()
        {
            if (actionTone != null) Destroy(actionTone);
            if (successTone != null) Destroy(successTone);
            if (refusalTone != null) Destroy(refusalTone);
        }
    }
}
