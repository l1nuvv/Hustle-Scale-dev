using System;
using Interactables;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FirstDealOutcomeBuilder
{
    [MenuItem("Tools/T05/Apply Outcomes")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        ApplyCurrent();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void ApplyCurrent()
    {
        var deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        var menu = GameObject.Find("T04 HUD").GetComponentsInChildren<Transform>(true);
        Transform Find(string name) => Array.Find(menu, t => t.name == name);
        var terms = Find("Terms").GetComponent<Text>();
        Layout(terms.rectTransform, new Vector2(0.035f, 0.27f), new Vector2(0.965f, 0.97f));
        terms.fontSize = 22;
        var seller = Find("Seller choices");
        var lot = Find("Confirm lot - $180").GetComponent<Button>();
        var battery = Find("Buy battery - $50").GetComponent<Button>();
        if (lot.transform.parent == seller)
            Layout(lot.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0.32f, 1));
        if (battery.transform.parent == seller)
            Layout(battery.GetComponent<RectTransform>(), new Vector2(0.34f, 0), new Vector2(0.66f, 1));
        var buyback = Find("Sell brought lamps");
        if (buyback == null)
        {
            var button = MakeButton(seller, "Sell brought lamps", "Продать принесённые\n$60 / $35", new Vector2(0.68f, 0), Vector2.one);
            UnityEventTools.AddPersistentListener(button.onClick, deal.SellBack);
        }
        var footer = Find("Footer");
        Layout(footer.GetComponent<RectTransform>(), new Vector2(0.12f, 0.035f), new Vector2(0.88f, 0.12f));
        var resume = Find("Resume / leave without buying").GetComponent<Button>();
        Layout(resume.GetComponent<RectTransform>(), new Vector2(0.51f, 0), Vector2.one);
        var summary = footer.Find("Shift summary")?.GetComponent<Button>();
        if (summary == null)
        {
            summary = MakeButton(footer, "Shift summary", "Итоги и следующий шаг", Vector2.zero, new Vector2(0.49f, 1));
            UnityEventTools.AddPersistentListener(summary.onClick, deal.OpenSummary);
        }
        var choices = Find("Outcome choices");
        if (choices == null)
        {
            choices = new GameObject("Outcome choices", typeof(RectTransform)).transform;
            choices.SetParent(terms.transform.parent, false);
            Layout((RectTransform)choices, new Vector2(0.12f, 0.15f), new Vector2(0.88f, 0.235f));
            var finish = MakeButton(choices, "Finish shift", "Завершить смену", new Vector2(0.15f, 0), new Vector2(0.85f, 1));
            UnityEventTools.AddPersistentListener(finish.onClick, deal.FinishShift);
            var restart = MakeButton(choices, "Restart shift", "Начать заново", new Vector2(0.15f, 0), new Vector2(0.85f, 1));
            UnityEventTools.AddPersistentListener(restart.onClick, deal.RestartShift);
        }
        Ref(deal, "outcomeButtons", choices.gameObject);
        Ref(deal, "finishButton", choices.Find("Finish shift").GetComponent<Button>());
        Ref(deal, "restartButton", choices.Find("Restart shift").GetComponent<Button>());
        Ref(deal, "summaryButton", summary);
        choices.gameObject.SetActive(false);
        EditorUtility.SetDirty(deal);
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
    {
        var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        target.transform.SetParent(parent, false);
        Layout((RectTransform)target.transform, min, max);
        target.GetComponent<Image>().color = new Color(0.15f, 0.3f, 0.42f);
        var text = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(target.transform, false);
        Layout(text.rectTransform, new Vector2(0.025f, 0.04f), new Vector2(0.975f, 0.96f));
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.text = label; text.raycastTarget = false;
        var button = target.GetComponent<Button>();
        var colors = button.colors;
        colors.selectedColor = new Color(0.5f, 0.85f, 1);
        button.colors = colors;
        return button;
    }

    private static void Layout(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Ref(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
