using System.Runtime.InteropServices;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// Where Windows plays an app (its per-app output device, as Windows' volume mixer settings set it), read and set from outside
/// Volumey: to set up a test and to put an app back on the default device afterwards. Never changes the default device.
/// </summary>
internal static class Routes
{
	private const string PolicyIdPrefix = @"\\?\SWD#MMDEVAPI#";
	private const string PolicyIdSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
	private static readonly ERole[] Roles = [ERole.Console, ERole.Multimedia, ERole.Communications];

	//the policy factory with a known interface; zero when Windows has none
	private static readonly Lazy<IntPtr> Policy = new(() =>
	{
		if(AudioPolicyConfigFactory.Activate(out IntPtr factory) < 0)
			return IntPtr.Zero;
		try
		{
			if(AudioPolicyConfigFactory.GetIids(factory, out Guid[] iids) < 0)
				return IntPtr.Zero;
			var known = AudioPolicyConfigFactory.KnownIids.Where(iids.Contains).ToArray();
			return known.Length > 0 && AudioPolicyConfigFactory.QueryInterface(factory, known[0], out IntPtr policy) >= 0 ? policy : IntPtr.Zero;
		}
		finally
		{
			AudioPolicyConfigFactory.Release(factory);
		}
	});

	public static bool Available => Policy.Value != IntPtr.Zero;

	/// <summary>The device Windows plays the process's app on; null when it follows the default device or Windows doesn't answer</summary>
	public static string? PinnedDevice(int processId)
	{
		if(!Available || AudioPolicyConfigFactory.GetPersistedDefaultAudioEndpoint(Policy.Value, (uint)processId, ERole.Console, out string id) < 0
		   || id.Length == 0)
			return null;
		return id.StartsWith(PolicyIdPrefix, StringComparison.OrdinalIgnoreCase) && id.EndsWith(PolicyIdSuffix, StringComparison.OrdinalIgnoreCase)
			? id[PolicyIdPrefix.Length..^PolicyIdSuffix.Length]
			: id;
	}

	/// <summary>Sets the app to play on the device, as Windows' own settings would (outside Volumey)</summary>
	public static void Pin(int processId, string deviceId) => Set(processId, PolicyIdPrefix + deviceId + PolicyIdSuffix);

	/// <summary>Puts the app back on the default device</summary>
	public static void Reset(int processId) => Set(processId, null);

	/// <summary>Puts the app back on the default device when disposed (dispose it before the app: Windows needs the running process)</summary>
	public static IDisposable ResetLater(TestAudioApp app)
	{
		int processId = app.Process.Id;
		return new Restore(() =>
		{
			try
			{
				Reset(processId);
			}
			catch(Exception e) when(e is COMException or ArgumentException)
			{
				//the app has exited: a new start of the test app's fresh executable follows the default device anyway
			}
		});
	}

	private static void Set(int processId, string? policyDeviceId)
	{
		if(!Available)
			throw new InvalidOperationException("Windows doesn't offer per-app output devices");
		foreach(var role in Roles)
			Marshal.ThrowExceptionForHR(AudioPolicyConfigFactory.SetPersistedDefaultAudioEndpoint(Policy.Value, (uint)processId, role, policyDeviceId));
	}

	[ComImport, Guid(GuidValue.External.PolicyConfigCOM)]
	private class PolicyConfigClass { }

	/// <summary>
	/// Turns an output device off or on, as Windows' sound settings do (an unplugged device looks the same to apps).
	/// Refuses a device that is the default for any role: the tests never change the default device
	/// </summary>
	public static void SetDeviceEnabled(string deviceId, bool enabled)
	{
		if(!enabled && Audio.IsDefaultForAnyRole(deviceId))
			throw new InvalidOperationException("The tests never turn off a default device");
		var policy = (IPolicyConfig)new PolicyConfigClass();
		try
		{
			Marshal.ThrowExceptionForHR(policy.SetEndpointVisibility(deviceId, enabled));
		}
		finally
		{
			Marshal.ReleaseComObject(policy);
		}
	}

	/// <summary>Turns the device on again when disposed</summary>
	public static IDisposable KeepDeviceEnabled(string deviceId) => new Restore(() => SetDeviceEnabled(deviceId, true));

	private sealed class Restore(Action restore) : IDisposable
	{
		public void Dispose() => restore();
	}
}
