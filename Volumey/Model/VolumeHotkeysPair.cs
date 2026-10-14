#nullable enable
using System;
using Volumey.Controls;

namespace Volumey.Model;

[Serializable]
public sealed class VolumeHotkeysPair
{
	public HotKey VolumeUp { get; }
	public HotKey VolumeDown { get; }

	public VolumeHotkeysPair(HotKey volUp, HotKey volDown)
	{
		VolumeUp = volUp;
		VolumeDown = volDown;
	}
}
