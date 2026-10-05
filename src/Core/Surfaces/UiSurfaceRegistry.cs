using System.Collections.Generic;
using System.Linq;
using HintIsolation.Core.Enums;

namespace HintIsolation.Core.Surfaces;

public static class UiSurfaceRegistry
{
	private static readonly List<UiSurfaceDescriptor> AllSurfaces = new List<UiSurfaceDescriptor>
	{
		new UiSurfaceDescriptor(UiSurface.Hint, "屏幕提示条", implemented: true, "HintBroker + UiInterception", "HintDisplay.Show(Hint)"),
		new UiSurfaceDescriptor(UiSurface.Broadcast, "屏幕中央广播", implemented: true, "BroadcastIsolation + UiInterception", "Broadcast.TargetAddElement / RpcAddElement / TargetClearElements / RpcClearElements"),
		new UiSurfaceDescriptor(UiSurface.ServerSettings, "服务器设置页控件", implemented: true, "SssRegistry", "ServerSpecificSettingsSync.DefinedSettings"),
		new UiSurfaceDescriptor(UiSurface.Console, "玩家控制台消息", implemented: true, "ConsoleSurfaceIsolation + UiInterception", "GameConsoleTransmission.SendToClient"),
		new UiSurfaceDescriptor(UiSurface.Cassie, "CASSIE 语音播报", implemented: true, "CassieSurfaceIsolation + UiInterception", "CassieAnnouncementDispatcher.AddToQueue / ClearAll"),
		new UiSurfaceDescriptor(UiSurface.AdminChat, "管理端聊天面板", implemented: true, "AdminChatSurfaceIsolation + UiInterception", "EncryptedChannelManager.EncryptedChannel.AdminChat"),
		new UiSurfaceDescriptor(UiSurface.HitMarker, "准星命中标记", implemented: true, "HitMarkerSurfaceIsolation + UiInterception", "Hitmarker.SendHitmarkerDirectly"),
		new UiSurfaceDescriptor(UiSurface.Intercom, "对讲机/广播室显示屏", implemented: true, "IntercomGuard(底层复写: 拦 Mirror 同步序列化, 下发前改回已放行的值)", "IntercomDisplay.SerializeSyncVars")
	};

	public static IReadOnlyList<UiSurfaceDescriptor> All => AllSurfaces;

	public static IReadOnlyList<UiSurfaceDescriptor> Implemented => AllSurfaces.Where((UiSurfaceDescriptor s) => s.Implemented).ToArray();

	public static IReadOnlyList<UiSurfaceDescriptor> Pending => AllSurfaces.Where((UiSurfaceDescriptor s) => !s.Implemented).ToArray();

	public static UiSurfaceDescriptor Describe(UiSurface surface)
	{
		return AllSurfaces.First((UiSurfaceDescriptor s) => s.Surface == surface);
	}
}
