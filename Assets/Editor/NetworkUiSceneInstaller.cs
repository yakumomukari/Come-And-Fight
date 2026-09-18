using ComeAndFight;
using ComeAndFight.Networking;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NetworkUiSceneInstaller
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    static readonly Color Background = new Color(0.025f, 0.035f, 0.06f, 0.96f);
    static readonly Color Panel = new Color(0.08f, 0.11f, 0.17f, 0.98f);
    static readonly Color ButtonColor = new Color(0.12f, 0.34f, 0.5f, 1f);
    static Font font;

    [MenuItem("Come And Fight/Install Network Menu")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject canvas = GameObject.Find("Battle UI");
        DuelGame game = Object.FindObjectOfType<DuelGame>();
        if (!canvas || !game) throw new System.InvalidOperationException("SampleScene is missing Battle UI or DuelGame.");

        Transform old = canvas.transform.Find("Network Menu");
        if (old) Object.DestroyImmediate(old.gameObject);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = UiObject("Network Menu", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());
        root.AddComponent<Image>().color = Background;
        NetworkMenuUI ui = root.AddComponent<NetworkMenuUI>();

        GameObject card = UiObject("Card", root.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
        cardRect.pivot = new Vector2(.5f, .5f);
        cardRect.sizeDelta = new Vector2(560, 690);
        card.AddComponent<Image>().color = Panel;

        GameObject modePanel = CreateVerticalPanel("Mode Panel", card.transform);
        CreateText("Title", modePanel.transform, "COME AND FIGHT", 38, FontStyle.Bold, 80);
        CreateText("Subtitle", modePanel.transform, "选择游戏模式", 22, FontStyle.Normal, 55);
        Button single = CreateButton("Single Player", modePanel.transform, "单人对战");
        Button online = CreateButton("Online Battle", modePanel.transform, "在线对战");
        UnityEventTools.AddPersistentListener(single.onClick, ui.StartSinglePlayer);
        UnityEventTools.AddPersistentListener(online.onClick, ui.ShowOnlinePanel);

        GameObject onlinePanel = CreateVerticalPanel("Online Panel", card.transform);
        CreateText("Title", onlinePanel.transform, "在线对战", 34, FontStyle.Bold, 72);
        Text description = CreateText("Description", onlinePanel.transform, "Steam P2P / Relay · 输入房主显示的 Steam ID\n双方需启动 Steam 并登录不同账号", 18, FontStyle.Normal, 76);
        description.color = new Color(.75f, .8f, .9f);
        Button create = CreateButton("Create Room", onlinePanel.transform, "创建房间（Host）");
        InputField address = CreateInput(onlinePanel.transform);
        Button join = CreateButton("Join Room", onlinePanel.transform, "加入房间（Client）");
        Text status = CreateText("Connection Status", onlinePanel.transform, "创建 Steam 房间，或输入房主 Steam ID 加入", 18, FontStyle.Normal, 65);
        status.color = new Color(1f, .83f, .32f);
        Button back = CreateButton("Back", onlinePanel.transform, "返回");
        UnityEventTools.AddPersistentListener(create.onClick, ui.CreateRoom);
        UnityEventTools.AddPersistentListener(join.onClick, ui.JoinRoom);
        UnityEventTools.AddPersistentListener(back.onClick, ui.CancelOnline);
        onlinePanel.SetActive(false);

        SerializedObject serialized = new SerializedObject(ui);
        serialized.FindProperty("menuRoot").objectReferenceValue = root;
        serialized.FindProperty("modePanel").objectReferenceValue = modePanel;
        serialized.FindProperty("onlinePanel").objectReferenceValue = onlinePanel;
        serialized.FindProperty("addressInput").objectReferenceValue = address;
        serialized.FindProperty("statusText").objectReferenceValue = status;
        serialized.FindProperty("createButton").objectReferenceValue = create;
        serialized.FindProperty("joinButton").objectReferenceValue = join;
        serialized.FindProperty("duelGame").objectReferenceValue = game;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Network] Serialized network menu installed in SampleScene.");
    }

    static GameObject CreateVerticalPanel(string name, Transform parent)
    {
        GameObject panel = UiObject(name, parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.08f, .06f); rect.anchorMax = new Vector2(.92f, .94f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20); layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true; layout.childControlHeight = false;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        return panel;
    }

    static Button CreateButton(string name, Transform parent, string caption)
    {
        GameObject go = UiObject(name, parent);
        LayoutElement element = go.AddComponent<LayoutElement>(); element.preferredHeight = 72;
        Image image = go.AddComponent<Image>(); image.color = ButtonColor;
        Button button = go.AddComponent<Button>(); button.targetGraphic = image;
        ColorBlock colors = button.colors; colors.highlightedColor = new Color(.18f, .48f, .68f); colors.pressedColor = new Color(.08f, .24f, .38f); button.colors = colors;
        Text label = CreateText("Label", go.transform, caption, 22, FontStyle.Bold, 72);
        Stretch(label.rectTransform());
        return button;
    }

    static InputField CreateInput(Transform parent)
    {
        GameObject go = UiObject("Address Input", parent);
        go.AddComponent<LayoutElement>().preferredHeight = 66;
        Image image = go.AddComponent<Image>(); image.color = new Color(.04f, .06f, .1f, 1f);
        InputField input = go.AddComponent<InputField>(); input.targetGraphic = image; input.readOnly = true;
        Text text = CreateText("Text", go.transform, "", 21, FontStyle.Normal, 66);
        text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleLeft; text.rectTransform().offsetMin = new Vector2(18, 0); text.rectTransform().offsetMax = new Vector2(-18, 0);
        Text placeholder = CreateText("Placeholder", go.transform, "房主 Steam ID（17 位数字）", 19, FontStyle.Italic, 66);
        placeholder.raycastTarget = false;
        placeholder.color = new Color(.5f, .55f, .65f); placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.rectTransform().offsetMin = new Vector2(18, 0); placeholder.rectTransform().offsetMax = new Vector2(-18, 0);
        input.textComponent = text; input.placeholder = placeholder; input.text = "";
        return input;
    }

    static Text CreateText(string name, Transform parent, string value, int size, FontStyle style, float height)
    {
        GameObject go = UiObject(name, parent);
        go.AddComponent<LayoutElement>().preferredHeight = height;
        Text text = go.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.fontStyle = style;
        text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.supportRichText = false;
        return text;
    }

    static GameObject UiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI"); go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static RectTransform rectTransform(this Text text) => text.GetComponent<RectTransform>();
}
