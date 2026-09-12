using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ComeAndFight
{
    public sealed class DuelGame : MonoBehaviour
    {
        const float DecisionSeconds = 2f;
        DuelState state;
        DuelAction aChoice, bChoice;
        float deadline;
        bool resolving, localTwoPlayer, gameOver;
        string status = "选择行动";
        Transform playerA, playerB;
        readonly List<SpriteRenderer> cells = new List<SpriteRenderer>();
        Texture2D white;

        public DuelState State => state;
        public string Status => status;
        public bool IsResolving => resolving;
        public bool IsGameOver => gameOver;
        public bool IsLocalTwoPlayer => localTwoPlayer;
        public bool ALocked => aChoice != DuelAction.None;
        public bool BLocked => bChoice != DuelAction.None;
        public float RemainingTime => resolving || gameOver ? 0 : Mathf.Max(0, deadline - Time.time);

        void Awake()
        {
            state = DuelState.NewMatch();
            ConfigureCamera();
            BuildWhitebox();
            BeginTurn();
        }

        void ConfigureCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true; cam.orthographicSize = 4.8f;
            cam.transform.position = new Vector3(0, .3f, -10);
            cam.backgroundColor = new Color(.035f, .045f, .065f);
        }

        void BuildWhitebox()
        {
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
            var sprite = Sprite.Create(white, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            for (int i = 1; i <= 7; i++)
            {
                var cell = MakeSprite("格子 " + i, sprite, new Vector3(CellX(i), -1.2f, 0), new Vector3(1.08f, .18f, 1), new Color(.7f, .74f, .8f));
                cells.Add(cell);
            }
            playerA = MakeFencer("玩家 A", sprite, new Color(.25f, .72f, 1f), true);
            playerB = MakeFencer("玩家 B", sprite, new Color(1f, .35f, .38f), false);
            SnapPlayers();
        }

        Transform MakeFencer(string name, Sprite sprite, Color color, bool facesRight)
        {
            var root = new GameObject(name).transform;
            MakeSprite("身体", sprite, new Vector3(0, .35f), new Vector3(.42f, 1.15f, 1), color).transform.SetParent(root, false);
            MakeSprite("头", sprite, new Vector3(0, 1.12f), new Vector3(.48f, .48f, 1), Color.white).transform.SetParent(root, false);
            float sign = facesRight ? 1 : -1;
            MakeSprite("剑", sprite, new Vector3(sign * .58f, .55f), new Vector3(.85f, .07f, 1), Color.white).transform.SetParent(root, false);
            return root;
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, Vector3 position, Vector3 scale, Color color)
        {
            var go = new GameObject(name); go.transform.position = position; go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color;
            return sr;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { localTwoPlayer = !localTwoPlayer; Restart(); }
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            if (gameOver || resolving) return;

            if (aChoice == DuelAction.None)
            {
                if (Input.GetKeyDown(KeyCode.D)) aChoice = DuelAction.Advance;
                else if (Input.GetKeyDown(KeyCode.A)) aChoice = DuelAction.Retreat;
                else if (Input.GetKeyDown(KeyCode.J)) aChoice = DuelAction.Thrust;
                else if (Input.GetKeyDown(KeyCode.K)) aChoice = DuelAction.Parry;
            }
            if (localTwoPlayer && bChoice == DuelAction.None)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) bChoice = DuelAction.Advance;
                else if (Input.GetKeyDown(KeyCode.RightArrow)) bChoice = DuelAction.Retreat;
                else if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Comma)) bChoice = DuelAction.Thrust;
                else if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Period)) bChoice = DuelAction.Parry;
            }
            if (!localTwoPlayer && bChoice == DuelAction.None) bChoice = ChooseAi();
            if ((aChoice != DuelAction.None && bChoice != DuelAction.None) || Time.time >= deadline)
            {
                if (aChoice == DuelAction.None) aChoice = DuelAction.Parry;
                if (bChoice == DuelAction.None) bChoice = DuelAction.Parry;
                StartCoroutine(ResolveTurn());
            }
        }

        IEnumerator ResolveTurn()
        {
            resolving = true;
            status = "公开：A " + ActionName(aChoice) + "  /  B " + ActionName(bChoice);
            yield return new WaitForSeconds(.45f);
            var result = TurnResolver.Resolve(state, aChoice, bChoice);
            state = result.state; status = result.message;
            RefreshBoard();
            yield return AnimatePlayers(.35f);
            gameOver = result.matchEnded;
            if (gameOver) status = (state.aScore > state.bScore ? "玩家 A" : "玩家 B") + " 获胜！按 R 再来一局";
            else { yield return new WaitForSeconds(.55f); BeginTurn(); }
            resolving = false;
        }

        IEnumerator AnimatePlayers(float duration)
        {
            Vector3 a0 = playerA.position, b0 = playerB.position;
            Vector3 at = new Vector3(CellX(state.aPosition), -.6f), bt = new Vector3(CellX(state.bPosition), -.6f);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duration);
                playerA.position = Vector3.Lerp(a0, at, k); playerB.position = Vector3.Lerp(b0, bt, k);
                yield return null;
            }
            SnapPlayers();
        }

        DuelAction ChooseAi()
        {
            int distance = state.bPosition - state.aPosition;
            float advance = state.bPosition >= 6 ? 3.2f : 2f;
            float retreat = state.bPosition >= 7 ? .05f : 1f;
            float thrust = distance <= 2 ? 3f : .4f;
            float parry = distance <= 1 ? 2.2f : .7f;
            if (state.suddenDeath) { advance += 4f; retreat *= .15f; }
            float roll = Random.value * (advance + retreat + thrust + parry);
            if ((roll -= advance) < 0) return DuelAction.Advance;
            if ((roll -= retreat) < 0) return DuelAction.Retreat;
            if ((roll -= thrust) < 0) return DuelAction.Thrust;
            return DuelAction.Parry;
        }

        void BeginTurn() { aChoice = bChoice = DuelAction.None; deadline = Time.time + DecisionSeconds; status = "选择行动"; RefreshBoard(); }
        void Restart() { StopAllCoroutines(); state = DuelState.NewMatch(); resolving = gameOver = false; SnapPlayers(); BeginTurn(); }
        void SnapPlayers() { playerA.position = new Vector3(CellX(state.aPosition), -.6f); playerB.position = new Vector3(CellX(state.bPosition), -.6f); }
        void RefreshBoard() { for (int i = 0; i < cells.Count; i++) cells[i].color = state.suddenDeath && TurnResolver.IsBurning(i + 1, state.fireDepth) ? new Color(1f, .22f, .05f) : new Color(.7f, .74f, .8f); }
        static float CellX(int cell) => (cell - 4) * 1.18f;

        public void SelectA(DuelAction action) { if (!resolving && !gameOver && aChoice == DuelAction.None) aChoice = action; }
        static string ActionName(DuelAction a) => a == DuelAction.Advance ? "前进" : a == DuelAction.Retreat ? "后退" : a == DuelAction.Thrust ? "击剑" : "招架";
    }
}
