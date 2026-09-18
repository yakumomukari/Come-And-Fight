using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ComeAndFight.Networking
{
    public enum PlayerSide : byte { None, A, B }
    public enum NetworkTurnPhase : byte { WaitingForPlayers, Choosing, Resolving, GameOver, Paused }

    public sealed class DuelNetworkController : MonoBehaviour
    {
        const string SubmitMessage = "caf.submit.action.v1";
        const string BeginTurnMessage = "caf.begin.turn.v1";
        const string ResolveMessage = "caf.resolve.turn.v1";
        const float DecisionSeconds = 2f;
        const float ResultSeconds = 1.7f;

        readonly Dictionary<ulong, PlayerSide> playerSides = new Dictionary<ulong, PlayerSide>();
        NetworkManager manager;
        DuelGame duelGame;
        DuelState authoritativeState;
        DuelAction playerAChoice, playerBChoice, automaticAction;
        float deadline, nextTurnAt;
        bool handlersRegistered, localChoiceSubmitted;
        float automaticSubmitAt, automaticDelay;
        int automaticSkipTurn = -1;
        bool automaticDuplicate, automaticStale;
        bool automaticDisconnectAfterSubmit;
        float automaticDisconnectAt = -1;

        public PlayerSide LocalPlayerSide { get; private set; }
        public NetworkTurnPhase Phase { get; private set; } = NetworkTurnPhase.WaitingForPlayers;
        public int TurnId { get; private set; }
        public int ConnectedPlayerCount => playerSides.Count;
        public DuelState AuthoritativeState => authoritativeState;
        public float RemainingTime => Phase == NetworkTurnPhase.Choosing ? Mathf.Max(0, deadline - Time.realtimeSinceStartup) : 0;
        public bool LocalChoiceSubmitted => localChoiceSubmitted;
        public event Action<int, DuelState> TurnStarted;
        public event Action<int, DuelAction, DuelAction, TurnResult> TurnResolved;

        public bool TryGetPlayerSide(ulong clientId, out PlayerSide side) => playerSides.TryGetValue(clientId, out side);

        public void Initialize(NetworkManager networkManager)
        {
            if (manager == networkManager) return;
            Unsubscribe();
            manager = networkManager;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.OnServerStarted += OnServerStarted;
            ParseAutomationArguments(Environment.GetCommandLineArgs());
        }

        void Update()
        {
            if (automaticDisconnectAt > 0 && Time.realtimeSinceStartup >= automaticDisconnectAt)
            {
                automaticDisconnectAt = -1;
                manager.Shutdown();
                return;
            }
            if (Phase == NetworkTurnPhase.Choosing && !localChoiceSubmitted)
            {
                if (automaticAction != DuelAction.None && TurnId != automaticSkipTurn && Time.realtimeSinceStartup >= automaticSubmitAt)
                {
                    SubmitLocalAction(automaticAction);
                    if (automaticDisconnectAfterSubmit) automaticDisconnectAt = Time.realtimeSinceStartup + .15f;
                    if (automaticDuplicate) SendRawSubmission(TurnId, automaticAction);
                    if (automaticStale && TurnId > 1) SendRawSubmission(TurnId - 1, automaticAction);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha1)) SubmitLocalAction(DuelAction.Advance);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SubmitLocalAction(DuelAction.Retreat);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SubmitLocalAction(DuelAction.Thrust);
                else if (Input.GetKeyDown(KeyCode.Alpha4)) SubmitLocalAction(DuelAction.Parry);
            }
            if (!manager || !manager.IsServer) return;
            if (Phase == NetworkTurnPhase.Choosing && Time.realtimeSinceStartup >= deadline) ResolveAuthoritativeTurn();
            else if (Phase == NetworkTurnPhase.Resolving && Time.realtimeSinceStartup >= nextTurnAt) BeginAuthoritativeTurn();
        }

        public bool SubmitLocalAction(DuelAction action)
        {
            if (!manager || !manager.IsConnectedClient || Phase != NetworkTurnPhase.Choosing || localChoiceSubmitted) return false;
            if (!IsActionValueValid(action) || !TurnResolver.CanChoose(authoritativeState, LocalPlayerSide == PlayerSide.A, action)) return false;
            localChoiceSubmitted = true;
            if (manager.IsServer) AcceptSubmission(manager.LocalClientId, TurnId, action);
            else SendRawSubmission(TurnId, action);
            return true;
        }

        void SendRawSubmission(int submittedTurnId, DuelAction action)
        {
            if (manager.IsServer) AcceptSubmission(manager.LocalClientId, submittedTurnId, action);
            else
            {
                using (var writer = new FastBufferWriter(8, Allocator.Temp))
                {
                    writer.WriteValueSafe(submittedTurnId); writer.WriteValueSafe((byte)action);
                    manager.CustomMessagingManager.SendNamedMessage(SubmitMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                }
            }
            Debug.Log($"[Network] Action submitted: turn {submittedTurnId}, Player {LocalPlayerSide}, {action}.");
        }

        void OnServerStarted()
        {
            RegisterHandlers();
            authoritativeState = DuelState.NewMatch();
            Debug.Log("[Network] Server started and ready for a client.");
        }

        void OnClientConnected(ulong clientId)
        {
            if (manager.IsServer && !playerSides.ContainsKey(clientId))
            {
                PlayerSide side = playerSides.Count == 0 ? PlayerSide.A : playerSides.Count == 1 ? PlayerSide.B : PlayerSide.None;
                if (side == PlayerSide.None)
                {
                    Debug.LogWarning($"[Network] Rejecting extra client {clientId}; this match supports two players.");
                    manager.DisconnectClient(clientId); return;
                }
                playerSides.Add(clientId, side);
                Debug.Log($"[Network] Client {clientId} assigned to Player {side}.");
            }
            if (clientId == manager.LocalClientId)
            {
                RegisterHandlers();
                LocalPlayerSide = manager.IsHost ? PlayerSide.A : PlayerSide.B;
                AttachDuelGame();
                Debug.Log($"[Network] Local player identified as Player {LocalPlayerSide}.");
            }
            if (manager.IsServer && playerSides.Count == 2 && Phase == NetworkTurnPhase.WaitingForPlayers) BeginAuthoritativeTurn();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (manager && manager.IsServer) { playerSides.Remove(clientId); if (Phase != NetworkTurnPhase.GameOver) Phase = NetworkTurnPhase.Paused; }
            if (manager && clientId == manager.LocalClientId) { LocalPlayerSide = PlayerSide.None; Phase = NetworkTurnPhase.Paused; }
            string reason = manager && !string.IsNullOrEmpty(manager.DisconnectReason) ? " Reason: " + manager.DisconnectReason : string.Empty;
            Debug.Log($"[Network] Client {clientId} disconnected; match paused.{reason}");
            if (duelGame && Phase == NetworkTurnPhase.Paused) duelGame.PauseOnlineMatch("连接中断，比赛已暂停");
        }

        void RegisterHandlers()
        {
            if (handlersRegistered || manager.CustomMessagingManager == null) return;
            if (manager.IsServer) manager.CustomMessagingManager.RegisterNamedMessageHandler(SubmitMessage, OnSubmitMessage);
            else
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(BeginTurnMessage, OnBeginTurnMessage);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(ResolveMessage, OnResolveMessage);
            }
            handlersRegistered = true;
        }

        void OnSubmitMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int submittedTurnId); reader.ReadValueSafe(out byte actionValue);
            AcceptSubmission(senderClientId, submittedTurnId, (DuelAction)actionValue);
        }

        void AcceptSubmission(ulong senderClientId, int submittedTurnId, DuelAction action)
        {
            if (!ValidateSubmission(senderClientId, submittedTurnId, action, out PlayerSide side)) return;
            if (side == PlayerSide.A) playerAChoice = action; else playerBChoice = action;
            Debug.Log($"[Network] Action accepted: turn {TurnId}, Player {side}, {action}.");
            if (playerAChoice != DuelAction.None && playerBChoice != DuelAction.None) ResolveAuthoritativeTurn();
        }

        bool ValidateSubmission(ulong senderClientId, int submittedTurnId, DuelAction action, out PlayerSide side)
        {
            if (!playerSides.TryGetValue(senderClientId, out side)) return Reject(senderClientId, "sender is not a match player");
            if (Phase != NetworkTurnPhase.Choosing) return Reject(senderClientId, "match is not choosing");
            if (submittedTurnId != TurnId) return Reject(senderClientId, $"turn {submittedTurnId} does not match {TurnId}");
            if (!IsActionValueValid(action)) return Reject(senderClientId, "action is invalid");
            if ((side == PlayerSide.A && playerAChoice != DuelAction.None) || (side == PlayerSide.B && playerBChoice != DuelAction.None)) return Reject(senderClientId, "choice was already submitted");
            if (!TurnResolver.CanChoose(authoritativeState, side == PlayerSide.A, action)) return Reject(senderClientId, "Thrust is blocked by recovery");
            return true;
        }

        static bool IsActionValueValid(DuelAction action) => action >= DuelAction.Advance && action <= DuelAction.Parry;
        static bool Reject(ulong clientId, string reason) { Debug.LogWarning($"[Network] Rejected action from client {clientId}: {reason}."); return false; }

        void BeginAuthoritativeTurn()
        {
            if (!manager.IsServer || playerSides.Count != 2) { Phase = NetworkTurnPhase.Paused; return; }
            TurnId++; playerAChoice = playerBChoice = DuelAction.None; localChoiceSubmitted = false;
            deadline = Time.realtimeSinceStartup + DecisionSeconds; Phase = NetworkTurnPhase.Choosing;
            automaticSubmitAt = Time.realtimeSinceStartup + automaticDelay;
            foreach (var pair in playerSides)
            {
                if (pair.Key == manager.LocalClientId) continue;
                var writer = new FastBufferWriter(NetworkMatchSerialization.MaxBeginTurnBytes, Allocator.Temp);
                NetworkMatchSerialization.WriteBeginTurn(ref writer, TurnId, authoritativeState, DecisionSeconds);
                manager.CustomMessagingManager.SendNamedMessage(BeginTurnMessage, pair.Key, writer, NetworkDelivery.ReliableSequenced);
                writer.Dispose();
            }
            Debug.Log($"[Network] Turn started: {TurnId}."); TurnStarted?.Invoke(TurnId, authoritativeState);
            if (duelGame) duelGame.BeginOnlineTurn(authoritativeState, DecisionSeconds);
        }

        void ResolveAuthoritativeTurn()
        {
            if (!manager.IsServer || Phase != NetworkTurnPhase.Choosing) return;
            if (playerAChoice == DuelAction.None) playerAChoice = DuelAction.Parry;
            if (playerBChoice == DuelAction.None) playerBChoice = DuelAction.Parry;
            TurnResult result = TurnResolver.Resolve(authoritativeState, playerAChoice, playerBChoice);
            authoritativeState = result.state;
            Phase = result.matchEnded ? NetworkTurnPhase.GameOver : NetworkTurnPhase.Resolving;
            nextTurnAt = Time.realtimeSinceStartup + ResultSeconds;
            foreach (var pair in playerSides)
            {
                if (pair.Key == manager.LocalClientId) continue;
                var writer = new FastBufferWriter(NetworkMatchSerialization.MaxResolveBytes, Allocator.Temp);
                NetworkMatchSerialization.WriteResolve(ref writer, TurnId, playerAChoice, playerBChoice, result);
                manager.CustomMessagingManager.SendNamedMessage(ResolveMessage, pair.Key, writer, NetworkDelivery.ReliableSequenced);
                writer.Dispose();
            }
            Debug.Log($"[Network] Turn resolved: {TurnId}, A={playerAChoice}, B={playerBChoice}, state={NetworkMatchSerialization.StateSummary(authoritativeState)}.");
            TurnResolved?.Invoke(TurnId, playerAChoice, playerBChoice, result);
            if (duelGame) duelGame.ApplyOnlineResolution(playerAChoice, playerBChoice, result);
        }

        void OnBeginTurnMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            NetworkMatchSerialization.ReadBeginTurn(ref reader, out int incomingTurnId, out DuelState state, out float seconds);
            if (incomingTurnId <= TurnId) return;
            TurnId = incomingTurnId; authoritativeState = state; deadline = Time.realtimeSinceStartup + seconds;
            automaticSubmitAt = Time.realtimeSinceStartup + automaticDelay;
            localChoiceSubmitted = false; Phase = NetworkTurnPhase.Choosing;
            Debug.Log($"[Network] Turn started: {TurnId}."); TurnStarted?.Invoke(TurnId, authoritativeState);
            if (duelGame) duelGame.BeginOnlineTurn(authoritativeState, seconds);
        }

        void OnResolveMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (senderClientId != NetworkManager.ServerClientId) return;
            NetworkMatchSerialization.ReadResolve(ref reader, out int incomingTurnId, out DuelAction actionA, out DuelAction actionB, out TurnResult result);
            if (incomingTurnId != TurnId) return;
            authoritativeState = result.state; Phase = result.matchEnded ? NetworkTurnPhase.GameOver : NetworkTurnPhase.Resolving;
            Debug.Log($"[Network] Turn resolved: {TurnId}, A={actionA}, B={actionB}, state={NetworkMatchSerialization.StateSummary(authoritativeState)}.");
            TurnResolved?.Invoke(TurnId, actionA, actionB, result);
            if (duelGame) duelGame.ApplyOnlineResolution(actionA, actionB, result);
        }

        void AttachDuelGame()
        {
            if (!duelGame) duelGame = FindObjectOfType<DuelGame>();
            if (!duelGame) { Debug.LogWarning("[Network] DuelGame presentation was not found."); return; }
            duelGame.OnlineActionSelected -= OnPresentationActionSelected;
            duelGame.OnlineActionSelected += OnPresentationActionSelected;
            duelGame.EnterOnlineMode(LocalPlayerSide == PlayerSide.A);
        }

        void OnPresentationActionSelected(DuelAction action) => SubmitLocalAction(action);

        void ParseAutomationArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-caf-auto-action" && i + 1 < args.Length && Enum.TryParse(args[++i], true, out DuelAction action) && IsActionValueValid(action)) automaticAction = action;
                else if (args[i] == "-caf-auto-delay" && i + 1 < args.Length && float.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float delay)) automaticDelay = Mathf.Max(0, delay);
                else if (args[i] == "-caf-auto-skip-turn" && i + 1 < args.Length && int.TryParse(args[++i], out int turn)) automaticSkipTurn = turn;
                else if (args[i] == "-caf-auto-duplicate") automaticDuplicate = true;
                else if (args[i] == "-caf-auto-stale") automaticStale = true;
                else if (args[i] == "-caf-auto-disconnect-after-submit") automaticDisconnectAfterSubmit = true;
            }
        }

        void OnDestroy() => Unsubscribe();
        void Unsubscribe()
        {
            if (!manager) return;
            if (handlersRegistered && manager.CustomMessagingManager != null)
            {
                if (manager.IsServer) manager.CustomMessagingManager.UnregisterNamedMessageHandler(SubmitMessage);
                else { manager.CustomMessagingManager.UnregisterNamedMessageHandler(BeginTurnMessage); manager.CustomMessagingManager.UnregisterNamedMessageHandler(ResolveMessage); }
            }
            handlersRegistered = false;
            if (duelGame) duelGame.OnlineActionSelected -= OnPresentationActionSelected;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            manager.OnServerStarted -= OnServerStarted;
        }
    }
}
