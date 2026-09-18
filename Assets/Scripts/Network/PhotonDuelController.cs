using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;

namespace ComeAndFight.Networking
{
    public sealed class PhotonDuelController : MonoBehaviour, INetworkRunnerCallbacks
    {
        const byte BeginTurnPacket = 1;
        const byte SubmitPacket = 2;
        const byte ResolvePacket = 3;
        const float DecisionSeconds = 2f;
        const float ResultSeconds = 1.7f;

        NetworkRunner runner;
        DuelGame duelGame;
        DuelState state;
        DuelAction playerAChoice;
        DuelAction playerBChoice;
        PlayerRef remotePlayer;
        float deadline;
        float nextTurnAt;
        int reliableSequence;
        bool localChoiceSubmitted;
        bool intentionalShutdown;

        public NetworkTurnPhase Phase { get; private set; } = NetworkTurnPhase.WaitingForPlayers;
        public PlayerSide LocalPlayerSide { get; private set; }
        public int TurnId { get; private set; }
        public bool IsRunning => runner && runner.IsRunning;
        public bool IsHost => runner && runner.IsServer;

        public async Task<bool> StartHostAsync(string roomCode, string region)
        {
            return await StartRunnerAsync(GameMode.Host, roomCode, region);
        }

        public async Task<bool> StartClientAsync(string roomCode, string region)
        {
            return await StartRunnerAsync(GameMode.Client, roomCode, region);
        }

        async Task<bool> StartRunnerAsync(GameMode mode, string roomCode, string region)
        {
            if (IsRunning) return false;
            intentionalShutdown = false;
            Phase = NetworkTurnPhase.WaitingForPlayers;
            var runnerObject = new GameObject("Photon Fusion Runner");
            runnerObject.transform.SetParent(transform, false);
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);

            FusionAppSettings settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.FixedRegion = region.ToLowerInvariant();
            StartGameResult result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomCode,
                PlayerCount = 2,
                DisableNATPunchthrough = true,
                CustomPhotonAppSettings = settings
            });
            if (result.Ok) return true;
            Debug.LogError($"[Photon] Start failed: {result.ShutdownReason} {result.ErrorMessage}");
            if (runner) Destroy(runner.gameObject);
            runner = null;
            return false;
        }

        public void Shutdown()
        {
            intentionalShutdown = true;
            if (runner && runner.IsRunning) _ = runner.Shutdown();
            Phase = NetworkTurnPhase.WaitingForPlayers;
            LocalPlayerSide = PlayerSide.None;
        }

        void Update()
        {
            if (!IsRunning || !IsHost) return;
            if (Phase == NetworkTurnPhase.Choosing && Time.realtimeSinceStartup >= deadline) ResolveTurn();
            else if (Phase == NetworkTurnPhase.Resolving && Time.realtimeSinceStartup >= nextTurnAt) BeginTurn();
        }

        public bool SubmitLocalAction(DuelAction action)
        {
            if (!IsRunning || Phase != NetworkTurnPhase.Choosing || localChoiceSubmitted) return false;
            if (action < DuelAction.Advance || action > DuelAction.Parry) return false;
            if (!TurnResolver.CanChoose(state, LocalPlayerSide == PlayerSide.A, action)) return false;
            localChoiceSubmitted = true;
            if (IsHost) AcceptSubmission(LocalPlayerSide, TurnId, action);
            else SendToServer(WriteSubmit(TurnId, action));
            return true;
        }

        void BeginTurn()
        {
            if (!IsHost || !remotePlayer.IsRealPlayer) return;
            TurnId++;
            playerAChoice = playerBChoice = DuelAction.None;
            localChoiceSubmitted = false;
            deadline = Time.realtimeSinceStartup + DecisionSeconds;
            Phase = NetworkTurnPhase.Choosing;
            SendToPlayer(remotePlayer, WriteBeginTurn(TurnId, state, DecisionSeconds));
            PresentTurn(DecisionSeconds);
        }

        void AcceptSubmission(PlayerSide side, int turnId, DuelAction action)
        {
            if (!IsHost || Phase != NetworkTurnPhase.Choosing || turnId != TurnId) return;
            if (action < DuelAction.Advance || action > DuelAction.Parry) return;
            if (!TurnResolver.CanChoose(state, side == PlayerSide.A, action)) return;
            if (side == PlayerSide.A)
            {
                if (playerAChoice != DuelAction.None) return;
                playerAChoice = action;
            }
            else if (side == PlayerSide.B)
            {
                if (playerBChoice != DuelAction.None) return;
                playerBChoice = action;
            }
            if (playerAChoice != DuelAction.None && playerBChoice != DuelAction.None) ResolveTurn();
        }

        void ResolveTurn()
        {
            if (!IsHost || Phase != NetworkTurnPhase.Choosing) return;
            if (playerAChoice == DuelAction.None) playerAChoice = DuelAction.Parry;
            if (playerBChoice == DuelAction.None) playerBChoice = DuelAction.Parry;
            TurnResult result = TurnResolver.Resolve(state, playerAChoice, playerBChoice);
            state = result.state;
            Phase = result.matchEnded ? NetworkTurnPhase.GameOver : NetworkTurnPhase.Resolving;
            nextTurnAt = Time.realtimeSinceStartup + ResultSeconds;
            SendToPlayer(remotePlayer, WriteResolve(TurnId, playerAChoice, playerBChoice, result));
            PresentResolution(playerAChoice, playerBChoice, result);
        }

        void PresentTurn(float seconds)
        {
            AttachDuelGame();
            if (duelGame) duelGame.BeginOnlineTurn(state, seconds);
        }

        void PresentResolution(DuelAction actionA, DuelAction actionB, TurnResult result)
        {
            if (duelGame) duelGame.ApplyOnlineResolution(actionA, actionB, result);
        }

        void AttachDuelGame()
        {
            if (!duelGame) duelGame = FindObjectOfType<DuelGame>();
            if (!duelGame) return;
            duelGame.OnlineActionSelected -= SubmitFromPresentation;
            duelGame.OnlineActionSelected += SubmitFromPresentation;
            duelGame.EnterOnlineMode(LocalPlayerSide == PlayerSide.A);
        }

        void SubmitFromPresentation(DuelAction action) => SubmitLocalAction(action);

        void SendToServer(byte[] data)
        {
            runner.SendReliableDataToServer(NextKey(), data);
        }

        void SendToPlayer(PlayerRef player, byte[] data)
        {
            runner.SendReliableDataToPlayer(player, NextKey(), data);
        }

        ReliableKey NextKey() => ReliableKey.FromInts(0x434146, ++reliableSequence, TurnId, 0);

        public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
        {
            if (player == networkRunner.LocalPlayer)
            {
                LocalPlayerSide = networkRunner.IsServer ? PlayerSide.A : PlayerSide.B;
                AttachDuelGame();
            }
            if (networkRunner.IsServer && player != networkRunner.LocalPlayer)
            {
                remotePlayer = player;
                state = DuelState.NewMatch();
                if (networkRunner.SessionInfo.PlayerCount == 2) BeginTurn();
            }
        }

        public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
        {
            if (Phase != NetworkTurnPhase.GameOver) Phase = NetworkTurnPhase.Paused;
            if (duelGame) duelGame.PauseOnlineMatch("对手已离开，比赛暂停");
        }

        public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            ProcessPacket(player, new ReadOnlySpan<byte>(data.Array, data.Offset, data.Count));
        }

        public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            ProcessPacket(player, data);
        }

        void ProcessPacket(PlayerRef sender, ReadOnlySpan<byte> data)
        {
            using (var stream = new MemoryStream(data.ToArray()))
            using (var reader = new BinaryReader(stream))
            {
                byte type = reader.ReadByte();
                if (type == SubmitPacket && IsHost && sender == remotePlayer)
                {
                    AcceptSubmission(PlayerSide.B, reader.ReadInt32(), (DuelAction)reader.ReadByte());
                }
                else if (type == BeginTurnPacket && !IsHost)
                {
                    TurnId = reader.ReadInt32();
                    state = ReadState(reader);
                    float seconds = reader.ReadSingle();
                    deadline = Time.realtimeSinceStartup + seconds;
                    localChoiceSubmitted = false;
                    Phase = NetworkTurnPhase.Choosing;
                    PresentTurn(seconds);
                }
                else if (type == ResolvePacket && !IsHost)
                {
                    int turnId = reader.ReadInt32();
                    if (turnId != TurnId) return;
                    DuelAction actionA = (DuelAction)reader.ReadByte();
                    DuelAction actionB = (DuelAction)reader.ReadByte();
                    TurnResult result = ReadResult(reader);
                    state = result.state;
                    Phase = result.matchEnded ? NetworkTurnPhase.GameOver : NetworkTurnPhase.Resolving;
                    PresentResolution(actionA, actionB, result);
                }
            }
        }

        static byte[] WriteSubmit(int turnId, DuelAction action)
        {
            return WritePacket(writer => { writer.Write(SubmitPacket); writer.Write(turnId); writer.Write((byte)action); });
        }

        static byte[] WriteBeginTurn(int turnId, DuelState state, float seconds)
        {
            return WritePacket(writer => { writer.Write(BeginTurnPacket); writer.Write(turnId); WriteState(writer, state); writer.Write(seconds); });
        }

        static byte[] WriteResolve(int turnId, DuelAction actionA, DuelAction actionB, TurnResult result)
        {
            return WritePacket(writer =>
            {
                writer.Write(ResolvePacket); writer.Write(turnId); writer.Write((byte)actionA); writer.Write((byte)actionB);
                WriteState(writer, result.state); writer.Write(result.aPoints); writer.Write(result.bPoints);
                writer.Write(result.aFell); writer.Write(result.bFell); writer.Write(result.aBurned); writer.Write(result.bBurned);
                writer.Write(result.collision); writer.Write(result.parry); writer.Write(result.thrustHit);
                writer.Write(result.aWhiff); writer.Write(result.bWhiff); writer.Write(result.boutEnded); writer.Write(result.matchEnded);
                writer.Write(result.message ?? string.Empty);
            });
        }

        static byte[] WritePacket(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream)) { write(writer); return stream.ToArray(); }
        }

        static void WriteState(BinaryWriter writer, DuelState state)
        {
            writer.Write(state.aPosition); writer.Write(state.bPosition); writer.Write(state.aScore); writer.Write(state.bScore);
            writer.Write(state.turn); writer.Write(state.fireDepth); writer.Write(state.suddenDeath);
            writer.Write(state.aThrustRecovering); writer.Write(state.bThrustRecovering);
        }

        static DuelState ReadState(BinaryReader reader)
        {
            return new DuelState
            {
                aPosition = reader.ReadInt32(), bPosition = reader.ReadInt32(), aScore = reader.ReadInt32(), bScore = reader.ReadInt32(),
                turn = reader.ReadInt32(), fireDepth = reader.ReadInt32(), suddenDeath = reader.ReadBoolean(),
                aThrustRecovering = reader.ReadBoolean(), bThrustRecovering = reader.ReadBoolean()
            };
        }

        static TurnResult ReadResult(BinaryReader reader)
        {
            var result = new TurnResult { state = ReadState(reader) };
            result.aPoints = reader.ReadInt32(); result.bPoints = reader.ReadInt32();
            result.aFell = reader.ReadBoolean(); result.bFell = reader.ReadBoolean();
            result.aBurned = reader.ReadBoolean(); result.bBurned = reader.ReadBoolean();
            result.collision = reader.ReadBoolean(); result.parry = reader.ReadBoolean(); result.thrustHit = reader.ReadBoolean();
            result.aWhiff = reader.ReadBoolean(); result.bWhiff = reader.ReadBoolean();
            result.boutEnded = reader.ReadBoolean(); result.matchEnded = reader.ReadBoolean(); result.message = reader.ReadString();
            return result;
        }

        public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason)
        {
            if (!intentionalShutdown && Phase != NetworkTurnPhase.GameOver)
            {
                Phase = NetworkTurnPhase.Paused;
                if (duelGame) duelGame.PauseOnlineMatch("Photon 连接中断，比赛暂停");
            }
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) => request.Accept();
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        void OnDestroy()
        {
            if (duelGame) duelGame.OnlineActionSelected -= SubmitFromPresentation;
            if (runner) runner.RemoveCallbacks(this);
        }
    }
}
