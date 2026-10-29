#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Volumey.Model;

/// <summary>
/// Where Windows plays an app: the default device, or one device it is pinned to. Windows stores it; Volumey never saves it
/// </summary>
public abstract record AppRoute
{
	public sealed record FollowsDefault : AppRoute;

	/// <param name="DeviceId">MMDevice ID, as <see cref="CoreAudioWrapper.Wrapper.IDevice.Id"/></param>
	public sealed record Pinned(string DeviceId) : AppRoute;
}

/// <summary>
/// An app's route as seen from one device
/// </summary>
public abstract record RouteHere
{
	public sealed record FollowsDefault(bool ThisIsDefault) : RouteHere;

	public sealed record PinnedHere : RouteHere;

	public sealed record PinnedElsewhere(bool DeviceIsActive) : RouteHere;
}

/// <summary>
/// What decides whether an app's row belongs in one device's list
/// </summary>
/// <param name="PlaysNowOnThisDevice">A session of the app on this device is active</param>
/// <param name="HasModelOnOtherDevice">The app has a row on another active device too</param>
public sealed record PlacementFacts(bool IsSystemSounds, bool PlaysNowOnThisDevice, RouteHere Route, bool HasModelOnOtherDevice);

/// <summary>
/// What tells one app from another: Windows' grouping parameter of its sessions and its executable
/// </summary>
public sealed record AppIdentity(Guid GroupingParam, string FilePath);

/// <summary>
/// After a move made in Volumey: the app's volume and mute on <paramref name="FromDeviceId"/> at the moment it first plays on
/// <paramref name="ToDeviceId"/> are copied there once
/// </summary>
public sealed record VolumeCopy(string FromDeviceId, string ToDeviceId);

/// <summary>
/// Windows' answer to setting the route of one process
/// </summary>
public abstract record RouteWrite
{
	public sealed record Done : RouteWrite;

	/// <summary>Windows refused with E_INVALIDARG and the process isn't running</summary>
	public sealed record ProcessGone : RouteWrite;

	public sealed record Refused(int HResult) : RouteWrite;
}

/// <summary>
/// How a move of an app ended
/// </summary>
public abstract record MoveOutcome
{
	/// <param name="Previous">The route before the move: an Undo moves back to it</param>
	public sealed record Moved(AppRoute Previous) : MoveOutcome;

	public sealed record Refused : MoveOutcome;

	/// <summary>The chosen device left before the move was made</summary>
	public sealed record DeviceGone : MoveOutcome;

	/// <summary>Every process of the app has exited</summary>
	public sealed record AppGone : MoveOutcome;
}

/// <summary>
/// The decisions of moving apps between output devices
/// </summary>
public static class AppRoutingRules
{
	/// <summary>
	/// One app: the same grouping parameter or the same executable. Device models group an app's sessions by this rule
	/// </summary>
	public static bool SameApp(AppIdentity a, AppIdentity b)
		=> a.GroupingParam != Guid.Empty && a.GroupingParam.Equals(b.GroupingParam)
		   || !string.IsNullOrEmpty(a.FilePath) && Equals(a.FilePath, b.FilePath);

	/// <summary>
	/// The apps among the models of all devices: models linked by <see cref="SameApp"/>, directly or through others, are one app
	/// (<see cref="SameApp"/> alone isn't transitive). Each group lists indexes into <paramref name="models"/>, in input order;
	/// the groups are in the order of their first model
	/// </summary>
	public static IReadOnlyList<IReadOnlyList<int>> GroupApps(IReadOnlyList<AppIdentity> models)
	{
		//union-find: each model points towards the first model of its group
		var parent = Enumerable.Range(0, models.Count).ToArray();
		int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);
		for(int i = 0; i < models.Count; i++)
			for(int j = 0; j < i; j++)
				if(SameApp(models[i], models[j]))
					parent[Math.Max(Root(i), Root(j))] = Math.Min(Root(i), Root(j));
		return Enumerable.Range(0, models.Count).GroupBy(Root).Select(g => (IReadOnlyList<int>)g.ToList()).ToList();
	}

	/// <param name="activeDeviceIds">The IDs of the active output devices</param>
	public static RouteHere Of(AppRoute route, string thisDeviceId, string? defaultDeviceId, IReadOnlyCollection<string> activeDeviceIds)
		=> route switch
		{
			AppRoute.Pinned pinned when SameDevice(pinned.DeviceId, thisDeviceId) => new RouteHere.PinnedHere(),
			AppRoute.Pinned pinned => new RouteHere.PinnedElsewhere(activeDeviceIds.Any(id => SameDevice(id, pinned.DeviceId))),
			_ => new RouteHere.FollowsDefault(SameDevice(defaultDeviceId, thisDeviceId))
		};

	/// <summary>Whether the app's row belongs in this device's list</summary>
	public static bool PlaysHere(PlacementFacts facts) => facts switch
	{
		{ IsSystemSounds: true } => true,
		{ PlaysNowOnThisDevice: true } => true,
		{ Route: RouteHere.PinnedHere } => true,
		{ Route: RouteHere.PinnedElsewhere { DeviceIsActive: true } } => false,
		//left over from an earlier pin or a move back to the default device
		{ Route: RouteHere.FollowsDefault { ThisIsDefault: false }, HasModelOnOtherDevice: true } => false,
		//an idle app that opened this device itself
		_ => true
	};

	/// <summary>
	/// The device of the app's home row: the pinned device when the app has a model there, else the default device, else the first
	/// in <paramref name="devicesWithModel"/>; null when it has none. Its hotkeys sit there, and a volume copy reads from it
	/// </summary>
	public static string? HomeDevice(AppRoute route, string? defaultDeviceId, IReadOnlyList<string> devicesWithModel)
		=> (route is AppRoute.Pinned pinned ? devicesWithModel.FirstOrDefault(id => SameDevice(id, pinned.DeviceId)) : null)
			?? devicesWithModel.FirstOrDefault(id => SameDevice(id, defaultDeviceId))
			?? devicesWithModel.FirstOrDefault();

	/// <summary>The copy runs once the app plays on its target device and still has its model on the source device</summary>
	public static bool AppliesTo(VolumeCopy copy, string deviceId, bool playsNow, bool sourceExists)
		=> SameDevice(copy.ToDeviceId, deviceId) && playsNow && sourceExists;

	/// <summary>
	/// A move from Windows' answers for each process of the app: one done is enough (Windows keeps the route per app)
	/// </summary>
	public static MoveOutcome OutcomeOf(IReadOnlyList<RouteWrite> writes, AppRoute previous)
		=> writes.Any(w => w is RouteWrite.Done) ? new MoveOutcome.Moved(previous)
			: writes.All(w => w is RouteWrite.ProcessGone) ? new MoveOutcome.AppGone()
			: new MoveOutcome.Refused();

	/// <summary>
	/// Whether to set the app's route again: Windows plays a pinned app on the default device while its device is away and doesn't
	/// send it back when the device returns (the app hears of its route only when it's set)
	/// </summary>
	/// <param name="returnedDeviceIds">Devices that became active since the apps were last read</param>
	public static bool ReappliesRoute(AppRoute route, IReadOnlyCollection<string> returnedDeviceIds, bool playsNowOnPinnedDevice)
		=> route is AppRoute.Pinned pinned && !playsNowOnPinnedDevice && returnedDeviceIds.Any(id => SameDevice(id, pinned.DeviceId));

	/// <summary>Device IDs Windows' audio policy returns may differ in case from the device enumerator's</summary>
	public static bool SameDevice(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
