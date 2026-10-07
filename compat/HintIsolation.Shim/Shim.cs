using System;
using HintIsolation.Core.Interfaces;

namespace HintIsolation.Core.Enums
{
	/// <summary>
	/// <b>兼容垫片</b>: 旧程序集名 <c>HintIsolation</c> 下的对齐枚举。
	/// <para>取值与 <c>HintChorus.Core.Enums.HintAlignment</c> 保持一致, 便于依赖方按旧方式使用。</para>
	/// </summary>
	public enum HintAlignment
	{
		/// <summary>左对齐。</summary>
		Left,
		/// <summary>居中。</summary>
		Center,
		/// <summary>右对齐。</summary>
		Right,
	}
}

namespace HintIsolation.Core.Interfaces
{
	/// <summary>
	/// <b>兼容垫片</b>: 旧名下的文本源接口。
	/// <para>成员形状与 <c>HintChorus.Core.Interfaces.IHintTextSource</c> 完全一致 ——
	/// 依赖方(如 FullModSquad)实现的就是这几个成员, 少一个都会导致类型加载失败。</para>
	/// </summary>
	public interface IHintTextSource
	{
		/// <summary>模块标识(全局唯一)。</summary>
		string ModuleId { get; }

		/// <summary>显示名。</summary>
		string DisplayName { get; }

		/// <summary>渲染优先级。</summary>
		byte Priority { get; }

		/// <summary>取该玩家当前要显示的文本。</summary>
		bool TryGetText(ReferenceHub hub, out string text);
	}
}

namespace HintIsolation
{
	/// <summary>
	/// <b>兼容垫片</b> —— 让"编译时引用了旧程序集名 <c>HintIsolation</c>"的插件,
	/// 在项目改名为 <c>HintChorus</c> 之后<b>无需重编</b>即可继续加载与运行。
	///
	/// <para>所有调用都<b>转发</b>到 <c>HintChorus.UiIsolation</c>, 本类不含任何业务逻辑。</para>
	///
	/// <para><b>何时可以删掉</b>: 当所有依赖旧名的插件都改成引用 <c>HintChorus</c> 并重新编译后,
	/// 这个垫片程序集与仓库里的 <c>compat/HintIsolation.Shim</c> 就可以一并移除。</para>
	/// </summary>
	public static class UiIsolation
	{
		/// <summary>把旧接口的实例适配成 HintChorus 的文本源。</summary>
		private sealed class Adapter : HintChorus.Core.Interfaces.IHintTextSource
		{
			private readonly IHintTextSource _inner;

			public Adapter(IHintTextSource inner)
			{
				_inner = inner;
			}

			public string ModuleId => _inner.ModuleId;

			public string DisplayName => _inner.DisplayName;

			public byte Priority => _inner.Priority;

			public bool TryGetText(ReferenceHub hub, out string text) => _inner.TryGetText(hub, out text);
		}

		/// <summary>注册一个文本源(转发到 HintChorus)。</summary>
		public static bool RegisterHintSource(IHintTextSource source)
		{
			if (source == null)
			{
				return false;
			}

			return HintChorus.UiIsolation.RegisterHintSource(new Adapter(source));
		}

		/// <summary>注销文本源(转发到 HintChorus)。</summary>
		public static bool UnregisterHintSource(string moduleId)
		{
			return HintChorus.UiIsolation.UnregisterHintSource(moduleId);
		}
	}
}
