using UnityEngine;
using UnityEngine.UI;

namespace ComeAndFight
{
    public sealed class DuelUI : MonoBehaviour
    {
        static readonly Color NormalColor = new Color(.16f, .2f, .28f, .96f);
        static readonly Color SelectedColor = new Color(.12f, .58f, .78f, 1f);
        static readonly Color LockedColor = new Color(.1f, .12f, .16f, .78f);

        public Text titleText, scoreText, turnText, statusText, timerText, modeText, helpText;
        public GameObject actionPanel;
        public DuelGame game;
        public Button retreatButton, advanceButton, thrustButton, parryButton;
        Button[] actionButtons;

        void Awake() { actionButtons = new[] { retreatButton, advanceButton, thrustButton, parryButton }; }
        void Update()
        {
            if (!game) return;
            if (!scoreText || !turnText || !statusText || !timerText || !modeText || !actionPanel) return;
            var s = game.State;
            scoreText.text = "玩家 A   " + s.aScore + "  :  " + s.bScore + "   玩家 B";
            turnText.text = s.suddenDeath ? "死斗 · 回合 " + s.turn : "回合 " + s.turn + " / 20";
            statusText.text = game.Status;
            timerText.text = TimerText();
            bool portrait = Screen.height > Screen.width;
            modeText.text = game.IsLocalTwoPlayer ? (portrait ? "本地双人模式" : "本地双人：B 使用 ←前进 / →后退 / ,击剑 / .招架") : "玩家 VS AI";
            helpText.text = portrait ? "触摸按钮选择行动" : "F1 切换模式  ·  R 重新开始";
            actionPanel.SetActive(true);
            timerText.gameObject.SetActive(!game.IsGameOver);
            RefreshButtons();
        }

        string TimerText()
        {
            if (game.Phase == DuelGame.TurnPhase.Reveal) return "行动公开";
            if (game.Phase == DuelGame.TurnPhase.Result) return "结算中";
            string a = game.ALocked ? "A 已选择：" + DuelGame.ActionName(game.AChoice) : "A 等待选择";
            if (!game.ALocked && game.AThrustRecovering) a += "（击剑恢复）";
            string b = game.IsLocalTwoPlayer ? (game.BLocked ? "  ·  B 已锁定" : game.BThrustRecovering ? "  ·  B 击剑恢复" : "  ·  B 等待选择") : "";
            return "剩余 " + game.RemainingTime.ToString("0.0") + " 秒  ·  " + a + b;
        }

        void RefreshButtons()
        {
            bool canChoose = game.Phase == DuelGame.TurnPhase.Choosing && !game.ALocked;
            foreach (var button in actionButtons)
            {
                if (!button) continue;
                DuelAction action = ActionForButton(button.name);
                bool selected = game.ALocked && action == game.AChoice;
                bool gameOverCommand = game.IsGameOver && (action == DuelAction.Retreat || action == DuelAction.Advance);
                bool recovering = action == DuelAction.Thrust && game.AThrustRecovering;
                button.interactable = canChoose && !recovering || gameOverCommand;
                var image = button.targetGraphic as Image;
                if (image) image.color = selected ? SelectedColor : gameOverCommand || canChoose ? NormalColor : LockedColor;
                var label = button.GetComponentInChildren<Text>();
                if (label)
                {
                    label.text = game.IsGameOver && action == DuelAction.Retreat ? "重新开始" : game.IsGameOver && action == DuelAction.Advance ? "切换模式" : Caption(action, recovering);
                    label.color = selected ? Color.white : new Color(1f, 1f, 1f, button.interactable ? 1f : .62f);
                }
            }
        }

        static string Caption(DuelAction action, bool recovering)
        {
            if (recovering) return Screen.height > Screen.width ? "击剑（恢复）" : "J / 击剑（恢复）";
            if (Screen.height > Screen.width) return DuelGame.ActionName(action);
            if (action == DuelAction.Retreat) return "A / 后退";
            if (action == DuelAction.Advance) return "D / 前进";
            if (action == DuelAction.Thrust) return "J / 击剑";
            return "K / 招架";
        }

        static DuelAction ActionForButton(string buttonName)
        {
            if (buttonName == "Advance") return DuelAction.Advance;
            if (buttonName == "Retreat") return DuelAction.Retreat;
            if (buttonName == "Thrust") return DuelAction.Thrust;
            if (buttonName == "Parry") return DuelAction.Parry;
            return DuelAction.None;
        }

        public void SelectRetreat() { if (game && game.IsGameOver) game.RestartMatch(); else Select(DuelAction.Retreat); }
        public void SelectAdvance() { if (game && game.IsGameOver) game.ToggleMode(); else Select(DuelAction.Advance); }
        public void SelectThrust() => Select(DuelAction.Thrust);
        public void SelectParry() => Select(DuelAction.Parry);
        void Select(DuelAction action) { if (game) game.SelectA(action); }
    }
}
