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
> **关键词 / Keywords**：SCP:SL ｜ SCP Secret Laboratory ｜ 秘密实验室 ｜ LabAPI ｜ EXILED ｜ Hint ｜ HUD ｜ 提示条 ｜ 屏幕文本 ｜ 多插件共存 ｜ 提示互相顶掉 ｜ 提示错位 ｜ UI 隔离 ｜ hint framework ｜ hint isolation ｜ UI isolation ｜ plugin compatibility ｜ zero code changes


> 一句话：游戏只给了一个「提示条」通道，谁后发谁覆盖。本插件把这个通道变成**多个互不干扰的信口**，
> 并且**按调用方自动归因** —— 插件照原来的样子写 `SendHint` / `SendBroadcast`，丢进去就能用。

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)
![Platform](https://img.shields.io/badge/SCP%3ASL-LabAPI-blue)

---

## 下载 / Download

**只需下载一个文件**（主 DLL 会在首次运行时自己释放前置引导器，无需手动装第二个文件）：

| 方式 | 链接 |
| --- | --- |
| 直接下载（仓库内，文件名带 `alpha` 前缀） | [`alpha-HintChorus.dll`](alpha-HintChorus.dll) |
| 发行页（推荐，含版本说明） | [alpha-v1.0.0](https://github.com/lll111III111/HintChorus/releases/latest) ｜ [全部发行](https://github.com/lll111III111/HintChorus/releases) |

> 文件名前面的 `alpha-` 只是**标记这是 alpha 阶段的产物**；插件内部的程序集名始终是 `HintChorus`，
> 所以改名不影响加载，也不影响引导器找它。

安装：

1. 把 `alpha-HintChorus.dll` 放进 `plugins/global/`
2. 启动服务器（首次会自动释放 `0HintChorus.Bootstrap.dll` 并提示重启一次，这是引导器抢位的设计）
3. 按需改 `configs/<端口>/HintChorus/config.yml`（**键名是 snake_case**，见 [`docs/default-config.yml`](docs/default-config.yml)）

---

## 它解决什么问题

游戏只给所有插件**一个**提示条通道。结果是：

- 插件 A 的 HUD 和插件 B 的击杀播报**互相顶掉**；
- 谁每秒重发，谁就把别人的 UI 刷没；
- 装得越多、越乱。

HintChorus 的做法：

1. **按调用方归因**：拦截提示条 / 广播 / 控制台 / CASSIE / 管理端聊天 / 命中标记六条通道的入口，
   沿调用栈认出**是哪个插件、哪个调用点**在发；
2. **派生确定性 UiId**：`UUIDv5(表面 | 程序集 | 注册点 | [IL偏移])` —— 重启后仍是同一个 id，可落盘、可起别名；
3. **一个 UiId 一个信口**：合并渲染、按优先级稳定排序，再由**唯一写者**统一下发；
4. **排版稳定**：见下节。

插件作者**不需要改任何代码**，也不需要引用本插件。

---

## 排版：为什么位置不会乱跳

这是本项目最核心的一处设计，也是"装很多插件后 UI 上下错位"的正解。

提示块是**底部锚定**的，所以块高一变，里面的行就整体位移。本插件提供两种排版模式（`layout_mode`）：

| 模式 | 做法 | 特点 |
| --- | --- | --- |
| **`rows`（默认）** | 易变区固定行数 + 常驻区每个归属预留一行，缺内容就**填空行** | 位置绝对稳定；代价是占屏 |
| **`offsets`** | 不给空行，每行用 `<voffset>` 修正量把它**摆到自己的固定槽位** | 位置同样稳定，**且不占屏** |
| `compact` | 什么都不补 | 最省屏，位置会随内容增减而变 |

`offsets` 的思路来自 **RueI（CC0）** 的"不是网格/行基，而是算偏移"；行高按"实际占几个视觉行"折算，
测量用本项目内嵌的文本度量表。**没有包含任何第三方代码**，详见 [`THIRD-PARTY.md`](THIRD-PARTY.md)。

`offsets` 默认关闭，因为它有两个量需要按你的屏幕/字体微调：

```yaml
layout_mode: rows        # 想省屏就改成 offsets
offset_row_height: 40    # 一行的高度(voffset 单位; 参考: 整屏约 2140)
offset_sign: 1           # 如果整块朝反方向偏了, 改成 -1
offset_font_size: 20     # 量文本宽度用的字号
```

---

## 位置：三类写法合一

游戏只给一条提示口，所以「位置」最终都要折算成行级 `<voffset>`。本插件把来源统一到一套**九宫格锚点**上：

| 来源 | 写法 | 开关 |
| --- | --- | --- |
| **① 兼容原有写法** | 插件文本里已自带 `<voffset>` / `<pos>` / `<align>` / `<line-height>` 等标签 → **原样放行**，本底层不重排 | `honor_plugin_position_syntax` |
| **② 自有写法 · 文本标记** | `{{hc:top-right}}` 或 `{{hc:pos=middle,offset=-90}}`（解析后从文本剥离，玩家看不到） | `enable_position_markers` |
| **③ 自有写法 · C# API** | `UiIsolation.SetHintPosition("MyPlugin", HintAnchor.MiddleCenter, -90f)` | — |
| **④ 自动排版** | 都没写时按**插件名**推断：`exp`/`level`/`经验`/`等级` → 屏幕中部再往下 90；`score`/`排行` → 右上；`kill`/`击杀` → 左上；`timer`/`倒计时` → 顶部居中；`team`/`队伍` → 中右；`music`/`点歌` → 右下 | `auto_layout_by_plugin_name` |

**解析优先级**：显式 API → 文本标记 → 自带位置标签 → 名称目录 → 默认（底部自然堆叠）。

### 生态兼容对照（2026-10 于 GitHub 实搜并读其源码/文档）

| 项目 | 它的位置写法 | 我们的处理 |
| --- | --- | --- |
| **HintServiceMeow** ★74 | `Hint.XCoordinate/YCoordinate`（画布单位，半宽 1200）+ `HintAlignment` / `HintVerticalAlign`，自行渲染 TMP 标签 | 登记为「自带位置体系」→ **原样放行**；其文本里的 `<voffset>` / `<pos>` / `<align>` 亦被自动识别 |
| **RueI** ★24 | **0–1000 纵向标尺**（源码：`baseline = 755 − 2.14 × pos`）+ `VerticalAlign: Up/Center/Down` | 同上；且我们自有写法**直接接受 0–1000 标尺**，数值可照抄 |
| **ruei-cm-lab** | 同一 0–1000 标尺（`offset = 700 − (1000 − pos) × 1.08`）+ 离屏哨兵行固定基线 | 同上 |
| **UsefulHints** ★18 | 直接在配置里写原生 TMP 标签 `<align=left><size=28><color=…>` | 识别 `<align>` 等位置标签 → **原样放行** |
| 任何直写 `<voffset>` / `<pos>` / `<line-height>` / `<indent>` / `<margin>` / `<line-indent>` 的插件 | 原生 TMP | 一律 **原样放行**，位置一动不动 |
| 纯样式标签（`<size>` / `<color>` / `<alpha>` / `<b>`…） | 原生 TMP | **不算位置** —— 仍由本底层摆位，样式保留 |

> 关键换算：RueI 的 `2.14 × 1000 = 2140`，正是本项目 `screen_height_units` 的默认值 —— 两套坐标天然对齐。
>
> 对「自带位置」的文本，我们**连对齐也不改**（保持插件自己的 `<align>`），确保位置与观感一动不动。

### 0–1000 纵向标尺（生态通用语言）

自有写法同时支持这个标尺：`0 = 屏幕底`、`500 = 屏幕中`、`1000 = 屏幕顶` —— 从 RueI / ruei-cm-lab 迁移过来可以直接照抄数值：

```
{{hc:pos=750}}                ← 与 RueI 的 Scaled position 同义
{{hc:pos=500,offset=-90}}     ← 标尺定位, 再往下 90
```

锚点可写英文或中文：`top-left` / `top` / `top-right` / `middle-left` / `middle` / `middle-right` /
`bottom-left` / `bottom` / `bottom-right`，也接受 `tl` / `tr` / `mc` 等缩写与 `左上` / `中部` / `右下`。

`offset` 以 **voffset 为单位、正数向上**（参考：整屏约 2140）。所以「屏幕中间再往下 90」就是
`{{hc:pos=middle,offset=-90}}`。

服主可用 `position_overrides` 覆盖任何插件的落点，**无需改插件**：

```yaml
position_overrides:
  MyPlugin: top-right
  LevelSystem: middle,offset=-90
```

> 保底不变：全部插件都在默认位置时，合成输出与旧版**逐字一致**；定位只在真的用到时介入。

---

## 兼容

- **LabAPI**（原生）与 **EXILED** 两套写法都能吃下，且不需要插件改写法；
- 覆盖的表面：提示条 / 屏幕广播 / 玩家控制台 / CASSIE 播报 / 管理端聊天 / 准星命中标记 / 设置页(SSS) / 对讲机显示屏；
- 强优先级加载（`LoadPriority.Highest`）+ 0 前缀引导器，保证**先于其它插件**装好拦截；
- `FrameworkCompat` 会打印"写法 → 拦截落点"的对照表，未覆盖的项会**显式标成盲区**，不会静默漏掉。

> 已知互斥：**同样自己给提示条打补丁的框架**（例如自行接管显示的 UI 框架）与本插件二选一，
> 两者都要独占同一条通道。若要共存，请在对方或本插件的配置里让出渲染权。

---

## 文档

| 文档 | 说明 |
| --- | --- |
| [`docs/zh/`](docs/zh/) | 中文文档（主）：深度说明、API 写法大全 |
| [`docs/en/README.md`](docs/en/README.md) | English summary |
| [`docs/fr/README.md`](docs/fr/README.md) | Résumé en français |
| [`docs/ru/README.md`](docs/ru/README.md) | Краткое описание на русском |
| [`docs/es/README.md`](docs/es/README.md) | Resumen en español |
| [`docs/languages.md`](docs/languages.md) | **多语言总入口**：全部 22 种客户端语言 + 翻译文件下载 |
| [`docs/translations/`](docs/translations/) | 17 种外部语言翻译文件（放入服务器 `translations/` 目录即生效） |
| [`docs/default-config.yml`](docs/default-config.yml) | **权威默认配置**（由编译产物实际序列化得到，66 项） |
| [`docs/api-guide.zh.txt`](docs/api-guide.zh.txt) | 插件作者要看的 API 写法指南 |
| [`docs/patch-target-report.txt`](docs/patch-target-report.txt) | 拦截目标逐条核验报告 |
| [`CHANGELOG.md`](CHANGELOG.md) | 变更记录 |

---

## 构建

```bash
# 需要 .NET SDK 与 SCP:SL 专用服务器的 Managed 目录
dotnet build src/HintChorus.csproj -c Release -p:ManagedDir="<你的服务器>/SCPSL_Data/Managed"
```

引导器是独立工程（`bootstrap/`），其产物会被主工程**内嵌**并在运行时释放。

---

## 许可 / License

## 相关项目 / Related

本项目与下列项目同处一个生态。它们**不是**本项目的依赖，链接在此仅为方便你对照不同思路：

- [**RueI**](https://github.com/pawslee/RueI) —— CC0 的 hint 框架；本项目的 `offsets` 排版模式借鉴了它"不是行基、而是算偏移"的构思
- [**HintServiceMeow**](https://github.com/MeowServer/HintServiceMeow) —— MIT 的 hint 框架，按坐标放置每个提示
- [**LabAPI**](https://github.com/northwood-studios/LabAPI) —— 本项目所依托的服务端插件 API

> 与自行接管提示显示的框架（上面这类）**二选一**：两者都要独占同一条提示通道。

---


本项目以 **CC0-1.0** 释出（公有领域献出，无任何附加条件）：见 [`LICENSE`](LICENSE)。

涉及第三方内容与参考来源的部分**不在本项目的权利声明范围内**，单独列在 [`THIRD-PARTY.md`](THIRD-PARTY.md)。
