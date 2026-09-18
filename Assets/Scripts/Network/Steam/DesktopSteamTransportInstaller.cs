using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace ComeAndFight.Networking.Steam
{
    static class DesktopSteamTransportInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            DesktopSteamBridge.CreateTransportHandler = CreateTransport;
            DesktopSteamBridge.InitializeHandler = SteamAPI.Init;
            DesktopSteamBridge.LocalUserIdHandler = () => SteamUser.GetSteamID().m_SteamID.ToString();
            DesktopSteamBridge.RunCallbacksHandler = SteamAPI.RunCallbacks;
            DesktopSteamBridge.ShutdownHandler = SteamAPI.Shutdown;
            DesktopSteamBridge.SetRemoteUserIdHandler = SetRemoteUserId;
        }

        static NetworkTransport CreateTransport(GameObject owner)
        {
            return owner.GetComponent<SteamNetworkingSocketsTransport>()
                ?? owner.AddComponent<SteamNetworkingSocketsTransport>();
        }

        static void SetRemoteUserId(NetworkTransport transport, ulong userId)
        {
            if (transport is SteamNetworkingSocketsTransport steamTransport)
            {
                steamTransport.ConnectToSteamID = userId;
                return;
            }

            throw new System.InvalidOperationException("The active desktop transport is not Steam Networking Sockets.");
        }
    }
}
