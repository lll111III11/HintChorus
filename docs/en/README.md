<!-- language tabs -->
## Language / 语言

| Language | Entry |
| --- | --- |
| **中文（主）** | [仓库首页中文说明](../../README.md) ｜ [完整技术文档](../zh/README.md) |
| **English**（secondary） | this page |

---

# HintIsolation — dynamic UI isolation layer for SCP:SL

**Let unrelated plugins share one screen without any of them changing a line of code.**

> The game exposes exactly **one** hint channel, and the last writer wins. HintIsolation turns that single
> channel into **independent slots**, one per caller, attributed automatically from the call stack —
> so a plugin that just calls `SendHint` / `SendBroadcast` works as-is, and stops clobbering everyone else.

[![License: CC0-1.0](https://img.shields.io/badge/License-CC0--1.0-lightgrey.svg)](https://creativecommons.org/publicdomain/zero/1.0/)
![Platform](https://img.shields.io/badge/SCP%3ASL-LabAPI-blue)

## Download

**One file is enough.** The main DLL releases its own prerequisite bootstrap on first run.

| | |
| --- | --- |
| Direct download (file carries the `alpha-` prefix) | [`alpha-HintIsolation.dll`](../alpha-HintIsolation.dll) |
| Release page (recommended) | [Releases](https://github.com/lll111III111/HintIsolation/releases) |

The `alpha-` prefix only marks the build stage — the **assembly name stays `HintIsolation`**, so renaming the
file changes nothing about loading, nor about how the bootstrap locates it.

Install:

1. Drop `alpha-HintIsolation.dll` into `plugins/global/`
2. Start the server (the first run writes `0HintIsolation.Bootstrap.dll` and asks you to restart once — by design)
3. Optionally edit `configs/<port>/HintIsolation/config.yml` (**keys are snake_case**; see [`docs/default-config.yml`](../docs/default-config.yml))

## What it does

1. **Attribution** — intercepts the six UI entry points (hint / broadcast / console / CASSIE / admin chat / hitmarker)
   and walks the call stack to find **which plugin and which call site** is emitting.
2. **Deterministic UiId** — `UUIDv5(surface | assembly | call site | [IL offset])`; stable across restarts, persistable, aliasable.
3. **One slot per UiId** — merged, stably ordered by priority, emitted by a single writer.
4. **Stable layout** — see below.

Plugin authors need **no code changes** and no reference to this plugin.

## Layout: why the positions stop jumping

The hint block is anchored to the bottom, so any change in its height shifts every line inside it.
Two layout modes are provided (`layout_mode`):

| Mode | How | Trade-off |
| --- | --- | --- |
| **`rows`** (default) | fixed row count for the volatile region + one reserved row per owner, blank rows fill the gaps | absolutely stable, but occupies screen space |
| **`offsets`** | no blank rows; each line gets a `<voffset>` correction that pins it to its own slot | equally stable, **and wastes no space** |
| `compact` | nothing is padded | most compact, positions shift as content comes and goes |

`offsets` follows the *idea* of **RueI (CC0)** — "not a grid- or line-based system, it computes the offset" —
with its own implementation, measuring line heights through the text-metrics table shipped in this repo.
**No third-party code is included**; see [`THIRD-PARTY.md`](../THIRD-PARTY.md).

`offsets` is off by default because two values need tuning for your screen/font:

```yaml
layout_mode: rows        # switch to offsets to save screen space
offset_row_height: 40    # height of one row, in voffset units (full screen ≈ 2140)
offset_sign: 1           # set to -1 if the whole block drifts the wrong way
offset_font_size: 20     # font size used to measure text width
```

## Compatibility

- Both **LabAPI** and **EXILED** call styles are handled, with no changes required on the plugin side.
- Covered surfaces: hint / broadcast / console / CASSIE / admin chat / hitmarker / server settings (SSS) / intercom display.
- Loads with `LoadPriority.Highest` plus a `0`-prefixed bootstrap, so patches are installed **before** other plugins.
- `FrameworkCompat` prints a "call style → interception point" table and marks unimplemented entries as
  blind spots instead of silently missing them.

> Known conflict: a framework that patches the hint display **itself** must be used *instead of* this plugin —
> both need to own the same single channel.

## Docs

| Document | Contents |
| --- | --- |
| [`docs/zh/`](../docs/zh/) | 中文文档（主）— deep dive and API guide |
| [`docs/default-config.yml`](../docs/default-config.yml) | **Authoritative default config** (61 keys, generated from the built assembly) |
| [`docs/api-guide.zh.txt`](../docs/api-guide.zh.txt) | API guide for plugin authors (Chinese) |
| [`docs/patch-target-report.txt`](../docs/patch-target-report.txt) | Per-target verification report |
| [`CHANGELOG.md`](../CHANGELOG.md) | Changes |

## Build

```bash
dotnet build src/HintIsolation.csproj -c Release -p:ManagedDir="<your server>/SCPSL_Data/Managed"
```

The bootstrap is a separate project (`bootstrap/`); its output is **embedded** into the main assembly and
released at runtime.

## License

**CC0-1.0** — public domain dedication, no conditions attached: see [`LICENSE`](../LICENSE).
Third-party content and referenced ideas are excluded from that dedication and listed separately in
[`THIRD-PARTY.md`](../THIRD-PARTY.md).
