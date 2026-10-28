<!-- language tabs: keep this block at the very top so the switcher is visible on GitHub -->
## 语言 / Language

| 语言 | 入口 |
| --- | --- |
| **中文（主）** | 本页即为中文说明 ｜ 完整技术文档：[docs/zh/](docs/zh/) |
| English（辅） | [docs/en/README.md](docs/en/README.md) |
| Français | [docs/fr/README.md](docs/fr/README.md) |
| Русский | [docs/ru/README.md](docs/ru/README.md) |
| Español | [docs/es/README.md](docs/es/README.md) |
| **多语言 / Multilingual**（全部 22 种语言 + 翻译文件） | [docs/languages.md](docs/languages.md) |

---

# HintChorus

**SCP:SL 动态 UI 隔离底层** —— 让**互不相识的插件**在同一个屏幕上和平共处，谁也不用改写法。
(**SCP:SL dynamic UI isolation layer** — lets **plugins that know nothing about each other** share one screen peacefully, with zero code changes.)

> **关键词 / Keywords**：SCP:SL ｜ SCP Secret Laboratory ｜ 秘密实验室 ｜ LabAPI ｜ EXILED ｜ Hint ｜ HUD ｜ 提示条 ｜ 屏幕文本 ｜ 多插件共存 ｜ 提示互相顶掉 ｜ 提示错位 ｜ UI 隔离 ｜ hint framework ｜ hint isolation ｜ UI isolation ｜ plugin compatibility ｜ zero code changes

> (English keywords: SCP:SL | SCP Secret Laboratory | LabAPI | EXILED | hint | HUD | on-screen text | multi-plugin coexistence | hints overwriting each other | misplaced hints | UI isolation | hint framework | hint isolation | UI isolation | plugin compatibility | zero code changes)


> 一句话：游戏只给了一个「提示条」通道，谁后发谁覆盖。本插件把这个通道变成**多个互不干扰的信口**，
> 并且**按调用方自动归因** —— 插件照原来的样子写 `SendHint` / `SendBroadcast`，丢进去就能用。
> (In one line: the game exposes a single hint channel, and whoever writes last overwrites everyone else. This plugin turns that channel into **multiple isolated slots** and performs **per-caller attribution**, so plugins keep calling `SendHint` / `SendBroadcast` exactly as before.)

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)
![Platform](https://img.shields.io/badge/SCP%3ASL-LabAPI-blue)

---

## 下载 / Download

**只需下载一个文件**（主 DLL 会在首次运行时自己释放前置引导器，无需手动装第二个文件）：
(**Only one file to download** — the main DLL extracts its prerequisite bootstrap on first run, so there is no second file to install by hand.)

| 方式 (Method) | 链接 (Link) |
| --- | --- |
| 直接下载（仓库内，文件名带 `alpha` 前缀）<br>(Direct download, inside the repo, filename carries the `alpha` prefix) | [`alpha-HintChorus.dll`](alpha-HintChorus.dll) |
| 发行页（推荐，含版本说明）<br>(Releases page — recommended, includes release notes) | [alpha-v1.0.0](https://github.com/lll111III111/HintChorus/releases/latest) ｜ [全部发行 (All releases)](https://github.com/lll111III111/HintChorus/releases) |

> 文件名前面的 `alpha-` 只是**标记这是 alpha 阶段的产物**；插件内部的程序集名始终是 `HintChorus`，
> 所以改名不影响加载，也不影响引导器找它。
> (The leading `alpha-` only **marks this as an alpha-stage build**; the assembly name inside the plugin is always `HintChorus`, so renaming the file affects neither loading nor the bootstrap's ability to find it.)

安装：
(Installation:)

1. 把 `alpha-HintChorus.dll` 放进 `plugins/global/`
   (Put `alpha-HintChorus.dll` into `plugins/global/`)
2. 启动服务器（首次会自动释放 `0HintChorus.Bootstrap.dll` 并提示重启一次，这是引导器抢位的设计）
   (Start the server — on first run it automatically extracts `0HintChorus.Bootstrap.dll` and asks you to restart once; that is by design, so the bootstrap can grab the highest load priority)
3. 按需改 `configs/<端口>/HintChorus/config.yml`（**键名是 snake_case**，见 [`docs/default-config.yml`](docs/default-config.yml)）
   (Optionally edit `configs/<端口>/HintChorus/config.yml`, where `<端口>` is your server port — **keys are snake_case**, see [`docs/default-config.yml`](docs/default-config.yml))

---

## 它解决什么问题 (What problem it solves)

游戏只给所有插件**一个**提示条通道。结果是：
(The game gives all plugins exactly **one** hint channel. The result:)

- 插件 A 的 HUD 和插件 B 的击杀播报**互相顶掉**；
  (Plugin A's HUD and plugin B's kill feed **overwrite each other**.)
- 谁每秒重发，谁就把别人的 UI 刷没；
  (Any plugin that re-sends every second wipes out everyone else's UI.)
- 装得越多、越乱。
  (The more plugins you install, the messier it gets.)

HintChorus 的做法：
(What HintChorus does:)

1. **按调用方归因**：拦截提示条 / 广播 / 控制台 / CASSIE / 管理端聊天 / 命中标记六条通道的入口，
   沿调用栈认出**是哪个插件、哪个调用点**在发；
   (**Per-caller attribution**: it intercepts the entry points of six channels — hint bar / broadcast / console / CASSIE / admin chat / hit marker — and walks the call stack to identify **which plugin, and which call site**, is sending.)
2. **派生确定性 UiId**：`UUIDv5(表面 | 程序集 | 注册点 | [IL偏移])` —— 重启后仍是同一个 id，可落盘、可起别名；
   (**Deterministic UiId derivation**: `UUIDv5(surface | assembly | registration site | [IL offset])` — the same id survives a restart, so it can be persisted and aliased.)
3. **一个 UiId 一个信口**：合并渲染、按优先级稳定排序，再由**唯一写者**统一下发；
   (**One slot per UiId**: merged rendering, stable priority ordering, then a **single writer** emits everything.)
4. **排版稳定**：见下节。
   (**Stable layout**: see the next section.)

插件作者**不需要改任何代码**，也不需要引用本插件。
(Plugin authors **do not need to change any code**, and do not need to reference this plugin.)

---

## 排版：为什么位置不会乱跳 (Layout: why positions never jump)

这是本项目最核心的一处设计，也是"装很多插件后 UI 上下错位"的正解。
(This is the most central piece of design in this project, and the correct fix for "the UI drifts up and down once you install many plugins".)

提示块是**底部锚定**的，所以块高一变，里面的行就整体位移。本插件提供两种排版模式（`layout_mode`）：
(The hint block is **bottom-anchored**, so whenever its height changes, every line inside shifts with it. This plugin offers two layout modes (`layout_mode`):)

| 模式 (Mode) | 做法 (How it works) | 特点 (Trade-off) |
| --- | --- | --- |
| **`rows`（默认 / default）** | 易变区固定行数 + 常驻区每个归属预留一行，缺内容就**填空行**<br>(fixed row count for the volatile region + one reserved row per owner in the persistent region; missing content is **padded with empty rows**) | 位置绝对稳定；代价是占屏<br>(Absolutely stable; the cost is screen space) |
| **`offsets`** | 不给空行，每行用 `<voffset>` 修正量把它**摆到自己的固定槽位**<br>(no padding rows; each line is shifted with a `<voffset>` correction into **its own fixed slot**) | 位置同样稳定，**且不占屏**<br>(Equally stable, **and costs no extra screen space**) |
| `compact` | 什么都不补<br>(pads nothing) | 最省屏，位置会随内容增减而变<br>(Most screen-efficient; positions move as content grows or shrinks) |

`offsets` 的思路来自 **RueI（CC0）** 的"不是网格/行基，而是算偏移"；行高按"实际占几个视觉行"折算，
测量用本项目内嵌的文本度量表。**没有包含任何第三方代码**，详见 [`THIRD-PARTY.md`](THIRD-PARTY.md)。
(The `offsets` approach comes from **RueI (CC0)** and its idea of "compute offsets instead of using a grid / row base"; line height is derived from "how many visual lines it actually occupies", measured with the text metric tables embedded in this project. **No third-party code is included** — see [`THIRD-PARTY.md`](THIRD-PARTY.md).)

`offsets` 默认关闭，因为它有两个量需要按你的屏幕/字体微调：
(`offsets` is off by default because two of its values need fine-tuning for your screen and font:)

```yaml
layout_mode: rows        # 想省屏就改成 offsets
offset_row_height: 40    # 一行的高度(voffset 单位; 参考: 整屏约 2140)
offset_sign: 1           # 如果整块朝反方向偏了, 改成 -1
offset_font_size: 20     # 量文本宽度用的字号
```

上面注释的英文含义 (What the comments above mean)：

- `layout_mode`：`rows` 为默认；想省屏就改成 `offsets`
  (`rows` is the default; switch to `offsets` if you want to save screen space)
- `offset_row_height`：一行的高度，单位是 voffset（参考：整屏约 2140）
  (height of one row in voffset units; for reference, a full screen is about 2140)
- `offset_sign`：如果整块朝反方向偏了，改成 `-1`
  (if the whole block drifts in the opposite direction, set it to `-1`)
- `offset_font_size`：量文本宽度所用的字号
  (font size used when measuring text width)

---

## 位置：三类写法合一 (Position: three call styles, unified)

游戏只给一条提示口，所以「位置」最终都要折算成行级 `<voffset>`。本插件把来源统一到一套**九宫格锚点**上：
(The game exposes only one hint slot, so "position" always has to be converted into line-level `<voffset>`. This plugin unifies every source onto one **3×3 anchor grid**:)

| 来源 (Source) | 写法 (Syntax) | 开关 (Switch) |
| --- | --- | --- |
| **① 兼容原有写法**<br>(① Honor existing syntax) | 插件文本里已自带 `<voffset>` / `<pos>` / `<align>` / `<line-height>` 等标签 → **原样放行**，本底层不重排<br>(the plugin's own text already carries tags such as `<voffset>` / `<pos>` / `<align>` / `<line-height>` → **passed through untouched**; this layer does not re-layout it) | `honor_plugin_position_syntax` |
| **② 自有写法 · 文本标记**<br>(② Native syntax · text markers) | `{{hc:top-right}}` / `{{hc:pos=750,align=left,offset=-90}}`（解析后从文本剥离，玩家看不到）<br>(stripped out of the text after parsing, so players never see them) | `enable_position_markers` |
| **③ 自有写法 · C# API**<br>(③ Native syntax · C# API) | `UiIsolation.SetHintPosition("MyPlugin", "pos=750,align=left")`，或 `(…, HintAnchor.MiddleCenter, -90f)` —— **与文本标记同一套语法**<br>(or `(…, HintAnchor.MiddleCenter, -90f)` — **the same grammar as text markers**) | — |
| **④ 自动排版**<br>(④ Automatic layout) | 都没写时按**插件名**推断：`exp`/`level`/`经验`/`等级` → 屏幕中部再往下 90；`score`/`排行` → 右上；`kill`/`击杀` → 左上；`timer`/`倒计时` → 顶部居中；`team`/`队伍` → 中右；`music`/`点歌` → 右下<br>(when nothing is specified, it is inferred from the **plugin name**: `exp`/`level` → 90 units below the middle; `score`/ranking → top-right; `kill` → top-left; `timer`/countdown → top-center; `team` → middle-right; `music`/song requests → bottom-right) | `auto_layout_by_plugin_name` |

**解析优先级**：显式 API → 文本标记 → 自带位置标签 → 名称目录 → 默认（底部自然堆叠）。
(**Resolution priority**: explicit API → text markers → built-in position tags → name table → default (natural bottom stacking).)

> **自有写法完整规范**（三个维度：纵向锚点/标尺 + 横向对齐 + 微调；完整语法、别名表、示例、常见问题）
> 见 [`docs/position-spec.zh.txt`](docs/position-spec.zh.txt)。
> (**Full spec of the native syntax** — three dimensions: vertical anchor/ruler + horizontal alignment + fine-tuning, with the complete grammar, alias table, examples and FAQ — see [`docs/position-spec.zh.txt`](docs/position-spec.zh.txt).)

### 生态兼容对照（2026-10 于 GitHub 实搜并读其源码/文档） (Ecosystem compatibility check — searched on GitHub in Oct 2026, with their source and docs read)

| 项目 (Project) | 它的位置写法 (Its positioning syntax) | 我们的处理 (What we do) |
| --- | --- | --- |
| **HintServiceMeow** ★74 | `Hint.XCoordinate/YCoordinate`（画布单位，半宽 1200）+ `HintAlignment` / `HintVerticalAlign`，自行渲染 TMP 标签<br>(canvas units with a half-width of 1200, plus `HintAlignment` / `HintVerticalAlign`; renders TMP tags itself) | 登记为「自带位置体系」→ **原样放行**；其文本里的 `<voffset>` / `<pos>` / `<align>` 亦被自动识别<br>(registered as "brings its own positioning" → **passed through untouched**; `<voffset>` / `<pos>` / `<align>` inside its text is auto-detected too) |
| **RueI** ★24 | **0–1000 纵向标尺**（源码：`baseline = 755 − 2.14 × pos`）+ `VerticalAlign: Up/Center/Down`<br>(**0–1000 vertical scale** — from its source: `baseline = 755 − 2.14 × pos` — plus `VerticalAlign: Up/Center/Down`) | 同上；且我们自有写法**直接接受 0–1000 标尺**，数值可照抄<br>(same as above; our native syntax **accepts the 0–1000 scale directly**, so the numbers can be copied as-is) |
| **ruei-cm-lab** | 同一 0–1000 标尺（`offset = 700 − (1000 − pos) × 1.08`）+ 离屏哨兵行固定基线<br>(the same 0–1000 scale — `offset = 700 − (1000 − pos) × 1.08` — plus an off-screen sentinel line that fixes the baseline) | 同上<br>(same as above) |
| **UsefulHints** ★18 | 直接在配置里写原生 TMP 标签 `<align=left><size=28><color=…>`<br>(raw TMP tags such as `<align=left><size=28><color=…>` written straight into the config) | 识别 `<align>` 等位置标签 → **原样放行**<br>(position tags such as `<align>` are detected → **passed through untouched**) |
| 任何直写 `<voffset>` / `<pos>` / `<line-height>` / `<indent>` / `<margin>` / `<line-indent>` 的插件<br>(Any plugin writing `<voffset>` / `<pos>` / `<line-height>` / `<indent>` / `<margin>` / `<line-indent>` directly) | 原生 TMP<br>(raw TMP) | 一律 **原样放行**，位置一动不动<br>(always **passed through untouched**; not moved by a single pixel) |
| 纯样式标签（`<size>` / `<color>` / `<alpha>` / `<b>`…）<br>(Pure style tags: `<size>` / `<color>` / `<alpha>` / `<b>`, …) | 原生 TMP<br>(raw TMP) | **不算位置** —— 仍由本底层摆位，样式保留<br>(**not counted as positioning** — this layer still places it, and the styles are preserved) |

> 关键换算：RueI 的 `2.14 × 1000 = 2140`，正是本项目 `screen_height_units` 的默认值 —— 两套坐标天然对齐。
> (Key conversion: RueI's `2.14 × 1000 = 2140`, which is exactly the default of this project's `screen_height_units` — the two coordinate systems line up natively.)
>
> 对「自带位置」的文本，我们**连对齐也不改**（保持插件自己的 `<align>`），确保位置与观感一动不动。
> (For text that "brings its own positioning" we **do not even change the alignment** (the plugin's own `<align>` is kept), so both position and look stay exactly as they were.)

### 0–1000 纵向标尺（生态通用语言） (The 0–1000 vertical scale — the ecosystem's shared language)

自有写法同时支持这个标尺：`0 = 屏幕底`、`500 = 屏幕中`、`1000 = 屏幕顶` —— 从 RueI / ruei-cm-lab 迁移过来可以直接照抄数值：
(The native syntax also supports this scale: `0 = bottom of the screen`, `500 = middle`, `1000 = top` — when migrating from RueI / ruei-cm-lab you can copy the numbers straight over:)

```
{{hc:pos=750}}                ← 与 RueI 的 Scaled position 同义
{{hc:pos=500,offset=-90}}     ← 标尺定位, 再往下 90
```

> 上面两行的英文含义 (What the two lines above mean)：
> `pos=750` 等价于 RueI 的 Scaled position；`pos=500,offset=-90` 表示按标尺定位后再往下 90。
> (`pos=750` is equivalent to RueI's Scaled position; `pos=500,offset=-90` means: locate by the scale, then move 90 units down.)

锚点可写英文或中文：`top-left` / `top` / `top-right` / `middle-left` / `middle` / `middle-right` /
`bottom-left` / `bottom` / `bottom-right`，也接受 `tl` / `tr` / `mc` 等缩写与 `左上` / `中部` / `右下`。
(Anchors can be written in English or Chinese: `top-left` / `top` / `top-right` / `middle-left` / `middle` / `middle-right` / `bottom-left` / `bottom` / `bottom-right`; abbreviations such as `tl` / `tr` / `mc` and the Chinese forms `左上` / `中部` / `右下` are accepted as well.)

`offset` 以 **voffset 为单位、正数向上**（参考：整屏约 2140）。所以「屏幕中间再往下 90」就是
`{{hc:pos=middle,offset=-90}}`。
(`offset` is **in voffset units, positive is up** (for reference, a full screen is about 2140). So "90 units below the middle of the screen" is `{{hc:pos=middle,offset=-90}}`.)

服主可用 `position_overrides` 覆盖任何插件的落点，**无需改插件**：
(Server owners can override where any plugin lands with `position_overrides`, **without touching the plugin**:)

```yaml
position_overrides:
  MyPlugin: top-right
  LevelSystem: middle,offset=-90
```

> 保底不变：全部插件都在默认位置时，合成输出与旧版**逐字一致**；定位只在真的用到时介入。
> (Safe by default: when every plugin sits at its default position, the composed output is **word-for-word identical** to the old version; positioning only steps in when it is actually used.)

---

## 兼容 (Compatibility)

- **LabAPI**（原生）与 **EXILED** 两套写法都能吃下，且不需要插件改写法；
  (Both **LabAPI** (native) and **EXILED** call styles are supported, and plugins do not need to change how they call anything.)
- 覆盖的表面：提示条 / 屏幕广播 / 玩家控制台 / CASSIE 播报 / 管理端聊天 / 准星命中标记 / 设置页(SSS) / 对讲机显示屏；
  (Surfaces covered: hint bar / on-screen broadcast / player console / CASSIE announcements / admin chat / crosshair hit marker / settings page (SSS) / radio display.)
- 强优先级加载（`LoadPriority.Highest`）+ 0 前缀引导器，保证**先于其它插件**装好拦截；
  (Loads with `LoadPriority.Highest` plus a `0`-prefixed bootstrap, guaranteeing the interception is installed **before any other plugin**.)
- `FrameworkCompat` 会打印"写法 → 拦截落点"的对照表，未覆盖的项会**显式标成盲区**，不会静默漏掉。
  (`FrameworkCompat` prints a "call style → interception target" table, and anything not covered is **explicitly marked as a blind spot** rather than silently dropped.)

> 已知互斥：**同样自己给提示条打补丁的框架**（例如自行接管显示的 UI 框架）与本插件二选一，
> 两者都要独占同一条通道。若要共存，请在对方或本插件的配置里让出渲染权。
> (Known conflict: **frameworks that patch the hint bar themselves** (for example UI frameworks that take over rendering) are mutually exclusive with this plugin, because both need exclusive control of the same channel. To coexist, give up rendering rights in either their config or ours.)

---

## 文档 (Documentation)

| 文档 (Document) | 说明 (Description) |
| --- | --- |
| [`docs/zh/`](docs/zh/) | 中文文档（主）：深度说明、API 写法大全<br>(Chinese docs, the main set: in-depth explanations and a full API syntax reference) |
| [`docs/en/README.md`](docs/en/README.md) | English summary |
| [`docs/fr/README.md`](docs/fr/README.md) | Résumé en français |
| [`docs/ru/README.md`](docs/ru/README.md) | Краткое описание на русском |
| [`docs/es/README.md`](docs/es/README.md) | Resumen en español |
| [`docs/languages.md`](docs/languages.md) | **多语言总入口**：全部 22 种客户端语言 + 翻译文件下载<br>(**Multilingual hub**: all 22 client languages plus translation-file downloads) |
| [`docs/translations/`](docs/translations/) | 17 种外部语言翻译文件（放入服务器 `translations/` 目录即生效）<br>(translation files for 17 external languages — drop them into the server's `translations/` folder and they take effect) |
| [`docs/default-config.yml`](docs/default-config.yml) | **权威默认配置**（由编译产物实际序列化得到，66 项）<br>(**authoritative default config**, serialized from the actual build output, 66 entries) |
| [`docs/position-spec.*.txt`](docs/position-spec.zh.txt) | **自有位置写法规范 v1**（UN-5：中 / 英 / 法 / 俄 / 西各一份）：语法 / 两个通道 / 示例 / 优先级 / 生态对应<br>(**native position-syntax spec v1** (UN-5: one copy each in Chinese / English / French / Russian / Spanish): grammar / the two channels / examples / priority / ecosystem mapping) |
| [`docs/api-guide.zh.txt`](docs/api-guide.zh.txt) | 插件作者要看的 API 写法指南<br>(the API syntax guide plugin authors should read) |
| [`docs/patch-target-report.txt`](docs/patch-target-report.txt) | 拦截目标逐条核验报告<br>(item-by-item verification report of the interception targets) |
| [`CHANGELOG.md`](CHANGELOG.md) | 变更记录<br>(changelog) |

---

## 构建 (Building)

```bash
# 需要 .NET SDK 与 SCP:SL 专用服务器的 Managed 目录
dotnet build src/HintChorus.csproj -c Release -p:ManagedDir="<你的服务器>/SCPSL_Data/Managed"
```

> 上面的注释意为：需要 .NET SDK，以及 SCP:SL 专用服务器的 `Managed` 目录；`<你的服务器>` 替换成你的服务器根目录。
> (The comment above means: you need the .NET SDK and the `Managed` folder of a dedicated SCP:SL server; replace `<你的服务器>` with your server's root folder.)

引导器是独立工程（`bootstrap/`），其产物会被主工程**内嵌**并在运行时释放。
(The bootstrap is a separate project (`bootstrap/`); its output is **embedded** into the main project and extracted at runtime.)

### 验证补丁是否真装上（重要） (Verify the patches actually got installed — important)

**编译通过 ≠ 补丁装得上。** Harmony 按**参数名**把补丁参数绑定到目标参数，
签名或参数名不符会抛 `Patching exception in method ...`，运行时只记一行错误就过去了 ——
表现为"某个 UI 通道莫名失效"。2026-10 试机时广播补丁就这么挂掉过一次。
(**Compiling successfully ≠ the patch is installed.** Harmony binds patch arguments to target arguments **by parameter name**, so a mismatched signature or parameter name throws `Patching exception in method ...`; at runtime only a single error line is logged and execution moves on — which shows up as "some UI channel mysteriously stopped working". During a test run in Oct 2026 the broadcast patch failed exactly this way.)

```powershell
pwsh -File tools/verify-patches.ps1
# 输出: 每个补丁类 [OK]/[FAIL] + 最内层异常，失败时退出码 1
# 自动探测 Managed 目录，也可用 -Managed / 环境变量 SCPSL_MANAGED 指定
```

> 上面注释的英文含义 (What the comments above mean)：
> 输出为每个补丁类的 `[OK]` / `[FAIL]` 加上最内层异常，失败时退出码为 1；
> 脚本会自动探测 Managed 目录，也可以用 `-Managed` 参数或环境变量 `SCPSL_MANAGED` 指定。
> (Output shows `[OK]` / `[FAIL]` per patch class plus the innermost exception, with exit code 1 on failure; the script auto-detects the Managed folder, and you can also point it there with the `-Managed` argument or the `SCPSL_MANAGED` environment variable.)

---

## 许可 / License

## 相关项目 / Related

本项目与下列项目同处一个生态。它们**不是**本项目的依赖，链接在此仅为方便你对照不同思路：
(This project lives in the same ecosystem as the projects below. They are **not** dependencies of this project; the links are here only to help you compare different approaches:)

- [**RueI**](https://github.com/pawslee/RueI) —— CC0 的 hint 框架；本项目的 `offsets` 排版模式借鉴了它"不是行基、而是算偏移"的构思
  ([**RueI**](https://github.com/pawslee/RueI) — a CC0 hint framework; this project's `offsets` layout mode borrows its idea of "compute offsets rather than use a row base".)
- [**HintServiceMeow**](https://github.com/MeowServer/HintServiceMeow) —— MIT 的 hint 框架，按坐标放置每个提示
  ([**HintServiceMeow**](https://github.com/MeowServer/HintServiceMeow) — an MIT hint framework that places each hint by coordinates.)
- [**LabAPI**](https://github.com/northwood-studios/LabAPI) —— 本项目所依托的服务端插件 API
  ([**LabAPI**](https://github.com/northwood-studios/LabAPI) — the server-side plugin API this project is built on.)

> 与自行接管提示显示的框架（上面这类）**二选一**：两者都要独占同一条提示通道。
> (It is **either-or** with frameworks that take over hint rendering themselves (the kind listed above): both need exclusive control of the same hint channel.)

---


本项目以 **CC0-1.0** 释出（公有领域献出，无任何附加条件）：见 [`LICENSE`](LICENSE)。
(This project is released under **CC0-1.0** (a public-domain dedication with no conditions attached): see [`LICENSE`](LICENSE).)

涉及第三方内容与参考来源的部分**不在本项目的权利声明范围内**，单独列在 [`THIRD-PARTY.md`](THIRD-PARTY.md)。
(Portions involving third-party content and reference sources are **not covered by this project's rights claims** and are listed separately in [`THIRD-PARTY.md`](THIRD-PARTY.md).)
