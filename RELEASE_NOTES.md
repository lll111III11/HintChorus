# HintChorus Beta 1.1.0 — “Wait, is it really entering beta?” … “Yep, it really is!”

> 发布标题 / Release title:
> **HintChorus Beta 1.1.0 — “Wait, is it really entering beta?” … “Yep, it really is!”**
>
> 中文副标题 / Chinese subtitle:
> **HintChorus 要进入 Beta 了，真的假的？我去，真的！**

---

## English (primary)

**Beta status** — HintChorus is now in Beta. It can be used on regular production servers, but please keep an eye on it. If anything goes wrong, roll back the installed hint framework(s) through the server console, or remove/delete HintChorus.

### What's changed

- Trimmed unnecessary hint-interception paths, and **experimentally merged the game's native hints into the composite render** (based on current observations, it works in most cases).
- **Compatibility fallback**: if something is incompatible, run `Hint orvs` in the server backend console to return the installed hint framework(s) to the vanilla display, one by one. If several are found, it lists them and asks you to pick one by number.
- **Detailed logging (off by default)**: records the font size and screen position of every line of text, plus the plugin that owns it. To inspect every plugin's text behaviour, enable `EnableDetailedUiLog` in the config file.
- **Default logging** keeps ordinary events and exceptions only (crash detection).
- Fixed about **80%** of the known bugs.
- Multiple hint plugins can now coexist without conflicts.

### Rollback

- Console: `Hint orvs` → choose the suspect plugin by number; or `Hint orvs all` / `Hint orvs reset`.
- Or remove/delete `HintChorus.dll` (and `0HintChorus.Bootstrap.dll`) and restart the server.

> Target build: current public branch 14.2.x. Patch targets will be re-verified after SCP:SL 15.0 “Megapatch III” ships.
> Download asset for this release: **`HintChorus.dll`** (the old `alpha-HintIsolation.dll` / `alpha-HintChorus.dll` names are no longer used).

---

## 中文（辅助）

**HintChorus 要进入 Beta 了，真的假的？**

我去，真的！

本次更新删减了不必要的 Hint 拦截，以及尝试性地将原版的 Hint 融入到渲染中（按照目前发现来看的话，有概率）。

本次更新也包含了：如果出现不兼容情况，可以退回原本已安装的 Hint 底层插件（通过服务器后台控制台）。

增加了更详细的日志（默认关闭），它会详细记录每一个文字大小以及位置信息。

默认情况下的日志会记录普通的事件及异常事件等。

以及修复了 80% 的 bug！

HintChorus 目前已经进入 beta 阶段：可以应用于正常的生产服务器，但仍需观察使用。如出现问题，可以通过指令退回原本已安装的 Hint 插件，或直接移出或删除 HintChorus。

### 退回方式

- 服务器后台控制台输入 `Hint orvs`，按编号选择要退回的插件；或 `Hint orvs all` / `Hint orvs reset`。
- 或者直接移出/删除 `HintChorus.dll`（以及 `0HintChorus.Bootstrap.dll`）后重启服务器。

> 目标版本：当前公开分支 14.2.x；15.0「Megapatch III」正式发布后会重新核对拦截落点。
> 本次发布资产请使用 **`HintChorus.dll`**（不再使用旧的 `alpha-HintIsolation.dll` / `alpha-HintChorus.dll`）。
