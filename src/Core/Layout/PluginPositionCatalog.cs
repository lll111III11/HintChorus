using System;
using System.Collections.Generic;

namespace HintChorus.Core.Layout;

/// <summary>
/// <b>插件位置目录(自动排版)</b>。
///
/// <para>解析顺序(前者优先):</para>
/// <list type="number">
///   <item><b>服主覆盖表</b> —— 配置 <c>position_overrides</c>(插件名 → 位置), 可随时增补;</item>
///   <item><b>已收录表</b> —— 按程序集名 / 显示名关键词匹配的社区插件位置(GitHub 等平台收录);</item>
///   <item><b>功能区推断</b> —— 名称关键词 → 功能类别 → 位置
///     (例: <c>exp</c>/<c>level</c>/<c>rank</c> → 屏幕中部、再往下 90)。</item>
/// </list>
///
/// <para>三名都没命中时返回 false, 调用方退回<see cref="HintPosition.Default"/>(底部自然堆叠)。</para>
/// </summary>
public static class PluginPositionCatalog
{
	private readonly struct Entry
	{
		public Entry(string[] keys, HintAnchor anchor, float offset)
		{
			Keys = keys;
			Anchor = anchor;
			Offset = offset;
		}

		public string[] Keys { get; }

		public HintAnchor Anchor { get; }

		public float Offset { get; }
	}

	/// <summary>服主覆盖表(插件关键词 → 位置)。由配置注入, 优先级最高。</summary>
	private static readonly Dictionary<string, HintPosition> Overrides = new Dictionary<string, HintPosition>(StringComparer.OrdinalIgnoreCase);

	private static readonly object Sync = new object();

	/// <summary>
	/// 已收录的社区插件位置(键为程序集名 / 显示名里的关键词, 命中即用)。
	/// <para>均为「某类 UI 在这一生态里约定俗成的位置」, 源自在 GitHub 等平台上的公开写法。</para>
	/// </summary>
	private static readonly Entry[] Curated = new Entry[]
	{
		new Entry(new[] { "levelsystem", "level-system", "等级系统" }, HintAnchor.MiddleCenter, -90f),
		new Entry(new[] { "expas", "exp-bar", "experience" }, HintAnchor.MiddleCenter, -90f),
		new Entry(new[] { "bgmcc", "musicplayer", "music-player", "点歌" }, HintAnchor.BottomRight, 0f),
		new Entry(new[] { "chatex", "chatext" }, HintAnchor.BottomLeft, 0f),
		new Entry(new[] { "fullmodsquad", "modsquad", "管理员小队" }, HintAnchor.MiddleRight, 0f),
		new Entry(new[] { "autotff", "autoff", "自动平衡", "自动换边" }, HintAnchor.TopLeft, 0f),
		new Entry(new[] { "maplightbooster", "lightbooster", "灯光" }, HintAnchor.TopLeft, 0f),
		new Entry(new[] { "scpstats", "scp-stats", "统计面板" }, HintAnchor.TopRight, 0f),
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
					position = new HintPosition(entry.Anchor, entry.Offset, managed: true);
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
