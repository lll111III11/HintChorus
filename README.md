<!-- language tabs: keep this block at the very top so the switcher is visible on GitHub -->
**语言 / Language：** **中文（主）** ｜ [English](docs/en/README.md)

---

# HintIsolation

**SCP:SL 动态 UI 隔离底层** —— 让**互不相识的插件**在同一个屏幕上和平共处，谁也不用改写法。

> 一句话：游戏只给了一个「提示条」通道，谁后发谁覆盖。本插件把这个通道变成**多个互不干扰的信口**，
> 并且**按调用方自动归因** —— 插件照原来的样子写 `SendHint` / `SendBroadcast`，丢进去就能用。

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)
![Platform](https://img.shields.io/badge/SCP%3ASL-LabAPI-blue)

---

## 下载 / Download

**只需下载一个文件**（主 DLL 会在首次运行时自己释放前置引导器，无需手动装第二个文件）：

| 方式 | 链接 |
| --- | --- |
| 直接下载（仓库内，文件名带 `alpha` 前缀） | [`alpha-HintIsolation.dll`](alpha-HintIsolation.dll) |
| 发行页（推荐，含版本说明） | [alpha-v1.0.0](https://github.com/lll111III111/HintIsolation/releases/tag/alpha-v1.0.0) ｜ [全部发行](https://github.com/lll111III111/HintIsolation/releases) |

> 文件名前面的 `alpha-` 只是**标记这是 alpha 阶段的产物**；插件内部的程序集名始终是 `HintIsolation`，
> 所以改名不影响加载，也不影响引导器找它。

安装：

1. 把 `alpha-HintIsolation.dll` 放进 `plugins/global/`
2. 启动服务器（首次会自动释放 `0HintIsolation.Bootstrap.dll` 并提示重启一次，这是引导器抢位的设计）
3. 按需改 `configs/<端口>/HintIsolation/config.yml`（**键名是 snake_case**，见 [`docs/default-config.yml`](docs/default-config.yml)）

---

## 它解决什么问题

游戏只给所有插件**一个**提示条通道。结果是：

- 插件 A 的 HUD 和插件 B 的击杀播报**互相顶掉**；
- 谁每秒重发，谁就把别人的 UI 刷没；
- 装得越多、越乱。

HintIsolation 的做法：

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
| [`docs/default-config.yml`](docs/default-config.yml) | **权威默认配置**（由编译产物实际序列化得到，61 项） |
| [`docs/api-guide.zh.txt`](docs/api-guide.zh.txt) | 插件作者要看的 API 写法指南 |
| [`docs/patch-target-report.txt`](docs/patch-target-report.txt) | 拦截目标逐条核验报告 |
| [`CHANGELOG.md`](CHANGELOG.md) | 变更记录 |

---

## 构建

```bash
# 需要 .NET SDK 与 SCP:SL 专用服务器的 Managed 目录
dotnet build src/HintIsolation.csproj -c Release -p:ManagedDir="<你的服务器>/SCPSL_Data/Managed"
```

引导器是独立工程（`bootstrap/`），其产物会被主工程**内嵌**并在运行时释放。

---

## 许可 / License

本项目以 **CC0-1.0** 释出（公有领域献出，无任何附加条件）：见 [`LICENSE`](LICENSE)。

涉及第三方内容与参考来源的部分**不在本项目的权利声明范围内**，单独列在 [`THIRD-PARTY.md`](THIRD-PARTY.md)。
