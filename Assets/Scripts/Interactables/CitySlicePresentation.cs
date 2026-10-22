using System;
using UnityEngine;

namespace Interactables
{
    // Bounded E5 presentation; it never writes to the trading model.
    public sealed partial class CityTradeView
    {
        private CityTradeReaction sellerReaction, buyerReaction, buyerBReaction;
        private AudioSource feedbackAudio;
        private AudioClip actionTone, successTone, refusalTone;

        private Transform SliceProp(GameObject prefab, string name, Vector3 position, Vector3 scale, bool solid = false)
        {
            if (prefab == null) throw new InvalidOperationException("Missing city slice prefab: " + name);
            var prop = Instantiate(prefab, root, false).transform;
            prop.name = name; prop.localPosition = position; prop.localScale = scale;
            foreach (var collider in prop.GetComponentsInChildren<Collider>()) collider.enabled = solid;
            if (solid && prop.GetComponent<Collider>() == null)
            {
                var renderers = prop.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var collider = prop.gameObject.AddComponent<BoxCollider>();
                collider.center = prop.InverseTransformPoint(bounds.center);
                collider.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
            }
            return prop;
        }

        private void BuildSliceWorkshop()
        {
            var bench = SliceProp(walk.WorkbenchPrefab, "Верстак", new Vector3(-72, 0.61f, 34), new Vector3(1, 1, 1.3f), true);
            AddPoint(bench, CityTradeKind.Workshop);
            var tester = SliceProp(walk.TesterPrefab, "Нагрузочный тестер", new Vector3(-71, 1.28f, 34), Vector3.one * 0.8f, true);
            tester.localRotation = Quaternion.Euler(-20, 0, 0);
            AddPoint(tester, CityTradeKind.Workshop);
            Board("01 / ДИАГНОСТИКА", new Vector3(-72, 2.95f, 34.8f), 0, new Vector2(3.4f, 0.4f));
            Board("ТЕСТ → РЕМОНТ → ТЕСТ", new Vector3(-72, 1.35f, 34.8f), 0, new Vector2(3.4f, 0.3f));
            BuildWorkshopDetails();
            Decor("Светильник верстака", new Vector3(-72, 3.15f, 34), new Vector3(2.8f, 0.1f, 0.18f), bodyMaterial);
            Decor("Рассеиватель верстака", new Vector3(-72, 3.09f, 34), new Vector3(2.6f, 0.02f, 0.12f), accentMaterial);
            foreach (var x in new[] { -66.26f, -63.74f })
                Decor("Наличник входа", new Vector3(x, 1.4f, 19.75f), new Vector3(0.08f, 2.8f, 0.1f), workshopMaterial);
            Decor("Козырёк входа", new Vector3(-65, 3.85f, 19.3f), new Vector3(3.2f, 0.12f, 1.1f), workshopMaterial);
            BuildWorkshopYard();
            // Colour accents stay on fixtures: floors/walls retain different materials.
            var room = GameObject.Find("Interior | WORKSHOP / BASE");
            if (room != null)
                foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.name.Contains("floor", StringComparison.OrdinalIgnoreCase)) renderer.sharedMaterial = bodyMaterial;
        }

        private void BuildWorkshopDetails()
        {
            // Shallow trims follow the existing room shell; all additions are visual only.
            Decor("Пол мастерской", new Vector3(-65, 0.025f, 30), new Vector3(23.5f, 0.02f, 19.5f), bodyMaterial);
            foreach (var x in new[] { -76.72f, -53.28f })
            {
                Decor("Нижняя панель стены", new Vector3(x, 0.7f, 30), new Vector3(0.04f, 1.3f, 19.5f), workshopMaterial);
                Decor("Рейка панели стены", new Vector3(x, 1.38f, 30), new Vector3(0.06f, 0.06f, 19.5f), bodyMaterial);
            }
            Decor("Нижняя панель задней стены", new Vector3(-65, 0.7f, 39.72f), new Vector3(23.5f, 1.3f, 0.04f), workshopMaterial);
            Decor("Рейка задней стены", new Vector3(-65, 1.38f, 39.7f), new Vector3(23.5f, 0.06f, 0.06f), bodyMaterial);
            // The free-standing tool station now has a full-height back, rather than floating signs.
            Decor("Перегородка рабочего места", new Vector3(-72, 1.65f, 35.12f), new Vector3(4.2f, 3.3f, 0.16f), plasterMaterial, true);
            foreach (var x in new[] { -74.15f, -69.85f })
                Decor("Рама рабочего места", new Vector3(x, 1.65f, 35), new Vector3(0.08f, 3.3f, 0.12f), workshopMaterial);
            foreach (var x in new[] { -74.5f, -69.5f })
                Decor("Граница рабочего места", new Vector3(x, 0.043f, 33), new Vector3(0.06f, 0.01f, 4), accentMaterial);
            Decor("Задняя граница рабочего места", new Vector3(-72, 0.043f, 35), new Vector3(5, 0.01f, 0.06f), accentMaterial);
            foreach (var y in new[] { 0.35f, 1.15f, 1.95f })
                Decor("Полка хранения мастерской", new Vector3(-57, y, 38.8f), new Vector3(5, 0.08f, 0.7f), bodyMaterial, true);
            foreach (var x in new[] { -59.45f, -54.55f })
                Decor("Стойка хранения мастерской", new Vector3(x, 1.2f, 38.8f), new Vector3(0.08f, 2.4f, 0.7f), workshopMaterial, true);
            for (var i = 0; i < 4; i++)
            {
                var position = new Vector3(-58.8f + i * 1.15f, 0.65f, 38.8f);
                Decor("Коробка запчастей", position, new Vector3(0.9f, 0.5f, 0.55f), plasterMaterial, true);
                Decor("Маркировка коробки", position + new Vector3(0, 0, -0.28f), new Vector3(0.22f, 0.28f, 0.015f), accentMaterial);
            }
            Board("ЗАПЧАСТИ / ХРАНЕНИЕ", new Vector3(-57, 2.5f, 39.3f), 0, new Vector2(4, 0.4f));
            // A threshold and shallow wall fixtures identify the exit without narrowing the doorway.
            Decor("Порог мастерской", new Vector3(-65, 0.045f, 20), new Vector3(2.4f, 0.02f, 0.4f), accentMaterial);
            foreach (var x in new[] { -67f, -63f })
            {
                Decor("Корпус входного света", new Vector3(x, 2.7f, 19.7f), new Vector3(0.22f, 0.45f, 0.16f), bodyMaterial);
                Decor("Рассеиватель входного света", new Vector3(x, 2.7f, 19.6f), new Vector3(0.16f, 0.3f, 0.03f), accentMaterial);
            }
        }

        private void BuildWorkshopYard()
        {
            yardMaterial = new Material(bodyMaterial) { color = new Color(0.34f, 0.36f, 0.32f) };
            Decor("Площадка двора мастерской", new Vector3(-65, 0.08f, 14), new Vector3(25, 0.04f, 10), yardMaterial);
            for (var x = -75; x <= -55; x += 5)
                Decor("Шов покрытия двора", new Vector3(x, 0.105f, 14), new Vector3(0.025f, 0.005f, 10), bodyMaterial);
            for (var z = 10; z <= 18; z += 4)
                Decor("Поперечный шов двора", new Vector3(-65, 0.105f, z), new Vector3(25, 0.005f, 0.025f), bodyMaterial);
            foreach (var x in new[] { -77.3f, -52.7f })
            {
                Box("Низкое ограждение двора", root, new Vector3(x, 0.35f, 14), new Vector3(0.22f, 0.7f, 10), workshopMaterial);
                Decor("Крышка ограждения", new Vector3(x, 0.72f, 14), new Vector3(0.3f, 0.06f, 10), bodyMaterial);
            }
            // A wide, unobstructed central approach is shared by foot traffic and cargo.
            foreach (var x in new[] { -68.5f, -61.5f })
                Decor("Граница проезда двора", new Vector3(x, 0.106f, 14), new Vector3(0.12f, 0.01f, 10), accentMaterial);
            foreach (var x in new[] { -75f, -72.5f })
            {
                Decor("Складской контейнер двора", new Vector3(x, 0.5f, 17), new Vector3(1.8f, 1, 1.2f), bodyMaterial, true);
                Decor("Крышка контейнера", new Vector3(x, 1.04f, 17), new Vector3(1.9f, 0.08f, 1.3f), workshopMaterial, true);
            }
            foreach (var y in new[] { 0.35f, 1.15f, 1.95f })
                Decor("Полка двора", new Vector3(-55, y, 17.5f), new Vector3(2.6f, 0.08f, 0.6f), bodyMaterial, true);
            foreach (var x in new[] { -56.25f, -53.75f })
                Decor("Стойка стеллажа двора", new Vector3(x, 1.15f, 17.5f), new Vector3(0.08f, 2.3f, 0.6f), bodyMaterial, true);
            Decor("Запас материалов двора", new Vector3(-55, 0.67f, 17.5f), new Vector3(2.1f, 0.5f, 0.5f), plasterMaterial, true);
            Board("ДВОР МАСТЕРСКОЙ\nСвободный проезд — по центру", new Vector3(-73, 1.65f, 18.7f), 0, new Vector2(4.4f, 0.65f));
        }

        private void BuildSliceCharacters()
        {
            sellerReaction = Character("Продавец", SellerPosition + new Vector3(1.1f, -1.22f, -0.75f), 180);
            buyerReaction = Character("Покупатель A", new Vector3(-26, 0.08f, 10.3f), 0);
            buyerBReaction = Character("Покупатель Б", new Vector3(-4, 0.08f, 10.3f), 0);
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
            var reaction = current.Kind == CityTradeKind.Seller ? sellerReaction :
                current.Kind == CityTradeKind.A ? buyerReaction : current.Kind == CityTradeKind.B ? buyerBReaction : null;
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
