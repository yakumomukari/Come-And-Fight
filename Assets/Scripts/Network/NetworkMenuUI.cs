using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ComeAndFight.Networking
{
    public sealed class NetworkMenuUI : MonoBehaviour
    {
        [SerializeField] GameObject menuRoot;
        [SerializeField] GameObject modePanel;
        [SerializeField] GameObject onlinePanel;
        [SerializeField] InputField addressInput;
        [SerializeField] Text statusText;
        [SerializeField] Button createButton;
        [SerializeField] Button joinButton;
        [SerializeField] DuelGame duelGame;

        NetworkBootstrap bootstrap;
        bool focusAddressOnNextGui;
        GUIStyle addressStyle;
        float copiedStatusUntil;
        Button onlineButton;
        Text descriptionText;

        void Start()
        {
            bootstrap = FindObjectOfType<NetworkBootstrap>();
            Transform onlineButtonTransform = modePanel ? modePanel.transform.Find("Online Battle") : null;
            if (onlineButtonTransform) onlineButton = onlineButtonTransform.GetComponent<Button>();
            Transform descriptionTransform = onlinePanel ? onlinePanel.transform.Find("Description") : null;
            if (descriptionTransform) descriptionText = descriptionTransform.GetComponent<Text>();
            ConfigurePlatformUi();
            if (addressInput) addressInput.readOnly = !Application.isMobilePlatform;
            ShowModeSelection();
        }

        void Update()
        {
            if (Application.isMobilePlatform && Input.GetKeyDown(KeyCode.Escape))
            {
                if (onlinePanel && onlinePanel.activeSelf) CancelOnline();
                else if (menuRoot && !menuRoot.activeSelf) ShowModeSelection();
                else Application.Quit();
                return;
            }
            if (!bootstrap) bootstrap = FindObjectOfType<NetworkBootstrap>();
            if (!bootstrap || !statusText) return;

            if (bootstrap.Phase == NetworkTurnPhase.WaitingForPlayers && bootstrap.IsListening)
            {
                if (bootstrap.IsHost && Time.unscaledTime < copiedStatusUntil) return;
                statusText.text = bootstrap.IsHost ? WaitingForOpponentText() : Application.isMobilePlatform ? "正在通过 Photon 连接房主…" : "正在通过 Steam 连接房主…";
            }
            else if (bootstrap.Phase == NetworkTurnPhase.Paused)
            {
                menuRoot.SetActive(true);
                modePanel.SetActive(false);
                onlinePanel.SetActive(true);
                if (duelGame) duelGame.InputLocked = true;
                statusText.text = bootstrap && !string.IsNullOrEmpty(bootstrap.LastConnectionError) ? bootstrap.LastConnectionError : "连接中断，比赛已暂停";
                SetConnectionButtons(true);
            }
            else if (bootstrap.Phase == NetworkTurnPhase.Choosing || bootstrap.Phase == NetworkTurnPhase.Resolving || bootstrap.Phase == NetworkTurnPhase.GameOver)
            {
                menuRoot.SetActive(false);
                if (duelGame) duelGame.InputLocked = false;
            }
        }

        public void StartSinglePlayer()
        {
            DisconnectIfNeeded();
            if (duelGame && duelGame.IsOnlineMode) duelGame.ExitOnlineMode();
            menuRoot.SetActive(false);
            if (duelGame) duelGame.InputLocked = false;
        }

        public void ShowOnlinePanel()
        {
            modePanel.SetActive(false);
            onlinePanel.SetActive(true);
            if (!bootstrap || !bootstrap.IsListening) SetCreateButtonCaption(Application.isMobilePlatform ? "创建 Photon 房间" : "创建房间（Host）");
            statusText.text = Application.isMobilePlatform ? "输入房主分享的 Photon 房间码" : "输入房主显示的 17 位 Steam ID，或 Ctrl+V 粘贴";
            if (duelGame) duelGame.InputLocked = true;
            focusAddressOnNextGui = true;
            StartCoroutine(FocusAddressInput());
        }

        public void ShowModeSelection()
        {
            menuRoot.SetActive(true);
            modePanel.SetActive(true);
            onlinePanel.SetActive(false);
            if (duelGame) duelGame.InputLocked = true;
            if (statusText) statusText.text = "选择游戏模式";
        }

        public async void CreateRoom()
        {
            if (!bootstrap) return;
            if (bootstrap.IsListening && bootstrap.IsHost)
            {
                CopyHostSteamId();
                return;
            }
            SetConnectionButtons(false);
            statusText.text = Application.isMobilePlatform ? "正在连接 Photon 并创建房间…" : "正在创建 Steam 房间…";
            if (!await bootstrap.StartHostForCurrentPlatformAsync()) { statusText.text = string.IsNullOrEmpty(bootstrap.LastConnectionError) ? "创建房间失败，请查看 Console" : bootstrap.LastConnectionError; SetConnectionButtons(true); }
            else
            {
                statusText.text = WaitingForOpponentText();
                createButton.interactable = true;
                SetCreateButtonCaption(Application.isMobilePlatform ? "复制 Photon 房间码" : "复制房主 Steam ID");
            }
        }

        public void CopyHostSteamId()
        {
            if (!bootstrap || string.IsNullOrWhiteSpace(bootstrap.LocalAddressSummary)) return;
            GUIUtility.systemCopyBuffer = bootstrap.LocalAddressSummary;
            statusText.text = (Application.isMobilePlatform ? "已复制 Photon 房间码：" : "已复制房主 Steam ID：") + bootstrap.LocalAddressSummary;
            copiedStatusUntil = Time.unscaledTime + 2f;
        }

        public async void JoinRoom()
        {
            if (!bootstrap) return;
            bootstrap.SetAddress(addressInput ? addressInput.text : string.Empty);
            SetConnectionButtons(false);
            statusText.text = Application.isMobilePlatform ? "正在验证房间码并连接 Photon…" : "正在连接 Steam 房主…";
            if (!await bootstrap.StartClientForCurrentPlatformAsync()) { statusText.text = string.IsNullOrEmpty(bootstrap.LastConnectionError) ? "连接失败，请检查房间信息" : bootstrap.LastConnectionError; SetConnectionButtons(true); }
            else statusText.text = "正在连接房主…";
        }

        public void CancelOnline()
        {
            DisconnectIfNeeded();
            SetConnectionButtons(true);
            SetCreateButtonCaption(Application.isMobilePlatform ? "创建 Photon 房间" : "创建房间（Host）");
            ShowModeSelection();
        }

        void DisconnectIfNeeded()
        {
            if (bootstrap) bootstrap.Shutdown();
        }

        void SetConnectionButtons(bool value)
        {
            if (createButton) createButton.interactable = value;
            if (joinButton) joinButton.interactable = value;
        }

        void SetCreateButtonCaption(string caption)
        {
            if (!createButton) return;
            Text label = createButton.GetComponentInChildren<Text>();
            if (label) label.text = caption;
        }

        void ConfigurePlatformUi()
        {
            if (!onlineButton) return;
            Text label = onlineButton.GetComponentInChildren<Text>();
            onlineButton.interactable = true;
            if (label) label.text = Application.isMobilePlatform ? "Android Photon 对战" : "Steam 在线对战";
            if (descriptionText) descriptionText.text = Application.isMobilePlatform
                ? "Android Photon 联机 · 创建或输入房间码\n需要互联网连接"
                : "Steam P2P / Relay · 输入房主显示的 Steam ID\n双方需启动 Steam 并登录不同账号";
            if (Application.isMobilePlatform)
            {
                SetCreateButtonCaption("创建 Photon 房间");
                SetButtonCaption(joinButton, "通过房间码加入");
                if (addressInput && addressInput.placeholder is Text placeholder) placeholder.text = "Photon 房间码";
            }
        }

        static void SetButtonCaption(Button button, string caption)
        {
            if (!button) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label) label.text = caption;
        }

        string WaitingForOpponentText()
        {
            return Application.isMobilePlatform ? "等待对手 · Photon 房间码：" + bootstrap.LocalAddressSummary : "等待对手 · 房主 Steam ID：" + bootstrap.LocalAddressSummary;
        }

        void OnGUI()
        {
            if (Application.isMobilePlatform || !onlinePanel || !onlinePanel.activeInHierarchy || !addressInput || bootstrap && bootstrap.IsListening) return;
            RectTransform rectTransform = addressInput.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Rect rect = new Rect(corners[0].x, Screen.height - corners[1].y, corners[2].x - corners[0].x, corners[1].y - corners[0].y);
            if (addressStyle == null)
            {
                addressStyle = new GUIStyle(GUI.skin.textField) { fontSize = 21, alignment = TextAnchor.MiddleLeft };
                addressStyle.padding = new RectOffset(18, 18, 4, 4);
            }
            GUI.SetNextControlName("HostAddressInput");
            addressInput.text = GUI.TextField(rect, addressInput.text, 20, addressStyle);
            if (focusAddressOnNextGui)
            {
                GUI.FocusControl("HostAddressInput");
                focusAddressOnNextGui = false;
            }
        }

        IEnumerator FocusAddressInput()
        {
            yield return null;
            if (!addressInput || !addressInput.IsInteractable()) yield break;
            EventSystem.current?.SetSelectedGameObject(addressInput.gameObject);
            addressInput.ActivateInputField();
            addressInput.Select();
            addressInput.selectionAnchorPosition = 0;
            addressInput.selectionFocusPosition = addressInput.text.Length;
        }
    }
}
