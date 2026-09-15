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
            ui.game = Object.FindObjectOfType<DuelGame>();
            var canvas = ui.GetComponent<Canvas>() ?? ui.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>() ?? ui.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = .5f;
            if (!ui.GetComponent<GraphicRaycaster>()) ui.gameObject.AddComponent<GraphicRaycaster>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.titleText = Text(ui.transform, "Title", "废话少说，来决斗吧！", font, 32, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(640, 50));
            ui.scoreText = Text(ui.transform, "Score", "玩家 A   0 : 0   玩家 B", font, 23, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -135), new Vector2(640, 44));
            ui.turnText = Text(ui.transform, "Turn", "回合 1 / 20", font, 17, FontStyle.Normal, new Vector2(.5f, 1), new Vector2(0, -200), new Vector2(640, 36));
            ui.statusText = Text(ui.transform, "Status", "选择行动", font, 24, FontStyle.Bold, new Vector2(.5f, 1), new Vector2(0, -270), new Vector2(640, 60)); ui.statusText.color = new Color(1, .86f, .35f);
            ui.timerText = Text(ui.transform, "Timer", "剩余 2.0 秒", font, 16, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 280), new Vector2(640, 50));
            ui.modeText = Text(ui.transform, "Mode", "玩家 VS AI", font, 15, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 90), new Vector2(640, 34));
            ui.helpText = Text(ui.transform, "Help", "触摸按钮选择行动", font, 14, FontStyle.Normal, new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(640, 30));
            ui.actionPanel = new GameObject("Action Panel", typeof(RectTransform), typeof(HorizontalLayoutGroup)); ui.actionPanel.layer = 5; ui.actionPanel.transform.SetParent(ui.transform, false);
            Place((RectTransform)ui.actionPanel.transform, new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(640, 96));
            var layout = ui.actionPanel.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 12; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            ui.retreatButton = Button(ui, font, "Retreat", "A / 后退", ui.SelectRetreat);
            ui.advanceButton = Button(ui, font, "Advance", "D / 前进", ui.SelectAdvance);
            ui.thrustButton = Button(ui, font, "Thrust", "J / 击剑", ui.SelectThrust);
            ui.parryButton = Button(ui, font, "Parry", "K / 招架", ui.SelectParry);
            InstallArena(ui.transform, ui.game);
            if (!Object.FindObjectOfType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorUtility.SetDirty(ui); if (ui.game) EditorUtility.SetDirty(ui.game);
            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene); EditorSceneManager.SaveScene(ui.gameObject.scene);
            Debug.Log("Battle UI generated and serialized into " + ui.gameObject.scene.path);
        }

        static Text Text(Transform parent, string name, string value, Font font, int size, FontStyle style, Vector2 anchor, Vector2 pos, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.layer = 5; go.transform.SetParent(parent, false); Place((RectTransform)go.transform, anchor, pos, dimensions);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
        static Button Button(DuelUI ui, Font font, string name, string caption, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.layer = 5; go.transform.SetParent(ui.actionPanel.transform, false); go.GetComponent<Image>().color = new Color(.16f, .2f, .28f, .96f);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.disabledColor = Color.white; colors.fadeDuration = .08f; button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Text(go.transform, "Label", caption, font, 18, FontStyle.Bold, Vector2.zero, Vector2.zero, Vector2.zero); label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        static void InstallArena(Transform parent, DuelGame game)
        {
            if (!game) return;
            var arena = new GameObject("Arena", typeof(RectTransform)).GetComponent<RectTransform>();
            arena.SetParent(parent, false); arena.SetAsFirstSibling();
            Place(arena, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(640, 300));
            game.cells = new Image[7];
            for (int i = 0; i < game.cells.Length; i++)
                game.cells[i] = Image(arena, "格子 " + (i + 1), new Vector2((i - 3) * 82, -70), new Vector2(76, 12), new Color(.7f, .74f, .8f));
            game.playerA = Fencer(arena, "玩家 A", -164, 1, new Color(.25f, .72f, 1f), out game.bodyA, out game.swordA);
            game.playerB = Fencer(arena, "玩家 B", 164, -1, new Color(1f, .35f, .38f), out game.bodyB, out game.swordB);
        }

        static RectTransform Fencer(Transform parent, string name, float x, float facing, Color color, out Image body, out Transform sword)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); Place(root, new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(120, 150));
            body = Image(root, "身体", new Vector2(0, 10), new Vector2(32, 84), color);
            Image(root, "头", new Vector2(0, 76), new Vector2(36, 36), Color.white);
            sword = Image(root, "剑", new Vector2(45 * facing, 30), new Vector2(64, 6), Color.white).transform;
            return root;
        }

        static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.color = color; image.raycastTarget = false;
            Place(image.rectTransform, new Vector2(.5f, .5f), position, size);
            return image;
        }
        static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size; }
    }
}
