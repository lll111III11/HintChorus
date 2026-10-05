using System;
using System.Collections.Generic;
using HintIsolation.Core.Broker;
using HintIsolation.Core.Interfaces;
using HintIsolation.Core.ServerSpecific;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;
using UserSettings.ServerSpecific;

namespace HintIsolation.Core.Models;

public sealed class UiScope : IDisposable
{
	private readonly List<Action> _teardown = new List<Action>();

	private bool _disposed;

	public string ModuleId { get; }

	public bool IsDisposed => _disposed;

	public int Count => _teardown.Count;

	internal UiScope(string moduleId)
	{
		ModuleId = moduleId;
	}

	public IHintChannel? RegisterChannel(string moduleId, string displayName, string text = "", float duration = 2f, byte priority = 128)
	{
		HintChannel? hintChannel = HintBroker.Instance.RegisterChannel(moduleId, displayName, text, duration, priority);
		if (hintChannel != null)
		{
			Attach(() =>
			{
				HintBroker.Instance.UnregisterChannel(moduleId);
			});
		}
		return hintChannel;
	}

	public bool RegisterSource(IHintTextSource source)
	{
		if (source == null)
		{
			return false;
		}
		bool flag = HintBroker.Instance.RegisterSource(source);
		if (flag)
		{
			string id = source.ModuleId;
			Attach(() =>
			{
				HintBroker.Instance.UnregisterSource(id);
			});
		}
		return flag;
	}

	public RegisterResult RegisterSssPorts(string moduleId, IEnumerable<ServerSpecificSettingBase> settings)
	{
		RegisterResult result = SssRegistry.RegisterModule(moduleId, settings);
		if (result.Success)
		{
			Attach(() =>
			{
				SssRegistry.UnregisterModule(moduleId);
			});
		}
		return result;
	}

	public void SubscribeSssValue(string moduleId, Action<ReferenceHub, ServerSpecificSettingBase> handler)
	{
		SssRegistry.SubscribeValue(moduleId, handler);
		Attach(() =>
		{
			SssRegistry.UnsubscribeValue(handler);
		});
	}

	public void SubscribeSssStatus(string moduleId, Action<ReferenceHub, SSSUserStatusReport> handler)
	{
		SssRegistry.SubscribeStatus(moduleId, handler);
		Attach(() =>
		{
			SssRegistry.UnsubscribeStatus(handler);
		});
	}

	internal void Attach(Action teardown)
	{
		if (teardown != null)
		{
			if (_disposed)
			{
				SafeInvoke(teardown);
			}
			else
			{
				_teardown.Add(teardown);
			}
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			for (int num = _teardown.Count - 1; num >= 0; num--)
			{
				SafeInvoke(_teardown[num]);
			}
			_teardown.Clear();
		}
	}

	private static void SafeInvoke(Action action)
	{
		try
		{
			action();
		}
		catch (Exception arg)
		{
			Logger.Error((object)$"[HintIsolation] 作用域注销动作异常(已隔离): {arg}");
		}
	}
}
