---
title: 多语言 / Multilingual
---

# 多语言 / Multilingual

**HintChorus** 的界面文本语言覆盖策略：

- **DLL 内嵌 5 种联合国常用语**：`en` / `zh` / `fr` / `ru` / `es` —— 开箱即用，永远可用；
- **其余语言走外部文件**：从本仓库 [`docs/translations/`](translations/) 下载对应 `<语言代码>.txt`，
  放进服务器的 `configs/<端口>/HintChorus/translations/` 目录即生效；
- **按玩家自适应**：每个玩家按其客户端语言（`playerPreferences.Language`）自动取对应译文，
  探测不到时回退配置默认（`native_hint_language`，默认 `zh`）；
- **逐条回退**：某语言缺某一行时，按「该语言 → 英文」的顺序逐条回退，绝不输出半成品。

## 语言清单

| 语言代码 | 语言 | 内嵌 DLL | 外部文件 |
| --- | --- | --- | --- |
| `en` | English 英语 | ✅ | — |
| `zh` / `zh_Hans` / `zh_Hans-2` / `zh_Flash_Hans` | 简体中文 | ✅ | — |
| `fr` | Français 法语 | ✅ | — |
| `ru` | Русский 俄语 | ✅ | — |
| `es` | Español 西班牙语 | ✅ | — |
| `zh_Hant` | 繁體中文 | —（回退简体） | [`translations/zh_Hant.txt`](translations/zh_Hant.txt) |
| `ca` | Català 加泰罗尼亚语 | — | [`translations/ca.txt`](translations/ca.txt) |
| `cs` | Čeština 捷克语 | — | [`translations/cs.txt`](translations/cs.txt) |
| `de` | Deutsch 德语 | — | [`translations/de.txt`](translations/de.txt) |
| `gl` | Galego 加利西亚语 | — | [`translations/gl.txt`](translations/gl.txt) |
| `it` | Italiano 意大利语 | — | [`translations/it.txt`](translations/it.txt) |
| `ko` | 한국어 韩语 | — | [`translations/ko.txt`](translations/ko.txt) |
| `pl` | Polski 波兰语 | — | [`translations/pl.txt`](translations/pl.txt) |
| `pt_BR` | Português (Brasil) 巴西葡萄牙语 | — | [`translations/pt_BR.txt`](translations/pt_BR.txt) |
| `sk` | Slovenčina 斯洛伐克语 | — | [`translations/sk.txt`](translations/sk.txt) |
| `sr_CYRL-BA` | Српски (ћирилица) 塞尔维亚语（西里尔） | — | [`translations/sr_CYRL-BA.txt`](translations/sr_CYRL-BA.txt) |
| `sr_LATN-BA` | Srpski (latinica) 塞尔维亚语（拉丁） | — | [`translations/sr_LATN-BA.txt`](translations/sr_LATN-BA.txt) |
| `tr` | Türkçe 土耳其语 | — | [`translations/tr.txt`](translations/tr.txt) |
| `uk` | Українська 乌克兰语 | — | [`translations/uk.txt`](translations/uk.txt) |
| `vi` | Tiếng Việt 越南语 | — | [`translations/vi.txt`](translations/vi.txt) |

## 翻译文件格式

每个 `<语言代码>.txt` 与游戏自带的 `GameHints.txt` 相同：**每行一条模板，行号 = 枚举下标 + 1**，
空行与 `#` 注释行忽略。共 6 条，对应游戏原生提示的 6 种文本。

占位符（如 `[type]` / `[max_type_count]` / `[max_item_count]`）按出现顺序由游戏参数填充，请原样保留。

示例（`de.txt`）：

```text
<color=red>Zugriff verweigert</color>
Grenze von <color=yellow>[type] Munition</color> erreicht (<color=yellow>[max_type_count] Schuss</color>).
<b>Bereits</b> die Grenze von <color=yellow>[type] Munition</color> erreicht (<color=yellow>[max_type_count] Schuss</color>).
Grenze von <color=yellow>[type]</color> erreicht (<color=yellow>[max_type_count] Gegenstände</color>).
<b>Bereits</b> die Grenze von <color=yellow>[type]</color> erreicht (<color=yellow>[max_type_count] Gegenstände</color>).
Nur <color=yellow>[max_item_count] Gegenstände</color> können getragen werden.
```

## 各语言说明 / Notes

- **English (primary)** — [docs/en/README.md](en/README.md)
- **中文（主）** — [docs/zh/README.md](zh/README.md)
- **Français** — [docs/fr/README.md](fr/README.md)
- **Русский** — [docs/ru/README.md](ru/README.md)
- **Español** — [docs/es/README.md](es/README.md)

## 位置写法规范 / Position syntax spec（UN-5）

自有位置写法 **v1** 的规范文档，已按 UN-5 语言各出一份：

- **中文** — [docs/position-spec.zh.txt](position-spec.zh.txt)
- **English** — [docs/position-spec.en.txt](position-spec.en.txt)
- **Français** — [docs/position-spec.fr.txt](position-spec.fr.txt)
- **Русский** — [docs/position-spec.ru.txt](position-spec.ru.txt)
- **Español** — [docs/position-spec.es.txt](position-spec.es.txt)

同一套语法同时服务于三处：文本标记 `{{hc:...}}`、C# API `UiIsolation.SetHintPosition`、配置项 `position_overrides`。
（`{{hc:...}}` 的锚点别名也接受中文，例如 `{{hc:右上}}`。）
