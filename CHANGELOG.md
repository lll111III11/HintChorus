# 变更记录 / Changelog

本项目处于 **alpha** 阶段，接口与行为仍可能调整。

## alpha (2026-10-07)

### 更名

- **项目更名为 `HintChorus`**（原名 `HintIsolation` 过于通用、检索易与其它项目混淆）。
  程序集名、根命名空间、引导器（`0HintChorus.Bootstrap.dll`）、配置目录
  （`configs/<端口>/HintChorus/`）、资源逻辑名与 Harmony ID 全部随动。
  **升级提示**：旧的 `HintIsolation.dll` / `0HintIsolation.Bootstrap.dll` 请从 `plugins/global/` 删除，
  旧配置目录 `configs/<端口>/HintIsolation/` 可改名或按新默认重新生成。

### 新增 — 屏幕位置体系（三类写法合一）

- **① 兼容原有写法**：插件文本里已自带位置标签
  （`<voffset>` / `<pos>` / `<align>` / `<line-height>` / `<margin>` / `<indent>`）时，
  判定为「插件自己摆了位」→ **原样放行、不被本底层重排**。开关 `honor_plugin_position_syntax`。
- **② 自有写法（双通道）**：
  - **文本标记**：`{{hc:top-right}}` 或 `{{hc:pos=middle,offset=-90}}` —— 解析后从文本剥离，
    玩家看不到；与其它框架的 `{0}` 模板不冲突。开关 `enable_position_markers`。
  - **C# API**：`UiIsolation.SetHintPosition("MyPlugin", HintAnchor.MiddleCenter, -90f)` /
    `UiIsolation.ClearHintPosition("MyPlugin")`。可在插件加载阶段先声明（信口尚未创建也生效）。
- **③ 自动排版**：未声明位置的插件按**名称**查「已收录表 → 功能区关键词」推断落点，
  例：`exp` / `level` / `rank` / `经验` / `等级` → **屏幕中部再往下 90**；
  `score` / `board` / `排行` → 右上；`kill` / `feed` / `击杀` → 左上；
  `timer` / `倒计时` → 顶部居中；`team` / `队伍` → 中右；`music` / `点歌` → 右下。
  开关 `auto_layout_by_plugin_name`；可用 `position_overrides` 覆盖。

### 生态兼容（2026-10 于 GitHub 实搜并阅读其源码/文档）

- 把生态内框架的**位置写法**接入为兼容对象——从这些框架出来的 UI，位置一动不动：

| 项目 | 其位置写法 | 处理 |
| --- | --- | --- |
| HintServiceMeow ★74 | `Hint.XCoordinate/YCoordinate`（画布单位，半宽 1200）+ `HintAlignment` / `HintVerticalAlign` | 登记「自带位置体系」→ **原样放行** |
| RueI ★24 | **0–1000 纵向标尺**（源码：`baseline = 755 − 2.14 × pos`）+ `VerticalAlign: Up/Center/Down` | 原样放行；自有写法接受同款标尺 |
| ruei-cm-lab | 同一 0–1000 标尺（`offset = 700 − (1000 − pos) × 1.08`）+ 离屏哨兵行固定基线 | 原样放行 |
| UsefulHints ★18 | 配置里直接写原生 TMP 标签 `<align=left><size=28>` | 识别位置标签 → 原样放行 |

- **新增 0–1000 纵向标尺**：自有写法 `{{hc:pos=750}}` / `{{hc:y=500,offset=-90}}` 与 RueI 的
  `Scaled position` 同义（`2.14 × 1000 = 2140`，正是 `screen_height_units` 默认值），迁移数值可照抄。
- 外来位置标签识别名单对齐 HintServiceMeow 标签白名单中的**位置类**标签
  （`voffset` / `pos` / `align` / `line-height` / `line-indent` / `indent` / `margin`）；
  `size` / `color` / `alpha` 等**纯样式**标签不算位置，仍由本底层摆位。
- 对「自带位置」的文本**连对齐也不覆盖**（保持插件自己的 `<align>`）。

### 自有写法 v1（文本标记与 C# API 统一为一套语法）

- **统一语法**：文本标记 `{{hc:...}}` 与 C# `UiIsolation.SetHintPosition(id, "…")` 解析**同一段字符串**，
  插件配置项也可直接存该字符串。解析收敛到 `HintPosition.TryParse` 一处实现。
- **补齐横向维度**：位置由三个维度描述 ——
  **纵向**（九宫格锚点 **或** 0–1000 标尺）、**横向**（`align=left|center|right`，省略则由锚点列推导）、
  **微调**（`offset`，正 = 上移）。
- 新增 C# 字符串重载 `UiIsolation.SetHintPosition(pluginId, string spec)`（无法识别返回 `-1`）；
  `IUiSlot` 暴露只读 `Position` 供插件自检落点。
- **新增规范文档 [`docs/position-spec.zh.txt`](docs/position-spec.zh.txt)**：
  语法表、别名表、示例、解析优先级、配置项、与 RueI / HSM 的对应关系、常见问题。

### 文档 UN-5 语言适配

- **位置写法规范 v1 一次出齐 5 份（UN-5 语言）**：`docs/position-spec.{zh,en,fr,ru,es}.txt`
  —— 语法表、两个通道、示例、解析优先级、配置项、生态对应、FAQ。
- 各语言 README 增补「位置」章节，并链到**对应语言**的规范：
  `docs/en` / `docs/fr` / `docs/ru` / `docs/es`（中文主 README 已有）。
- [`docs/languages.md`](docs/languages.md) 增加「位置写法规范」索引（5 语言）。
- **修复**：`docs/es/README.md` 自加入仓库起一直是 **0 字节**（当初目录嵌套失误，内容落在了
  `docs/es/es/`）。本次补全西班牙语 README。

### 修复：广播补丁装不上（试机暴露）

- **现象**：试机日志报 `安装表面补丁 BroadcastTargetAddElementPatch 失败: Patching exception in method ...`，
  两个广播补丁都装不上 → 自愈每 5s 重试、连续 3 次失败后放弃 → **广播拦截整体失效**。
- **根因**（本地复现取证）：Harmony 是**按参数名**把补丁参数绑定到目标参数的。目标参数名是
  `data` / `time` / `flags`，而补丁里写成了 `message` / `duration` / `type` →
  内层异常 `Parameter "message" not found in method ...`。签名类型其实是对的，是**名字**不匹配。
- **修复**：两个广播补丁的参数名改回与游戏一致，并在源码注释里写死这条约束。

### 新增：`tools/verify-patches.ps1`（防回归）

- 本地加载游戏程序集 + 0Harmony，对插件里每个 `[HarmonyPatch]` 类逐个执行
  `CreateClassProcessor(...).Patch()`，打印最内层异常；失败时退出码 1。
- 同类问题**编译期完全看不出来**，只有真装一次才会暴露 —— 现固化为可重复执行的检查。

### 实现要点

- 新增 `HintPosition`（九宫格锚点 + 偏移 + 自定位标记）、`PositionSyntax`（标记与外来标签解析）、
  `PluginPositionCatalog`（已收录表 + 功能区推断 + 服主覆盖表）。
- `HintBroker` 改为**多锚点分区合成**：按「档位（底/中/顶）+ 偏移」分区，各自换算绝对位置，
  逐行给 `<voffset>`；**底部区永远排在合成串末尾**，因此「全部默认位置」时的输出与旧版**逐字一致**。
- 新增配置：`honor_plugin_position_syntax` / `auto_layout_by_plugin_name` / `enable_position_markers` /
  `screen_height_units` / `position_overrides`。
- 位置解析优先级：**显式 API → 文本标记 → 自带位置标签 → 名称目录 → 默认**。

## alpha (2026-10-05)

### 修复

- **广播补丁签名错误（致命）**：`TargetAddElement(NetworkConnection, string, ushort, BroadcastFlags)` 首参是
  连接、`duration` 是普通 `ushort`（非 ref）。早期补丁写成 `ref ushort time`，签名不匹配导致补丁**静默挂不上**、
  广播拦截整体失效。现按真实签名重写；节流改为"间隔内纯抑制"（非 ref 参数无法压短时长）。
- **SSS `SendToPlayer(hub, collection, version)` 补丁挂不上（致命）**：该重载的 `collection` 是**非 ref 参数**，
  前缀即使声明成 `ref` 也会因签名不匹配静默失效。改为 **Transpiler**（`ldarg.1` → 合并结果），
  EXILED 的 `UserSettings.SendToPlayer(player, settings)` 从此也被覆盖。
- **瞬态提示过期不清理**：瞬态链表按插入顺序排列而 `Until` 由各自时长决定，"后插入更短命"完全可能；
  只看队首剪枝会把短命提示压在长命提示后面一直留着。改为**整链扫描剔除**。
- **`_leeway` 小于整轮刷新周期导致静态内容空屏**：`_leeway` 改为 `Max(_refresh, resendLeeway)`，
  保证续期提前量至少覆盖一个整轮周期。
- **折叠判死误用 `MinRemaining`**：取所有条目（含已过期）的最小值会把"部分过期"的信口误判成空信口
  而退出折叠，让较新内容被同插件另一信口顶掉。改为按 `AliveCount` 判定。
- **同帧归因污染**：归因结果按帧号缓存会把同帧第二个插件的 UI 并入第一个插件的信口（反向还把
  游戏原生提示误判成插件提示）。改为**逐次调用独立解析**，Native 结果不缓存。
- **SSS `Terminate` 残留孤儿设置项**：早期先 `RewriteArrayLocked`（剔除自己+加回自己=原样写回）再清空
  Modules，卸载后设置项残留并越攒越多。改为**只剔除不加回**（`RemoveAllLocked`）。
- **SSS 回调未按模块归属过滤**：插件 A 会收到插件 B 的 setting，只能靠自己在回调里二次过滤。
  现按 `IsSettingOwnedBy` 过滤，订阅者只收到自己注册口的变化。
- **SSS `ForceMergeBeforeSend` 短路恒为假**：`keep.Count == current.Length` 与"全部在场"互斥，
  导致每次下发都触发整数组重建。短路条件只保留"全部在场"。
- **信任名单漏 RueI**：RueI 的调用被当成未知插件归因。`TrustedNames` 补 `RueI`。
- **`Disable()` 未卸载 AssemblyResolve**：热重载后残留解析器。补 `RuntimeHome.UninstallResolver()`。

### 新增 / 变更

- **原生提示多语言自适应（玩家级）**：内嵌 **5 种联合国常用语**（`en` / `zh` / `fr` / `ru` / `es`）——
  开箱即用；其余客户端语言从自宿主 `translations\` 目录加载（GitHub 仓库
  [`docs/translations/`](docs/translations/) 提供全部 **17 种**外部语言文件，下载放入服务器即生效）。
  每个玩家按 `ReferenceHub.playerPreferences.Language` **自动取自己的语言**（反射探测一次并缓存委托，
  失败回退配置默认 `native_hint_language`）；繁体回退简体、逐条回退英文，绝不输出半成品。
- 语言归一化：`zh_Hans` / `zh_Hans-2` / `zh_Flash_Hans` → `zh`；`zh_Hant` 独立键（外部可精确提供繁体）。
- 多语言文档入口：`docs/languages.md`（22 种语言总表 + 翻译文件下载），FR / RU / ES 全译 README。

### 验证

- 仓库工程 `src/` 0 警告 0 错误（含 layout 排版 `rows` / `offsets` / `compact`）。

## alpha (2026-10-04)

### 修复

- **广播内容会被永久丢弃**：被节流的重复广播此前只写入"广播信口"且原下发被取消，而广播信口并不参与合并渲染
  → 玩家什么都看不到。现改为**压短时长后照样放行**（符合"列表追加 → 节流 + 压缩时长"的设计语义）。
- **同一帧内不同插件被归成同一来源**：归因结果此前按帧号做缓存，导致同帧内第二个插件的 UI 被并入第一个插件的信口
  （反向还会把游戏原生提示误判成插件提示）。现改为**逐次调用独立解析**。
- **补丁自愈有两处盲区**：目标改名/消失时被静默跳过，诊断永远显示"自愈 0 次"；且失败后每 5 秒无退避地重装补丁。
  现区分上报"类型/方法不存在"（标为严重）并为自愈加入失败计数与上限。
- **`uiids.yml` 在 UI 活跃期间被反复整份重写**（每 10 秒，且写盘期间持有全局锁；非原子写有截断风险）。
  现拆分为"结构变更"与"命中统计"两种落盘频率，并改为**临时文件 + 原子替换**。
- **持全局锁执行全服下发**：SSS 设置下发被移出锁（锁内只合并数组）。
- **一次性提示被长命提示压住后不会消失**：瞬态队列改为整链剪枝。
- **自发送守卫用共享非原子标志**：并发下可能永久失效 → 改为线程局部（`[ThreadStatic]`）。
- **两处空 `catch`** 会让对讲机守卫/命中标记归因静默变成空操作 → 改为限流上报。
- **`FrameworkCompat.Detect` 不认 `RueI`**：枚举里存在但检测未登记，会被判成 `Unknown`。

### 新增 / 变更

- **排版模式 `layout_mode`：`rows` / `offsets` / `compact`**（`offsets` 见下），取代原先的布尔开关。
- **`offsets` 排版模式**：每行按槽位计算固定目标位置并用 `<voffset>` 修正，缺席的槽位既不占屏也不推动别人。
- **放行时"让位"**：所有无法吸收的提示放行路径都会调用 `HoldSlot`，把"被原生顶掉后不回来"的窗口
  从最久约 2.35 秒压缩到约 40 毫秒。
- **空文本提示直接吞掉**（此前放行会把合并块清成空白块）。
- 诊断报告新增"补丁落点已找不到目标"与"自愈连续失败"两项。
- `layout_mode` 等新键刻意使用**字符串 + 容错解析**：配置里的枚举一旦拼错，加载器会丢弃整份配置回落默认值。

### 验证

- 排版不变量：`rows` 模式 18 项、`offsets` 模式 11 项断言（离线可重跑）。
- 拦截目标：16 个目标逐条核验通过（见 `docs/patch-target-report.txt`）。
- 默认配置：66 项由编译产物实际序列化得到（`docs/default-config.yml`）。

### 已知限制

- 允许被放行的提示仍会短暂接管提示口（单通道的物理上限）；已把窗口压到约 40 毫秒。
- 预留行/槽位的总量上限同时限制"位置稳定性"：归属数超过上限后，稳定性会退化。
- 与自行接管提示显示的框架互斥（见 README）。