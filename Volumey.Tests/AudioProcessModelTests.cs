using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.Controls;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

[TestClass]
public class AudioProcessModelTests
{
	private readonly HotkeyService hotkeys = new HotkeyService();
	private AudioProcessModel proc;
	private Mock<IAudioSessionStateNotifications> trackedSessionStateNotif;
	private Mock<IAudioSessionVolume> sessionVolumeMock;

	public AudioProcessModelTests()
	{
		sessionVolumeMock = new Mock<IAudioSessionVolume>();
		trackedSessionStateNotif = new Mock<IAudioSessionStateNotifications>();

		proc = GetProcessMock("app", 50, false, hotkeys);

		var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
			trackedSessionStateNotif.Object);

		proc.AddSession(model);
	}

	[TestMethod]
	public void TrackedSessionMustChangeToAnotherSession()
	{
		var trackedSession = proc.Sessions[0];
		var otherNotifications = new Mock<IAudioSessionStateNotifications>();
		proc.AddSession(new AudioSessionModel(true, 12, "1", proc.ProcessId, default, default, Guid.Empty,
			new Mock<IAudioSessionVolume>().Object, otherNotifications.Object));

		trackedSessionStateNotif.Raise(n => n.SessionEnded += null);
		//the session leaves the list on the dispatcher, then the tracked session changes and the ended one is disposed
		RunQueuedDispatcherWork();
		Assert.IsTrue(SpinWait.SpinUntil(() =>
		{
			try
			{
				trackedSessionStateNotif.Verify(n => n.Dispose(), Times.Once);
				return true;
			}
			catch(MockException)
			{
				return false;
			}
		}, TimeSpan.FromSeconds(5)), "the ended session wasn't disposed");

		Assert.DoesNotContain(trackedSession, proc.Sessions);
		Assert.ContainsSingle(proc.Sessions);
		//the app's volume now follows the other session
		otherNotifications.Raise(n => n.VolumeChanged += null, new VolumeChangedEventArgs(77, false));
		Assert.AreEqual(77, proc.Volume);
	}

	//sessions of one app, each set to its own volume like the sub-sliders do (a session that joins takes the app's volume first)
	private (AudioProcessModel app, AudioSessionModel[] sessions, Mock<IAudioSessionStateNotifications>[] notifications) AppWithSessions(params int[] volumes)
	{
		var app = GetProcessMock("app", volumes[0], false, hotkeys);
		var sessions = new AudioSessionModel[volumes.Length];
		var notifications = new Mock<IAudioSessionStateNotifications>[volumes.Length];
		for(int i = 0; i < volumes.Length; i++)
		{
			notifications[i] = new Mock<IAudioSessionStateNotifications>();
			sessions[i] = new AudioSessionModel(false, volumes[i], i.ToString(), app.ProcessId, default, default, Guid.Empty,
				new Mock<IAudioSessionVolume>().Object, notifications[i].Object);
			app.AddSession(sessions[i]);
		}
		for(int i = 0; i < volumes.Length; i++)
			sessions[i].Volume = volumes[i];
		return (app, sessions, notifications);
	}

	[TestMethod]
	public void TheAppsVolumeIsTheLoudestSession()
	{
		var (app, _, _) = AppWithSessions(20, 70, 50);

		Assert.AreEqual(70, app.Volume);
	}

	[TestMethod]
	public void TheOwnSliderOfAnySessionMovesTheAppsVolumeWhenItIsTheLoudest()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);

		sessions[1].Volume = 90;
		Assert.AreEqual(90, app.Volume);

		sessions[1].Volume = 30;
		Assert.AreEqual(70, app.Volume);
	}

	[TestMethod]
	public void AChangeFromOutsideInAnySessionReachesTheApp()
	{
		var (app, _, notifications) = AppWithSessions(70, 20);

		notifications[1].Raise(n => n.VolumeChanged += null, new VolumeChangedEventArgs(85, false));

		Assert.AreEqual(85, app.Volume);
	}

	[TestMethod]
	public void WhenTheLoudestSessionEndsTheAppFollowsTheNextOne()
	{
		var (app, sessions, notifications) = AppWithSessions(70, 20, 50);

		notifications[0].Raise(n => n.SessionEnded += null);
		RunQueuedDispatcherWork();
		Assert.IsTrue(SpinWait.SpinUntil(() =>
		{
			try
			{
				notifications[0].Verify(n => n.Dispose(), Times.Once);
				return true;
			}
			catch(MockException)
			{
				return false;
			}
		}, TimeSpan.FromSeconds(5)), "the ended session wasn't disposed");

		Assert.AreEqual(50, app.Volume);
	}

	[TestMethod]
	public void SettingTheAppsVolumeSetsEverySessionAndIsReportedOnceWithTheNewValue()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);
		var reported = new System.Collections.Generic.List<int>();
		app.PropertyChanged += (_, e) =>
		{
			if(e.PropertyName == nameof(AudioProcessModel.Volume))
				reported.Add(app.Volume);
		};

		app.Volume = 40;

		Assert.AreEqual(40, sessions[0].Volume);
		Assert.AreEqual(40, sessions[1].Volume);
		CollectionAssert.AreEqual(new[] { 40 }, reported);
	}

	private Mock<IHotkeyManager> RegisterApp(AudioProcessModel app, HotKey up, HotKey down, HotKey mute = null,
		System.Collections.Generic.IReadOnlyList<AudioProcessModel> sameApp = null)
	{
		var manager = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(manager.Object);
		app.SetHotkeys(up, down, mute, out _, sameApp == null ? null : () => sameApp);
		return manager;
	}

	[TestMethod]
	public void AVolumeHotkeyStepsEverySessionOnItsOwn()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);
		var up = new HotKey(Key.A);
		var manager = RegisterApp(app, up, new HotKey(Key.B));
		int step = hotkeys.VolumeStep;

		manager.Raise(m => m.HotkeyPressed += null, up);

		Assert.AreEqual(70 + step, sessions[0].Volume);
		Assert.AreEqual(20 + step, sessions[1].Volume);
		Assert.AreEqual(70 + step, app.Volume);
	}

	[TestMethod]
	public void AVolumeHotkeyStopsEachSessionAtItsOwnLimit()
	{
		var (app, sessions, _) = AppWithSessions(100, 1);
		var down = new HotKey(Key.B);
		var manager = RegisterApp(app, new HotKey(Key.A), down);
		int step = hotkeys.VolumeStep;

		manager.Raise(m => m.HotkeyPressed += null, down);

		Assert.AreEqual(100 - step, sessions[0].Volume);
		Assert.AreEqual(Math.Max(0, 1 - step), sessions[1].Volume);
	}

	[TestMethod]
	public void AVolumeHotkeyShowsTheOsdWithTheNewLoudestVolume()
	{
		SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = false;
		var (app, _, _) = AppWithSessions(70, 20);
		var shown = new System.Collections.Generic.List<int>();
		var mediator = new Mock<IAudioProcessStateMediator>();
		mediator.Setup(m => m.NotifyAudioStateChange(It.IsAny<IManagedMasterAudioSession>())).Callback(() => shown.Add(app.Volume));
		app.SetStateMediator(mediator.Object);
		var up = new HotKey(Key.A);
		var manager = RegisterApp(app, up, new HotKey(Key.B));

		manager.Raise(m => m.HotkeyPressed += null, up);

		CollectionAssert.AreEqual(new[] { 70 + hotkeys.VolumeStep }, shown);
	}

	[TestMethod]
	public void AVolumeHotkeyLeavesALockedAppAsItIs()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);
		app.Lock.Hold(new VolumeLock(VolumeLockMode.UntilClosed, 40, 50));
		var up = new HotKey(Key.A);
		var manager = RegisterApp(app, up, new HotKey(Key.B));

		manager.Raise(m => m.HotkeyPressed += null, up);

		Assert.AreEqual(40, sessions[0].Volume);
		Assert.AreEqual(40, sessions[1].Volume);
		Assert.AreEqual(40, app.Volume);
	}

	[TestMethod]
	public void AVolumeHotkeyStepsTheSessionsOfTheAppsOtherModelsOnTheirOwnToo()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);
		var (other, otherSessions, _) = AppWithSessions(50, 10);
		var up = new HotKey(Key.A);
		var manager = RegisterApp(app, up, new HotKey(Key.B), sameApp: [app, other]);
		int step = hotkeys.VolumeStep;

		manager.Raise(m => m.HotkeyPressed += null, up);

		Assert.AreEqual(70 + step, sessions[0].Volume);
		Assert.AreEqual(20 + step, sessions[1].Volume);
		Assert.AreEqual(50 + step, otherSessions[0].Volume);
		Assert.AreEqual(10 + step, otherSessions[1].Volume);
	}

	[TestMethod]
	public void AMuteHotkeyTogglesEverySession()
	{
		var (app, sessions, _) = AppWithSessions(70, 20);
		var mute = new HotKey(Key.M);
		var manager = RegisterApp(app, new HotKey(Key.A), new HotKey(Key.B), mute);

		manager.Raise(m => m.HotkeyPressed += null, mute);

		Assert.IsTrue(sessions[0].IsMuted);
		Assert.IsTrue(sessions[1].IsMuted);
	}

	private static void RunQueuedDispatcherWork()
	{
		var frame = new DispatcherFrame();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
		Dispatcher.PushFrame(frame);
	}

	[TestMethod]
	public void VolumeMustChangeToCorrectValue()
	{
		var newVolume = proc.Volume + 1;

		trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
			new object[] { new VolumeChangedEventArgs(newVolume, proc.IsMuted) });

		Assert.AreEqual(newVolume, proc.Volume);
	}

	[TestMethod]
	public void MuteStateMustChanged()
	{
		var newMuteState = !proc.IsMuted;

		trackedSessionStateNotif.Raise(m => m.VolumeChanged += null,
			new object[] { new VolumeChangedEventArgs(proc.Volume, newMuteState) });

		Assert.AreEqual(newMuteState, proc.IsMuted);
	}

	[TestMethod]
	public void HotkeyMustIncrementVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		proc.SetVolumeHotkeys(upHotkey, downHotkey);
		var newVolume = proc.Volume + hotkeys.VolumeStep;

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, upHotkey);

		//assert
		//verify the call to external API is made
		sessionVolumeMock.Verify(m => m.SetVolume(newVolume, GuidValue.Internal.VolumeGUID), Times.AtLeastOnce);
		//simualate callback from external API
		trackedSessionStateNotif.Raise(m => m.VolumeChanged += null, new VolumeChangedEventArgs(newVolume, proc.IsMuted));
		Assert.AreEqual(newVolume, proc.Volume);
	}

	[TestMethod]
	public void RandomHotkeyMustNotChangeVolume()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		var upHotkey = new HotKey(Key.A);
		var downHotkey = new HotKey(Key.B);
		proc.SetVolumeHotkeys(upHotkey, downHotkey);

		//act
		hManagerMock.Raise(m => m.HotkeyPressed += null, new HotKey(Key.C));

		//assert
		//verify increment/decrement volume calls to external API are not made
		sessionVolumeMock.Verify(m => m.SetVolume(proc.Volume + hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
		sessionVolumeMock.Verify(m => m.SetVolume(proc.Volume - hotkeys.VolumeStep, GuidValue.Internal.Empty), Times.Never);
	}

	[TestMethod]
	public void FirstAddedSessionMustSetAsTracked()
	{
		//arrange
		var model = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
										  trackedSessionStateNotif.Object);
		proc.Sessions.Clear();
		FieldInfo field = typeof(AudioProcessModel).GetField("_trackedSession", BindingFlags.NonPublic | BindingFlags.Instance);

		//act
		proc.AddSession(model);

		//assert
		AudioSessionModel session = field.GetValue(proc) as AudioSessionModel;
		Assert.AreSame(session, model);
	}

	[TestMethod]
	public void TrackedSessionStateChangesMustReflectOnProcess()
	{
		//arrange
		var tracked = new AudioSessionModel(false, 50, "0", proc.ProcessId, default, default, Guid.Empty, sessionVolumeMock.Object,
										  trackedSessionStateNotif.Object);
		proc.Sessions.Clear();
		proc.Volume = 0;
		proc.IsMuted = false;
		proc.AddSession(tracked);

		//act
		tracked.Volume = 75;
		tracked.IsMuted = true;

		//assert
		Assert.AreEqual(proc.Volume, tracked.Volume);
		Assert.AreEqual(proc.IsMuted, tracked.IsMuted);
	}

	[TestMethod]
	public void ProcessStateChangesMustReflectOnItsSessions()
	{
		//arrange
		proc.Sessions.Clear();
		var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
		var additionalSession1 = GetSessionMock(25, true, proc.ProcessId.ToString());
		proc.AddSession(additionalSession);
		proc.AddSession(additionalSession1);

		//act
		proc.Volume = 99;
		proc.IsMuted = false;

		//assert
		Assert.AreEqual(proc.Volume, additionalSession.Volume);
		Assert.AreEqual(proc.Volume, additionalSession1.Volume);
		Assert.AreEqual(proc.IsMuted, additionalSession.IsMuted);
		Assert.AreEqual(proc.IsMuted, additionalSession1.IsMuted);
	}

	[TestMethod]
	public void AdditionalSessionsMustHaveSameStateAsProcess()
	{
		//arrange
		var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
		var additionalSession1 = GetSessionMock(25, true, proc.ProcessId.ToString());

		//verify tracked session is set before adding more sessions
		Assert.ContainsSingle(proc.Sessions);

		//act 
		proc.AddSession(additionalSession);
		proc.AddSession(additionalSession1);

		//assert
		Assert.AreEqual(proc.Volume, additionalSession.Volume);
		Assert.AreEqual(proc.Volume, additionalSession1.Volume);
		Assert.AreEqual(proc.IsMuted, additionalSession.IsMuted);
		Assert.AreEqual(proc.IsMuted, additionalSession1.IsMuted);
	}

	[TestMethod]
	public void VolumeMustNotExceedMaxValue()
	{
		//arrange 
		var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
		proc.AddSession(additionalSession);
		int maxVolume = 100;
		proc.Volume = maxVolume;

		//act
		proc.Volume += 5;

		//assert 
		Assert.AreEqual(maxVolume, proc.Volume);
	}

	[TestMethod]
	public void VolumeMustNotBeLessThanMinValue()
	{
		//arrange 
		var additionalSession = GetSessionMock(12, true, proc.ProcessId.ToString());
		proc.AddSession(additionalSession);
		int minVolume = 0;
		proc.Volume = minVolume;

		//act
		proc.Volume -= 5;

		//assert 
		Assert.AreEqual(minVolume, proc.Volume);
	}

	[TestMethod]
	public void VolumeHotkeysMustReset()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		hotkeys.SetHotkeyManager(hManagerMock.Object);
		HotKey upHotkey = new HotKey(Key.Z);
		HotKey downHotkey = new HotKey(Key.X);

		FieldInfo volumeUpField = typeof(AudioProcessModel).GetField("_volumeUp", BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo volumeDownField = typeof(AudioProcessModel).GetField("_volumeDown", BindingFlags.NonPublic | BindingFlags.Instance);

		proc.SetVolumeHotkeys(upHotkey, downHotkey);

		//act
		proc.ResetVolumeHotkeys();

		//arrange
		//verify hotkey manager is called twice to unregister the required hotkeys in system
		hManagerMock.Verify(m => m.UnregisterHotkey(upHotkey), Times.Once);
		hManagerMock.Verify(m => m.UnregisterHotkey(downHotkey), Times.Once);

		HotKey actualVolumeUpValue = (HotKey)volumeUpField.GetValue(proc);
		HotKey actualVolumeDownValue = (HotKey)volumeDownField.GetValue(proc);

		Assert.IsNull(actualVolumeUpValue);
		Assert.IsNull(actualVolumeDownValue);
	}

	[TestMethod]
	public void SetVolumeHotkeysReturnsFalse_IfHotkeyManagerFails()
	{
		//arrange
		var hManagerMock = new Mock<IHotkeyManager>();
		HotKey up = new HotKey(Key.V);
		HotKey down = new HotKey(Key.B);
		hManagerMock.Setup(hm => hm.RegisterHotkey(up)).Throws(new COMException());
		hManagerMock.Setup(hm => hm.RegisterHotkey(down)).Throws(new COMException());
		hotkeys.SetHotkeyManager(hManagerMock.Object);

		//act
		var result = proc.SetVolumeHotkeys(up, down);

		//assert
		Assert.IsFalse(result);
	}

	[TestMethod]
	public void StateMediatorMustReset()
	{
		SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = false;
		var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
		proc.SetStateMediator(stateMediatorMock.Object);

		Assert.AreEqual(stateMediatorMock.Object, proc.StateNotificationMediator);

		proc.ResetStateMediator();

		Assert.IsNull(proc.StateNotificationMediator);
	}

	[TestMethod]
	public void StateMediatorMustNotReset_IfReactingToAnyVolumeChangesEnabled()
	{
		SettingsProvider.Settings.Notifications.ReactToAllVolumeChanges = true;
		var stateMediatorMock = new Mock<IAudioProcessStateMediator>();
		proc.SetStateMediator(stateMediatorMock.Object);

		Assert.AreEqual(stateMediatorMock.Object, proc.StateNotificationMediator);

		proc.ResetStateMediator();

		Assert.AreEqual(stateMediatorMock.Object, proc.StateNotificationMediator);
	}

	[TestMethod]
	public void MuteCommandInvertsMuteState()
	{
		//arrange
		var muteState = proc.IsMuted;

		//act
		proc.MuteCommand.Execute(null);

		//assert
		Assert.AreEqual(!muteState, proc.IsMuted);
	}

	[TestMethod]
	public void ADisposedModelReleasesItsProcess()
	{
		//watching a process for its exit keeps its handle open (and the model alive) until the app exits, so Dispose must release it
		using var child = StartIdleProcess();
		try
		{
			var process = System.Diagnostics.Process.GetProcessById(child.Id);
			var model = new AudioProcessModel(50, false, "app", (uint)child.Id, default, default, process, hotkeys);
			Assert.AreEqual(child.Id, process.Id);

			model.Dispose();

			//a disposed Process has no process associated anymore
			Assert.Throws<InvalidOperationException>(() => _ = process.Id);
		}
		finally
		{
			child.Kill();
			child.WaitForExit();
		}
	}

	[TestMethod]
	public void AModelReportsItsProcessExit()
	{
		using var child = StartIdleProcess();
		using var exited = new System.Threading.ManualResetEventSlim();
		var model = new AudioProcessModel(50, false, "app", (uint)child.Id, default, default, System.Diagnostics.Process.GetProcessById(child.Id), hotkeys);
		model.Exited += _ => exited.Set();

		child.Kill();
		child.WaitForExit();

		Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(5)), "the model didn't report its process's exit");
		model.Dispose();
	}

	[TestMethod]
	public void EachVolumeOrMuteChangeIsReportedOnce()
	{
		var model = GetProcessMock("app", 50, false, hotkeys);
		var tracked = GetSessionMock(50, false, "1");
		model.AddSession(tracked);
		model.AddSession(GetSessionMock(50, false, "2"));
		var changes = new System.Collections.Generic.List<string>();
		model.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

		model.Volume = 60;
		model.IsMuted = true;
		//a change of one session's own slider reaches the app's row too
		tracked.Volume = 70;

		CollectionAssert.AreEqual(new[] { nameof(AudioProcessModel.Volume), nameof(AudioProcessModel.IsMuted), nameof(AudioProcessModel.Volume) }, changes);
		Assert.AreEqual(70, model.Volume);
	}

	//process IDs are multiples of 4, so this one never runs: an app that has exited
	private const uint ExitedProcessId = 3;
	private static readonly string OwnPath = Environment.ProcessPath!;

	private AudioProcessModel ModelWithoutProcessHandle(uint processId)
		=> new AudioProcessModel(50, false, "app", processId, OwnPath, default, null, hotkeys);

	private static AudioSessionModel SessionOf(uint processId, IAudioSessionStateNotifications notifications = null)
		=> new AudioSessionModel(false, 50, "s" + processId, processId, "app", OwnPath, Guid.Empty,
			new Mock<IAudioSessionVolume>().Object, notifications ?? new Mock<IAudioSessionStateNotifications>().Object);

	[TestMethod]
	public void AModelWithoutAProcessHandleFindsItsExitInTheProcessList()
	{
		//like a game guarded by anti-cheat, which refuses a handle to wait on
		using var exited = new System.Threading.ManualResetEventSlim();
		var model = ModelWithoutProcessHandle(ExitedProcessId);
		model.Exited += _ => exited.Set();

		model.WatchExitInProcessList(TimeSpan.FromMilliseconds(50));

		Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(3)), "the model didn't find its process's exit");
		model.Dispose();
	}

	[TestMethod]
	public void ARunningProcessKeepsItsModel()
	{
		using var exited = new System.Threading.ManualResetEventSlim();
		var model = ModelWithoutProcessHandle((uint)Environment.ProcessId);
		model.Exited += _ => exited.Set();

		model.WatchExitInProcessList(TimeSpan.FromMilliseconds(50));

		Assert.IsFalse(exited.Wait(TimeSpan.FromMilliseconds(400)), "the model reported an exit while its process runs");
		model.Dispose();
	}

	[TestMethod]
	public void ARestartedAppsSessionKeepsTheModel()
	{
		//the restarted app's new session joins the model (same executable) after the first process has exited
		using var exited = new System.Threading.ManualResetEventSlim();
		var model = ModelWithoutProcessHandle(ExitedProcessId);
		model.AddSession(SessionOf((uint)Environment.ProcessId));
		model.Exited += _ => exited.Set();

		model.WatchExitInProcessList(TimeSpan.FromMilliseconds(50));

		Assert.IsFalse(exited.Wait(TimeSpan.FromMilliseconds(400)), "the model ended while its other session's process runs");
		model.Dispose();
	}

	[TestMethod]
	public void AnInactiveSessionMakesTheModelLookForItsExitRightAway()
	{
		using var exited = new System.Threading.ManualResetEventSlim();
		var notifications = new Mock<IAudioSessionStateNotifications>();
		var model = ModelWithoutProcessHandle(ExitedProcessId);
		model.AddSession(SessionOf(ExitedProcessId, notifications.Object));
		model.Exited += _ => exited.Set();
		//the regular check wouldn't come during the test
		model.WatchExitInProcessList(TimeSpan.FromMinutes(10));

		notifications.Raise(m => m.Deactivated += null);

		Assert.IsTrue(exited.Wait(TimeSpan.FromSeconds(3)), "the model didn't look for its process's exit when its session went inactive");
		model.Dispose();
	}

	private static System.Diagnostics.Process StartIdleProcess()
		=> System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c pause")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = true
		});

	internal static AudioSessionModel GetSessionMock(int volume, bool muteState, string id)
	{
		var sessionVolumeMock = new Mock<IAudioSessionVolume>();
		var sessionStateNotifications = new Mock<IAudioSessionStateNotifications>();
		return new AudioSessionModel(muteState, volume, id, default, default, default, Guid.Empty, sessionVolumeMock.Object, sessionStateNotifications.Object);
	}

	internal static AudioProcessModel GetProcessMock(string name, int volume, bool muteState, IHotkeyService hotkeys = null)
	{
		return new AudioProcessModel(volume, muteState, name, default, default, default, null, hotkeys ?? new HotkeyService());
	}
}
