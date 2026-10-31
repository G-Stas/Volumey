#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using Volumey.CoreAudioWrapper.CoreAudio.Enums;
using Volumey.CoreAudioWrapper.CoreAudio.Interfaces;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.CoreAudioWrapper.Wrapper;

/// <summary>
/// App routes through Windows' audio policy factory. The factory is created on first use and kept for the app's lifetime;
/// it is agile, so any thread can call it.
/// </summary>
public sealed class AudioPolicyRouting : IAppAudioRouting
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(AudioPolicyRouting));

	private const string PolicyIdPrefix = @"\\?\SWD#MMDEVAPI#";
	//DEVINTERFACE_AUDIO_RENDER
	private const string PolicyIdSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

	private static readonly ERole[] Roles = [ERole.Console, ERole.Multimedia, ERole.Communications];

	//IntPtr.Zero when Windows doesn't offer a known interface
	private readonly Lazy<IntPtr> policy = new(Activate, LazyThreadSafetyMode.ExecutionAndPublication);

	public bool IsAvailable => policy.Value != IntPtr.Zero;

	public AppRoute? GetRoute(uint processId)
	{
		int hr = AudioPolicyConfigFactory.GetPersistedDefaultAudioEndpoint(Policy, processId, ERole.Console, out string policyId);
		if(hr < 0)
		{
			Logger.Debug($"No route of process {processId}: 0x{hr:X8}");
			return null;
		}
		return FromPolicyId(policyId) is { } deviceId ? new AppRoute.Pinned(deviceId) : new AppRoute.FollowsDefault();
	}

	public RouteWrite SetRoute(uint processId, AppRoute route)
	{
		string? policyId = route is AppRoute.Pinned pinned ? ToPolicyId(pinned.DeviceId) : null;
		foreach(var role in Roles)
		{
			int hr = AudioPolicyConfigFactory.SetPersistedDefaultAudioEndpoint(Policy, processId, role, policyId);
			if(hr < 0)
				return hr == AudioPolicyConfigFactory.E_INVALIDARG && !NativeMethods.IsProcessRunning((int)processId)
					? new RouteWrite.ProcessGone()
					: new RouteWrite.Refused(hr);
		}
		return new RouteWrite.Done();
	}

	private IntPtr Policy => IsAvailable ? policy.Value : throw new InvalidOperationException("Windows doesn't offer app routes");

	/// <summary>The device ID in the form the policy factory takes: <c>\\?\SWD#MMDEVAPI#{id}#{render interface}</c></summary>
	internal static string ToPolicyId(string deviceId)
		=> deviceId.StartsWith(PolicyIdPrefix, StringComparison.OrdinalIgnoreCase) ? deviceId : PolicyIdPrefix + deviceId + PolicyIdSuffix;

	/// <summary>The MMDevice ID in a device ID the policy factory returned; null for none, an unknown form unchanged</summary>
	internal static string? FromPolicyId(string? policyId) => policyId switch
	{
		null or "" => null,
		_ when policyId.StartsWith(PolicyIdPrefix, StringComparison.OrdinalIgnoreCase)
			&& policyId.EndsWith(PolicyIdSuffix, StringComparison.OrdinalIgnoreCase)
			&& policyId.Length > PolicyIdPrefix.Length + PolicyIdSuffix.Length
			=> policyId[PolicyIdPrefix.Length..^PolicyIdSuffix.Length],
		_ => policyId
	};

	/// <summary>The known interface among the ones the factory lists; null when it lists none (its vtable would be unknown)</summary>
	internal static Guid? PickInterface(IReadOnlyList<Guid> iids)
		=> AudioPolicyConfigFactory.KnownIids.Where(iids.Contains).Select(iid => (Guid?)iid).FirstOrDefault();

	private static IntPtr Activate()
	{
		try
		{
			int hr = AudioPolicyConfigFactory.Activate(out IntPtr factory);
			if(hr < 0)
			{
				Logger.Warn($"App routes aren't available: Windows refused the audio policy factory, 0x{hr:X8}");
				return IntPtr.Zero;
			}
			try
			{
				hr = AudioPolicyConfigFactory.GetIids(factory, out Guid[] iids);
				if(hr < 0)
				{
					Logger.Warn($"App routes aren't available: the audio policy factory didn't list its interfaces, 0x{hr:X8}");
					return IntPtr.Zero;
				}
				if(PickInterface(iids) is not { } iid)
				{
					Logger.Warn($"App routes aren't available: the audio policy factory has no known interface, it lists {string.Join(", ", iids)}");
					return IntPtr.Zero;
				}
				hr = AudioPolicyConfigFactory.QueryInterface(factory, iid, out IntPtr result);
				if(hr < 0)
				{
					Logger.Warn($"App routes aren't available: the audio policy factory refused interface {iid}, 0x{hr:X8}");
					return IntPtr.Zero;
				}
				return result;
			}
			finally
			{
				AudioPolicyConfigFactory.Release(factory);
			}
		}
		catch(Exception e)
		{
			Logger.Warn("App routes aren't available: the audio policy factory couldn't be created", e);
			return IntPtr.Zero;
		}
	}
}
