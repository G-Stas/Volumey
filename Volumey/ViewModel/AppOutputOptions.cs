#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Volumey.Model;

namespace Volumey.ViewModel;

/// <summary>
/// An item of an app's device box
/// </summary>
/// <param name="DeviceId">The device; null for "Default device"</param>
/// <param name="Name">The device's name; empty for "Default device" and for a device Windows can't name</param>
/// <param name="Detail">"Default device": the current default's name. A device Windows can't name: the same, for the closed box,
/// which then shows where Windows plays the app meanwhile</param>
/// <param name="IsEnabled">False for a device the app is pinned to that isn't active</param>
public sealed record AppOutputOption(string? DeviceId, string Name, string? Detail, bool IsEnabled);

/// <summary>A notice at the bottom of the mixer; each one is shown, also one with the same text as the last</summary>
/// <param name="canUndo">It has an Undo button</param>
public sealed class MixerNotice(string text, bool canUndo)
{
	public string Text { get; } = text;
	public bool CanUndo { get; } = canUndo;
}

public static class AppOutputOptions
{
	/// <summary>
	/// "Default device" first, then the active devices in their order; a device the app is pinned to that isn't active comes last
	/// </summary>
	public static IReadOnlyList<AppOutputOption> Build(IReadOnlyList<(string Id, string Name)> activeDevices, string defaultDeviceName,
		AppRoute route, string? pinnedDeviceName)
	{
		var options = activeDevices.Select(d => new AppOutputOption(d.Id, d.Name, null, true))
			.Prepend(new AppOutputOption(null, "", defaultDeviceName, true)).ToList();
		if(route is AppRoute.Pinned pinned && !activeDevices.Any(d => AppRoutingRules.SameDevice(d.Id, pinned.DeviceId)))
			options.Add(new AppOutputOption(pinned.DeviceId, pinnedDeviceName ?? "", pinnedDeviceName == null ? defaultDeviceName : null, false));
		return options;
	}

	/// <summary>The index of the app's current choice in <paramref name="options"/></summary>
	public static int IndexOf(IReadOnlyList<AppOutputOption> options, AppRoute route)
		=> route is AppRoute.Pinned pinned
			? Math.Max(0, options.ToList().FindIndex(o => o.DeviceId != null && AppRoutingRules.SameDevice(o.DeviceId, pinned.DeviceId)))
			: 0;
}
