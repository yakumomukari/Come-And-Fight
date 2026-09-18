# Come And Fight — Network Development

## Current stage

Phase 4 now has two platform-selected transports while preserving the same Host-authoritative gameplay protocol:

- Windows/macOS/Linux use Steam Networking Sockets.
- Android uses Unity Relay over DTLS with an anonymous Unity Authentication session and a short room code.

The Android path is Android-to-Android only in this milestone. PC-to-Android cross-platform play remains deferred because the PC build still uses Steam transport.

The first Steam milestone uses the Host's 17-digit Steam ID as the room address. Steam Lobby and friend invitations are deferred until the transport is verified on two PCs.

Verification status (2026-09-17): passed with two Windows player instances on one PC. The Host assigned client 0 to Player A and the connecting client 1 to Player B; the client identified itself as Player B.

Implemented:

- Netcode for GameObjects and Unity Transport connection lifecycle.
- Explicit Player A/Player B identity and a two-player limit.
- Reliable ordered messages for `BeginTurn`, `SubmitAction`, and `ResolveTurn`.
- Every submission carries `turnId` and `action` only.
- Host validation of sender, phase, `turnId`, duplicates, Action range, and Thrust recovery.
- Host-owned deadline with `Parry` as the missing-action fallback.
- Host-only `TurnResolver.Resolve` and a complete authoritative `DuelState` in every result.
- Old or mismatched result messages are ignored by the Client.
- A scene-authored menu for single-player and platform-specific online rooms.
- Both peers feed the received authoritative result into the existing `DuelGame` reveal and animation pipeline.
- Android Host creation, one-tap room-code copying, room-code joining, and system-keyboard input.
- Desktop-only Steam integration isolated in `ComeAndFight.Steam`, so Android does not compile or load the Steam transport.

Not implemented: PC/Android cross-platform rooms, in-match reconnect, rematch agreement, Steam Lobby, or friend invitations.

## Windows player settings

Windows builds start in a resizable `1280 × 720` window. The game continues running while another window has focus, which allows Host and Client instances to be tested side by side. Players can still use the normal fullscreen shortcut because fullscreen switching remains enabled.

The networking assembly calls the pure `TurnResolver`; the rules assembly does not reference NGO. Single-player gameplay does not depend on networking.

## Packages

- `com.unity.netcode.gameobjects` 1.7.1
- `com.unity.transport` 1.4.0 (the version required by NGO 1.7.1)
- `com.unity.services.authentication` 2.7.4
- `com.unity.services.relay` 1.2.0
- `com.rlabrecque.steamworks.net` (pinned Git revision)
- `com.community.netcode.transport.steamnetworkingsockets` (pinned Git revision)

## Steam development test

1. Start Steam on both PCs and sign into two different Steam accounts.
2. Make a Windows Build. During the current test phase, the build postprocessor copies `steam_appid.txt` with Valve's development App ID `480` beside the executable for both normal and Development builds.
3. Start the game on both PCs. The Host creates a room, then clicks `复制房主 Steam ID` and sends the copied 17-digit ID to the Client.
4. The Client enters that Steam ID and joins. IP addresses, port forwarding, and UDP firewall rules are not used by this path.

App ID `480` is for development only. Before release, replace it with the project's assigned Steam App ID and do not ship `steam_appid.txt`; Steam supplies the App ID when launching the released game.

## Platform selection

- Windows/macOS/Linux desktop builds select Steam Networking Sockets automatically.
- Android builds exclude the desktop-only Steam assembly and select Unity Transport + Relay automatically.
- Android creates a Relay allocation for one remote player, signs in anonymously, and uses a DTLS-protected Relay connection. No LAN IP, firewall rule, public IP, or port forwarding is required.
- Android uses both ARMv7 and ARM64 architectures, fullscreen rendering, autorotation, touch-only action labels, and the system Back button to return to the mode menu or quit.
- Cross-platform networking is intentionally deferred. A later milestone must move both PC and Android onto a common backend for mixed-platform matches.

## Unity Cloud setup required before Android testing

The project files currently do not contain a Unity Cloud Project ID. Before Relay can work at runtime:

1. Open the project in Unity while signed in.
2. Open `Edit > Project Settings > Services` (or the Services window in this Unity version).
3. Link this local project to a Unity Cloud project owned by your organization.
4. In the Unity Dashboard for that project, enable Multiplayer/Relay and accept any required terms.
5. Return to Unity and wait until the Authentication and Relay packages finish importing without Console errors.

Without this link, the Android UI will open normally but room creation reports a Unity Services initialization/project configuration error.

## Android Relay test

1. Build and install the APK on two Android devices with internet access.
2. On device A choose `Android Relay 对战`, then `创建 Relay 房间`.
3. After the room code appears, tap `复制 Relay 房间码` and send it to device B.
4. On device B open the same menu, tap the room-code field, paste or type the code, then choose `通过房间码加入`.
5. Verify both menus close and each device can submit exactly one action per turn.

Relay codes are temporary: if the Host closes the app or shuts down the session, create a new room and share its new code.

### Regional availability

The Relay allocation API can return HTTP 451 when Unity cannot provide the service in the player's region for legal reasons. This is a server-side policy response, not a firewall, room-code, or Android network-permission failure. When this occurs, the Android release needs a different, region-compliant relay/backend; changing local Wi-Fi or opening ports does not fix it.

## Legacy direct-connection test

The default endpoint is `127.0.0.1:7777`. This path remains useful for editor/protocol diagnostics but is no longer exposed by the player menu.

### Menu workflow

Use command-line roles or development-only transport diagnostics if direct Unity Transport testing is needed. Shipping desktop UI uses Steam ID and shipping Android UI uses Relay code; neither asks for an IP address.

The online menu is serialized under `Battle UI/Network Menu` in `SampleScene`. `NetworkUiSceneInstaller` is an editor-only reproducible scene installer; runtime code does not construct UI objects.

### Development shortcuts

1. Run two Windows instances.
2. Press `F5` in the first instance to start Host.
3. Press `F6` in the second instance to start Client.
4. Confirm logs show server start, connection, and Player A/Player B identification.
5. Press `F7` to disconnect.

When both players are connected and a turn starts, submit a network action with:

- `1`: Advance
- `2`: Retreat
- `3`: Thrust
- `4`: Parry

Standalone instances can start automatically:

```text
Come And Fight!.exe -caf-role host
Come And Fight!.exe -caf-role client -caf-address 127.0.0.1 -caf-port 7777
```

For automated protocol testing, add `-caf-auto-action Advance`, `Retreat`, `Thrust`, or `Parry`. The instance submits that action once per turn.

Additional automated exception flags:

- `-caf-auto-delay <seconds>` delays each automatic submission.
- `-caf-auto-skip-turn <turnId>` leaves one turn unanswered for timeout testing.
- `-caf-auto-duplicate` resends the same choice.
- `-caf-auto-stale` sends the previous `turnId` after the current submission.
- `-caf-auto-disconnect-after-submit` performs an NGO shutdown shortly after submission.

Expected logs include `Local networking initialized`, `Host started`, `Client connecting`, `Client <id> assigned to Player A/B`, and `Local player identified as Player A/B`.

## Architecture and limitations

`ComeAndFight.Networking` is a separate assembly. It owns connection identity, the network turn phase, server deadline, submitted choices, and the authoritative match state. Only the Host calls `TurnResolver.Resolve`. The Host is temporarily Player A and the first remote client Player B; this mapping is explicit and isolated for later replacement.

Phase 3 verification (2026-09-17): two Windows instances completed a full automatic match while network results drove the existing presentation layer. Host and Client logged identical action pairs and authoritative state summaries for every `turnId`; the match ended at the same 2–0 score on both peers. No presentation integration exceptions occurred.

The rules and serialization regression suite currently passes 13/13 tests. After a manual two-PC LAN pass of the exception matrix below, the next network stage is Relay.

Direct-connection exception verification (2026-09-17):

- Missing Client Action timed out to Host-selected `Parry`.
- Duplicate submissions were rejected without additional resolution.
- Previous-turn submissions were rejected by `turnId`.
- Client graceful disconnect after submission paused the Host before resolution.
- Host graceful disconnect paused the Client.

A hard process crash or physical network loss is detected by the transport timeout rather than immediately. With the current two-second decision window, that timeout can occur after the Host deadline. Fast failure detection, heartbeat, and resync remain part of the later reconnect design; the current guarantee applies to explicit disconnects.

Automated regression status: all 12 existing EditMode duel-rule tests pass after adding the network assembly.
