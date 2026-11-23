# 变更记录 / Changelog

本项目已从 **alpha** 进入 **beta**：可以进入正常的生产服务器，但仍需**谨慎使用**；
一旦出现异常，请立即按本文末尾的「回退」步骤退回上一版本或关闭插件。

## beta 1.1.1 (2026-11-23)

### Release headline / 发布标题

**HintChorus Beta 1.1.1 — If another hint plugin jumps the queue, it gets kicked back out.**

- **Force takeover (default on)**: `HintDisplay.Show` now runs at `int.MaxValue` Harmony priority, with a postfix fallback. If another plugin's prefix swallows the call and our prefix never runs, HintChorus immediately re-sends its composite instead of disappearing.
- **Pre-override snapshots**: every third-party hint is recorded before it is absorbed or suppressed, so rollback can replay the exact data.
- **`Hint orvs` mainstream detection**: when no mainstream hint plugin is present, the command says so and offers option **1** = restore vanilla and replay the pre-override data. When mainstream plugins are present, it lists them by number; selecting one rolls that plugin back and hot-applies its last recorded data per player.
- New config: `ForceTakeover`. New commands: `Hint orvs native`, `Hint orvs all`, `Hint orvs reset`.
- Note: this intentionally outranks conflicting hint plugins. If it causes trouble, run `Hint orvs native`, or remove `HintChorus.dll`.

### 更新速览

- **强接管（默认开）**：`HintDisplay.Show` 提到 Harmony 最高优先级 `int.MaxValue`，并加 Postfix 兜底。别的插件的前缀把调用吞掉、导致我们的前缀没跑时，立即强制重发复合块，不再出现“UI 直接不显示”。
- **覆盖前快照**：第三方提示在吸收/压制之前先记录；回退时可以原样热应用回去。
- **`Hint orvs` 主流插件检测**：没有主流 Hint 插件时明确告诉服主，并提供编号 **1** = 整体退回原生 + 恢复覆盖前数据；有主流插件时按编号列出，选中后单独回退并把该插件最后一条数据热应用。
- 新配置：`ForceTakeover`；新命令：`Hint orvs native`、`Hint orvs all`、`Hint orvs reset`。
- 注意：这是**故意抢在冲突插件前面**的强接管。若反而出问题，用 `Hint orvs native` 退回，或直接移出/删除 `HintChorus.dll`。

## beta 1.1.0 (2026-11-21)

### Release headline / 发布标题

**HintChorus Beta 1.1.0 — “Wait, is it really entering beta?” … “Yep, it really is!”**

HintChorus is now in Beta. Usable on regular production servers, but keep an eye on it.

- Trimmed unnecessary hint-interception paths, and experimentally merged the game's native hints into the composite render.
- Compatibility fallback: run `Hint orvs` in the server console to return the installed hint framework(s) to the vanilla display, one by one (numbered selection when several are found).
- Detailed logging (off by default) records font size, screen position and owning plugin for every line; default logging keeps ordinary events and exceptions only.
- Fixed about **80%** of the known bugs; multiple hint plugins can coexist without conflicts.

Full bilingual release notes: [RELEASE_NOTES.md](RELEASE_NOTES.md)

### 更新速览

HintChorus 进入 Beta —— 可以应用于正常的生产服务器，但仍需观察使用。

- 本次更新**精简了不必要的 Hint 拦截路径**，并**尝试性地把游戏原版 Hint 融入合并渲染**（按目前观测，有较大概率成立）。
- **兼容兜底**：出现不兼容时，可在服务器后台控制台执行 `Hint orvs`，把已安装的 Hint 底层插件逐个退回原版显示。
- 新增**更详细的日志（默认关闭）**：详细记录每一条文字的字号、屏幕位置与所属插件。
- **默认日志**只记录普通事件与异常事件（崩溃检测）。
- 修复了约 **80%** 的已知问题。

> 出问题时：① 控制台 `Hint orvs` 退回可疑插件；② 或直接把 HintChorus 移出/删除后重启。

> 目标游戏版本：当前公开分支（14.2.x）。15.0「Megapatch III」正式发布后需重新核对 6 条 UI 通道的补丁落点。

### 新增 — 控制台回退 `Hint orvs`（大小写不敏感）

某个 Hint 插件与本底层不兼容时，**不用改配置文件、不用卸载插件**，在服务器后台控制台输入：

| 命令 | 作用 |
| --- | --- |
| `Hint orvs` | 列出已发现的 Hint 插件；只有 1 个时直接切回原生；多个时给编号并要求输入 |
| `Hint orvs <编号>` | 把该插件切回**游戏原版提示**（原生直通），其余插件不受影响 |
| `Hint orvs <编号> merge` | 把该插件恢复为合并渲染 |
| `Hint orvs all` | 全部切回原生 |
| `Hint orvs reset` | 全部恢复合并 |

- **支持多个 Hint 插件同时存在且互不冲突**：只有被选中的插件走原生，其它仍由合并层托管。
- 切换只影响本次运行，**不写配置文件**；重启服务器即恢复默认（全部合并）。
- 所有切换都会写入报告日志，便于事后追查。

### 新增 — 报告日志 / UI 位置日志（默认只开崩溃报告）

日志目录：`configs/<端口>/HintChorus/logs/`。

| 配置键 | 默认 | 说明 |
| --- | --- | --- |
| `EnableCrashReportLog` | **开** | 崩溃/异常报告：插件异常、补丁自愈失败、命令异常、启动报告，写入 `report-yyyyMMdd.log` |
| `EnableDetailedUiLog` | **关** | 详细 UI 日志：每个插件的提示内容、最终屏幕位置、每个信口占几行，写入 `ui-yyyyMMdd.log` |
| `DetailedUiLogPerPlayer` | **关** | 逐玩家记录最终合成文本（需同时打开 `EnableDetailedUiLog`），IO 开销最大 |

默认只保留报告日志（崩溃检测）；要看每个插件的文字运行情况，必须去配置文件里**显式打开**详细日志。

### 新增 — 新 Hint 写法（零配置，Beta 起可用）

不修改任何配置文件，一条调用即可独立发提示，自动与其它插件隔离：

```csharp
// 位置字符串（与 {{hc:...}} 同一套语法）
UiIsolation.SendHint("MyPlugin", "你好", 3f, "top-right");

// 九宫格锚点
UiIsolation.SendHint("MyPlugin", "你好", HintAnchor.MiddleCenter, -90f, 3f);

// 清除
UiIsolation.ClearHint("MyPlugin");
```

下一版本会继续沿用这套写法，并把它作为推荐入口；旧写法（`RegisterHintChannel` / `SetHintPosition` / 文本标记 `{{hc:...}}`）保持兼容。

### 删除 / 弃用

- **删除**：`HintBroker.GlobalEffects`（从未被赋值、始终为 `null`，属于遗留占位）。
- **弃用**：`layout_mode: compact` —— 功能仍在，但位置会随内容增减而漂移，后续版本将移除；请改用 `rows` 或 `offsets`。
- **保留**：`HintIsolation.Shim` 兼容垫片（依赖旧程序集名的插件重编后可删）。
- **命名**：发行文件名不再使用 `alpha-` 前缀，改由版本号与发行页标注阶段。

### 回退（出问题时立即执行）

1. 服务器控制台输入 `Hint orvs`，把可疑插件切回原生；或
2. 把 `config.yml` 的 `InterceptThirdPartyUi` 改为 `false` 并重启；或
3. 直接停用/删除 `HintChorus.dll` 与 `0HintChorus.Bootstrap.dll` 后重启，恢复到未安装状态。

### 已知限制

- 详细 UI 日志会周期性写盘，**只建议排障时开启**。
- `Hint orvs` 的回退状态不持久化（重启重置），避免配置被意外写坏。
- 15.0 正式版尚未发布；私测 Beta 期间不保证补丁落点稳定。

### 兼容垫片：HintIsolation.dll（让依赖旧名的插件不必重编）

- **背景**：改名后，凡是**编译时引用了旧程序集名**的插件会整只加载失败 —— 本机 FullModSquad 即如此
  （报 Missing dependencies: HintIsolation v1.0.0.0）。**仅重启不会恢复**。
- **新增** compat/HintIsolation.Shim：产出程序集名仍为 HintIsolation, Version=1.0.0.0 的小垫片，
  只包含依赖方实际用到的 3 个类型（HintIsolation.UiIsolation 的 RegisterHintSource / UnregisterHintSource、
  HintIsolation.Core.Interfaces.IHintTextSource、HintIsolation.Core.Enums.HintAlignment），
  **全部转发到 HintChorus**，自身不含任何逻辑。
- **部署位置**：LabAPI\dependencies\global\HintIsolation.dll（依赖解析目录，不会被当成插件加载）。
- **验证**：本地按加载器同样的解析路径模拟，FullModSquad.GetTypes() 通过（40 个类型）。
- **何时可删**：依赖方改为引用 HintChorus 并重编后，垫片与 compat/ 目录可一并移除。

## alpha (2026-10-07 · 试机修复 + 位置锁定)

### 修复
- **广播补丁装不上**（试机暴露）：Harmony 按**参数名**绑定补丁参数，目标参数名是 data / 	ime / lags，
  而补丁里写成了 message / duration / 	ype → Parameter "message" not found in method ...
  → 两个广播补丁**完全装不上**、自愈 3 次后放弃、广播拦截整体失效。已按真实参数名修正。
- 本地逐个安装验证：**16/16 补丁类全部成功**（修复前 14/16）。

### 变更 — 位置锁定
- **位置一旦固定即锁定**：归属首次出现时分配一个**锁定行号**，此后永不重排 ——
  别的插件出现/消失/内容增减，都不会推动已经落位的行。
- 同一位置区内改为按**固定行高依次占位**，不再按内容高度浮动（不再"叠加"）。
- 摆放只按锁定行号计算，**基准只锚定该区的第 0 行**，与该区当前有多少行无关。
- 未声明位置的插件不参与锁定摆放，仍走底部自然堆叠 —— 既有观感不变。
- HintBroker.Stop() 时复位锁定状态。

### 新增
- 	ools/verify-patches.ps1：本地加载游戏程序集 + 0Harmony，逐个安装补丁并打印最内层异常，失败退出码 1。
  "编译通过但装不上"这类问题编译期完全看不出来，现固化为可重复检查。

### 文档
- docs/position-spec.{zh,en,fr,ru,es}.txt 各补「位置锁定」一节（UN-5 同步）。

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