using System;
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
/// <para><b>三种来源(优先级从高到低)</b>:</para>
/// <list type="number">
///   <item><b>显式</b>: 插件用自有写法声明(文本标记 <c>{{hc:pos=...}}</c>, 或 C# API <c>SetSlotPosition</c>);</item>
///   <item><b>兼容</b>: 插件文本里已自带位置标签(<c>&lt;voffset&gt;</c> / <c>&lt;pos&gt;</c> / <c>&lt;align&gt;</c> 等)
///     → <see cref="SelfPositioned"/>, 本底层<b>原样放行</b>不重排(见 <c>PositionSyntax</c>);</item>
///   <item><b>自动</b>: 按插件名 / 已收录表推断(见 <see cref="PluginPositionCatalog"/>)。</item>
/// </list>
///
/// <para><b>为什么不直接写死坐标</b>: 客户端提示只有<b>一个槽</b>, 所有内容必须合成一条文本下发。
/// 因此"位置"最终都折算成行级 <c>&lt;voffset&gt;</c> 修正量 —— 这里是声明式的意图, 换算是
/// <c>HintBroker</c> 的事。</para>
/// </summary>
public readonly struct HintPosition : IEquatable<HintPosition>
{
	/// <summary>默认位置: 底部中央 + 零偏移 + 受管 —— 与未启用定位时的行为<b>逐字一致</b>。</summary>
	public static readonly HintPosition Default = new HintPosition(HintAnchor.BottomCenter, 0f, managed: true);

	/// <summary>自带位置标签: 插件已自己摆好, 本底层原样放行、不加任何 <c>&lt;voffset&gt;</c>。</summary>
	public static readonly HintPosition SelfPositioned = new HintPosition(HintAnchor.BottomCenter, 0f, managed: false);

	public HintPosition(HintAnchor anchor, float offsetUnits = 0f, bool managed = true)
		: this(anchor, offsetUnits, -1f, managed)
	{
	}

	public HintPosition(HintAnchor anchor, float offsetUnits, float scale, bool managed = true)
	{
		Anchor = anchor;
		OffsetUnits = offsetUnits;
		Scale = scale;
		Managed = managed;
	}

	/// <summary>锚点(九宫格)。</summary>
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

	/// <summary>是否使用了 0–1000 纵向标尺。</summary>
	public bool HasScale => Managed && Scale >= 0f;

	/// <summary>按 0–1000 标尺构造(生态兼容入口)。</summary>
	public static HintPosition FromScale(float scale, float offsetUnits = 0f)
	{
		return new HintPosition(HintAnchor.MiddleCenter, offsetUnits, scale, managed: true);
	}

	/// <summary>true = 由本底层摆位; false = 插件自带位置标签, 原样放行。</summary>
	public bool Managed { get; }

	/// <summary>是否等同于默认位置(底部中央 + 零偏移 + 未用标尺 + 受管)。</summary>
	public bool IsDefault => Managed && !HasScale && Anchor == HintAnchor.BottomCenter && Math.Abs(OffsetUnits) < 0.01f;

	/// <summary>是否"自带位置标签"(不参与本底层的摆位换算)。</summary>
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

	/// <summary>该锚点对应的水平对齐(仅对非默认位置生效, 以免改变既有观感)。</summary>
	public static HintAlignment AlignOf(HintAnchor anchor)
	{
		return anchor switch
		{
			HintAnchor.BottomLeft or HintAnchor.MiddleLeft or HintAnchor.TopLeft => HintAlignment.Left,
			HintAnchor.BottomRight or HintAnchor.MiddleRight or HintAnchor.TopRight => HintAlignment.Right,
			_ => HintAlignment.Center,
		};
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
			&& Managed == other.Managed;

	public override bool Equals(object? obj) => obj is HintPosition other && Equals(other);

	public override int GetHashCode()
		=> ((((int)Anchor * 397) ^ OffsetUnits.GetHashCode()) * 397 ^ Scale.GetHashCode()) ^ (Managed ? 1 : 0);

	public override string ToString()
	{
		if (!Managed)
		{
			return "self";
		}
		if (HasScale)
		{
			return "scale:" + Scale.ToString("0.#") + (Math.Abs(OffsetUnits) < 0.01f ? string.Empty : "(" + OffsetUnits.ToString("0.#") + ")");
		}
		return Anchor + (Math.Abs(OffsetUnits) < 0.01f ? string.Empty : "(" + OffsetUnits.ToString("0.#") + ")");
	}
}
