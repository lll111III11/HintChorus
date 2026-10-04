using System;
using LabApi.Features.Console;
using Logger = LabApi.Features.Console.Logger;

namespace HintIsolation.Core.Utilities;

public static class SafeEvents
{
	public static Action Wrap(string moduleId, Action handler)
	{
		return () =>
		{
			try
			{
				handler();
			}
			catch (Exception arg)
			{
				Logger.Error((object)$"[SafeEvents:{moduleId}] 事件处理异常(已隔离): {arg}");
			}
		};
	}

	public static Action<T1> Wrap<T1>(string moduleId, Action<T1> handler)
	{
		return (T1 p1) =>
		{
			try
			{
				handler(p1);
			}
			catch (Exception arg)
			{
				Logger.Error((object)$"[SafeEvents:{moduleId}] 事件处理异常(已隔离): {arg}");
			}
		};
	}

	public static Action<T1, T2> Wrap<T1, T2>(string moduleId, Action<T1, T2> handler)
	{
		return (T1 p1, T2 p2) =>
		{
			try
			{
				handler(p1, p2);
			}
			catch (Exception arg)
			{
				Logger.Error((object)$"[SafeEvents:{moduleId}] 事件处理异常(已隔离): {arg}");
			}
		};
	}

	public static Action<T1, T2, T3> Wrap<T1, T2, T3>(string moduleId, Action<T1, T2, T3> handler)
	{
		return (T1 p1, T2 p2, T3 p3) =>
		{
			try
			{
				handler(p1, p2, p3);
			}
			catch (Exception arg)
			{
				Logger.Error((object)$"[SafeEvents:{moduleId}] 事件处理异常(已隔离): {arg}");
			}
		};
	}
}
