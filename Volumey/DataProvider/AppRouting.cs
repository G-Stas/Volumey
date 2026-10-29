#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using log4net;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.DataProvider;

/// <summary>
/// Keeps every app's route, its device's name and whether its rows belong in their device's lists equal to what Windows and the
/// sessions say, and moves apps to other devices. Windows holds the routes; the models only show them.
/// One worker does every refresh and move, one at a time: a trigger (a session starting or stopping, an app or device coming or going,
/// the default device changing) only marks an app as waiting, so an app waits for one refresh at most.
/// The device, app and session lists are read on the UI thread, their only writer; Windows is called on the worker.
/// The worker never waits for the UI thread to finish other work, and the UI thread never waits for the worker.
/// </summary>
public sealed class AppRouting : IDisposable
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(AppRouting));

	private static readonly TimeSpan SlowCall = TimeSpan.FromSeconds(2);

	private sealed record PendingMove(AudioProcessModel App, AppRoute To, AppRoute Previous, string? FromDeviceId, IReadOnlyList<uint> ProcessIds)
	{
		public TaskCompletionSource<MoveOutcome> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}

	private sealed record Member(AudioProcessModel App, string DeviceId, bool PlaysNow);

	/// <summary>An app's models on all devices and what the UI thread knew when the refresh started</summary>
	private sealed record AppSnapshot(IReadOnlyList<Member> Members, string? DefaultDeviceId,
		IReadOnlyList<(string Id, string Name)> ActiveDevices, IReadOnlyList<uint> ProcessIds, bool IsSystemSounds);

	private readonly IDeviceProvider devices;
	private readonly IAppAudioRouting windows;
	private readonly Func<string, string?> readDeviceName;
	private readonly Dispatcher ui;

	private readonly object gate = new();
	private readonly HashSet<AudioProcessModel> waiting = new();
	private readonly Queue<PendingMove> moves = new();
	private bool working;
	private bool stopped;

	//UI thread only
	private readonly HashSet<OutputDeviceModel> watchedDevices = new();
	private readonly HashSet<AudioProcessModel> watchedApps = new();
	private readonly List<(AppIdentity App, VolumeCopy Copy)> copies = new();
	private HashSet<AudioProcessModel> reportedHomes = new();

	//the names of devices apps are pinned to that aren't active; read on the worker, dropped when the device becomes active
	private readonly ConcurrentDictionary<string, string?> inactiveDeviceNames = new(StringComparer.OrdinalIgnoreCase);

	/// <param name="readDeviceName">Reads the name of a device in any state from Windows; null when Windows no longer knows it</param>
	/// <param name="ui">The UI thread's dispatcher; the constructor runs on it</param>
	public AppRouting(IDeviceProvider devices, IAppAudioRouting windows, Func<string, string?> readDeviceName, Dispatcher ui)
	{
		this.devices = devices;
		this.windows = windows;
		this.readDeviceName = readDeviceName;
		this.ui = ui;
		devices.ActiveDevices.CollectionChanged += OnDevicesChanged;
		devices.DefaultDeviceChanged += OnDefaultDeviceChanged;
		foreach(var device in devices.ActiveDevices)
			Watch(device);
		reportedHomes = GetApps().ToHashSet();
		RefreshAll();
	}

	/// <summary>Whether Windows lets apps be moved (asks Windows on the first call)</summary>
	public bool IsAvailable => windows.IsAvailable;

	/// <summary>
	/// The home model of each running app on all devices (<see cref="AppRoutingRules.HomeDevice"/>). On the UI thread
	/// </summary>
	public IImmutableList<AudioProcessModel> GetApps()
	{
		var models = AllModels();
		return Groups(models).Select(group => HomeOf(group)).OfType<AudioProcessModel>().ToImmutableList();
	}

	/// <summary>All models of the app on all devices (<see cref="AppRoutingRules.GroupApps"/>). On the UI thread</summary>
	public IReadOnlyList<AudioProcessModel> GetModels(AudioProcessModel app)
		=> GroupOf(AllModels(), app)?.Select(m => m.App).ToList() ?? [];

	/// <summary>The home model of the app one of whose sessions belongs to the process, on any device; null for none. On the UI thread</summary>
	public AudioProcessModel? FindApp(uint processId)
	{
		var models = AllModels();
		var found = models.FirstOrDefault(m => m.App.ProcessId == processId || m.App.SessionsSnapshot().Any(s => s.ProcessId == processId));
		return found == null ? null : HomeOf(GroupOf(models, found.App)!);
	}

	/// <summary>A home model changed (<see cref="GetApps"/>); on the UI thread</summary>
	public event Action? AppsChanged;

	/// <summary>Reads every app again, e.g. when the mixer opens: Windows tells nobody about routes changed outside Volumey</summary>
	public void RefreshAll()
	{
		foreach(var member in AllModels())
			MarkWaiting(member.App);
	}

	/// <summary>
	/// Sets a new route for the app on Windows (all its processes) and reads the app again. On the UI thread. A move to a device that
	/// isn't active is not made. After a move, the app's volume and mute on the device it played on are copied once to the device it
	/// moved to, when it first plays there.
	/// </summary>
	public Task<MoveOutcome> MoveAsync(AudioProcessModel app, AppRoute to)
	{
		if(to is AppRoute.Pinned pinned && !devices.ActiveDevices.Any(d => AppRoutingRules.SameDevice(d.Id, pinned.DeviceId)))
			return Task.FromResult<MoveOutcome>(new MoveOutcome.DeviceGone());
		var members = GroupOf(AllModels(), app) ?? [new Member(app, "", false)];
		var processIds = members.SelectMany(m => m.App.SessionsSnapshot().Select(s => s.ProcessId)).Where(id => id != 0).Distinct().ToList();
		string? from = AppRoutingRules.HomeDevice(app.Route, devices.DefaultDevice?.Id, DeviceIds(members));
		var move = new PendingMove(app, to, app.Route, from, processIds);
		lock(gate)
		{
			if(stopped)
				move.Result.SetCanceled();
			else
			{
				moves.Enqueue(move);
				StartWorker();
			}
		}
		return move.Result.Task;
	}

	private void MarkWaiting(AudioProcessModel app)
	{
		lock(gate)
		{
			if(stopped)
				return;
			waiting.Add(app);
			StartWorker();
		}
	}

	//under the gate
	private void StartWorker()
	{
		if(working)
			return;
		working = true;
		Task.Run(WorkAsync).Forget(Logger, "The app routing worker failed");
	}

	private async Task WorkAsync()
	{
		bool available = windows.IsAvailable;
		while(true)
		{
			PendingMove? move = null;
			AudioProcessModel? app = null;
			lock(gate)
			{
				if(stopped || !available)
				{
					foreach(var pending in moves)
						pending.Result.TrySetResult(new MoveOutcome.Refused());
					moves.Clear();
					waiting.Clear();
					working = false;
					return;
				}
				if(moves.Count > 0)
					move = moves.Dequeue();
				else if(waiting.Count > 0)
				{
					//taken out before the snapshot: a trigger during the refresh puts it back
					app = waiting.First();
					waiting.Remove(app);
				}
				else
				{
					working = false;
					return;
				}
			}
			try
			{
				if(move != null)
					await RunMoveAsync(move);
				else
					await RefreshAsync(app!);
			}
			catch(Exception e)
			{
				move?.Result.TrySetResult(new MoveOutcome.Refused());
				if(!IsStopped)
					Logger.Error($"Failed to {(move != null ? "move" : "read the route of")} [{(move?.App ?? app)?.Name}]", e);
			}
		}
	}

	private bool IsStopped
	{
		get
		{
			lock(gate)
				return stopped;
		}
	}

	private async Task RefreshAsync(AudioProcessModel app)
	{
		var snapshot = await OnUi(() => Snapshot(app));
		if(snapshot == null || IsStopped)
			return;
		var route = snapshot.IsSystemSounds ? new AppRoute.FollowsDefault() : ReadRoute(snapshot.ProcessIds);
		string? deviceName = route is AppRoute.Pinned pinned ? NameOf(pinned.DeviceId, snapshot.ActiveDevices) : null;
		var activeIds = snapshot.ActiveDevices.Select(d => d.Id).ToList();
		var placements = snapshot.Members.Select(m => (m.App, PlaysHere: AppRoutingRules.PlaysHere(new PlacementFacts(m.App.IsSystemSounds, m.PlaysNow,
			AppRoutingRules.Of(route, m.DeviceId, snapshot.DefaultDeviceId, activeIds),
			snapshot.Members.Any(other => !AppRoutingRules.SameDevice(other.DeviceId, m.DeviceId)))))).ToList();
		if(IsStopped)
			return;
		await OnUi(() =>
		{
			if(IsStopped)
				return;
			foreach(var (model, playsHere) in placements)
				if(!model.IsDisposed)
					model.SetPlacement(route, deviceName, playsHere);
			ApplyVolumeCopy(snapshot.Members);
			ReportHomes();
		});
	}

	private async Task RunMoveAsync(PendingMove move)
	{
		var writes = move.ProcessIds.Select(processId => Timed(() => windows.SetRoute(processId, move.To))).ToList();
		var outcome = AppRoutingRules.OutcomeOf(writes, move.Previous);
		if(IsStopped)
		{
			move.Result.TrySetCanceled();
			return;
		}
		await OnUi(() =>
		{
			string? target = move.To is AppRoute.Pinned pinned ? pinned.DeviceId : devices.DefaultDevice?.Id;
			string targetName = devices.ActiveDevices.FirstOrDefault(d => AppRoutingRules.SameDevice(d.Id, target))?.Master.Name ?? target ?? "";
			foreach(var refused in writes.OfType<RouteWrite.Refused>())
				Logger.Warn($"Windows refused to move [{move.App.Name}] to [{targetName}]: 0x{refused.HResult:X8}");
			switch(outcome)
			{
				case MoveOutcome.Moved:
					Logger.Info($"Moved [{move.App.Name}] to [{(move.To is AppRoute.Pinned ? targetName : "the default device")}]");
					copies.RemoveAll(c => AppRoutingRules.SameApp(c.App, move.App.Identity));
					if(move.FromDeviceId != null && target != null && !AppRoutingRules.SameDevice(move.FromDeviceId, target))
						copies.Add((move.App.Identity, new VolumeCopy(move.FromDeviceId, target)));
					break;
				case MoveOutcome.AppGone:
					Logger.Info($"[{move.App.Name}] exited before it could be moved");
					break;
			}
		});
		await RefreshAsync(move.App);
		move.Result.TrySetResult(outcome);
	}

	/// <summary>The route of the first process Windows answers for; an app none answers for follows the default device</summary>
	private AppRoute ReadRoute(IReadOnlyList<uint> processIds)
	{
		foreach(uint processId in processIds)
		{
			if(IsStopped)
				break;
			if(Timed(() => windows.GetRoute(processId)) is { } route)
				return route;
		}
		return new AppRoute.FollowsDefault();
	}

	private string? NameOf(string deviceId, IReadOnlyList<(string Id, string Name)> activeDevices)
	{
		foreach(var (id, name) in activeDevices)
			if(AppRoutingRules.SameDevice(id, deviceId))
				return name;
		return inactiveDeviceNames.GetOrAdd(deviceId, readDeviceName);
	}

	/// <summary>Runs a call into Windows, with one warning while it takes longer than <see cref="SlowCall"/> (it can't be cancelled)</summary>
	private static T Timed<T>(Func<T> call)
	{
		using var done = new CancellationTokenSource();
		Task.Delay(SlowCall, done.Token).ContinueWith(_ => Logger.Warn($"A call to Windows' audio policy takes longer than {SlowCall.TotalSeconds} s"),
			CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
		try
		{
			return call();
		}
		finally
		{
			done.Cancel();
		}
	}

	private Task<T> OnUi<T>(Func<T> work) => ui.InvokeAsync(work).Task;

	private Task OnUi(Action work) => ui.InvokeAsync(work).Task;

	//UI thread
	private AppSnapshot? Snapshot(AudioProcessModel app)
	{
		if(app.IsDisposed)
			return null;
		var members = GroupOf(AllModels(), app);
		if(members == null)
			return null;
		lock(gate)
		{
			//this refresh covers them
			waiting.ExceptWith(members.Select(m => m.App));
		}
		return new AppSnapshot(members, devices.DefaultDevice?.Id,
			devices.ActiveDevices.Select(d => (d.Id, d.Master.Name)).ToList(),
			members.SelectMany(m => m.App.SessionsSnapshot().Select(s => s.ProcessId)).Where(id => id != 0).Distinct().ToList(),
			members.Any(m => m.App.IsSystemSounds));
	}

	//UI thread: the app's volume and mute on the device it was moved from, once it plays on the device it was moved to
	private void ApplyVolumeCopy(IReadOnlyList<Member> members)
	{
		int index = copies.FindIndex(c => members.Any(m => AppRoutingRules.SameApp(c.App, m.App.Identity)));
		if(index < 0)
			return;
		var copy = copies[index].Copy;
		var source = members.FirstOrDefault(m => !m.App.IsDisposed && AppRoutingRules.SameDevice(m.DeviceId, copy.FromDeviceId));
		var target = members.FirstOrDefault(m => !m.App.IsDisposed && AppRoutingRules.SameDevice(m.DeviceId, copy.ToDeviceId));
		if(source == null)
			copies.RemoveAt(index);
		else if(target != null && AppRoutingRules.AppliesTo(copy, target.DeviceId, target.App.PlaysNow, sourceExists: true))
		{
			target.App.Volume = source.App.Volume;
			target.App.IsMuted = source.App.IsMuted;
			copies.RemoveAt(index);
		}
	}

	//UI thread
	private void ReportHomes()
	{
		var homes = GetApps().ToHashSet();
		if(homes.SetEquals(reportedHomes))
			return;
		reportedHomes = homes;
		AppsChanged?.Invoke();
	}

	//UI thread: the models on the active devices, in the devices' order
	private List<Member> AllModels()
		=> devices.ActiveDevices.SelectMany(d => d.GetImmutableProcesses().Where(p => !p.IsDisposed).Select(p => new Member(p, d.Id, p.PlaysNow))).ToList();

	private static IEnumerable<IReadOnlyList<Member>> Groups(List<Member> models)
		=> AppRoutingRules.GroupApps(models.Select(m => m.App.Identity).ToList()).Select(group => (IReadOnlyList<Member>)group.Select(i => models[i]).ToList());

	private static IReadOnlyList<Member>? GroupOf(List<Member> models, AudioProcessModel app)
		=> Groups(models).FirstOrDefault(group => group.Any(m => m.App == app));

	private static List<string> DeviceIds(IReadOnlyList<Member> members) => members.Select(m => m.DeviceId).Distinct().ToList();

	private AudioProcessModel? HomeOf(IReadOnlyList<Member> members)
	{
		string? home = AppRoutingRules.HomeDevice(members[0].App.Route, devices.DefaultDevice?.Id, DeviceIds(members));
		return members.FirstOrDefault(m => AppRoutingRules.SameDevice(m.DeviceId, home))?.App;
	}

	private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach(var device in e.OldItems?.OfType<OutputDeviceModel>() ?? [])
			Unwatch(device);
		foreach(var device in e.NewItems?.OfType<OutputDeviceModel>() ?? [])
		{
			inactiveDeviceNames.TryRemove(device.Id, out _);
			Watch(device);
		}
		//whether a pinned device is active changes the rows of its apps everywhere
		RefreshAll();
		ReportHomes();
	}

	private void OnDefaultDeviceChanged(OutputDeviceModel? device)
	{
		RefreshAll();
		ReportHomes();
	}

	private void Watch(OutputDeviceModel device)
	{
		if(!watchedDevices.Add(device))
			return;
		device.Processes.CollectionChanged += OnAppsChanged;
		foreach(var app in device.Processes)
			WatchApp(app);
	}

	private void Unwatch(OutputDeviceModel device)
	{
		if(!watchedDevices.Remove(device))
			return;
		device.Processes.CollectionChanged -= OnAppsChanged;
		foreach(var app in device.Processes)
			UnwatchApp(app);
	}

	private void WatchApp(AudioProcessModel app)
	{
		if(watchedApps.Add(app))
			app.ActivityChanged += MarkWaiting;
	}

	private void UnwatchApp(AudioProcessModel app)
	{
		if(watchedApps.Remove(app))
			app.ActivityChanged -= MarkWaiting;
	}

	//UI thread: an app started or exited on a device
	private void OnAppsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach(var app in e.OldItems?.OfType<AudioProcessModel>() ?? [])
		{
			UnwatchApp(app);
			//its other models may be leftover rows that belong where it played
			foreach(var member in AllModels().Where(m => AppRoutingRules.SameApp(m.App.Identity, app.Identity)))
				MarkWaiting(member.App);
		}
		foreach(var app in e.NewItems?.OfType<AudioProcessModel>() ?? [])
		{
			WatchApp(app);
			MarkWaiting(app);
		}
		if(e.Action == NotifyCollectionChangedAction.Reset)
			RefreshAll();
		ReportHomes();
	}

	/// <summary>
	/// Stops at once without waiting for the worker: a call into Windows in flight finishes on its own, and nothing is applied after it.
	/// Moves not made yet end as cancelled. On the UI thread
	/// </summary>
	public void Dispose()
	{
		lock(gate)
		{
			stopped = true;
			waiting.Clear();
			foreach(var move in moves)
				move.Result.TrySetCanceled();
			moves.Clear();
		}
		devices.ActiveDevices.CollectionChanged -= OnDevicesChanged;
		devices.DefaultDeviceChanged -= OnDefaultDeviceChanged;
		foreach(var device in watchedDevices.ToList())
			Unwatch(device);
		AppsChanged = null;
	}
}
