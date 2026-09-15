using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ComeAndFight
{
    public sealed class DuelGame : MonoBehaviour
    {
        public enum TurnPhase { Choosing, Reveal, Result, GameOver }

        const float DecisionSeconds = 2f;
        const float RevealSeconds = .5f;
        const float ResultSeconds = 1.2f;
        DuelState state;
        DuelAction aChoice, bChoice;
        float deadline;
        int latestChoiceFrame = -1;
        bool localTwoPlayer;
        TurnPhase phase;
        string status = "选择行动";
        public Transform playerA, playerB, swordA, swordB;
        public Image bodyA, bodyB;
        public Image[] cells;
        Color bodyAColor, bodyBColor;

        public DuelState State => state;
        public string Status => status;
        public TurnPhase Phase => phase;
        public bool IsResolving => phase == TurnPhase.Reveal || phase == TurnPhase.Result;
        public bool IsGameOver => phase == TurnPhase.GameOver;
        public bool IsLocalTwoPlayer => localTwoPlayer;
        public bool ALocked => aChoice != DuelAction.None;
        public bool BLocked => bChoice != DuelAction.None;
        public DuelAction AChoice => aChoice;
        public bool AThrustRecovering => state.aThrustRecovering;
        public bool BThrustRecovering => state.bThrustRecovering;
        public float RemainingTime => phase == TurnPhase.Choosing ? Mathf.Max(0, deadline - Time.time) : 0;

        void Awake()
        {
            state = DuelState.NewMatch();
            ConfigureCamera();
            bodyAColor = bodyA.color; bodyBColor = bodyB.color;
            SnapPlayers();
            BeginTurn();
        }

        void ConfigureCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                Debug.LogError("SampleScene must contain a serialized Main Camera.");
                enabled = false;
                return;
            }
            cam.rect = new Rect(0, 0, 1, 1);
            cam.orthographic = true; cam.orthographicSize = 4.8f;
            cam.transform.position = new Vector3(0, .3f, -10);
            cam.backgroundColor = new Color(.035f, .045f, .065f);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) ToggleMode();
            if (Input.GetKeyDown(KeyCode.R)) RestartMatch();
            if (phase != TurnPhase.Choosing) return;

            if (aChoice == DuelAction.None)
            {
                if (Input.GetKeyDown(KeyCode.D)) SelectA(DuelAction.Advance);
                else if (Input.GetKeyDown(KeyCode.A)) SelectA(DuelAction.Retreat);
                else if (Input.GetKeyDown(KeyCode.J)) SelectA(DuelAction.Thrust);
                else if (Input.GetKeyDown(KeyCode.K)) SelectA(DuelAction.Parry);
            }
            if (localTwoPlayer && bChoice == DuelAction.None)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectB(DuelAction.Advance);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) SelectB(DuelAction.Retreat);
                else if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Comma)) SelectB(DuelAction.Thrust);
                else if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Period)) SelectB(DuelAction.Parry);
            }
            bool timedOut = Time.time >= deadline;
            bool bothReady = aChoice != DuelAction.None && (localTwoPlayer ? bChoice != DuelAction.None : true);
            if ((timedOut || bothReady) && Time.frameCount > latestChoiceFrame)
            {
                if (aChoice == DuelAction.None) aChoice = DuelAction.Parry;
                if (bChoice == DuelAction.None) bChoice = localTwoPlayer ? DuelAction.Parry : ChooseAi();
                StartCoroutine(ResolveTurn());
            }
        }

        IEnumerator ResolveTurn()
        {
            phase = TurnPhase.Reveal;
            status = "A：" + ActionName(aChoice) + "  |  B：" + ActionName(bChoice);
            yield return new WaitForSeconds(RevealSeconds);
            var before = state;
            var result = TurnResolver.Resolve(state, aChoice, bChoice);
            state = result.state;
            phase = TurnPhase.Result;
            status = ResultMessage(result, before);
            RefreshBoard();
            yield return AnimateResolution(before, result);
            yield return new WaitForSeconds(ResultSeconds);
            if (result.matchEnded)
            {
                phase = TurnPhase.GameOver;
                status = (state.aScore > state.bScore ? "玩家 A" : "玩家 B") + " 获胜！按 R 再来一局";
            }
            else BeginTurn();
        }

        IEnumerator AnimateResolution(DuelState before, TurnResult result)
        {
            Vector3 a0 = playerA.localPosition, b0 = playerB.localPosition;
            Vector3 at = PresentationTarget(true, before, result, aChoice);
            Vector3 bt = PresentationTarget(false, before, result, bChoice);
            Vector3 swordAScale = swordA.localScale, swordBScale = swordB.localScale;
            Quaternion swordARotation = swordA.localRotation, swordBRotation = swordB.localRotation;
            const float duration = .6f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / duration);
                float pulse = Mathf.Sin(p * Mathf.PI);
                playerA.localPosition = Vector3.Lerp(a0, at, Mathf.SmoothStep(0, 1, p));
                playerB.localPosition = Vector3.Lerp(b0, bt, Mathf.SmoothStep(0, 1, p));
                ApplyActionPose(aChoice, swordA, swordAScale, swordARotation, 1, pulse);
                ApplyActionPose(bChoice, swordB, swordBScale, swordBRotation, -1, pulse);
                bodyA.color = FeedbackColor(bodyAColor, result.aFell || result.aBurned || (result.thrustHit && result.bPoints > 0), result.parry && aChoice == DuelAction.Parry, pulse, result.aBurned);
                bodyB.color = FeedbackColor(bodyBColor, result.bFell || result.bBurned || (result.thrustHit && result.aPoints > 0), result.parry && bChoice == DuelAction.Parry, pulse, result.bBurned);
                if (result.collision)
                {
                    float shake = Mathf.Sin(p * Mathf.PI * 8) * .08f * (1 - p);
                    playerA.localPosition += Vector3.left * shake * 60f;
                    playerB.localPosition += Vector3.right * shake * 60f;
                }
                yield return null;
            }
            swordA.localScale = swordAScale; swordB.localScale = swordBScale;
            swordA.localRotation = swordARotation; swordB.localRotation = swordBRotation;
            bodyA.color = bodyAColor; bodyB.color = bodyBColor;
            SnapPlayers();
        }

        static void ApplyActionPose(DuelAction action, Transform sword, Vector3 scale, Quaternion rotation, float facing, float pulse)
        {
            sword.localScale = action == DuelAction.Thrust ? new Vector3(scale.x * (1 + pulse * 1.25f), scale.y, scale.z) : scale;
            sword.localRotation = action == DuelAction.Parry ? Quaternion.Euler(0, 0, facing * 65f * pulse) : rotation;
        }

        static Color FeedbackColor(Color normal, bool hurt, bool parrying, float pulse, bool burned)
        {
            if (burned) return Color.Lerp(normal, new Color(1f, .22f, .03f), pulse);
            if (hurt) return Color.Lerp(normal, Color.white, pulse);
            if (parrying) return Color.Lerp(normal, new Color(1f, .9f, .2f), pulse);
            return normal;
        }

        Vector3 PresentationTarget(bool isA, DuelState before, TurnResult result, DuelAction action)
        {
            bool fell = isA ? result.aFell : result.bFell;
            bool burned = isA ? result.aBurned : result.bBurned;
            if (fell) return new Vector3(CellX(isA ? 0 : 8), -150f);
            if (burned) return new Vector3(CellX(isA ? before.aPosition : before.bPosition), -70f);
            if (!result.boutEnded) return new Vector3(CellX(isA ? result.state.aPosition : result.state.bPosition), 0);
            int start = isA ? before.aPosition : before.bPosition;
            int delta = action == DuelAction.Advance ? (isA ? 1 : -1) : action == DuelAction.Retreat ? (isA ? -1 : 1) : 0;
            return new Vector3(CellX(Mathf.Clamp(start + delta, 1, 7)), 0);
        }

        string ResultMessage(TurnResult result, DuelState before)
        {
            string message = result.message;
            bool pushed = result.parry && (aChoice == DuelAction.Advance || bChoice == DuelAction.Advance);
            bool blocked = result.parry && (aChoice == DuelAction.Thrust || bChoice == DuelAction.Thrust);
            if (pushed && !result.aFell && !result.bFell) message = "前进推动招架者";
            else if (blocked && !result.aFell && !result.bFell) message = "招架成功";
            if (result.aBurned && result.bBurned) message = "双方被火焰吞噬，不计分";
            else if (result.aBurned || result.bBurned) message = "火焰吞噬！";
            else if (result.aWhiff && result.bWhiff) message = "双方空刺 · 下回合均不能击剑";
            else if (result.aWhiff) message = "A 空刺 · 下回合不能击剑";
            else if (result.bWhiff) message = "B 空刺 · 下回合不能击剑";
            if (result.aPoints > 0 || result.bPoints > 0)
            {
                string points = result.aPoints > 0 ? "A +" + result.aPoints : "";
                if (result.bPoints > 0) points += (points.Length > 0 ? " / " : "") + "B +" + result.bPoints;
                message += "  ·  得分 " + points;
            }
            if (!before.suddenDeath && result.state.suddenDeath) message = "死斗开始！边缘已燃烧";
            return message;
        }

        DuelAction ChooseAi()
        {
            int distance = state.bPosition - state.aPosition;
            float advance = state.bPosition >= 6 ? 3.2f : 2f;
            float retreat = state.bPosition >= 7 ? .05f : 1f;
            float thrust = state.bThrustRecovering ? 0 : distance <= 2 ? 3f : .4f;
            float parry = distance <= 1 ? 2.2f : .7f;
            if (state.suddenDeath) { advance += 4f; retreat *= .15f; }
            float roll = Random.value * (advance + retreat + thrust + parry);
            if ((roll -= advance) < 0) return DuelAction.Advance;
            if ((roll -= retreat) < 0) return DuelAction.Retreat;
            if ((roll -= thrust) < 0) return DuelAction.Thrust;
            return DuelAction.Parry;
        }

        void BeginTurn()
        {
            aChoice = bChoice = DuelAction.None;
            latestChoiceFrame = -1;
            deadline = Time.time + DecisionSeconds;
            phase = TurnPhase.Choosing;
            if (state.aThrustRecovering) status = "空刺恢复：本回合不能击剑";
            else if (localTwoPlayer && state.bThrustRecovering) status = "B 空刺恢复：本回合不能击剑";
            else status = "选择行动";
            RefreshBoard();
        }

        public void RestartMatch()
        {
            StopAllCoroutines();
            state = DuelState.NewMatch();
            bodyA.color = bodyAColor; bodyB.color = bodyBColor;
            swordA.localRotation = swordB.localRotation = Quaternion.identity;
            SnapPlayers();
            BeginTurn();
        }

        public void ToggleMode()
        {
            localTwoPlayer = !localTwoPlayer;
            RestartMatch();
        }
        void SnapPlayers() { playerA.localPosition = new Vector3(CellX(state.aPosition), 0); playerB.localPosition = new Vector3(CellX(state.bPosition), 0); }
        void RefreshBoard() { for (int i = 0; i < cells.Length; i++) cells[i].color = state.suddenDeath && TurnResolver.IsBurning(i + 1, state.fireDepth) ? new Color(1f, .22f, .05f) : new Color(.7f, .74f, .8f); }
        static float CellX(int cell) => (cell - 4) * 82f;

        public void SelectA(DuelAction action)
        {
            if (phase != TurnPhase.Choosing || aChoice != DuelAction.None) return;
            if (!TurnResolver.CanChoose(state, true, action))
            {
                status = "空刺恢复：本回合不能击剑";
                return;
            }
            aChoice = action;
            latestChoiceFrame = Time.frameCount;
            status = "已选择：" + ActionName(action);
        }

        void SelectB(DuelAction action)
        {
            if (phase != TurnPhase.Choosing || bChoice != DuelAction.None) return;
            if (!TurnResolver.CanChoose(state, false, action)) return;
            bChoice = action;
            latestChoiceFrame = Time.frameCount;
        }

        public static string ActionName(DuelAction action)
        {
            if (action == DuelAction.Advance) return "前进";
            if (action == DuelAction.Retreat) return "后退";
            if (action == DuelAction.Thrust) return "击剑";
            if (action == DuelAction.Parry) return "招架";
            return "未选择";
        }
    }
}
