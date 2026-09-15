using ComeAndFight;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ComeAndFight.Editor
{
    public static class DuelSceneUIInstaller
    {
        [InitializeOnLoadMethod]
        static void Schedule()
        {
            EditorApplication.delayCall += Install;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Install;
        }

        [MenuItem("Come And Fight/Build Fixed Scene UI")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var ui = Object.FindObjectOfType<DuelUI>();
            if (!ui || ui.titleText) return;
            var canvas = ui.GetComponent<Canvas>() ?? ui.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>() ?? ui.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            if (!ui.GetComponent<GraphicRaycaster>()) ui.gameObject.AddComponent<GraphicRaycaster>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.titleText = Text(ui.transform, "Title", "废话少说，来决斗吧！", font, 32, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -18), new Vector2(900, 44));
            ui.scoreText = Text(ui.transform, "Score", "玩家 A   0 : 0   玩家 B", font, 23, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -68), new Vector2(700, 36));
            ui.turnText = Text(ui.transform, "Turn", "回合 1 / 20", font, 17, FontStyle.Normal, new Vector2(.5f, 1), new Vector2(0, -105), new Vector2(500, 30));
            ui.statusText = Text(ui.transform, "Status", "选择行动", font, 24, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -143), new Vector2(800, 38)); ui.statusText.color = new Color(1, .86f, .35f);
            ui.timerText = Text(ui.transform, "Timer", "剩余 2.0 秒", font, 16, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 145), new Vector2(900, 28));
            ui.modeText = Text(ui.transform, "Mode", "玩家 VS AI", font, 15, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 42), new Vector2(900, 24));
            ui.helpText = Text(ui.transform, "Help", "F1 切换模式  ·  R 重新开始", font, 14, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(900, 22));
            ui.actionPanel = new GameObject("Action Panel", typeof(RectTransform), typeof(HorizontalLayoutGroup)); ui.actionPanel.layer = 5; ui.actionPanel.transform.SetParent(ui.transform, false);
            Place((RectTransform)ui.actionPanel.transform, new Vector2(.5f, 0), new Vector2(0, 78), new Vector2(620, 56));
            var layout = ui.actionPanel.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 12; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            Button(ui, font, "Retreat", "A / 后退", ui.SelectRetreat); Button(ui, font, "Advance", "D / 前进", ui.SelectAdvance); Button(ui, font, "Thrust", "J / 击剑", ui.SelectThrust); Button(ui, font, "Parry", "K / 招架", ui.SelectParry);
            if (!Object.FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorUtility.SetDirty(ui); EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene);
            Debug.Log("Battle UI generated and serialized into " + ui.gameObject.scene.path);
        }

        static Text Text(Transform parent, string name, string value, Font font, int size, FontStyle style, Vector2 anchor, Vector2 pos, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.layer = 5; go.transform.SetParent(parent, false); Place((RectTransform)go.transform, anchor, pos, dimensions);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
        static void Button(DuelUI ui, Font font, string name, string caption, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.layer = 5; go.transform.SetParent(ui.actionPanel.transform, false); go.GetComponent<Image>().color = new Color(.16f, .2f, .28f, .96f);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.disabledColor = Color.white; colors.fadeDuration = .08f; button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Text(go.transform, "Label", caption, font, 18, FontStyle.Bold, Vector2.zero, Vector2.zero, Vector2.zero); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size; }
    }
}
