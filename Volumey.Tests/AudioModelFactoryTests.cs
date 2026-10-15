using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests;

/// <summary>
/// The existing sessions of a device grouped into app models (<see cref="AudioModelFactory.GroupIntoProcesses{TControl}"/>),
/// with fake session controls instead of Windows' audio objects
/// </summary>
[TestClass]
public class AudioModelFactoryTests
{
	/// <summary>A session as Windows lists it; <see cref="Broken"/>: its app model can't be built</summary>
	private sealed record Control(string File, bool SystemSounds = false, bool Broken = false, bool Unreadable = false, bool Throws = false)
	{
		//two sessions of one app are still two sessions
		public Guid Id { get; } = Guid.NewGuid();
	}

	private readonly HotkeyService hotkeys = new HotkeyService();
	//every session model's Windows registration, to check which ones were released
	private readonly Dictionary<Control, Mock<IAudioSessionStateNotifications>> registrations = new();
	private readonly List<Control> released = new();

	private AudioSessionModel CreateSession(Control control)
	{
		if(control.Unreadable)
			return null;
		var notifications = new Mock<IAudioSessionStateNotifications>();
		registrations[control] = notifications;
		//Windows gives the sessions of one app the same grouping parameter
		var grouping = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(control.File)));
		return new AudioSessionModel(false, 50, control.File, 1, control.File, control.File, grouping, new Mock<IAudioSessionVolume>().Object, notifications.Object);
	}

	private AudioProcessModel CreateProcess(AudioSessionModel session, Control control, out bool isSystemSounds)
	{
		isSystemSounds = control.SystemSounds;
		if(control.Throws)
			throw new InvalidOperationException("the system sounds name couldn't be saved");
		return control.Broken ? null : new AudioProcessModel(50, false, control.File, 1, control.File, null, null, hotkeys);
	}

	private List<AudioProcessModel> Group(params Control[] controls)
		=> AudioModelFactory.GroupIntoProcesses(controls.Length, i => controls[i], CreateSession, CreateProcess, released.Add);

	private bool WasReleased(Control control) => registrations[control].Invocations.Any(i => i.Method.Name == nameof(IDisposable.Dispose));

	[TestMethod]
	public void SessionsOfOneAppAreGroupedAndSystemSoundsComeFirst()
	{
		var chrome = new Control("chrome.exe");
		var chromeTab = new Control("chrome.exe");
		var sounds = new Control("system", SystemSounds: true);

		var processes = Group(chrome, chromeTab, sounds);

		CollectionAssert.AreEqual(new[] { "system", "chrome.exe" }, processes.Select(p => p.Name).ToList());
		Assert.HasCount(2, processes[1].Sessions);
		Assert.IsFalse(registrations.Keys.Any(WasReleased), "a session that's shown was released");
	}

	[TestMethod]
	public void ASessionWhoseAppModelCantBeBuiltIsReleased()
	{
		var broken = new Control("broken.exe", Broken: true);
		var throws = new Control("sounds", SystemSounds: true, Throws: true);
		var fine = new Control("fine.exe");

		var processes = Group(broken, throws, fine);

		Assert.AreEqual("fine.exe", Assert.ContainsSingle(processes).Name);
		Assert.IsTrue(WasReleased(broken), "the session without an app model kept its registration");
		Assert.IsTrue(WasReleased(throws), "the session whose app model threw kept its registration");
		Assert.IsFalse(WasReleased(fine));
	}

	[TestMethod]
	public void AControlWithoutASessionModelIsReleasedAndTheRestIsRead()
	{
		var unreadable = new Control("gone.exe", Unreadable: true);
		var fine = new Control("fine.exe");
		var controls = new[] { unreadable, null, fine };

		//the second one can't even be read from Windows' list
		var processes = AudioModelFactory.GroupIntoProcesses(controls.Length,
			i => controls[i] ?? throw new InvalidOperationException("the session list changed"), CreateSession, CreateProcess, released.Add);

		Assert.AreEqual("fine.exe", Assert.ContainsSingle(processes).Name);
		CollectionAssert.AreEqual(new[] { unreadable }, released);
	}
}
