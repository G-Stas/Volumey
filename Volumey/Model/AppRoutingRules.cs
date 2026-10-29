#nullable enable
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
/// Windows' answer to setting the route of one process
/// </summary>
public abstract record RouteWrite
{
	public sealed record Done : RouteWrite;

	/// <summary>Windows refused with E_INVALIDARG and the process isn't running</summary>
	public sealed record ProcessGone : RouteWrite;

	public sealed record Refused(int HResult) : RouteWrite;
}
