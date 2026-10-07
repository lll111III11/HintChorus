<!-- language tabs -->
**语言 / Language：** **中文（主）** ｜ [English](../en/README.md)

> 本页是完整的中文技术文档（含 API 写法大全）。仓库入口见 [根目录 README](../../README.md)。

---
# HintChorus —— SCP:SL 动态 UI 隔离底层（LabAPI）

| 位置 | 路径 |
| --- | --- |
| 源码（桌面） | `<HintChorus>` + `<HintChorus.Bootstrap>` |
| 源码（工作区） | `...\work-mode-projects\<项目>\HintChorus` + `HintChorus.Bootstrap` |
| 部署 | `<AppData>\SCP Secret Laboratory\LabAPI\plugins\global\HintChorus.dll` |
| UiId 明细 | `...\LabAPI\configs\<端口>\HintChorus\uiids.yml` |
| 引导器状态 | `...\LabAPI\configs\<端口>\HintChorus\bootstrap.state` |

> `plugins\global\` 对 7400 / 7777 等**所有端口**生效，一次部署全端口覆盖。

**定位：纯 API 底层。不提供任何命令，只提供接口与调度；不含任何玩家玩法逻辑。**

---

## 一、动态 UIID 系统（核心）

两个插件都写 `Player.SendHint(文本, 时长秒)`，游戏只给一个全局提示口，谁后发谁覆盖。

**解法：给每个注册点派生确定性 UUID（UiId），每个 UiId 独占一个"信口"。**

```
插件 A 的某个方法 ─┐
插件 B 的某个方法 ─┼─→ 拦截层截获 ─→ 解析调用栈 ─→ 派生 UiId ─→ 各自独立信口
插件 C 的某个方法 ─┘                                    │
                                              HintBroker 唯一写者合并下发
```

```
UiId = UUIDv5( "HintChorus.UiId/v1" | UI表面 | 程序集 | 注册点 | [IL偏移] )
```

- **确定性**：同一份 DLL 服务器重启后重新注册，得到**完全相同的 UUID**（可持久化、可人工固定）。
- **四档粒度**（`UiIdGranularity`）：

| 粒度 | 一个信口对应 | 适用 |
| --- | --- | --- |
| `Assembly` | 一个插件 | 最省槽位，两插件即隔离 |
| `Type` | 一个类 | 同插件不同类也隔离 |
| **`Method`（默认）** | 一个方法 | 隔离度/槽位数平衡点 |
| `CallSite` | 一个调用点（方法+IL 偏移） | 最细，同一方法两处调用也各占信口 |

- **信口行为**：按文本去重 + 续期。插件"每秒重发"维持常驻 UI 时不会堆成多条，
  且不同 UiId 各自独立，内容相同也不会串到一起。

---

## 二、抢占最先头（0 前缀引导器）

| 步骤 | 实现 |
| --- | --- |
| **1. 优先加载** | 主 DLL 内**嵌套**引导器 DLL（嵌入资源），首次加载释放为 `plugins\global\`<br>`0HintChorus.Bootstrap.dll` —— **文件名以 0 开头** |
| **2. 提示与改写** | 引导器首次运行弹大号控制台横幅（正文：<br>「请重启服务器, 以便让底层启动」），参数 `Prompted` 由 **0 改写为 1**（此后不再提醒） |
| **3. 驱动辅助** | 下次重启后，引导器以 `0` 前缀 + `LoadPriority.Highest` **最先 Enable**，反射调用主 DLL 抢先安装入口 |

**三层保证，缺一不可：**

1. **枚举顺序** —— `EnablePlugins` 用 `OrderBy(Priority)`（稳定排序），同级保持枚举顺序，`0` 前缀排最前。
2. **时序前提** —— 已核实 LabAPI 源码：`LoadAllPlugins` 先对所有插件 `Assembly.Load`，之后才逐个 `Enable()`。所以引导器最先 Enable 时主 DLL 已在内存，反射调用成立。
3. **Harmony 硬优先级** —— 全部 8 个前缀标注 `[HarmonyPriority(Priority.First)]`（800，最高档）。**即使别人比我们更早打补丁，我们的前缀依然先执行。**

> 引导器用**反射**而非直接引用 —— LabAPI 用 `Assembly.Load(bytes)` 载入插件，
> 插件目录不在程序集探测路径上，直接引用会在运行时解析失败。

---

## 三、Harmony 补丁（16 个语义层落点 + 网络层哨兵的 4 个落点）

| # | 目标 | 作用 |
| --- | --- | --- |
| 1 | `HintDisplay.Show` | 提示条：按调用方归因到独立 UiId 信口 |
| 2 | `Broadcast.TargetAddElement` | 广播：单人入队（带 `BroadcastFlags`，AdminChat 直通） |
| 3 | `Broadcast.RpcAddElement` | 广播：全服入队 |
| 4 | `Broadcast.TargetClearElements` | 广播：阻止插件清空他人广播 |
| 5 | `Broadcast.RpcClearElements` | 广播：同上（全服） |
| 6 | `GameConsoleTransmission.SendToClient` | 玩家控制台：归因记账 + 可选节流（默认只记账） |
| 7 | `CassieAnnouncementDispatcher.AddToQueue` | CASSIE：归因记账（永远放行，队列本就不会互相顶掉） |
| 8 | `CassieAnnouncementDispatcher.ClearAll` | CASSIE：阻止插件清空他人播报 |
| 9 | `ServerSpecificSettingsSync.SendToAll` | **强力复写**：下发前拼回本核心设置项 |
| 10 | `ServerSpecificSettingsSync.SendToPlayersConditionally` | 强力复写：条件下发 |
| 11 | `ServerSpecificSettingsSync.SendToPlayer` | 强力复写：单人下发 |
| 12 | `ServerSpecificSettingsSync.SendToPlayer(hub, collection, versionOverride)` | **盖掉式复写**：改静态字段对它无效，直接 `ref` 盖掉传进来的集合 |
| 13 | `EncryptedChannelManager.TrySendMessageToClient(string, EncryptedChannel)` | **管理端聊天**：LabAPI 的 `Server.SendAdminChatMessage` 落点；归因记账 |
| 14 | `Hitmarker.SendHitmarkerDirectly(NetworkConnection, float)` | **准星命中标记**：只归因记账，从不拦截 |
| 15 | `IntercomDisplay.SerializeSyncVars(NetworkWriter, bool)` | **对讲机显示屏**：拦 Mirror 的同步序列化，下发前把值改回已放行的那份 |
| 16 | `AspectRatioSync.UserCode_CmdSetAspectRatio__Single(float)` | 玩家改宽高比后标脏，让合并结果按新宽高比重排 |

> 这 16 条已用 Harmony 自己的解析方式（`AccessTools.TypeByName` / `AccessTools.Method`）
> 对着真实游戏程序集逐条跑过一遍，全部命中；逐条证据见同目录 **`补丁目标核验报告_2026-10-04.txt`**。

> 每个补丁都带 `try/catch` + 放行原生：拦截层自身出任何问题，游戏的下发都不会被挡住。

### 网络层哨兵（可选，默认关闭）

语义层补丁只能挡住“按正常写法调用”的插件。任何插件都可以像 Exiled 的
`MirrorExtensions.SendFakeTargetRpc` 那样，自己拼一个 `RpcMessage` / `HintMessage`，
直接交给 `NetworkConnection.Send<T>` —— 那条路径**完全不经过**被补丁的方法。

把 `EnableNetworkSentinel` 设为 `true`，补丁就会挂到 Mirror 的**消息汇流点**上：

| 补丁目标 | 在这一层能看到什么 |
| --- | --- |
| `NetworkConnection.Send<HintMessage>` | 完整的 `Hint` 对象 —— 可判定“是不是绕过 `HintDisplay.Show` 的直发” |
| `NetworkConnection.Send<CassieTtsPayload>` | 播报载荷 —— 判定是否绕过播报队列 |
| `NetworkConnection.Send<RpcMessage>` | `netId / componentIndex / functionHash` —— 按组件定位到广播 |

这一层与语义层**互补而非重叠**：语义层被前缀吞掉的调用走不到这里；而伪造包只出现在这里。

> 默认 `false`。打开后仍默认**只观测不改写**（`NetworkSentinelIntercept: false`）；
> 打开 `NetworkSentinelIntercept` 才会把“绕过语义层的提示”收编进隔离信口。
> 默认关的理由是诚实的一刀：这一层在**每一条出站消息**上都有开销，且“是不是伪造”的判定要读调用栈。

### 构建质量：告警零容忍

两个工程的 `csproj` 都开了静态分析并设置 `TreatWarningsAsErrors`：

```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<AnalysisMode>Recommended</AnalysisMode>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

也就是说**任何一条新告警都会直接让构建失败**，而不是悄悄留在日志里。
确属“设计如此”的规则，必须用 `[SuppressMessage(..., Justification = "…")]` 写明理由才能通过 ——
目前只有三处：UiId 派生用 MD5（要的是确定性而非安全性）、引导器 DLL 内容比对用 MD5、以及 `IHintBroker.Stop()` 的命名。

### 框架兼容：LabAPI 与 EXILED 两套写法

插件作者写的不是游戏 API，而是**框架封装过的写法**。本底层同时吃下两套框架：

| 表面 | LabAPI 写法 | EXILED 写法 | 共同落点 |
| --- | --- | --- | --- |
| 提示条 | `Player.SendHint(text, duration)` ×3 | `Player.ShowHint(text, duration)` / `ShowHint(Hint)` | `HintDisplay.Show` |
| 广播 | `Player.SendBroadcast` / `Server.SendBroadcast` | `Player.Broadcast(duration, message, type, shouldClearPrevious)` / `Map.Broadcast` | `Broadcast.TargetAddElement` / `RpcAddElement` |
| 清广播 | `Player.ClearBroadcasts()` / `Server.ClearBroadcasts()` | `Player.ClearBroadcasts()` / `Map.ClearBroadcasts()` | `Broadcast.TargetClearElements` / `RpcClearElements` |
| 控制台 | `Player.SendConsoleMessage(message, color)` | 同左 | `GameConsoleTransmission.SendToClient` |
| 管理端聊天 | `Server.SendAdminChatMessage(message, isSilent)` | — | `EncryptedChannel.AdminChat` |
| CASSIE | `Announcer.Message(...)` / `Clear()` | `Cassie.Message(...)` / `Clear()` | `CassieAnnouncementDispatcher.*` |
| 设置面板 | `SSPlaintextSetting` / `SSTextArea` / … | `Exiled…Core.UserSettings.*`（最终也走原生 SSS） | `ServerSpecificSettingsSync` |
| 命中标记 | `Player.SendHitMarker(size)` | — | `Hitmarker.SendHitmarkerDirectly` |

**这张表不是文档，是可查询的事实** —— 它就编译在插件里（`FrameworkCompat.Routes`），
诊断报告会逐条打印，未覆盖的条目直接标成 `[盲区]`。

**已核实的两个"看着像缺口、其实不是"的点：**

1. **EXILED 的 CASSIE**：GitHub master 分支的 `Cassie.Message` 调 `RespawnEffectsController.PlayCassieAnnouncement`，
   但你服务器上这个游戏版本里 `RespawnEffectsController` 已经是**空壳**（只有 `AllControllers` 与 `Weaved()`）。
   本机实际部署的 EXILED 9.14.2 用的正是我们已覆盖的 `CassieAnnouncementDispatcher.AddToQueue`。
2. **EXILED 的 UserSettings**：它自己不搞第二套面板，最终还是落到原生 `ServerSpecificSettingsSync` —— 已被 SSS 覆盖。

### 最后一个补丁：SSS 注册强力复写（最硬的一道）

前几道防线都在**写数组时**生效；这一个把防线推到**下发的那一刻**。

即使某插件完全绕过本底层：

```csharp
ServerSpecificSettingsSync.DefinedSettings = 我的数组;   // 整体覆盖
ServerSpecificSettingsSync.SendToAll();                  // 直接下发
```

玩家收到的面板里**依然**含有本核心的设置项 —— 因为在数据真正发出去之前，补丁会把它们强行拼回数组。

> 只改数组、不调用下发，因此不会与自身形成递归。
> `SendToPlayer(hub, collection, versionOverride)` 走的是**盖掉式复写**：它吃的是显式集合参数、
> 压根不读 `DefinedSettings`，所以"改静态字段"那一招对它完全无效。
> 既然拦它的**执行**会破坏分页与按人推送的正常语义，就改成把它传进来的那份集合**盖掉** ——
> 下发前补进本核心的设置项，已经全在场就一行不动。EXILED 的 `UserSettings.SendToPlayer(player, settings)` 走的正是这条路。

### UI 表面覆盖（不会静默漏掉）

| 表面 | 入口 | 状态 |
| --- | --- | --- |
| **Hint** 提示条 | `HintDisplay.Show` | ✅ |
| **Broadcast** 屏幕广播 | `Broadcast.TargetAddElement / RpcAddElement / TargetClearElements / RpcClearElements` | ✅ |
| **Console** 玩家控制台 | `GameConsoleTransmission.SendToClient` | ✅ |
| **AdminChat** 管理端聊天面板 | `EncryptedChannelManager.EncryptedChannel.AdminChat` | ✅ |
| **Cassie** 语音播报 | `CassieAnnouncementDispatcher.AddToQueue / ClearAll` | ✅ |
| **HitMarker** 准星命中标记 | `Hitmarker.SendHitmarkerDirectly` | ✅ |
| **ServerSettings** 设置页 | `ServerSpecificSettingsSync` | ✅ |
| **Intercom** 对讲机显示屏 | `IntercomDisplay.Network_overrideText` | ✅ 底层复写 |

> **Intercom 是怎么拿下的**：它的写入口是 Mirror 的 `[SyncVar]` **裸字段**，写侧根本没有方法可以打补丁。
> 所以不在写侧拦 —— 改拦 **Mirror 把字段写进网络包的那一刻**（`IntercomDisplay.SerializeSyncVars`）。
> 前缀在下发前把值改回已放行的那一份，插件那次写入就永远到不了客户端。
> **不跟字段较劲，跟发包较劲。**

### 补丁自愈：被摘掉了会自己装回来

社区里已有框架会执行这样的代码：

```csharp
Harmony.Unpatch(typeof(HintDisplay).GetMethod("Show"), HarmonyPatchType.All, "*");
```

这一行**不区分调用方**，会把我们打在同一个方法上的前缀**一并铲掉** —— 而且不报错，
表现为“所有 UI 忽然又回到互相顶掉的状态”，排查起来极难。

`PatchGuard` 每 5 秒核对 12 个关键落点上是否还挂着**属于本底层**的前缀（靠 Harmony owner ID 认），
一旦丢失就让拦截层整体重装。它不争抢、不报复，只把自己补回去；自愈次数会打进诊断报告。

未接管的表面在 `UiIsolation.Surfaces` 里显式标记，**盲区是可见的**。

> 各表面的隔离语义并不相同，这是刻意的：
> 提示条是**单槽替换**（所以要合并渲染）；广播是**列表追加**（所以要节流 + 压缩时长）；
> 控制台是**追加日志**（不会互相顶掉，所以默认只归因不拦）；
> CASSIE 是**优先级队列**（入队本身不冲突，只有 `ClearAll` 会一锅端）。

---

## 四、分层架构

```
HintChorus/                      主工程
├── UiIsolation.cs                  统一门面(静态 + Service 单例)
├── HintChorusPlugin.cs          纯生命周期(Highest 优先级)
└── Core/
    ├── Identity/    UiId / UiIdRegistry / UiIdRecord / UiIdCatalog
    ├── Enums/       UiSurface / UiIdGranularity / HintChannelPriority /
    │                HintOrigin / NativeHintPolicy / HintAlignment
    ├── Interfaces/  IUiIsolation / IUiInterception / IHintChannel /
    │                IHintTextSource / IUiSlot / IHintBroker / IUiIdCatalog
    ├── Models/      UiSlot / HintChannel / HintEntry / UiScope /
    │                RegisterResult / HintStatistics
    ├── Broker/      HintBroker(合并渲染) / UiSlotRegistry(信口表)
    ├── Surfaces/    IUiSurfaceInterceptor / UiSurfaceRegistry /
    │                HintSurfaceIsolation / BroadcastSurfaceIsolation
    ├── Interception/ UiInterception(表面编排) / UiPatches /
    │                SssForceRewrite(强力复写) / PluginCallerResolver
    ├── Bootstrap/   BootstrapBridge / BootstrapInstaller / BootstrapConfigurator
    ├── ServerSpecific/ SssRegistry(端口隔离 + 防劫持 + 强力复写)
    ├── Utilities/   SafeEvents / HintFormat
    └── Diagnostics/ HintChorusDiagnostics

HintChorus.Bootstrap/            0 前缀引导器工程(被主工程嵌入并释放)
```

---

## 五、配置（`configs\<端口>\HintChorus\config.yml`）

> **键名是 snake_case，不是 PascalCase。** 插件用 YamlDotNet 的 UnderscoredNamingConvention，
> 所以 `EnableHintChorus` 这种写法会被当成"未知键"静默忽略。完整 66 项见同目录
> **`参考_默认config.yml`**（由编译产物实际序列化得到，键名与默认值可直接信赖）。

```yaml
# ── 合并渲染核心 ──
enable_hint_isolation: true
hint_refresh_interval: 0.75        # 合并刷新间隔(秒)
hint_resend_leeway: 0.15           # 续期提前量(秒), 防闪烁
hint_debug_log: false
suppress_unchanged_resend: true    # 内容没变就绝不重发
input_signature_enabled: true      # 输入摘要没变连字符串合成都跳过
layout_mode: rows                  # rows=空行占位 | offsets=算偏移(不占屏, 用 <voffset>) | compact=紧凑
offset_row_height: 40              # offsets 模式一行高度(voffset 单位, 整屏约 2140)
offset_sign: 1                     # offsets 模式方向(整体偏反了就改 -1)
offset_font_size: 20               # offsets 模式量宽度用的字号
volatile_rows: 3                   # 易变区(一次性提示+原生提示)固定行数
rows_per_plugin: 1                 # 同一插件最多几行: 1=一行 | 0=不限
persistent_rows_max: 8             # 常驻区最多补几行空位

# ── 动态 UIID 系统 / 拦截层 ──
intercept_third_party_ui: false    # 拦截层总开关(默认关)
auto_attribute_third_party_ui: true
ui_id_granularity: Method          # Assembly | Type | Method | CallSite
persist_ui_ids: true
native_hint_policy: Isolate        # Isolate=也并入隔离信口 | PassThrough=放行原生(会闪)

# ── 提示条表面 ──
intercept_hints: true
hint_slot_priority: 128
hint_show_labels: false
translate_native_hints: true               # 把游戏原生译文提示翻成纯文本并入复合体
native_hint_translation_language: zh       # zh | en
hint_max_entries_per_slot: 1
hint_max_entry_duration: 10

# ── 广播表面 ──
intercept_broadcasts: true
block_third_party_broadcast_clear: true
broadcast_slot_priority: 128
broadcast_show_labels: false
broadcast_max_entries_per_slot: 2
broadcast_max_entry_duration: 15
broadcast_repeat_interval: 1       # 重复广播两次放行之间的最小间隔(秒)
broadcast_repeat_duration: 1       # 重复广播被压到的时长上限(秒)

# ── 控制台 / CASSIE / 管理端聊天 / 命中标记（默认只归因记账，不拦内容） ──
intercept_console: true
console_repeat_interval: 0
intercept_cassie: true
block_third_party_cassie_clear: true
intercept_admin_chat: true
admin_chat_repeat_interval: 0
intercept_hit_marker: true

# ── 对讲机显示屏守卫 ──
enable_intercom_guard: true
intercom_throttle_interval: 0      # 0 = 只观测不改写；>0 才真的回滚

# ── 网络层哨兵（可选，默认关） ──
enable_network_sentinel: false     # 挂在 Mirror 的 NetworkConnection.Send<T> 汇流点
network_sentinel_intercept: false  # false = 只观测不改写

# ── 抢占最先头 / SSS 端口隔离 ──
enable_bootstrap_first_loader: true
enable_sss_registry: true
enable_sss_force_rewrite: true     # 下发前强力复写(最后一道保证)
protect_sss_from_overwrites: true  # 3 秒轮询守卫
```

> 说明：`hint_resend_leeway` 写成 `0.150000006` 是 float 0.15 的正常十进制展开，不是错值。

---
## 六、完整 API 写法大全

统一入口：`using HintChorus;`（契约在 `using HintChorus.Core.Interfaces;`）

### 6.1 拦截层控制

```csharp
// 总开关
UiIsolation.IsThirdPartyInterceptionEnabled;   // bool
UiIsolation.EnableThirdPartyInterception();    // 安装全部表面补丁
UiIsolation.DisableThirdPartyInterception();   // UnpatchAll, 零残留

// 细粒度(经契约实例)
IUiInterception it = UiIsolation.Interception;
it.AutoAttribute = true;                       // 是否按调用方自动归因
it.NativePolicy = NativeHintPolicy.Isolate;    // 游戏自身 UI: 并入隔离信口(默认) / 放行原生
it.Granularity = UiIdGranularity.Method;       // UiId 派生粒度
it.HintEnabled = true;                         // 提示条表面开关
it.BroadcastEnabled = true;                    // 广播表面开关
it.BlockThirdPartyBroadcastClear = true;       // 阻止插件清空他人广播
it.Install();  it.Uninstall();
it.InterceptedCount;  it.PassedThroughCount;
IReadOnlyList<string> plugins = it.AttributedPlugins;

// 具体类上的渲染参数(需强转)
UiInterception concrete = UiInterception.Instance;
concrete.HintSlotPriority = (byte)HintChannelPriority.High;
concrete.HintShowLabels = true;
concrete.HintMaxEntries = 4;
concrete.HintMaxDuration = 10f;
concrete.BroadcastSlotPriority = (byte)HintChannelPriority.Low;
concrete.BroadcastShowLabels = false;
concrete.BroadcastMaxEntries = 2;
concrete.BroadcastMaxDuration = 15f;
concrete.ApplyConfig();                        // 改完调一次生效
IReadOnlyList<IUiSurfaceInterceptor> surfaces = concrete.Surfaces;
```

### 6.2 通道（常驻提示）

```csharp
IHintChannel ch = UiIsolation.RegisterHintChannel(
    moduleId:     "MyPlugin.Hud",     // 全局唯一, 占用即拒绝
    displayName:  "HUD",
    text:         "初始内容",
    duration:     3f,
    priority:     (byte)HintChannelPriority.High);

ch.SetText("更新内容");                  // 下次刷新自动重发
ch.Enabled = false;                     // 单独停用本通道
ch.ShowLabel = true;                    // 行首显示 [HUD]
ch.Alignment = HintAlignment.Right;     // Left / Center / Right
ch.Duration = 5f;
ch.Priority = 100;
ch.ReceiverFilter = hub => hub.isLocalPlayer == false;  // 按原生 ReferenceHub 过滤
string id = ch.ModuleId;

UiIsolation.GetHintChannel("MyPlugin.Hud");    // 查询
UiIsolation.UnregisterHintChannel("MyPlugin.Hud");
UiIsolation.HintChannels;                      // 全部通道 ID
```

### 6.3 动态文本源（每玩家不同内容）

```csharp
public sealed class MyHud : IHintTextSource
{
    public string ModuleId => "MyPlugin.Hud.Dynamic";
    public string DisplayName => "状态";
    public byte Priority => (byte)HintChannelPriority.Low;

    public bool TryGetText(ReferenceHub hub, out string text)
    {
        text = $"HP {hub.playerStats.GetModule<HealthStat>().CurValue:F0}";
        return true;        // false = 该玩家不显示这一行
    }
}

UiIsolation.RegisterHintSource(new MyHud());
UiIsolation.UnregisterHintSource("MyPlugin.Hud.Dynamic");
UiIsolation.HintSources;
```

### 6.4 一次性提示

```csharp
UiIsolation.ShowTransient(hub, "<color=#32CD32>击杀 +1</color>", 3f);  // 原生类型
UiIsolation.ShowTransient(player, "LabAPI 包装类重载", 3f);

UiIsolation.Broker.ClearTransients(hub);      // 清某玩家
UiIsolation.Broker.Enabled = false;           // 暂停全部下发
UiIsolation.Broker.ReSort();                  // 改过 Priority 后重排
UiIsolation.Broker.IsRunning;                 // 是否已启动
```

### 6.5 SSS 端口（设置页控件）

```csharp
RegisterResult r = UiIsolation.RegisterSssPorts("MyPlugin", new ServerSpecificSettingBase[]
{
    new SSGroupHeader("我的插件"),
    new SSButton(41101, "治愈", "点我", 0.5f),
    new SSSliderSetting(41102, "数值", 0f, 100f, 50f, true),
});

r.Success;      // 是否成功
r.Conflicts;    // 失败时列出撞车的 SettingId
r.Message;

UiIsolation.SubscribeSssValue("MyPlugin", (hub, setting) =>
{
    if (setting.SettingId == 41101) { /* 你的玩法逻辑 */ }
});
UiIsolation.UnsubscribeSssValue(handler);

UiIsolation.SubscribeSssStatus("MyPlugin", (hub, status) => { /* 玩家开关设置页 */ });
UiIsolation.UnsubscribeSssStatus(handler);

UiIsolation.UnregisterSssPorts("MyPlugin");
UiIsolation.SssPorts;
```

### 6.6 SSS 注册强力复写

```csharp
UiIsolation.EnableSssForceRewrite();      // 安装下发前复写补丁
UiIsolation.DisableSssForceRewrite();
UiIsolation.IsSssForceRewriteEnabled;     // bool
UiIsolation.ForceRewriteSettings();       // 手动立即复写一次 → bool(是否真的改了)
UiIsolation.SssForceRewriteCount;         // 累计强制复写次数

// 更底层(直接操作注册核心)
SssRegistry.ForceMergeBeforeSend();       // 只改数组, 不下发
SssRegistry.ReAssert();                   // 合并数组并立即下发
SssRegistry.GuardianTick();               // 手动跑一次防劫持检查
```

### 6.7 原生 .NET 事件隔离

```csharp
// LabAPI 的 PlayerEvents/ServerEvents 自带隔离, 直接用; 原生 event 用这层包
SomeNativeEvent += UiIsolation.WrapEvent("MyPlugin", () => { });
SomeNativeEvent += UiIsolation.WrapEvent<int>("MyPlugin", n => { });
SomeNativeEvent += UiIsolation.WrapEvent<A, B>("MyPlugin", (a, b) => { });
SomeNativeEvent += UiIsolation.WrapEvent<A, B, C>("MyPlugin", (a, b, c) => { });
// 卸载时用同一个返回值做对称 -=
```

### 6.8 生命周期作用域（自动注销）

```csharp
using UiScope scope = UiIsolation.CreateScope("MyPlugin");

scope.RegisterChannel("MyPlugin.A", "A", "内容");        // 作用域内注册
scope.RegisterSource(new MyHud());
scope.RegisterSssPorts("MyPlugin", settings);
scope.SubscribeSssValue("MyPlugin", handler);

scope.ModuleId;  scope.Count;  scope.IsDisposed;
// 离开 using → 逆序自动全部注销, 不会漏、不会误清别人的
```

### 6.9 UiId 目录（谁、哪个注册点）

```csharp
IUiIdCatalog ids = UiIsolation.UiIds;

ids.Count;
IReadOnlyList<UiIdRecord> records = ids.Records;
// UiIdRecord: Id / ShortId / Surface / Plugin / Member /
//             Granularity / Hits / FirstSeenUtc / LastSeenUtc / Alias / MergeInto

ids.TryGet("A1B2C3D4", out UiIdRecord rec);
ids.SetAlias("A1B2C3D4", "我的HUD");       // 改显示名(不影响隔离)
ids.SetAlias("A1B2C3D4", null);            // 清除别名
ids.Merge("A1B2C3D4", "E5F6A7B8");         // 两个信口合并成一个槽位
ids.Unmerge("A1B2C3D4");
ids.FilePath;                              // uiids.yml 路径
```

### 6.10 信口与表面查询

```csharp
IReadOnlyList<IUiSlot> slots = UiIsolation.GetSlots();                         // 全部
UiIsolation.GetSlots(UiSurface.Broadcast);                                     // 按表面
UiIsolation.GetSlots(UiSurface.Hint, "FullModSquad");                          // 按插件
// IUiSlot: Id / Surface / SlotId / PluginId / DisplayName / Priority /
//          ShowLabel / EntryCount / TotalReceived / LastActivityUtc / RenderText()

UiIsolation.AttributedPlugins;                     // 被归因的插件清单
UiIsolation.UiIds.Records;                         // UiId 明细

foreach (UiSurfaceDescriptor s in UiIsolation.Surfaces)
{
    s.Surface;  s.DisplayName;  s.Implemented;  s.Handler;  s.EntryPoint;
}
UiSurfaceRegistry.Implemented;   // 已接管
UiSurfaceRegistry.Pending;       // 未接管的盲区
```

### 6.11 诊断

```csharp
UiIsolation.CreateDiagnosticsReport();   // 多行富文本完整报告
UiIsolation.CaptureStatistics();         // HintStatistics 快照
HintChorusDiagnostics.CreateUiIdReport();     // 只要 UiId 明细
HintChorusDiagnostics.CreateSurfaceReport();  // 只要表面覆盖
```

### 6.12 接口单例（依赖注入风格）

```csharp
IUiIsolation ui = UiIsolation.Service;   // 上面的静态方法在契约上都有对应成员
ui.Broker;  ui.Interception;  ui.GetSlots();  ui.UiIds;
```

### 6.13 枚举速查

| 枚举 | 取值 |
| --- | --- |
| `HintChannelPriority` | `Lowest=64` / `Low=96` / **`Medium=128`** / `High=160` / `Highest=192`（越小越靠上） |
| `HintAlignment` | `Left`（默认，不输出标签）/ `Center` / `Right` |
| `NativeHintPolicy` | `Isolate`（默认，原生提示也并入隔离信口）/ `PassThrough`（放行原生，会与合并结果交替闪烁） |
| `UiIdGranularity` | `Assembly` / `Type` / **`Method`** / `CallSite` |
| `UiSurface` | `Hint` / `Broadcast` / `Console`（预留）/ `Cassie`（预留）/ `ServerSettings` |
| `HintOrigin` | `Plugin` / `Attributed` / `Native` / `Transient` |

### 6.14 什么都没做也能生效（给存量插件）

配置里打开：

```yaml
InterceptThirdPartyUi: true
AutoAttributeThirdPartyUi: true
```

之后插件继续写 `player.SendHint(...)` / `hintDisplay.Show(...)` / `player.SendBroadcast(...)`，
**一行都不用改**，就会各自拿到独立 UiId 信口。用 `UiIsolation.AttributedPlugins` 查看归因结果。

### 6.15 引擎直连下发（绕开游戏包装组件）

不想走合并渲染、要自己直发时用这一组 —— 它们走的是**游戏自身最后那一次发包**，
所以即使某个插件把对应的包装组件禁用 / 替换了，这条路径依旧能上屏。

```csharp
using HintChorus;

// 提示条：绕开 HintDisplay 组件（仍尊重游戏的抑制表）
UiIsolation.SendHintDirect(hub, "文本", 3f);
UiIsolation.SendHintDirect(player, "文本", 3f);

// 玩家控制台（F8）：绕开 GameConsoleTransmission 组件
UiIsolation.SendConsoleDirect(hub, "文本", "green");
UiIsolation.SendConsoleDirect(player, "文本", "red");

// CASSIE 播报：绕开播报队列，不受队列里其它插件影响
UiIsolation.SendCassie("ATTENTION ALL PERSONNEL", "注意", playBackground: true);

// 屏幕广播：全服 / 只发给一个人
UiIsolation.SendBroadcastDirect("文本", 5);
UiIsolation.SendBroadcastDirect("文本", 5, hub);
```

> 直连意味着**不进隔离信口**，因此不会被本核心合并 —— 想和别的插件共存就用
> `RegisterHintChannel` 或原版 `SendHint`；确实要自己独占发一条，才用直连。

---

## 七、性能

| 项 | 做法 |
| --- | --- |
| 渲染单元缓存 | 注册/注销/新信口才重排，Tick 顺序遍历 → 每玩家 O(M) |
| 锁粒度 | 锁内只剪枝+快照；插件的 `TryGetText`/过滤/字符串构建全在锁外 |
| 瞬态队列 | `LinkedList` 按时间从头剪枝，摊还 O(1) |
| 归因开销 | 可信程序集**引用比较**；UiId 结果按调用点缓存 |
| 反射读取 | `TextHint.Text`(protected) 用**缓存委托** |
| 防自环 | 提示用 `_ownHints` 登记；广播用 `SelfSendDepth` 深度计数；SSS 用 `_selfSending` 标记 |
| 落盘 | `uiids.yml` 最多 10 秒一次 |

---

## 八、边界与如实说明

- **Hint 通道全局唯一是游戏硬限制**；本底层用"唯一写者 + 合并 + 每 UiId 一个信口"绕开。
- **归因依据是调用栈**：若插件通过共享公共库发 UI，会归因到那个库（仍能隔离，只是标签显示库名）。
- **Console / Cassie / AdminChat / HitMarker / Intercom 都已接管**（见第三节表 6~16），
  在 `UiIsolation.Surfaces` 里逐条列明入口；未接管的表面才会标成"预留/盲区"。
- 广播默认**阻止第三方清空广播**；若某插件依赖 `ClearBroadcasts()` 清自己的内容，把 `BlockThirdPartyBroadcastClear` 设为 false。
- 引导器**首次上线需要重启一次**才会生效（它是首次运行时释放的，这是设计的必然）。
- 拦截层默认**关闭**，关闭走 `UnpatchAll`，零残留。

## 九、排查（日志关键词）

- `已安装 HintDisplay.Show 拦截层` → 拦截层就位。
- `已在其它插件之前安装 UI 拦截补丁(顺序 #N)` → 引导器抢位成功。
- `已安装 SSS 强力复写补丁` → 最后一道防线就位。
- `检测到有插件整体覆盖了 ... DefinedSettings` → 守卫已自动合并恢复。
- `注册口 'xxx' 已存在` → moduleId 被占。
- `SettingId 冲突, 已拒绝注册` → SSS 端口撞车，改自己的 ID 段。
- `抛异常(已放行原生)` / `已隔离` → 拦截层自身或插件回调异常，已安全降级。
- `Hint 刷新异常: ... 刷新步骤 xxx 异常` → 合并刷新某一步出错，**只跳过该步**，其余照常。若同时还看到 `单个玩家渲染异常`，说明问题只出在那一个玩家/那一个信口上。
- `嵌套命中 N 次` → 游戏把某个下发入口转调到了另一个，SSS 三个补丁同时命中。正常恒为 `0`；持续增长就该重新核对补丁覆盖是否完整。
- `UiId 合并链成环 / 指向不存在的信口 / 超过 8 跳` → `uiids.yml` 里的 `MergeInto` 被改坏了，已自动回退到原信口。把指向改回正确的短 ID 即可。
