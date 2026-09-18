using System;
using Unity.Netcode;
using UnityEngine;

namespace ComeAndFight.Networking
{
    public static class DesktopSteamBridge
    {
        public static Func<GameObject, NetworkTransport> CreateTransportHandler;
        public static Func<bool> InitializeHandler;
        public static Func<string> LocalUserIdHandler;
        public static Action RunCallbacksHandler;
        public static Action ShutdownHandler;
        public static Action<NetworkTransport, ulong> SetRemoteUserIdHandler;

        public static bool IsAvailable => CreateTransportHandler != null
            && InitializeHandler != null
            && LocalUserIdHandler != null
            && SetRemoteUserIdHandler != null;

        public static NetworkTransport CreateTransport(GameObject owner) => CreateTransportHandler(owner);
        public static bool Initialize() => InitializeHandler();
        public static string LocalUserId() => LocalUserIdHandler();
        public static void RunCallbacks() => RunCallbacksHandler?.Invoke();
        public static void Shutdown() => ShutdownHandler?.Invoke();
        public static void SetRemoteUserId(NetworkTransport transport, ulong userId) => SetRemoteUserIdHandler(transport, userId);
    }
}
