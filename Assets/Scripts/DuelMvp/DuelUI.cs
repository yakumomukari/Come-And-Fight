using UnityEngine;
using UnityEngine.UI;

namespace ComeAndFight
{
    public sealed class DuelUI : MonoBehaviour
    {
        public Text titleText, scoreText, turnText, statusText, timerText, modeText, helpText;
        public GameObject actionPanel;
        DuelGame game;

        void Start() { game = FindObjectOfType<DuelGame>(); }
        void Update()
        {
            if (!game) { game = FindObjectOfType<DuelGame>(); if (!game) return; }
            if (!scoreText || !turnText || !statusText || !timerText || !modeText || !actionPanel) return;
            var s = game.State;
            scoreText.text = "玩家 A   " + s.aScore + "  :  " + s.bScore + "   玩家 B";
            turnText.text = s.suddenDeath ? "死斗 · 回合 " + s.turn : "回合 " + s.turn + " / 20";
            statusText.text = game.Status;
            timerText.text = "剩余 " + game.RemainingTime.ToString("0.0") + " 秒  ·  " + (game.ALocked ? "A 已锁定" : "A 等待选择") + (game.IsLocalTwoPlayer ? (game.BLocked ? "  ·  B 已锁定" : "  ·  B 等待选择") : "");
            modeText.text = game.IsLocalTwoPlayer ? "本地双人：B 使用 ←前进 / →后退 / ,击剑 / .招架" : "玩家 VS AI";
            actionPanel.SetActive(!game.IsResolving && !game.IsGameOver);
            timerText.gameObject.SetActive(!game.IsResolving && !game.IsGameOver);
        }

        public void SelectRetreat() => Select(DuelAction.Retreat);
        public void SelectAdvance() => Select(DuelAction.Advance);
        public void SelectThrust() => Select(DuelAction.Thrust);
        public void SelectParry() => Select(DuelAction.Parry);
        void Select(DuelAction action) { if (!game) game = FindObjectOfType<DuelGame>(); if (game) game.SelectA(action); }
    }
}
