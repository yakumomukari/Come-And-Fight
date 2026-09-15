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
        DuelGame game;
        Button[] actionButtons;

        void Start() { game = FindObjectOfType<DuelGame>(); CacheButtons(); }
        void Update()
        {
            if (!game) { game = FindObjectOfType<DuelGame>(); if (!game) return; }
            if (!scoreText || !turnText || !statusText || !timerText || !modeText || !actionPanel) return;
            var s = game.State;
            scoreText.text = "玩家 A   " + s.aScore + "  :  " + s.bScore + "   玩家 B";
            turnText.text = s.suddenDeath ? "死斗 · 回合 " + s.turn : "回合 " + s.turn + " / 20";
            statusText.text = game.Status;
            timerText.text = TimerText();
            modeText.text = game.IsLocalTwoPlayer ? "本地双人：B 使用 ←前进 / →后退 / ,击剑 / .招架" : "玩家 VS AI";
            actionPanel.SetActive(true);
            timerText.gameObject.SetActive(!game.IsGameOver);
            RefreshButtons();
        }

        string TimerText()
        {
            if (game.Phase == DuelGame.TurnPhase.Reveal) return "行动公开";
            if (game.Phase == DuelGame.TurnPhase.Result) return "结算中";
            string a = game.ALocked ? "A 已选择：" + DuelGame.ActionName(game.AChoice) : "A 等待选择";
            string b = game.IsLocalTwoPlayer ? (game.BLocked ? "  ·  B 已锁定" : "  ·  B 等待选择") : "";
            return "剩余 " + game.RemainingTime.ToString("0.0") + " 秒  ·  " + a + b;
        }

        void CacheButtons() { actionButtons = actionPanel ? actionPanel.GetComponentsInChildren<Button>(true) : new Button[0]; }

        void RefreshButtons()
        {
            if (actionButtons == null || actionButtons.Length == 0) CacheButtons();
            bool canChoose = game.Phase == DuelGame.TurnPhase.Choosing && !game.ALocked;
            foreach (var button in actionButtons)
            {
                bool selected = game.ALocked && ActionForButton(button.name) == game.AChoice;
                button.interactable = canChoose;
                var image = button.targetGraphic as Image;
                if (image) image.color = selected ? SelectedColor : game.ALocked || game.IsResolving || game.IsGameOver ? LockedColor : NormalColor;
                var label = button.GetComponentInChildren<Text>();
                if (label) label.color = selected ? Color.white : new Color(1f, 1f, 1f, canChoose ? 1f : .62f);
            }
        }

        static DuelAction ActionForButton(string buttonName)
        {
            if (buttonName == "Advance") return DuelAction.Advance;
            if (buttonName == "Retreat") return DuelAction.Retreat;
            if (buttonName == "Thrust") return DuelAction.Thrust;
            if (buttonName == "Parry") return DuelAction.Parry;
            return DuelAction.None;
        }

        public void SelectRetreat() => Select(DuelAction.Retreat);
        public void SelectAdvance() => Select(DuelAction.Advance);
        public void SelectThrust() => Select(DuelAction.Thrust);
        public void SelectParry() => Select(DuelAction.Parry);
        void Select(DuelAction action) { if (!game) game = FindObjectOfType<DuelGame>(); if (game) game.SelectA(action); }
    }
}
