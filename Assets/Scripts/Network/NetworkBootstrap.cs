using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace ComeAndFight.Networking
{
    [DefaultExecutionOrder(-1000)]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] string address = "";
        [SerializeField] ushort port = 7777;
        NetworkManager manager;
        NetworkTransport transport;
        string localAddressSummary = "Steam 尚未初始化";
        string lastConnectionError;
        bool steamInitialized = false;
        string roomCode;

        public NetworkManager Manager => manager;
        public string Address => address;
        public ushort Port => port;
        public string LocalAddressSummary => localAddressSummary;
        public string LastConnectionError => lastConnectionError;
        public bool SteamReady => steamInitialized;
        public bool OnlineSupported => true;
        public string RoomCode => roomCode;

        public void SetAddress(string value)
        {
            address = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateBootstrap()
        {
            if (FindObjectOfType<NetworkBootstrap>() != null) return;
            var root = new GameObject("Network Bootstrap");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkBootstrap>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            try
            {
                string[] addresses = Dns.GetHostAddresses(Dns.GetHostName()).Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)).Select(ip => ip.ToString()).ToArray();
                if (addresses.Length > 0) localAddressSummary = string.Join(" / ", addresses);
            }
            catch (SocketException exception)
            {
                Debug.LogWarning($"[Network] Could not enumerate LAN addresses: {exception.Message}");
            }
            manager = GetComponent<NetworkManager>() ?? gameObject.AddComponent<NetworkManager>();
            if (!Application.isMobilePlatform && DesktopSteamBridge.IsAvailable)
            {
                transport = DesktopSteamBridge.CreateTransport(gameObject);
                try
                {
                    steamInitialized = DesktopSteamBridge.Initialize();
                    if (steamInitialized)
                    {
                        localAddressSummary = DesktopSteamBridge.LocalUserId();
                        Debug.Log($"[Network] Steam initialized. Local Steam ID: {localAddressSummary}.");
                    }
                    else lastConnectionError = "Steam 初始化失败：请启动 Steam，并确认 steam_appid.txt 位于程序旁。";
                }
                catch (Exception exception)
                {
                    lastConnectionError = "Steam 初始化异常：" + exception.Message;
                    Debug.LogError("[Network] " + lastConnectionError);
                }
            }
            if (!transport)
            {
                var unityTransport = GetComponent<UnityTransport>() ?? gameObject.AddComponent<UnityTransport>();
                unityTransport.ConnectTimeoutMS = 1000;
                unityTransport.MaxConnectAttempts = 5;
                transport = unityTransport;
            }
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            var controller = GetComponent<DuelNetworkController>() ?? gameObject.AddComponent<DuelNetworkController>();
            controller.Initialize(manager);
            manager.OnTransportFailure += OnTransportFailure;
            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log("[Network] Local networking initialized.");
        }

        void Start() => ApplyCommandLineRole(Environment.GetCommandLineArgs());

        void Update()
        {
            if (steamInitialized) DesktopSteamBridge.RunCallbacks();
            if (Input.GetKeyDown(KeyCode.F5)) StartHost();
            if (Input.GetKeyDown(KeyCode.F6)) StartClient();
            if (Input.GetKeyDown(KeyCode.F7)) Shutdown();
        }

        public bool StartHost()
        {
            if (!CanStart()) return false;
            lastConnectionError = null;
            if (!Application.isMobilePlatform && !steamInitialized) return FailSteamStart();
            if (transport is UnityTransport unityTransport) unityTransport.SetConnectionData(address, port, "0.0.0.0");
            bool started = manager.StartHost();
            Debug.Log(started ? $"[Network] Steam Host started. Steam ID: {LocalAddressSummary}." : "[Network] Host failed to start.");
            return started;
        }

        public async Task<bool> StartHostForCurrentPlatformAsync()
        {
            if (!Application.isMobilePlatform) return StartHost();
            if (!CanStart()) return false;
            lastConnectionError = null;
            try
            {
                await EnsureUnityServicesAsync();
                var allocation = await RelayService.Instance.CreateAllocationAsync(1);
                roomCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                var unityTransport = transport as UnityTransport;
                if (!unityTransport) throw new InvalidOperationException("Android Relay requires Unity Transport.");
                unityTransport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                bool started = manager.StartHost();
                if (started) localAddressSummary = roomCode;
                else lastConnectionError = "Relay 房间创建成功，但 Host 启动失败。";
                Debug.Log(started ? $"[Network] Android Relay Host started. Join code: {roomCode}." : "[Network] Android Relay Host failed to start.");
                return started;
            }
            catch (Exception exception)
            {
                lastConnectionError = FormatRelayError("创建 Relay 房间", exception);
                Debug.LogError("[Network] " + lastConnectionError);
                return false;
            }
        }

        public bool StartClient()
        {
            if (!CanStart()) return false;
            lastConnectionError = null;
            if (!Application.isMobilePlatform)
            {
                if (!steamInitialized) return FailSteamStart();
                if (!ulong.TryParse(address, out ulong hostSteamId) || hostSteamId == 0)
                {
                    lastConnectionError = "Steam ID 无效，应为房主显示的 17 位数字。";
                    return false;
                }
                DesktopSteamBridge.SetRemoteUserId(transport, hostSteamId);
            }
            else
            if (transport is UnityTransport unityTransport) unityTransport.SetConnectionData(address, port);
            bool started = manager.StartClient();
            Debug.Log(started ? $"[Network] Client connecting to Steam ID {address}." : "[Network] Client failed to start.");
            return started;
        }

        public async Task<bool> StartClientForCurrentPlatformAsync()
        {
            if (!Application.isMobilePlatform) return StartClient();
            if (!CanStart()) return false;
            lastConnectionError = null;
            try
            {
                roomCode = address.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(roomCode))
                {
                    lastConnectionError = "请输入房主分享的 Relay 房间码。";
                    return false;
                }
                await EnsureUnityServicesAsync();
                var allocation = await RelayService.Instance.JoinAllocationAsync(roomCode);
                var unityTransport = transport as UnityTransport;
                if (!unityTransport) throw new InvalidOperationException("Android Relay requires Unity Transport.");
                unityTransport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                bool started = manager.StartClient();
                if (!started) lastConnectionError = "已找到 Relay 房间，但 Client 启动失败。";
                Debug.Log(started ? $"[Network] Android Client joining Relay code {roomCode}." : "[Network] Android Relay Client failed to start.");
                return started;
            }
            catch (Exception exception)
            {
                lastConnectionError = FormatRelayError("加入 Relay 房间", exception);
                Debug.LogError("[Network] " + lastConnectionError);
                return false;
            }
        }

        static string FormatRelayError(string operation, Exception exception)
        {
            string details = exception?.Message ?? "未知错误";
            if (details.IndexOf("451", StringComparison.OrdinalIgnoreCase) >= 0
                || details.IndexOf("Unavailable For Legal Reasons", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Unity Relay 当前无法在所在地区提供服务（HTTP 451）。这不是房间码、防火墙或网络设置问题，需要更换联机后端。";
            }

            return operation + "失败：" + details;
        }

        static async Task EnsureUnityServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        bool FailSteamStart()
        {
            lastConnectionError = "Steam 尚未就绪。请启动 Steam，并使用不同账号测试两台设备。";
            Debug.LogError("[Network] " + lastConnectionError);
            return false;
        }

        public void Shutdown()
        {
            if (!manager || !manager.IsListening) return;
            manager.Shutdown();
            roomCode = null;
            Debug.Log("[Network] Connection shut down.");
        }

        bool CanStart()
        {
            if (!manager) { Debug.LogError("[Network] NetworkManager is unavailable."); return false; }
            if (manager.IsListening) { Debug.LogWarning("[Network] A network session is already running."); return false; }
            return true;
        }

        void ApplyCommandLineRole(string[] args)
        {
            string role = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-caf-address" && i + 1 < args.Length) address = args[++i];
                else if (args[i] == "-caf-port" && i + 1 < args.Length && ushort.TryParse(args[++i], out ushort value)) port = value;
                else if (args[i] == "-caf-role" && i + 1 < args.Length) role = args[++i].ToLowerInvariant();
            }
            if (role == "host") StartHost();
            else if (role == "client") StartClient();
            else if (role != null) Debug.LogWarning($"[Network] Unknown role '{role}'. Use host or client.");
        }

        void OnDestroy()
        {
            if (manager) manager.OnTransportFailure -= OnTransportFailure;
            if (manager) manager.OnClientConnectedCallback -= OnClientConnected;
            if (manager) manager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (manager && manager.IsListening) manager.Shutdown();
            if (steamInitialized) DesktopSteamBridge.Shutdown();
        }

        void OnClientConnected(ulong clientId)
        {
            if (manager && clientId == manager.LocalClientId) lastConnectionError = null;
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (!manager || clientId != manager.LocalClientId || manager.IsHost) return;
            lastConnectionError = Application.isMobilePlatform
                ? $"无法加入 Relay 房间 {roomCode}。请确认房间码有效且网络可访问 Unity 服务。"
                : $"无法连接 Steam ID {address}。请确认双方 Steam 在线、账号不同且房主已创建房间。";
        }

        void OnTransportFailure()
        {
            lastConnectionError = Application.isMobilePlatform
                ? "Relay 网络传输失败，请检查移动网络和 Unity 服务状态。"
                : $"Steam 网络传输失败：{address}。请检查 Steam 登录状态和 App ID。";
            Debug.LogError("[Network] " + lastConnectionError);
        }
    }
}
