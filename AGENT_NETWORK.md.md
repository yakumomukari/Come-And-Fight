# AGENT_NETWORK.md

## 1. 职责

你负责 `Come-And-Fight` 项目的双人网络联机模块。

项目地址：

`yakumomukari/Come-And-Fight`

你的目标不是重写游戏，而是在保持现有单机规则与表现逻辑稳定的前提下，为游戏增加可靠的双人在线对战能力。

优先保证：

1. 双方游戏状态一致
2. 双方选择在揭晓前保持隐藏
3. 网络逻辑与核心规则解耦
4. 弱网或断线不会直接破坏游戏状态
5. 不为了联机大规模重构现有代码
6. PC 双实例验证完成后再处理移动端
7. 每次修改后更新相关开发文档

---

# 2. 当前项目结构

当前核心代码位于：

```text
Assets/Scripts/DuelMvp/
├── DuelBootstrap.cs
├── DuelGame.cs
├── DuelRules.cs
├── DuelUI.cs
└── TurnResolver.cs
```

核心状态：

```csharp
DuelState
```

核心行动：

```csharp
DuelAction
```

包括：

```text
Advance
Retreat
Thrust
Parry
```

游戏核心规则已经集中于：

```csharp
TurnResolver.Resolve(
    DuelState old,
    DuelAction a,
    DuelAction b
)
```

这是整个联机架构最重要的边界。

---

# 3. 核心原则

## 3.1 TurnResolver 必须保持纯规则层

禁止让 `TurnResolver`：

```text
访问 NetworkManager
访问 NetworkVariable
访问 UI
读取 Input
读取 Time.time
生成 NetworkObject
调用 RPC
播放动画
```

它只能：

```text
输入：
DuelState
DuelAction A
DuelAction B

输出：
TurnResult
```

网络层只负责决定：

```text
什么时候调用 Resolve
使用什么 Action 调用 Resolve
谁拥有最终 Resolve 权限
```

---

# 4. 联机模型

第一阶段使用：

```text
Netcode for GameObjects
Unity Transport
Unity Authentication
Unity Relay
```

暂不加入：

```text
Lobby
Matchmaker
Dedicated Server
账号注册系统
排行榜
好友系统
数据库
```

采用：

```text
Host + Client
```

模型。

Host 是当前比赛的权威端。

```text
Player A / Host
        │
        │
     Relay
        │
        │
Player B / Client
```

双方不直接通过公网 IP 建立连接。

---

# 5. 网络同步模型

本项目禁止使用高频 Transform 同步。

不要添加：

```text
NetworkTransform
NetworkRigidbody
角色位置每帧同步
剑的位置同步
动画 Transform 同步
UI 同步
特效同步
```

本游戏是离散回合制。

网络只同步：

```text
玩家提交的 Action
回合编号
权威 DuelState
TurnResult 所需信息
比赛状态
```

正常情况下，每个回合只需要发生少量网络通信。

---

# 6. 权威模型

Host 负责维护：

```text
DuelState authoritativeState
int turnId
DuelAction playerAChoice
DuelAction playerBChoice
TurnPhase
deadline
```

Client 不允许直接修改最终：

```text
位置
分数
火焰状态
恢复状态
回合编号
```

Client 只能提交：

```text
我在 turn X 选择了 Action Y
```

最终状态必须由 Host：

```csharp
TurnResolver.Resolve(...)
```

产生。

---

# 7. 回合协议

标准回合流程：

```text
Host
│
├─ BeginTurn(turnId)
│
├─ 设置服务器 deadline
│
└─ 通知 Client

A 玩家选择 ActionA
B 玩家选择 ActionB

↓
双方分别提交

Host 收集 Action

↓
双方均已提交
或者
服务器 deadline 到达

↓

缺失 Action 使用默认行为

↓

TurnResolver.Resolve()

↓

得到 TurnResult

↓

Host 更新 authoritativeState

↓

广播 ResolveResult

↓

双方本地播放完全相同的表现动画

↓

下一回合
```

---

# 8. 超时处理

禁止客户端自行决定：

```text
当前回合已经超时
```

客户端可以显示倒计时，但没有裁决权。

真正的 deadline 由 Host 控制。

客户端：

```text
只负责显示剩余时间
```

Host：

```text
负责确定回合结束
负责缺省 Action
```

默认超时行为当前保持：

```text
Parry
```

除非设计文档之后明确修改。

---

# 9. 行动提交

行动提交必须包含：

```text
turnId
action
```

例如：

```text
Turn 7
Thrust
```

Host 必须验证：

```text
提交者是不是当前比赛玩家
turnId 是否匹配
是否已经提交过
Action 是否合法
Thrust 是否被恢复状态禁止
比赛是否已经结束
当前是否处于 Choosing Phase
```

禁止信任 Client 声称：

```text
自己的位置
自己的分数
攻击命中
本回合结果
```

---

# 10. 防止重复提交

每名玩家每回合只能拥有一次有效选择。

选择提交后：

```text
不可修改
```

除非以后玩法设计明确加入：

```text
取消选择
重新选择
```

收到重复 RPC 时：

```text
忽略
```

不要产生额外结算。

---

# 11. 同时选择的信息隐藏

这是本项目最重要的联网规则之一。

游戏核心是：

```text
双方秘密选择
→
同时揭晓
```

因此 Client B 提交行动后：

```text
Player A 不得通过正常 UI 获知 B 的 Action
```

同理。

第一阶段可以接受：

```text
Host 在技术上能够读取 Client 提交的 Action
```

因为当前目标是普通好友联机。

但是架构中不要让：

```text
DuelGame
UI
普通客户端代码
```

直接读取对方尚未揭晓的选择。

---

# 12. Commit-Reveal 预留

如果未来加入公开 PvP，可升级为：

```text
Commit
↓
双方确认 Commit 完成
↓
Reveal
↓
验证
↓
Resolve
```

Commit：

```text
Hash(turnId + action + nonce)
```

Reveal：

```text
action
nonce
```

验证：

```text
Hash(turnId + action + nonce)
==
此前 commit
```

第一阶段不要实现完整 Commit-Reveal。

但网络代码不要设计成严重依赖：

```text
Host UI 可以直接访问 remoteChoice
```

为以后升级保留边界。

---

# 13. DuelGame 重构原则

当前 `DuelGame` 同时包含：

```text
输入
AI
本地双人
倒计时
状态
规则调用
动画
UI 状态
重新开始
```

联网改造时优先分离：

```text
DuelGame
```

负责：

```text
游戏表现
动画
棋盘刷新
状态展示
```

```text
DuelNetworkController
```

负责：

```text
连接状态
玩家身份
Action 提交
回合控制
Host 权威 Resolve
RPC
状态同步
```

```text
DuelInput
```

可根据实际复杂度决定是否单独拆分。

不要为了“架构漂亮”过度拆文件。

---

# 14. 推荐新增文件

优先考虑：

```text
Assets/Scripts/Network/
├── NetworkBootstrap.cs
├── DuelNetworkController.cs
├── RelayManager.cs
└── NetworkMatchState.cs
```

如功能很少，可以进一步合并。

避免出现：

```text
30 个只有几十行的 Manager
```

不要 Manager 套 Manager。

---

# 15. NetworkBootstrap

负责：

```text
初始化 Unity Services
匿名 Authentication
创建 Host
加入 Host
Relay 创建
Relay Join Code
NetworkManager 启动
网络错误处理
```

不要负责：

```text
游戏规则
玩家行动
分数
动画
```

---

# 16. RelayManager

负责：

```text
CreateRelay()
JoinRelay(code)
Disconnect()
```

应尽量是网络服务包装层。

游戏规则代码不应该知道：

```text
Allocation
RelayServerData
JoinAllocation
```

等底层 Relay 对象。

---

# 17. DuelNetworkController

这是游戏网络模块核心。

职责：

```text
识别 A/B 玩家
管理 turnId
收集选择
校验选择
Host 调用 TurnResolver
广播 TurnResult
开始下一回合
RestartMatch
断线状态处理
```

不要负责：

```text
角色动画细节
按钮位置
Canvas
颜色
音效
```

---

# 18. 表现层

网络收到：

```text
beforeState
actionA
actionB
TurnResult
```

之后：

```text
DuelGame
```

根据结果播放原有动画。

不要通过网络同步：

```text
动画第几帧
角色当前 localPosition
剑当前 Rotation
颜色渐变值
```

动画完全本地执行。

只要：

```text
输入结果相同
```

两台设备最终表现就应该一致。

---

# 19. 状态一致性

每次完整 Resolve 后，Host 应提供权威：

```text
DuelState
```

不要完全依赖两端自己长期模拟。

即：

```text
Client 收到 TurnResult
↓
使用 Host 提供的新 DuelState
```

这样即使发生：

```text
掉包
旧 RPC
临时状态错误
```

也更容易恢复。

---

# 20. turnId

所有回合网络事件必须绑定：

```text
turnId
```

例如：

```text
SubmitAction(12, Thrust)
ResolveTurn(12, ...)
BeginTurn(13, ...)
```

Client 收到：

```text
旧 turnId
```

事件时应直接忽略。

这样避免：

```text
延迟 RPC
重复包
断线恢复
```

污染新回合。

---

# 21. RestartMatch

重新开始不能由任意客户端直接本地调用：

```text
RestartMatch()
```

联网模式下必须经过网络协议。

第一阶段可以设计：

```text
Host 点击重新开始
→
Host 重置 DuelState
→
广播 Restart
```

以后再加入：

```text
双方 Rematch 确认
```

---

# 22. 玩家身份

不要通过：

```text
场景中的左边玩家
是否 Host
Object 查找顺序
```

推断玩家身份。

明确维护：

```text
PlayerSide.A
PlayerSide.B
```

或者：

```text
bool isPlayerA
```

Host 不一定永久等于设计意义上的 Player A。

为以后：

```text
交换出生方向
Rematch
随机先后手
```

留下空间。

---

# 23. 断线处理

第一阶段最低要求：

Client 断开：

```text
暂停比赛
显示连接中断
不继续 Resolve
```

Host 断开：

```text
Client 返回联机菜单
提示房主已离开
```

不要第一版实现复杂 Host Migration。

不要因为断线：

```text
继续本地跑 AI
继续倒计时然后结算
```

---

# 24. 手机端注意事项

等 PC 双实例联机稳定后再测试 Android。

手机必须考虑：

```text
应用进入后台
应用恢复
Wi-Fi → 蜂窝网络
短时掉线
系统暂停 Unity
```

第一阶段允许：

```text
掉线后返回房间页面
```

不强制实现完整比赛内重连。

以后再设计：

```text
Reconnecting
Resync State
Resume Match
```

---

# 25. UI 规则

禁止在运行时为了联机临时创建大量 UI。

不要写：

```csharp
new GameObject("Button")
new GameObject("Panel")
new GameObject("InputField")
```

然后在代码里现场拼整个联机菜单。

UI 应尽量：

```text
在 Unity Scene / Prefab 中提前创建
通过 Inspector 绑定
```

代码只控制：

```text
显示
隐藏
文本
按钮事件
状态
```

不要把 UI 构造器塞进运行时代码。

---

# 26. 第一阶段界面

只需要：

```text
[单人]

[本地双人]

[在线对战]
```

在线对战：

```text
[创建房间]

Join Code:
XXXXXX

[输入房间码]
[加入]
```

连接完成：

```text
等待对手
```

双方进入：

```text
3
2
1
决斗
```

不要第一阶段制作：

```text
好友列表
服务器列表
头像
段位
账号注册
聊天
复杂匹配 UI
```

---

# 27. 不要破坏本地模式

联网模块加入后：

```text
单人 AI
本地双人
```

必须继续可用。

网络模式不应该成为：

```text
DuelGame 唯一运行方式
```

推荐：

```text
GameMode
├── AI
├── Local
└── Online
```

具体实现可以根据现有结构调整。

---

# 28. 包管理

当前项目为 Unity 2022.3。

添加联机依赖时：

优先选择与 Unity 2022.3 LTS 兼容的稳定版本。

不要：

```text
为了使用最新 Multiplayer Services API
直接升级整个项目到 Unity 6
```

不要一次性安装大量无关 Multiplayer Package。

每增加一个包，都确认：

```text
为什么需要
由谁依赖
当前项目是否实际使用
```

---

# 29. asmdef

项目已经使用：

```text
ComeAndFight.Runtime.asmdef
```

新增 Networking 代码前检查 asmdef 引用。

不要出现：

```text
代码写完
Unity 因 asmdef 没引用 NGO 编译失败
```

必要时添加对应 package assembly reference。

---

# 30. Debug

网络调试日志应至少能够看到：

```text
Services Initialized
Authenticated
Relay Created
Join Code
Client Connected
Player Assigned
Turn Started
Action Submitted
Both Actions Ready
Turn Resolved
Client Disconnected
```

开发阶段可以详细。

不要每帧打印日志。

---

# 31. 测试顺序

严格按以下顺序推进。

## Phase 1

不接 Relay。

使用：

```text
Host
Client
```

在 PC 本地直接连接。

验证：

```text
两实例成功连接
```

## Phase 2

验证：

```text
双方 Action 提交
Host Resolve
双方结果一致
```

## Phase 3

验证完整：

```text
20 回合
空刺恢复
推进
招架
击剑
坠落
死斗
火焰
双方同时死亡
比分
比赛结束
```

## Phase 4

加入 Relay。

验证：

```text
创建 Join Code
另一实例通过 Code 加入
```

## Phase 5

Windows 两台不同设备测试。

## Phase 6

Android 真机。

不要跳过前面的测试直接：

```text
Build APK
```

然后在手机上调网络 Bug。

---

# 32. 必测异常

至少测试：

```text
同一玩家连续点击多个 Action
回合最后一帧提交
Client 提交后立刻断线
Host 在 Resolve 时退出
收到旧 turnId RPC
Client 重复连接
非法 Join Code
Relay 创建失败
Authentication 失败
网络断开
重新开始比赛
```

---

# 33. 不允许的方案

禁止为了方便直接：

```text
两边各自 Resolve，然后认为结果一定一致
```

权威结果必须由 Host 产生。

禁止：

```text
客户端直接修改 DuelState
```

禁止：

```text
NetworkVariable<Vector3> position
```

来驱动棋子。

禁止：

```text
每 Update 发 RPC
```

禁止：

```text
把整个 DuelGame 改成 NetworkBehaviour
然后所有变量全部 NetworkVariable
```

禁止：

```text
在 TurnResolver 里塞联网判断
```

禁止：

```text
为了联网重写已经稳定的战斗规则
```

---

# 34. 修改策略

每次只解决一个明确阶段。

例如：

第一批：

```text
安装 NGO
添加 NetworkManager
双实例连接
```

第二批：

```text
Action RPC
```

第三批：

```text
Host Resolve
```

第四批：

```text
Relay
```

不要一次提交：

```text
网络
UI
登录
匹配
动画重构
规则修改
Android适配
```

这样出了 Bug 根本不知道谁炸的。

---

# 35. 文档要求

每次完成重要网络阶段后更新项目文档。

至少维护：

```text
README.md
```

或者新增：

```text
Docs/NETWORK.md
```

NETWORK.md 应说明：

```text
当前网络架构
使用的 Packages
Host / Client 模型
Relay 使用方法
本地测试方法
已完成能力
当前限制
后续计划
```

不要让代码进入：

```text
只有 AI 知道怎么运行
```

的状态。

---

# 36. 开发文档

如果做出架构层修改，例如：

```text
DuelGame 职责变化
RPC 协议变化
DuelState 网络格式变化
Relay 流程变化
```

必须同步更新：

```text
Docs/NETWORK.md
```

如果没有 Docs 目录，可以创建。

---

# 37. 完成标准

第一阶段联网模块完成的定义不是：

```text
Unity 没报错
```

而是：

```text
两台 PC / 两个实例
↓
Host 创建房间
↓
Client 加入
↓
双方同时选择行动
↓
选择不会提前泄露
↓
Host 唯一 Resolve
↓
双方播放一致结果
↓
连续完成完整比赛
↓
重新开始正常
↓
主动断线不会造成异常状态
```

以上全部满足，才进入手机适配阶段。

---

# 38. 当前第一任务

从当前仓库状态开始时：

不要先修改战斗规则。

第一步只做：

```text
安装必要 Networking Package
创建 NetworkBootstrap
创建 DuelNetworkController
建立 Host / Client 本地连接
```

然后验证：

```text
Host 和 Client 可以识别彼此
```

此阶段甚至不要同步战斗 Action。

确认基础连接稳定后，再进入 Action 协议开发。

---

# 39. 开发行为要求

开始工作前：

```text
阅读 DuelGame.cs
阅读 DuelRules.cs
阅读 TurnResolver.cs
阅读当前 README / MVP 文档
```

修改前理解现有行为，不要凭文件名猜逻辑。

每完成一个阶段：

```text
检查编译
检查 Console
检查本地模式
更新文档
```

不要删除现有功能来让新功能“看起来能跑”。

如发现现有代码与本文档冲突：

优先保证：

```text
规则正确
状态唯一权威
低耦合
现有玩法不回归
```

并在开发文档记录理由。