# 白模 MVP 使用说明

**实现版本：** MVP v0.4
**更新日期：** 2026-09-15
**Unity：** 2022.3.62f2c1

打开 `Scenes/SampleScene.unity`，场景层级中的 `Duel MVP` 已挂载完整游戏控制器，`Battle UI` 是标准 Unity Canvas。

Canvas 下的文本、按钮、布局和 EventSystem 由编辑器安装器一次性生成并序列化到场景。运行时只刷新已有 UI 组件，不创建 UI。如果场景尚未完成安装，请退出 Play Mode；安装器会在编辑模式生成并保存，也可以手动执行菜单 `Come And Fight > Build Fixed Scene UI`。

## 操作

- 玩家 A：`A` 后退、`D` 前进、`J` 击剑、`K` 招架；也可点击按钮。
- 默认玩家对权重 AI。
- `R` 重新开始。
- 选择窗口为 2 秒；超时未选视为招架。
- 击剑空刺后，下一回合的击剑按钮和按键暂时不可用；完成该回合后恢复。

## 原型规则约定

设计文档 v0.4 中的规则在本原型采用以下确定解释：

1. 同时坠落或同时被火烧死均不计分，小局继续。
2. 前进后的目标格触及招架者所在格时，将招架者向后推动一格。
3. 击剑会命中攻击方向相邻格中“回合开始时已存在”或“本回合移动进入”的目标。
4. 击剑未命中且未被有效招架时为空刺，下一回合不能再次击剑；恢复回合结束后自动解除。
5. 双方同时空刺时双方都进入恢复；被成功招架或成功命中的击剑不触发恢复。
6. 行动一旦选择即锁定，不能修改。
7. 第 20 个正常回合完成且无人得分后，立即点燃第 1、7 格；从后续死斗回合开始继续向内收缩。
8. 同时被烧死时，逻辑位置保留在对应燃烧格，下一回合继续；随着安全区收缩，该状态最终必须产生非同时死亡结果，避免无限重置。

## 结构

- `DuelRules.cs`：纯数据状态、行动与结算结果。
- `TurnResolver.cs`：不依赖动画或物理系统的确定性规则层。
- `DuelGame.cs`：输入、AI、白模表现与比赛流程。
- `DuelUI.cs`：引用并刷新场景中已经存在的 UGUI，不负责创建 UI。
- `DuelBootstrap.cs`：保留类型兼容，运行时启动已禁用；游戏对象由场景直接提供。
- `Scripts/Editor/DuelSceneUIInstaller.cs`：只在编辑模式一次性生成并保存固定 UI。
- `Tests/EditMode/TurnResolverTests.cs`：核心规则 EditMode 测试。

当前白模使用场景中已序列化的纯色 UGUI Image，不含外部美术素材，运行时不创建对象。

## 场景层级

```text
SampleScene
├─ Main Camera
├─ Global Light 2D
├─ Duel MVP
├─ Battle UI (Canvas)
│  ├─ Title
│  ├─ Arena
│  │  ├─ 格子 1-7
│  │  ├─ 玩家 A（身体 / 头 / 剑）
│  │  └─ 玩家 B（身体 / 头 / 剑）
│  ├─ Score
│  ├─ Turn
│  ├─ Status
│  ├─ Timer
│  ├─ Action Panel
│  │  ├─ Retreat
│  │  ├─ Advance
│  │  ├─ Thrust
│  │  └─ Parry
│  ├─ Mode
│  └─ Help
└─ EventSystem
```

## 当前完成状态

- 核心规则与表现层分离：已完成。
- 完整比赛循环：已完成。
- 玩家对权重 AI：已完成。
- 局域网 Host/Client 联机：已完成。
- 固定场景 UGUI：已完成。
- 空刺恢复与击剑禁用反馈：已完成。
- 核心规则 EditMode 测试：已建立。
- 正式美术、音效与动画：尚未制作，当前使用白模反馈。

## UI 维护

固定 UI 使用 Unity Legacy Text，以 `LegacyRuntime.ttf` 作为内置字体。Unity 2022.3 不再接受 `Arial.ttf` 作为内置字体路径。

完成首次安装后，可直接在场景中调整 Canvas 子对象。安装器检测到 `DuelUI.titleText` 已绑定后不会重复生成或覆盖 UI。
