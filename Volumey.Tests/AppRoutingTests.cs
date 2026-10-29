using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

/// <summary>
/// <see cref="AppRouting"/> with fake Windows answers, on a real UI thread (its own dispatcher), as in the app.
/// </summary>
[TestClass]
public class AppRoutingTests
{
	private const string A = "{0.0.0.00000000}.{a}";
	private const string B = "{0.0.0.00000000}.{b}";
	private const string Unplugged = "{0.0.0.00000000}.{unplugged}";

	/// <summary>Windows' side: the route per process, Get blocks while <see cref="GetGate"/> is closed</summary>
	private sealed class FakeWindows : IAppAudioRouting
	{
		public bool Available = true;
		public bool IsAvailable => Available;
		public readonly ConcurrentDictionary<uint, AppRoute> Routes = new();
		public readonly ManualResetEventSlim GetGate = new(true);
		public int Gets;
		public int Sets;
		public Func<uint, AppRoute, RouteWrite> OnSet = (_, _) => new RouteWrite.Done();

		public AppRoute GetRoute(uint processId)
		{
			Interlocked.Increment(ref Gets);
			GetGate.Wait();
			return Routes.TryGetValue(processId, out var route) ? route : new AppRoute.FollowsDefault();
		}

		public RouteWrite SetRoute(uint processId, AppRoute route)
		{
			Interlocked.Increment(ref Sets);
			var write = OnSet(processId, route);
			if(write is RouteWrite.Done)
				Routes[processId] = route;
			return write;
		}
	}

	private sealed class App
	{
		public AudioProcessModel Model;
		public readonly List<Mock<IAudioSessionStateNotifications>> Sessions = new();
		public void Play(int session = 0) => Sessions[session].Raise(n => n.Activated += null);
		public void Stop(int session = 0) => Sessions[session].Raise(n => n.Deactivated += null);
	}

	private Thread uiThread;
	private Dispatcher ui;
	private HotkeyService hotkeys;
	private FakeWindows windows;
	private ObservableCollection<OutputDeviceModel> devices;
	private Mock<IDeviceProvider> provider;
	private AppRouting routing;

	[TestInitialize]
	public void Initialize()
	{
		using var started = new ManualResetEventSlim();
		uiThread = new Thread(() =>
		{
			ui = Dispatcher.CurrentDispatcher;
			started.Set();
			Dispatcher.Run();
		});
		uiThread.SetApartmentState(ApartmentState.STA);
		uiThread.Start();
		started.Wait();

		hotkeys = new HotkeyService();
		windows = new FakeWindows();
		devices = new ObservableCollection<OutputDeviceModel>();
		provider = new Mock<IDeviceProvider>();
		provider.SetupProperty(p => p.DefaultDevice);
		provider.Setup(p => p.ActiveDevices).Returns(devices);
	}

	[TestCleanup]
	public void Cleanup()
	{
		windows.GetGate.Set();
		OnUi(() => routing?.Dispose());
		ui.InvokeShutdown();
		uiThread.Join();
	}

	private T OnUi<T>(Func<T> work) => ui.Invoke(work);
	private void OnUi(Action work) => ui.Invoke(work);

	private OutputDeviceModel Device(string id, string name, params App[] apps) => OnUi(() =>
	{
		var master = new MasterSessionModel(name, name, 70, false, id, null, new Mock<IAudioSessionVolume>().Object,
			new Mock<IMasterVolumeNotificationHandler>().Object, hotkeys);
		var device = new Mock<IDevice>();
		device.Setup(d => d.GetId()).Returns(id);
		var model = new OutputDeviceModel(device.Object, new Mock<IDeviceStateNotificationHandler>().Object, new Mock<ISessionProvider>().Object,
			master, apps.Select(a => a.Model).ToList());
		devices.Add(model);
		return model;
	});

	private static readonly Dictionary<string, Guid> GroupingParams = new();

	/// <summary>An app with one session per process ID, like Windows makes them</summary>
	private App NewApp(string name, int volume, bool playing, params uint[] processIds)
	{
		var app = new App();
		app.Model = new AudioProcessModel(volume, false, name, processIds[0], $@"C:\Apps\{name}.exe", null, null, hotkeys);
		foreach(uint processId in processIds)
		{
			var notifications = new Mock<IAudioSessionStateNotifications>();
			var session = new AudioSessionModel(false, volume, name + processId, processId, name, $@"C:\Apps\{name}.exe",
				GroupingParamOf(name), new Mock<IAudioSessionVolume>().Object, notifications.Object);
			session.ApplyInitialState(playing);
			app.Model.AddSession(session);
			app.Sessions.Add(notifications);
		}
		return app;
	}

	private static Guid GroupingParamOf(string name)
	{
		lock(GroupingParams)
		{
			if(!GroupingParams.TryGetValue(name, out var param))
				GroupingParams[name] = param = Guid.NewGuid();
			return param;
		}
	}

	private static readonly Guid SystemSoundsParam = Guid.NewGuid();

	private App SystemSounds()
	{
		var app = new App();
		app.Model = new AudioProcessModel(50, false, "System Sounds", 0, "0", null, null, hotkeys);
		var notifications = new Mock<IAudioSessionStateNotifications>();
		app.Model.AddSession(new AudioSessionModel(false, 50, "system", 0, "System Sounds", "0", SystemSoundsParam,
			new Mock<IAudioSessionVolume>().Object, notifications.Object));
		app.Sessions.Add(notifications);
		return app;
	}

	/// <summary>The app's model starts on a device later, as a new session there makes it</summary>
	private void Arrive(OutputDeviceModel device, App app) => OnUi(() => device.Processes.Add(app.Model));

	private void Start(OutputDeviceModel defaultDevice, Func<string, string> names = null)
	{
		OnUi(() => provider.Object.DefaultDevice = defaultDevice);
		routing = OnUi(() => new AppRouting(provider.Object, windows, names ?? (_ => null), ui));
	}

	private void ChangeDefault(OutputDeviceModel device) => OnUi(() =>
	{
		provider.Object.DefaultDevice = device;
		provider.Raise(p => p.DefaultDeviceChanged += null, device);
	});

	private MoveOutcome Move(App app, AppRoute to)
	{
		var move = OnUi(() => routing.MoveAsync(app.Model, to));
		Assert.IsTrue(move.Wait(3000), "the move didn't end");
		return move.Result;
	}

	private void Eventually(Func<bool> condition, string message)
	{
		var watch = Stopwatch.StartNew();
		while(!OnUi(condition))
		{
			if(watch.ElapsedMilliseconds > 3000)
				Assert.Fail(message);
			Thread.Sleep(10);
		}
	}

	/// <summary>Waits until the worker has nothing left (a few rounds of the UI thread without a new Windows call)</summary>
	private void Settle()
	{
		int Calls() => Volatile.Read(ref windows.Gets) + Volatile.Read(ref windows.Sets);
		int calls;
		do
		{
			calls = Calls();
			Thread.Sleep(100);
			OnUi(() => { });
		}
		while(calls != Calls());
	}

	/// <summary>Waits until the worker is inside its first Windows call</summary>
	private void WaitForFirstGet()
	{
		var watch = Stopwatch.StartNew();
		while(Volatile.Read(ref windows.Gets) == 0)
		{
			if(watch.ElapsedMilliseconds > 3000)
				Assert.Fail("Windows wasn't asked");
			Thread.Sleep(5);
		}
	}

	private bool PlaysHere(App app) => OnUi(() => app.Model.PlaysHere);

	// U3
	[TestMethod]
	public void WithoutWindowsSupportNothingIsAskedAndRowsStayAsToday()
	{
		windows.Available = false;
		var app = NewApp("game", 50, false, 100);
		windows.Routes[100] = new AppRoute.Pinned(B);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);
		OnUi(routing.RefreshAll);
		Settle();

		Assert.IsFalse(OnUi(() => routing.IsAvailable));
		Assert.AreEqual(0, windows.Gets);
		Assert.IsTrue(PlaysHere(app));
		Assert.AreEqual(new AppRoute.FollowsDefault(), OnUi(() => app.Model.Route));
	}

	// U4 (rows): an app pinned elsewhere outside Volumey leaves this list once it's read
	[TestMethod]
	public void AnIdleAppPinnedToAnotherDeviceOutsideVolumeyIsHiddenAtStart()
	{
		var app = NewApp("game", 50, false, 100);
		windows.Routes[100] = new AppRoute.Pinned(B);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);

		Eventually(() => !app.Model.PlaysHere, "the row wasn't hidden");
		Assert.AreEqual(new AppRoute.Pinned(B), OnUi(() => app.Model.Route));
		Assert.AreEqual("Headset", OnUi(() => app.Model.RouteDeviceName));
	}

	// U7
	[TestMethod]
	public void AnIdleAppLeavesTheOldDeviceRightAfterAMove()
	{
		var app = NewApp("game", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);
		Settle();

		var outcome = Move(app, new AppRoute.Pinned(B));

		Assert.AreEqual(new MoveOutcome.Moved(new AppRoute.FollowsDefault()), outcome);
		Assert.IsFalse(PlaysHere(app));
		Assert.AreEqual(new AppRoute.Pinned(B), windows.Routes[100]);
		Assert.AreEqual("Headset", OnUi(() => app.Model.RouteDeviceName));
	}

	// U7
	[TestMethod]
	public void APlayingAppStaysOnTheOldDeviceUntilItPlaysOnTheNewOne()
	{
		var onA = NewApp("player", 50, true, 100);
		var a = Device(A, "Speakers", onA);
		var b = Device(B, "Headset");
		Start(a);
		Settle();

		Move(onA, new AppRoute.Pinned(B));
		Assert.IsTrue(PlaysHere(onA), "an app still playing on the old device keeps its row there");

		var onB = NewApp("player", 100, false, 100);
		Arrive(b, onB);
		onB.Play();
		onA.Stop();

		Eventually(() => !onA.Model.PlaysHere, "the old row stayed");
		Assert.IsTrue(PlaysHere(onB));
	}

	// U8
	[TestMethod]
	public void RowsOfAppsFollowingTheDefaultFollowItsChange()
	{
		var onA = NewApp("browser", 50, false, 100);
		var onB = NewApp("browser", 50, false, 100);
		var a = Device(A, "Speakers", onA);
		var b = Device(B, "Headset", onB);
		Start(a);
		Eventually(() => onA.Model.PlaysHere && !onB.Model.PlaysHere, "the leftover row on the other device wasn't hidden");

		ChangeDefault(b);

		Eventually(() => !onA.Model.PlaysHere && onB.Model.PlaysHere, "the rows didn't follow the default device");
	}

	// U9
	[TestMethod]
	public void UndoRestoresThePreviousRoute()
	{
		var app = NewApp("game", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);
		Settle();
		var moved = (MoveOutcome.Moved)Move(app, new AppRoute.Pinned(B));

		var undone = Move(app, moved.Previous);

		Assert.IsInstanceOfType<MoveOutcome.Moved>(undone);
		Assert.AreEqual(new AppRoute.FollowsDefault(), windows.Routes[100]);
		Assert.AreEqual(new AppRoute.FollowsDefault(), OnUi(() => app.Model.Route));
		Assert.IsTrue(PlaysHere(app));
	}

	// U10
	[TestMethod]
	public void TheAppTakesItsVolumeOfThatMomentToTheNewDeviceOnce()
	{
		var onA = NewApp("player", 30, false, 100);
		var a = Device(A, "Speakers", onA);
		var b = Device(B, "Headset");
		Start(a);
		Settle();
		Move(onA, new AppRoute.Pinned(B));
		//a hotkey press between the move and the first playback
		OnUi(() => { onA.Model.Volume = 40; onA.Model.IsMuted = true; });

		var onB = NewApp("player", 100, false, 100);
		Arrive(b, onB);
		Settle();
		Assert.AreEqual(100, OnUi(() => onB.Model.Volume), "copied before the app played there");
		onB.Play();

		Eventually(() => onB.Model.Volume == 40 && onB.Model.IsMuted, "the volume wasn't copied");
		OnUi(() => onA.Model.Volume = 10);
		onB.Stop();
		onB.Play();
		Settle();
		Assert.AreEqual(40, OnUi(() => onB.Model.Volume), "copied twice");
	}

	// U10
	[TestMethod]
	public void WithoutAMoveMadeInVolumeyNothingIsCopied()
	{
		var onA = NewApp("voice", 30, false, 100);
		var onB = NewApp("voice", 100, false, 100);
		var a = Device(A, "Speakers", onA);
		Device(B, "Headset", onB);
		Start(a);
		Settle();
		Assert.IsFalse(PlaysHere(onB), "the idle leftover row wasn't hidden");

		onB.Play();
		Eventually(() => onB.Model.PlaysHere, "the row didn't show");
		Settle();

		Assert.AreEqual(100, OnUi(() => onB.Model.Volume));
	}

	// U19
	[TestMethod]
	public void UndoTakesTheVolumeSetOnTheNewDeviceBack()
	{
		var onA = NewApp("player", 30, false, 100);
		var a = Device(A, "Speakers", onA);
		var b = Device(B, "Headset");
		Start(a);
		Settle();
		var moved = (MoveOutcome.Moved)Move(onA, new AppRoute.Pinned(B));
		var onB = NewApp("player", 100, false, 100);
		Arrive(b, onB);
		onB.Play();
		Eventually(() => onB.Model.Volume == 30, "the volume wasn't copied");
		OnUi(() => onB.Model.Volume = 55);

		Move(onA, moved.Previous);
		onB.Stop();
		onA.Play();

		Eventually(() => onA.Model.Volume == 55, "the old device didn't get the volume set on the new one");
	}

	// U11
	[TestMethod]
	public void AnExitedHelperProcessDoesNotTurnAMoveIntoARefusal()
	{
		var app = NewApp("browser", 50, false, 100, 104);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);
		Settle();
		windows.OnSet = (processId, _) => processId == 100 ? new RouteWrite.ProcessGone() : new RouteWrite.Done();

		Assert.IsInstanceOfType<MoveOutcome.Moved>(Move(app, new AppRoute.Pinned(B)));
	}

	// U11
	[TestMethod]
	public void ARefusedMoveChangesNothingAndIsLogged()
	{
		var app = NewApp("game", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		Start(a);
		Settle();
		windows.OnSet = (_, _) => new RouteWrite.Refused(unchecked((int)0x80070005));

		var repository = (log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository(typeof(AppRouting).Assembly);
		bool wasConfigured = repository.Configured;
		var log = new log4net.Appender.MemoryAppender();
		log4net.Config.BasicConfigurator.Configure(repository, log);
		try
		{
			Assert.AreEqual(new MoveOutcome.Refused(), Move(app, new AppRoute.Pinned(B)));

			Assert.AreEqual(new AppRoute.FollowsDefault(), OnUi(() => app.Model.Route));
			Assert.IsTrue(PlaysHere(app));
			var warning = log.GetEvents().Single(e => e.Level == log4net.Core.Level.Warn && e.LoggerName == typeof(AppRouting).FullName);
			StringAssert.Contains(warning.RenderedMessage, "0x80070005");
			StringAssert.Contains(warning.RenderedMessage, "game");
			StringAssert.Contains(warning.RenderedMessage, "Headset");
		}
		finally
		{
			repository.Root.RemoveAppender(log);
			repository.Configured = wasConfigured;
		}
	}

	// U11
	[TestMethod]
	public void AMoveToADeviceThatLeftIsNotMade()
	{
		var app = NewApp("game", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Start(a);
		Settle();

		Assert.AreEqual(new MoveOutcome.DeviceGone(), Move(app, new AppRoute.Pinned(Unplugged)));
		Assert.AreEqual(0, windows.Sets);
	}

	// U13
	[TestMethod]
	public void AppsAreFoundOnEveryDeviceAtTheirHomeRow()
	{
		var gameOnA = NewApp("game", 50, false, 100);
		var gameOnB = NewApp("game", 50, false, 104);
		var voice = NewApp("voice", 50, true, 200);
		var system = SystemSounds();
		windows.Routes[100] = windows.Routes[104] = new AppRoute.Pinned(B);
		var a = Device(A, "Speakers", system, gameOnA);
		Device(B, "Headset", gameOnB, voice);
		Start(a);
		Eventually(() => gameOnA.Model.Route is AppRoute.Pinned && gameOnB.Model.Route is AppRoute.Pinned, "the routes weren't read");

		CollectionAssert.AreEquivalent(new[] { system.Model, gameOnB.Model, voice.Model }, OnUi(() => routing.GetApps()).ToArray());
		CollectionAssert.AreEquivalent(new[] { gameOnA.Model, gameOnB.Model }, OnUi(() => routing.GetModels(gameOnA.Model)).ToArray());
		//a process only the hidden row has a session of
		Assert.AreSame(gameOnB.Model, OnUi(() => routing.FindApp(100)));
		Assert.AreSame(voice.Model, OnUi(() => routing.FindApp(200)));
		Assert.IsNull(OnUi(() => routing.FindApp(999)));
	}

	// U13
	[TestMethod]
	public void AppsChangedIsRaisedWhenAnAppsHomeRowChanges()
	{
		var gameOnA = NewApp("game", 50, false, 100);
		var gameOnB = NewApp("game", 50, false, 104);
		var a = Device(A, "Speakers", gameOnA);
		Device(B, "Headset", gameOnB);
		Start(a);
		Settle();
		int changes = 0;
		OnUi(() => routing.AppsChanged += () => changes++);

		Move(gameOnA, new AppRoute.Pinned(B));

		Assert.AreEqual(1, changes);
		Assert.AreSame(gameOnB.Model, OnUi(() => routing.GetApps()).Single());
	}

	// U17
	[TestMethod]
	public void AChangeDuringTheAppsRefreshLeadsToAnotherRefresh()
	{
		var app = NewApp("player", 50, false, 100);
		var onB = NewApp("player", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset", onB);
		windows.GetGate.Reset();
		Start(a);
		WaitForFirstGet();

		onB.Play();
		windows.GetGate.Set();
		Settle();

		Assert.IsTrue(PlaysHere(onB), "the change during the refresh was lost");
		Assert.AreEqual(2, windows.Gets);
	}

	// U17
	[TestMethod]
	public void DisposeDoesNotWaitForAHungWindowsCallAndNothingHappensAfterIt()
	{
		var app = NewApp("game", 50, false, 100);
		var a = Device(A, "Speakers", app);
		Device(B, "Headset");
		windows.GetGate.Reset();
		Start(a);
		WaitForFirstGet();

		var watch = Stopwatch.StartNew();
		OnUi(routing.Dispose);
		Assert.IsTrue(watch.ElapsedMilliseconds < 1000, "Dispose waited");
		windows.Routes[100] = new AppRoute.Pinned(B);
		windows.GetGate.Set();
		Thread.Sleep(300);

		Assert.IsTrue(PlaysHere(app));
		Assert.AreEqual(new AppRoute.FollowsDefault(), OnUi(() => app.Model.Route));
	}

	// U16
	[TestMethod]
	public void ManyChangesWhileWindowsIsBusyMakeOneRefresh()
	{
		var app = NewApp("browser", 50, false, 100);
		var a = Device(A, "Speakers", app);
		windows.GetGate.Reset();
		Start(a);
		WaitForFirstGet();

		for(int i = 0; i < 50; i++)
		{
			app.Play();
			app.Stop();
		}
		windows.GetGate.Set();
		Settle();

		Assert.AreEqual(2, windows.Gets);
	}

	private readonly List<(uint ProcessId, AppRoute Route)> sets = new();

	private void RecordSets() => windows.OnSet = (processId, route) =>
	{
		lock(sets)
			sets.Add((processId, route));
		return new RouteWrite.Done();
	};

	// U20
	[TestMethod]
	public void AnAppPinnedToADeviceThatComesBackIsSentThereAgain()
	{
		//Windows plays it on the default device while its device is away, and doesn't send it back when the device returns
		var app = NewApp("player", 50, true, 100);
		windows.Routes[100] = new AppRoute.Pinned(B);
		RecordSets();
		var a = Device(A, "Speakers", app);
		Start(a);
		Settle();
		Assert.AreEqual(0, sets.Count, "set while its device is away");

		Device(B, "Headset");
		Settle();

		CollectionAssert.AreEqual(new[] { (100u, (AppRoute)new AppRoute.Pinned(B)) }, sets.ToArray());
		OnUi(routing.RefreshAll);
		Settle();
		Assert.AreEqual(1, sets.Count, "set again on a later refresh");
	}

	// U20
	[TestMethod]
	public void OnlyAppsPinnedToTheReturningDeviceAndNotPlayingThereAreSentAgain()
	{
		var following = NewApp("browser", 50, true, 100);
		var elsewhere = NewApp("game", 50, true, 200);
		var playingThere = NewApp("voice", 50, false, 300);
		var playingThereOnB = NewApp("voice", 50, true, 300);
		windows.Routes[200] = new AppRoute.Pinned(Unplugged);
		windows.Routes[300] = new AppRoute.Pinned(B);
		RecordSets();
		var a = Device(A, "Speakers", following, elsewhere, playingThere);
		Start(a);
		Settle();

		Device(B, "Headset", playingThereOnB);
		Settle();

		Assert.AreEqual(0, sets.Count, string.Join(", ", sets));
	}

	// U12 (badge data): an app pinned to an unplugged device keeps the device's name, read from Windows once
	[TestMethod]
	public void AnUnpluggedPinnedDeviceIsNamedByWindowsOnce()
	{
		var app = NewApp("game", 50, false, 100, 104);
		windows.Routes[100] = windows.Routes[104] = new AppRoute.Pinned(Unplugged);
		var a = Device(A, "Speakers", app);
		int reads = 0;
		Start(a, id => { Interlocked.Increment(ref reads); return id == Unplugged ? "Old headset" : null; });

		Eventually(() => app.Model.RouteDeviceName == "Old headset", "the unplugged device wasn't named");
		OnUi(routing.RefreshAll);
		Settle();

		Assert.AreEqual(1, reads);
		//Windows plays the app on the default device meanwhile
		Assert.IsTrue(PlaysHere(app));
	}
}
