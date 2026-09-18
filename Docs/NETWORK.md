# Come And Fight — Network Development

## Current transport selection

- Windows/macOS/Linux: Steam Networking Sockets.
- Android: Photon Fusion 2 Host Mode, using Photon relay (`DisableNATPunchthrough = true`).
- Single-player continues to use the local rules pipeline and does not depend on either service.
- PC/Android cross-platform rooms remain deferred because the desktop and Android builds use different backends.

Both online paths preserve the same Host-authoritative protocol. The Host is Player A, the first remote player is Player B, and only the Host calls `TurnResolver.Resolve`. Reliable packets carry `BeginTurn`, `SubmitAction`, and `ResolveTurn`; a submission contains only its `turnId` and action, while the result contains the complete authoritative `DuelState`.

## Android Photon setup

Photon Fusion 2.1.2 is imported under `Assets/Photon`. The Fusion App ID is stored in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`; do not publish that value in screenshots or public repositories unless intended.

Photon confirmed through ticket 76221 that this Fusion App ID is unlocked for China Mainland. Android now requests region `cn`; Fusion automatically routes that region through the China Name Server (`ns.photonengine.cn`). Both devices must use this same build, App ID, and region because rooms are isolated between Photon regions.

Unity Gaming Services, anonymous Authentication, and Unity Relay are no longer used by Android. The earlier Unity Relay path returned HTTP 451 in mainland China; enabling Unity Cloud Services cannot bypass that regional policy response.

## Android build and two-device test

1. Close Photon Fusion Hub so Unity can refresh and compile the project.
2. Confirm the Console has no red compile errors.
3. Open `File > Build Settings`, select Android, and use `Build` or `Build And Run`. Leave `Export Project` and `Build App Bundle` unchecked for an ordinary APK test.
4. Install the same APK on two Android devices. Both devices need normal internet access, but they do not need to be on the same Wi-Fi, expose an IP address, open ports, or change firewall rules.
5. On device A open online battle and choose `创建 Photon 房间`. Send the displayed six-character room code to device B.
6. On device B tap the room-code field, enter the code, and choose `通过房间码加入`.
7. Verify the room UI closes on both devices and each player can choose one action per turn. Verify both devices show the same score, positions, and round number.

If creation or joining fails, capture the full Unity/Android log containing `[Photon]`. Confirm both devices have the identical APK and App ID, use the identical region, and enter the room code without spaces. A room disappears when its Host exits.

## Steam desktop test

1. Start Steam on both PCs and sign into two different Steam accounts.
2. Build the Windows player. During development the postprocessor places `steam_appid.txt` with Valve test App ID 480 beside the executable.
3. The Host creates a room and uses `复制房主 Steam ID`.
4. The Client enters that 17-digit Steam ID and joins.

App ID 480 is development-only. Before release, replace it with the assigned Steam App ID and do not ship `steam_appid.txt`.

## Architecture and current limitations

`ComeAndFight.Networking` owns connection identity, phases, deadlines, submissions, and authoritative state. `ComeAndFight.Steam` is desktop-only, so Android does not compile or load Steamworks. `PhotonDuelController` is used only on Android and communicates with Fusion through reliable data messages rather than NetworkObjects, which keeps the existing turn resolver and presentation pipeline unchanged.

The current milestone does not implement reconnect, host migration, rematch agreement, Steam Lobby/friend invitations, or mixed PC/Android rooms. Photon room codes are six uppercase hexadecimal characters and are temporary.

Previous protocol regression coverage includes missing-action timeout to Parry, duplicate/stale submission rejection, authoritative result agreement, and disconnect pausing. The existing duel-rule EditMode tests remain the rules-level safety net; the Photon path still requires the physical two-device test above.
