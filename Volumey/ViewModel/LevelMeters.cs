#nullable enable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Volumey.Model;

namespace Volumey.ViewModel;

/// <summary>
/// The level meters of the mixer: while the mixer is visible and the meters are on, reads the peaks of the shown device's sessions
/// about 30 times a second and sets the <c>Level</c> of every session, app and the device. Peaks have no event in Windows, so they
/// are polled; nothing is read while the mixer is hidden.
/// </summary>
public sealed partial class LevelMeters : ObservableObject
{
	private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

	/// <summary>A level falls to this share of itself every 1/30 s after a peak: about half a second to silence</summary>
	private const double DecayPerFrame = 0.8;
	private const double FramesPerSecond = 30;

	/// <summary>Below this a level shows as nothing</summary>
	private const double Floor = 0.002;

	private readonly AppBehaviorViewModel window;
	private readonly DeviceProviderViewModel devices;
	private readonly DispatcherTimer timer;
	private readonly Stopwatch clock = new Stopwatch();
	private TimeSpan lastTick;
	private OutputDeviceModel? metered;

	/// <summary>
	/// Whether the mixer's sliders show levels: the meters are on and the shown device isn't muted
	/// (a muted device plays nothing, so its apps show their plain volume)
	/// </summary>
	[ObservableProperty]
	public partial bool ShowLevels { get; private set; }

	internal LevelMeters(AppBehaviorViewModel window, DeviceProviderViewModel devices)
	{
		this.window = window;
		this.devices = devices;
		timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = Interval };
		timer.Tick += OnTick;
		window.PropertyChanged += OnStateChanged;
		devices.PropertyChanged += OnStateChanged;
		Update();
	}

	private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName is nameof(AppBehaviorViewModel.WindowIsVisible) or nameof(AppBehaviorViewModel.LevelMeters)
			or nameof(DeviceProviderViewModel.SelectedDevice))
			Update();
	}

	private void OnMasterChanged(object? sender, PropertyChangedEventArgs e)
	{
		if(e.PropertyName == nameof(MasterSessionModel.IsMuted))
			Update();
	}

	/// <summary>Starts or stops reading, and moves to the device the mixer shows</summary>
	private void Update()
	{
		var device = devices.SelectedDevice;
		if(device != metered)
		{
			if(metered != null)
			{
				metered.Master.PropertyChanged -= OnMasterChanged;
				Clear(metered);
			}
			metered = device;
			if(metered != null)
				metered.Master.PropertyChanged += OnMasterChanged;
		}

		ShowLevels = window.LevelMeters && device is { Master.IsMuted: false };
		bool run = ShowLevels && window.WindowIsVisible;
		if(run && !timer.IsEnabled)
		{
			clock.Restart();
			lastTick = TimeSpan.Zero;
			timer.Start();
		}
		else if(!run && timer.IsEnabled)
		{
			timer.Stop();
			if(metered != null)
				Clear(metered);
		}
	}

	private void OnTick(object? sender, EventArgs e)
	{
		if(metered == null)
			return;
		var now = clock.Elapsed;
		Read(metered, now - lastTick);
		lastTick = now;
	}

	/// <summary>
	/// Sets the levels of the device's sessions, apps and the device from the sessions' current peaks.
	/// A session's level is its peak, or what is left of an earlier one; an app's is its loudest unmuted session;
	/// the device's is its loudest unmuted app at that app's volume. Muted ones show nothing.
	/// </summary>
	/// <param name="elapsed">Time since the last reading: how far the earlier levels have fallen</param>
	internal static void Read(OutputDeviceModel device, TimeSpan elapsed)
	{
		double deviceLevel = 0;
		foreach(var process in device.GetImmutableProcesses())
		{
			double processLevel = 0;
			foreach(var session in process.SessionsSnapshot())
			{
				double level = session.IsMuted ? 0 : Next(session.Level, session.ReadPeak(), elapsed);
				session.Level = level;
				processLevel = Math.Max(processLevel, level);
			}
			if(process.IsMuted)
				processLevel = 0;
			process.Level = processLevel;
			deviceLevel = Math.Max(deviceLevel, processLevel * process.Volume / 100.0);
		}
		device.Master.Level = device.Master.IsMuted ? 0 : deviceLevel;
	}

	/// <summary>A new peak shows at once; without one the level falls by <see cref="DecayPerFrame"/> per 1/30 s</summary>
	internal static double Next(double current, double peak, TimeSpan elapsed)
	{
		double fallen = current * Math.Pow(DecayPerFrame, Math.Max(0, elapsed.TotalSeconds) * FramesPerSecond);
		double level = Math.Clamp(Math.Max(peak, fallen), 0, 1);
		return level < Floor ? 0 : level;
	}

	/// <summary>Levels of a device that is no longer shown (or no longer metered) go back to nothing</summary>
	private static void Clear(OutputDeviceModel device)
	{
		foreach(var process in device.GetImmutableProcesses())
		{
			foreach(var session in process.SessionsSnapshot())
				session.Level = 0;
			process.Level = 0;
		}
		device.Master.Level = 0;
	}
}
