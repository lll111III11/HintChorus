using System;
using System.Globalization;
using HintChorus.Core.Enums;

namespace HintChorus.Core.Layout;

/// <summary>
/// <b>九宫格锚点</b>。游戏提示块是<b>底部锚定</b>的, 因此所有位置都以"距屏幕底部的距离"为参考系描述。
/// </summary>
public enum HintAnchor : byte
{
	/// <summary>屏幕底部 · 居中(默认; 与未启用定位时的自然堆叠一致)。</summary>
	BottomCenter = 0,
	/// <summary>屏幕底部 · 居左。</summary>
	BottomLeft = 1,
	/// <summary>屏幕底部 · 居右。</summary>
	BottomRight = 2,
	/// <summary>屏幕中部 · 居中(例: 经验/等级类 HUD 的默认落点)。</summary>
	MiddleCenter = 3,
	/// <summary>屏幕中部 · 居左。</summary>
	MiddleLeft = 4,
	/// <summary>屏幕中部 · 居右。</summary>
	MiddleRight = 5,
	/// <summary>屏幕顶部 · 居中。</summary>
	TopCenter = 6,
	/// <summary>屏幕顶部 · 居左(例: 击杀播报)。</summary>
	TopLeft = 7,
	/// <summary>屏幕顶部 · 居右(例: 排行榜/战绩)。</summary>
	TopRight = 8,
}

/// <summary>
/// <b>一个信口的屏幕位置</b> —— 本项目「自有位置写法」的运行时表示。
///
/// <para><b>自有写法的两个通道共用同一套语法</b>(见 <see cref="TryParse"/>):</para>
/// <list type="bullet">
///   <item><b>文本标记</b>: <c>{{hc:pos=750,align=left,offset=-90}}</c> —— 解析后从文本剥离, 玩家看不到;</item>
///   <item><b>C# API</b>: <c>UiIsolation.SetHintPosition("MyPlugin", "pos=750,align=left")</c>
///     或 <c>UiIsolation.SetHintPosition("MyPlugin", HintAnchor.MiddleCenter, -90f)</c>。</item>
/// </list>
///
/// <para><b>三个维度</b>:</para>
/// <list type="number">
///   <item><b>纵向</b> —— 二选一:
///     <b>九宫格锚点</b>(<see cref="Anchor"/>, 粗定位) 或 <b>0–1000 标尺</b>(<see cref="Scale"/>, 精定位);</item>
///   <item><b>横向</b> —— <see cref="Align"/>(left / center / right), 不写则由锚点列推导;</item>
///   <item><b>微调</b> —— <see cref="OffsetUnits"/>(voffset 单位, 正 = 上移)。</item>
/// </list>
///
/// <para><b>解析链中的位置</b>(由上层 <c>HintBroker</c> 决定): 显式 API → 文本标记 → 自带位置标签
/// (<see cref="SelfPositioned"/>) → 名称目录 → <see cref="Default"/>。</para>
/// </summary>
public readonly struct HintPosition : IEquatable<HintPosition>
{
	/// <summary>默认位置: 底部中央 + 零偏移 + 未用标尺 + 受管 —— 与未启用定位时的行为<b>逐字一致</b>。</summary>
	public static readonly HintPosition Default = new HintPosition(HintAnchor.BottomCenter, 0f, -1f, null, managed: true);

	/// <summary>自带位置标签: 插件已自己摆好, 本底层原样放行、不加任何 <c>&lt;voffset&gt;</c>、也不改对齐。</summary>
	public static readonly HintPosition SelfPositioned = new HintPosition(HintAnchor.BottomCenter, 0f, -1f, null, managed: false);

	public HintPosition(HintAnchor anchor, float offsetUnits = 0f, bool managed = true)
		: this(anchor, offsetUnits, -1f, null, managed)
	{
	}

	public HintPosition(HintAnchor anchor, float offsetUnits, HintAlignment? align, bool managed = true)
		: this(anchor, offsetUnits, -1f, align, managed)
	{
	}

	public HintPosition(HintAnchor anchor, float offsetUnits, float scale, HintAlignment? align, bool managed = true)
	{
		Anchor = anchor;
		OffsetUnits = offsetUnits;
		Scale = scale;
		Align = align;
		Managed = managed;
	}

	/// <summary>锚点(九宫格)。写标尺时它只用于决定"默认横向对齐"与分区顺序。</summary>
	public HintAnchor Anchor { get; }

	/// <summary>额外偏移(voffset 单位; <b>正 = 上移</b>; 参考: 整屏约 2140)。</summary>
	public float OffsetUnits { get; }

	/// <summary>
	/// <b>0–1000 纵向标尺</b>(整个生态的通用语言): <c>0 = 屏幕底</c>、<c>500 = 屏幕中</c>、<c>1000 = 屏幕顶</c>。
	/// <para>与 <b>RueI</b> / <b>ruei-cm-lab</b> 的 <c>Scaled position</c> 同一含义 —— 它们的换算
	/// <c>baseline = 755 − 2.14 × pos</c> 与本项目"整屏约 2140 voffset 单位"完全对应
	/// (<c>1000 × 2.14 = 2140</c>)。接受这个标尺, 从那些框架迁移过来的作者可以照抄原数值。</para>
	/// <para><c>&lt; 0</c> 表示未使用, 退回到按 <see cref="Anchor"/> 摆放。</para>
	/// </summary>
	public float Scale { get; }

	/// <summary>显式横向对齐; <c>null</c> = 由 <see cref="Anchor"/> 的列推导。</summary>
	public HintAlignment? Align { get; }

	/// <summary>true = 由本底层摆位; false = 插件自带位置标签, 原样放行。</summary>
	public bool Managed { get; }

	/// <summary>是否使用了 0–1000 纵向标尺。</summary>
	public bool HasScale => Managed && Scale >= 0f;

	/// <summary>是否等同于默认位置(底部中央 + 零偏移 + 未用标尺 + 无显式对齐 + 受管)。</summary>
	public bool IsDefault
		=> Managed && !HasScale && Anchor == HintAnchor.BottomCenter
			&& Math.Abs(OffsetUnits) < 0.01f && !Align.HasValue;

	/// <summary>是否"自带位置标签"(不参与本底层的摆位换算, 对齐也不覆盖)。</summary>
	public bool IsSelfPositioned => !Managed;

	/// <summary>
	/// 是否属于底部锚定区。
	/// <para><b>重要</b>: 底部区必须排在合成串的<b>末尾</b> —— 提示块底部锚定, 只有最后一行才贴着屏幕底;
	/// 把底部区放最后, 它的自然堆叠就与旧行为完全一致(零 voffset)。</para>
	/// </summary>
	public bool IsBottomAnchored => !HasScale && (byte)Anchor <= (byte)HintAnchor.BottomRight;

	/// <summary>
	/// 垂直档位: 0 = 底(含自定位), 1 = 中, 2 = 顶。合成时按档位分区。
	/// <para>用了 0–1000 标尺的一律归入"中"档 —— 它的落点由标尺<b>绝对</b>决定, 与档位基准无关。</para>
	/// </summary>
	public byte Tier => !Managed ? (byte)0 : (HasScale ? (byte)1 : (byte)((byte)Anchor / 3));

	/// <summary>
	/// 求出实际生效的横向对齐。
	/// <para>规则: 自定位 → 完全沿用插件自己的(<paramref name="fallback"/>); 显式 <see cref="Align"/> → 用它;
	/// 默认位置 → 沿用 <paramref name="fallback"/>(保持旧观感); 其余 → 由锚点列推导。</para>
	/// </summary>
	public HintAlignment ResolveAlign(HintAlignment fallback)
	{
		if (!Managed)
		{
			return fallback;
		}
		if (Align.HasValue)
		{
			return Align.Value;
		}
		return IsDefault ? fallback : AlignOf(Anchor);
	}

	/// <summary>该锚点对应的水平对齐。</summary>
	public static HintAlignment AlignOf(HintAnchor anchor)
	{
		return anchor switch
		{
			HintAnchor.BottomLeft or HintAnchor.MiddleLeft or HintAnchor.TopLeft => HintAlignment.Left,
			HintAnchor.BottomRight or HintAnchor.MiddleRight or HintAnchor.TopRight => HintAlignment.Right,
			_ => HintAlignment.Center,
		};
	}

	/// <summary>按 0–1000 标尺构造(生态兼容入口)。</summary>
	public static HintPosition FromScale(float scale, float offsetUnits = 0f, HintAlignment? align = null)
	{
		return new HintPosition(HintAnchor.MiddleCenter, offsetUnits, scale, align, managed: true);
	}

	/// <summary>
	/// <b>解析自有写法</b> —— 文本标记与 C# 字符串 API 共用这一处实现。
	///
	/// <para>语法(逗号或分号分隔, 顺序任意, 大小写不敏感):</para>
	/// <list type="bullet">
	///   <item><c>pos=top-right</c> / <c>anchor=中部</c> / 直接写 <c>top-right</c> —— 九宫格锚点;</item>
	///   <item><c>pos=750</c> / <c>y=750</c> / <c>scale=750</c> / 直接写 <c>750</c> —— 0–1000 纵向标尺;</item>
	///   <item><c>offset=-90</c> / <c>voffset=-90</c> —— 微调(voffset 单位, 正 = 上移);</item>
	///   <item><c>align=left</c> / <c>align=右</c> —— 显式横向对齐。</item>
	/// </list>
	///
	/// <para>例: <c>"pos=750,align=left,offset=-90"</c>、<c>"middle,offset=-90"</c>、<c>"top-right"</c>、<c>"750"</c>。</para>
	/// </summary>
	/// <returns>至少识别出一个有效键时返回 true。</returns>
	public static bool TryParse(string? spec, out HintPosition position)
	{
		position = Default;
		if (string.IsNullOrWhiteSpace(spec))
		{
			return false;
		}

		HintAnchor? anchor = null;
		float? scale = null;
		float offset = 0f;
		HintAlignment? align = null;

		foreach (string rawPart in spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
		{
			string part = rawPart.Trim();
			if (part.Length == 0)
			{
				continue;
			}

			int eq = part.IndexOf('=');
			if (eq > 0)
			{
				string key = part.Substring(0, eq).Trim().ToLowerInvariant();
				string val = part.Substring(eq + 1).Trim();

				switch (key)
				{
					case "pos":
					case "anchor":
					case "位置":
						if (TryParseAnchor(val, out HintAnchor named))
						{
							anchor = named;
						}
						else if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float posValue))
						{
							// 数字形式的 pos = 生态通用的 0–1000 纵向标尺(0 底 / 500 中 / 1000 顶)
							scale = posValue;
						}
						break;

					case "y":
					case "scale":
					case "标尺":
						if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float yValue))
						{
							scale = yValue;
						}
						break;

					case "offset":
					case "voffset":
					case "偏移":
						if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float offsetValue))
						{
							offset = offsetValue;
						}
						break;

					case "align":
					case "对齐":
						if (TryParseAlign(val, out HintAlignment alignment))
						{
							align = alignment;
						}
						break;
				}

				continue;
			}

			if (TryParseAnchor(part, out HintAnchor direct))
			{
				anchor = direct;
			}
			else if (TryParseAlign(part, out HintAlignment directAlign))
			{
				align = directAlign;
			}
			else if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float bare))
			{
				// 裸数字 = 0–1000 标尺
				scale = bare;
			}
		}

		if (scale.HasValue)
		{
			position = FromScale(scale.Value, offset, align);
			return true;
		}

		if (!anchor.HasValue)
		{
			return false;
		}

		position = new HintPosition(anchor.Value, offset, align, managed: true);
		return true;
	}

	/// <summary>解析横向对齐别名(接受英文与中文)。</summary>
	public static bool TryParseAlign(string? text, out HintAlignment alignment)
	{
		alignment = HintAlignment.Center;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		switch (text.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-'))
		{
			case "left":
			case "l":
			case "start":
			case "左":
			case "居左":
			case "靠左":
				alignment = HintAlignment.Left;
				return true;
			case "center":
			case "centre":
			case "c":
			case "middle":
			case "中":
			case "居中":
				alignment = HintAlignment.Center;
				return true;
			case "right":
			case "r":
			case "end":
			case "右":
			case "居右":
			case "靠右":
				alignment = HintAlignment.Right;
				return true;
			default:
				return false;
		}
	}

	/// <summary>解析锚点别名(接受英文 kebab/缩写与中文)。</summary>
	public static bool TryParseAnchor(string? text, out HintAnchor anchor)
	{
		anchor = HintAnchor.BottomCenter;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		string key = text.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
		switch (key)
		{
			case "bottom-center":
			case "bottom":
			case "bottomcentre":
			case "bottomcenter":
			case "bc":
			case "底部":
			case "底部居中":
			case "下":
			case "中下":
				anchor = HintAnchor.BottomCenter;
				return true;
			case "bottom-left":
			case "bottomleft":
			case "bl":
			case "左下":
			case "底部居左":
				anchor = HintAnchor.BottomLeft;
				return true;
			case "bottom-right":
			case "bottomright":
			case "br":
			case "右下":
			case "底部居右":
				anchor = HintAnchor.BottomRight;
				return true;
			case "middle-center":
			case "middle":
			case "center":
			case "centre":
			case "mc":
			case "中部":
			case "中部居中":
			case "中":
			case "屏幕中间":
			case "正中":
				anchor = HintAnchor.MiddleCenter;
				return true;
			case "middle-left":
			case "middleleft":
			case "ml":
			case "中左":
			case "中部居左":
				anchor = HintAnchor.MiddleLeft;
				return true;
			case "middle-right":
			case "middleright":
			case "mr":
			case "中右":
			case "中部居右":
				anchor = HintAnchor.MiddleRight;
				return true;
			case "top-center":
			case "top":
			case "topcentre":
			case "topcenter":
			case "tc":
			case "顶部":
			case "顶部居中":
			case "上":
			case "中上":
				anchor = HintAnchor.TopCenter;
				return true;
			case "top-left":
			case "topleft":
			case "tl":
			case "左上":
			case "顶部居左":
				anchor = HintAnchor.TopLeft;
				return true;
			case "top-right":
			case "topright":
			case "tr":
			case "右上":
			case "顶部居右":
				anchor = HintAnchor.TopRight;
				return true;
			default:
				return false;
		}
	}

	public bool Equals(HintPosition other)
		=> Anchor == other.Anchor
			&& Math.Abs(OffsetUnits - other.OffsetUnits) < 0.01f
			&& Math.Abs(Scale - other.Scale) < 0.01f
			&& Align == other.Align
			&& Managed == other.Managed;

	public override bool Equals(object? obj) => obj is HintPosition other && Equals(other);

	public override int GetHashCode()
	{
		int hash = ((((int)Anchor * 397) ^ OffsetUnits.GetHashCode()) * 397) ^ Scale.GetHashCode();
		hash = (hash * 397) ^ (Align.HasValue ? (int)Align.Value + 1 : 0);
		return hash ^ (Managed ? 1 : 0);
	}

	public override string ToString()
	{
		if (!Managed)
		{
			return "self";
		}

		string body = HasScale
			? "scale:" + Scale.ToString("0.#", CultureInfo.InvariantCulture)
			: Anchor.ToString();
		if (Align.HasValue)
		{
			body += "/" + Align.Value;
		}
		if (Math.Abs(OffsetUnits) >= 0.01f)
		{
			body += "(" + OffsetUnits.ToString("0.#", CultureInfo.InvariantCulture) + ")";
		}
		return body;
	}
}
