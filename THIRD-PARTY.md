# 第三方内容与参考来源 / Third-Party Notice

本文件是本项目许可的**排除声明**：下列内容不属于本项目的权利声明范围。

## 1. 本项目自身的代码

本项目代码以 [CC0-1.0](LICENSE) 释出。除本文件列出的部分外，不保留任何权利。

## 2. 游戏相关内容（权利归 Northwood Studios）

本插件需要与 SCP: Secret Laboratory 交互，因此仓库中含有**极少量**游戏相关的字符串/数据，
仅为插件运行所必需，权利归 Northwood Studios：

| 内容 | 位置 | 说明 |
| --- | --- | --- |
| 游戏内提示模板（6 条英文/中文对照） | `src/Core/Compat/NativeHintTranslator.cs` | 用于把游戏自身的提示并入统一排版；与服务器 `Translations/en/GameHints.txt` 同源 |
| 文本度量表（字符宽度/色调） | `src/Resources/HintIsolation.textwidth.bin` | 用于估算文本实际占几个视觉行（`offsets` 排版模式需要） |
| **22 种语言的官方提示译文** | `src/Resources/lang/*.txt` | 取自游戏客户端的 `Translations/<语言>/GameHints.txt`（6 行/语言，合计约 14 KB）。**专用服务器只带 `en`**，所以想在其他语言下也正确显示游戏原生提示，只能由插件自带一份。缺失时逐条回退英文 |

若权利人要求，可随时移除上述内容（移除后：原生提示不再并入排版、也无法翻译成其他语言；`offsets` 模式退回按行估算）。

## 3. 参考项目（只借鉴构思，未包含其代码）

设计过程中参考过以下**公开**项目的**做法与思路**。**本仓库不包含它们的任何代码或资源**，
下述引用仅为如实标注灵感来源：

| 项目 | 协议 | 借鉴了什么 |
| --- | --- | --- |
| [pawslee/RueI](https://github.com/pawslee/RueI) | **CC0-1.0** | "显示多个提示而不互相干扰"的总体目标；**"不是网格/行基，而是算偏移"**这一思路（本项目 `offsets` 排版模式据此自行实现）；宽高比变化后重排的做法 |
| [MeowServer/HintServiceMeow](https://github.com/MeowServer/HintServiceMeow) | **MIT** | 仅记录其"每个提示自带坐标 / 可替换投递层"的设计取舍；**未采用**（它要求插件作者自己选位置，与"不改写法就能用"相冲突） |

> 具体差异：本项目**不复制**上述项目的代码；`offsets` 模式的偏移公式、单位与行高折算
> 均按本项目自己的度量表实现，只在量级上与 RueI 公布的常量处于同一参考系。

## 4. 依赖

运行期依赖由服务器自身提供（LabAPI / Harmony / Unity / .NET Framework 4.8）或其上游分发，
本项目不再分发这些程序集。
