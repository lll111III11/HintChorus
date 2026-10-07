using System;
using System.Collections.Generic;

namespace HintChorus.Core.Layout;

/// <summary>
/// <b>插件位置目录(自动排版)</b>。
///
/// <para>解析顺序(前者优先):</para>
/// <list type="number">
///   <item><b>服主覆盖表</b> —— 配置 <c>position_overrides</c>(插件名 → 位置), 可随时增补;</item>
///   <item><b>已收录表</b> —— 按程序集名 / 显示名关键词匹配的社区项目(GitHub 实搜所得);</item>
///   <item><b>功能区推断</b> —— 名称关键词 → 功能类别 → 位置
///     (例: <c>exp</c>/<c>level</c>/<c>rank</c> → 屏幕中部、再往下 90)。</item>
/// </list>
///
/// <para><b>已收录表的数据来源</b>(2026-10 于 GitHub 实测检索并阅读其源码/文档):</para>
/// <list type="bullet">
///   <item><b>MeowServer/HintServiceMeow</b> ★74 —— 生态内事实标准; 其 <c>Hint</c> 带
///     <c>XCoordinate/YCoordinate</c> + <c>HintAlignment/HintVerticalAlign</c>, 自行渲染 TMP 标签;</item>
///   <item><b>pawslee/RueI</b> ★24 —— <b>0–1000 纵向标尺</b>(<c>baseline = 755 − 2.14 × pos</c>),
///     <c>VerticalAlign: Up/Center/Down</c>;</item>
///   <item><b>Michaelihc/ruei-cm-lab</b> —— 同一 0–1000 标尺, 用离屏哨兵行固定基线;</item>
///   <item><b>Vretu-Dev/UsefulHints</b> ★18 —— 直接在配置里写原生 TMP 标签
///     (<c>&lt;align=left&gt;&lt;size=28&gt;</c>)。</item>
/// </list>
///
/// <para>上面这些"自己会摆位的框架"被登记为 <see cref="HintPosition.SelfPositioned"/>:
/// 一旦识别到, 其产物<b>原样放行、绝不重排</b> —— 从这些框架出来的 UI, 位置一动不动。</para>
/// </summary>
public static class PluginPositionCatalog
{
	private readonly struct Entry
	{
		public Entry(string[] keys, HintAnchor anchor, float offset, bool selfPositioned = false)
		{
			Keys = keys;
			Anchor = anchor;
			Offset = offset;
			SelfPositioned = selfPositioned;
		}

		public string[] Keys { get; }

		public HintAnchor Anchor { get; }

		public float Offset { get; }

		/// <summary>true = "自己会摆位的框架", 遇到它的产物一律原样放行。</summary>
		public bool SelfPositioned { get; }
	}

	/// <summary>服主覆盖表(插件关键词 → 位置)。由配置注入, 优先级最高。</summary>
	private static readonly Dictionary<string, HintPosition> Overrides =
		new Dictionary<string, HintPosition>(StringComparer.OrdinalIgnoreCase);

	private static readonly object Sync = new object();

	/// <summary>
	/// 已收录的社区项目(键为程序集名 / 显示名里的关键词, 命中即用)。
	/// <para>前 4 条是"自带位置体系的框架" —— 一律原样放行; 其余是具体插件按功能归位。</para>
	/// </summary>
	private static readonly Entry[] Curated = new Entry[]
	{
		// ── 自带位置体系的框架: 原样放行, 位置一动不动 ─────────────────────
		new Entry(new[] { "hintservicemeow", "hint-service-meow" }, HintAnchor.BottomCenter, 0f, selfPositioned: true),
		new Entry(new[] { "customizableuimeow", "customizable-ui-meow" }, HintAnchor.BottomCenter, 0f, selfPositioned: true),
		new Entry(new[] { "ruei", "ruei-cm", "rueicmlab" }, HintAnchor.BottomCenter, 0f, selfPositioned: true),

		// ── 具体社区插件(实测收录) ─────────────────────────────────────────
		new Entry(new[] { "usefulhints", "useful-hints" }, HintAnchor.TopCenter, 0f),
		new Entry(new[] { "hintreminder", "hint-reminder" }, HintAnchor.TopCenter, 0f),
		new Entry(new[] { "customhint", "custom-hint" }, HintAnchor.BottomLeft, 0f),
		new Entry(new[] { "hitmarkers", "hitmarker", "hit-marker", "伤害数字" }, HintAnchor.MiddleCenter, 0f),
		new Entry(new[] { "hintframework", "hint-framework" }, HintAnchor.BottomCenter, 0f),
		new Entry(new[] { "saskycstyles", "saskyc-styles" }, HintAnchor.BottomCenter, 0f),
		new Entry(new[] { "hintsmrp", "hints-mrp" }, HintAnchor.BottomCenter, 0f),

		// ── 本机服务器上实际安装的插件 ─────────────────────────────────────
		new Entry(new[] { "levelsystem", "level-system", "等级系统" }, HintAnchor.MiddleCenter, -90f),
		new Entry(new[] { "expas", "exp-bar", "experience" }, HintAnchor.MiddleCenter, -90f),
		new Entry(new[] { "bgmcc", "musicplayer", "music-player", "点歌" }, HintAnchor.BottomRight, 0f),
		new Entry(new[] { "chatex", "chatext" }, HintAnchor.BottomLeft, 0f),
		new Entry(new[] { "fullmodsquad", "modsquad", "管理员小队" }, HintAnchor.MiddleRight, 0f),
		new Entry(new[] { "autotff", "autoff", "自动平衡", "自动换边" }, HintAnchor.TopLeft, 0f),
		new Entry(new[] { "maplightbooster", "lightbooster", "灯光" }, HintAnchor.TopLeft, 0f),
		new Entry(new[] { "nameprefix", "name-prefix", "称号", "名片" }, HintAnchor.MiddleRight, 0f)
	};

	/// <summary>功能区关键词(按名称推断)。顺序即优先级, 越靠前越先匹配。</summary>
	private static readonly Entry[] Categories = new Entry[]
	{
		// 经验/等级/成长 —— 玩家看向屏幕中部时的第一视线区
		new Entry(new[] { "exp", "xp", "level", "rank", "progression", "upgrade", "经验", "等级", "成长", "熟练", "升级" }, HintAnchor.MiddleCenter, -90f),
		// 排行/战绩/统计 —— 右上角, 不与战斗视线冲突
		new Entry(new[] { "score", "scoreboard", "leaderboard", "ranking", "stat", "战绩", "排行", "统计", "排名" }, HintAnchor.TopRight, 0f),
		// 击杀/播报 —— 左上角
		new Entry(new[] { "killfeed", "kill-feed", "kill", "death", "feed", "击杀", "淘汰", "播报", "死亡" }, HintAnchor.TopLeft, 0f),
		// 计时/倒计时 —— 顶部居中
		new Entry(new[] { "timer", "countdown", "clock", "计时", "倒计时", "时间" }, HintAnchor.TopCenter, 0f),
		// 队伍/玩家列表 —— 中右
		new Entry(new[] { "team", "squad", "roster", "playerlist", "player-list", "队伍", "小队", "玩家列表", "名单" }, HintAnchor.MiddleRight, 0f),
		// 音乐/播放器 —— 右下
		new Entry(new[] { "music", "bgm", "audio", "soundtrack", "音乐", "播放器", "点歌" }, HintAnchor.BottomRight, 0f),
		// 聊天/通讯 —— 左下
		new Entry(new[] { "chat", "message-log", "通讯", "聊天", "消息记录" }, HintAnchor.BottomLeft, 0f),
		// 状态/HUD 条 —— 左下(常驻、低干扰)
		new Entry(new[] { "hud", "status", "statusbar", "status-bar", "info", "状态", "信息", "面板" }, HintAnchor.BottomLeft, 0f),
		// 提示/公告 —— 底部居中(默认区)
		new Entry(new[] { "hint", "tip", "notice", "announce", "broadcast", "message", "提示", "公告", "广播", "通知" }, HintAnchor.BottomCenter, 0f)
	};

	/// <summary>注入/替换服主覆盖表(传 null 或空表示清空)。</summary>
	public static void SetOverrides(IReadOnlyDictionary<string, HintPosition>? overrides)
	{
		lock (Sync)
		{
			Overrides.Clear();
			if (overrides is null)
			{
				return;
			}

			foreach (KeyValuePair<string, HintPosition> pair in overrides)
			{
				if (!string.IsNullOrWhiteSpace(pair.Key))
				{
					Overrides[pair.Key.Trim().ToLowerInvariant()] = pair.Value;
				}
			}
		}
	}

	/// <summary>当前覆盖表条数(诊断用)。</summary>
	public static int OverrideCount
	{
		get
		{
			lock (Sync)
			{
				return Overrides.Count;
			}
		}
	}

	/// <summary>
	/// 按插件名解析位置。命中返回 true; 未收录且无法按名称推断时返回 false。
	/// </summary>
	/// <param name="pluginId">程序集名(归因得到的插件标识)。</param>
	/// <param name="displayName">信口显示名(可能更贴近功能)。</param>
	/// <param name="position">解析结果。</param>
	public static bool TryResolve(string? pluginId, string? displayName, out HintPosition position)
	{
		position = HintPosition.Default;

		string id = (pluginId ?? string.Empty).ToLowerInvariant();
		string name = (displayName ?? string.Empty).ToLowerInvariant();
		if (id.Length == 0 && name.Length == 0)
		{
			return false;
		}

		// ① 服主覆盖表
		lock (Sync)
		{
			foreach (KeyValuePair<string, HintPosition> pair in Overrides)
			{
				if (Contains(id, pair.Key) || Contains(name, pair.Key))
				{
					position = pair.Value;
					return true;
				}
			}
		}

		// ② 已收录表 → ③ 功能区推断
		if (TryMatch(Curated, id, name, out position))
		{
			return true;
		}

		return TryMatch(Categories, id, name, out position);
	}

	private static bool TryMatch(Entry[] table, string id, string name, out HintPosition position)
	{
		foreach (Entry entry in table)
		{
			foreach (string key in entry.Keys)
			{
				if (Contains(id, key) || Contains(name, key))
				{
					position = entry.SelfPositioned
						? HintPosition.SelfPositioned
						: new HintPosition(entry.Anchor, entry.Offset, managed: true);
					return true;
				}
			}
		}

		position = HintPosition.Default;
		return false;
	}

	/// <summary>子串包含(两边都已小写; 空串视为不匹配)。</summary>
	private static bool Contains(string haystack, string needle)
	{
		return haystack.Length > 0 && needle.Length > 0 && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
	}
}
