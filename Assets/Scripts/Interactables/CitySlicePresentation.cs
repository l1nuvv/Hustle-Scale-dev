using System;
using UnityEngine;

namespace Interactables
{
    // Bounded E5 presentation; it never writes to the trading model.
    public sealed partial class CityTradeView
    {
        private CityTradeReaction sellerReaction, buyerReaction;
        private AudioSource feedbackAudio;
        private AudioClip actionTone, successTone, refusalTone;

        private Transform SliceProp(GameObject prefab, string name, Vector3 position, Vector3 scale)
        {
            if (prefab == null) throw new InvalidOperationException("Missing city slice prefab: " + name);
            var prop = Instantiate(prefab, root, false).transform;
            prop.name = name; prop.localPosition = position; prop.localScale = scale;
            // The existing city colliders and interaction points remain authoritative.
            foreach (var collider in prop.GetComponentsInChildren<Collider>()) collider.enabled = false;
            return prop;
        }

        private void BuildSliceWorkshop()
        {
            SliceProp(walk.WorkbenchPrefab, "Верстак", new Vector3(-72, 0.61f, 34), new Vector3(1, 1, 1.3f));
            var tester = SliceProp(walk.TesterPrefab, "Нагрузочный тестер", new Vector3(-71, 1.28f, 34), Vector3.one * 0.8f);
            tester.localRotation = Quaternion.Euler(-20, 0, 0);
            Board("ДИАГНОСТИКА\nE — тест / замена батареи", new Vector3(-72, 2.65f, 34.8f), 0, new Vector2(3.4f, 0.65f));
            Board("ПОСЛЕ РЕМОНТА — ПОВТОРНЫЙ ТЕСТ", new Vector3(-72, 1.95f, 34.8f), 0, new Vector2(3.4f, 0.4f));
            // Flush yard markings leave the whole dock and its vehicle approach clear.
            foreach (var x in new[] { -70.1f, -67.9f })
                Decor("Граница разгрузки", new Vector3(x, 0.045f, 32), new Vector3(0.08f, 0.01f, 2), accentMaterial);
            Decor("Светильник верстака", new Vector3(-72, 3.15f, 34), new Vector3(2.8f, 0.1f, 0.18f), bodyMaterial);
            Decor("Рассеиватель верстака", new Vector3(-72, 3.09f, 34), new Vector3(2.6f, 0.02f, 0.12f), accentMaterial);
            foreach (var x in new[] { -66.26f, -63.74f })
                Decor("Наличник входа", new Vector3(x, 1.4f, 19.75f), new Vector3(0.08f, 2.8f, 0.1f), workshopMaterial);
            Decor("Козырёк входа", new Vector3(-65, 3.85f, 19.3f), new Vector3(3.2f, 0.12f, 1.1f), workshopMaterial);
            for (var i = 0; i < 4; i++)
                Decor("Шов мощения двора", new Vector3(-65, 0.085f, 12 + i * 1.5f), new Vector3(6, 0.006f, 0.015f), bodyMaterial);
            // Colour accents stay on fixtures: floors/walls retain different materials.
            var room = GameObject.Find("Interior | WORKSHOP / BASE");
            if (room != null)
                foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.name.Contains("floor", StringComparison.OrdinalIgnoreCase)) renderer.sharedMaterial = bodyMaterial;
        }

        private void BuildSliceCharacters()
        {
            sellerReaction = Character("Продавец", new Vector3(89.1f, 0.08f, -36), 0);
            buyerReaction = Character("Покупатель A", new Vector3(-27.8f, 0.08f, 11.8f), 0);
            feedbackAudio = root.gameObject.AddComponent<AudioSource>();
            feedbackAudio.playOnAwake = false; feedbackAudio.spatialBlend = 0; feedbackAudio.volume = 0.12f;
            feedbackAudio.ignoreListenerPause = true;
            actionTone = Tone("Interaction", 440, 0.07f);
            successTone = Tone("Result", 660, 0.18f);
            refusalTone = Tone("Unavailable", 220, 0.12f);
        }

        private CityTradeReaction Character(string name, Vector3 position, float yaw)
        {
            var actor = SliceProp(walk.WorkerPrefab, name, position, Vector3.one);
            actor.localRotation = Quaternion.Euler(0, yaw, 0);
            var reaction = actor.gameObject.AddComponent<CityTradeReaction>();
            reaction.Bind(this);
            return reaction;
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
            var reaction = current.Kind == CityTradeKind.Seller ? sellerReaction : current.Kind == CityTradeKind.A ? buyerReaction : null;
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
