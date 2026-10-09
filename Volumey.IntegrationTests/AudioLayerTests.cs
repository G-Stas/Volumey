using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.CoreAudioApi;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using Volumey.ViewModel.Settings;
using static Volumey.IntegrationTests.AudioTestEnvironment;

namespace Volumey.IntegrationTests
{
	/// <summary>
	/// The audio layer against real Windows audio. Only the test process's own session volume is changed;
	/// device volumes are only read.
	/// </summary>
	[TestClass]
	[TestCategory("Integration")]
	public class AudioLayerTests
	{
		/// <summary>
		/// The device models as the app builds them on startup, on the UI thread.
		/// </summary>
		private sealed class LoadedDevices : IDisposable
		{
			private readonly DeviceStateNotificationsHandler handler;
			public List<OutputDeviceModel> All { get; }

			public LoadedDevices()
			{
				(this.handler, this.All) = OnUi(() =>
				{
					var enumerator = new DeviceEnumerator(new HotkeyService());
					var stateHandler = new DeviceStateNotificationsHandler(enumerator);
					return (stateHandler, enumerator.GetCurrentActiveDevices(stateHandler));
				});
			}

			public OutputDeviceModel Get(string deviceId) => this.All.Single(d => d.CompareId(deviceId));

			public void Dispose() => OnUi(() =>
			{
				foreach(var device in this.All)
					device.Dispose();
				this.handler.Dispose();
			});
		}

		[TestInitialize]
		public void Initialize() => RequireOutputDevice();

		private static AudioProcessModel FindOwnProcess(OutputDeviceModel device)
			=> device.Processes.FirstOrDefault(p => p.ProcessId == TestAudioSession.ProcessId);

		private static AudioProcessModel WaitForOwnProcess(OutputDeviceModel device)
		{
			WaitFor(() => FindOwnProcess(device) != null, "the test's audio session wasn't listed\n");
			return OnUi(() => FindOwnProcess(device));
		}

		[TestMethod]
		public void ExistingSessionIsListed()
		{
			using var session = new TestAudioSession();
			session.SetVolumeAndWait(0.8f);
			using var devices = new LoadedDevices();

			var process = WaitForOwnProcess(devices.Get(session.DeviceId));

			Assert.AreEqual(80, OnUi(() => process.Volume));
			Assert.IsFalse(OnUi(() => process.IsMuted));
			Assert.IsFalse(string.IsNullOrEmpty(OnUi(() => process.Name)));
		}

		[TestMethod]
		public void NewSessionIsAdded()
		{
			using var devices = new LoadedDevices();
			using var session = new TestAudioSession();

			var process = WaitForOwnProcess(devices.Get(session.DeviceId));

			WaitFor(() => process.Volume == session.VolumePercent, "the new session's volume wasn't shown");
		}

		[TestMethod]
		public void AppVolumeAndMuteReachWindows()
		{
			using var session = new TestAudioSession();
			using var devices = new LoadedDevices();
			var process = WaitForOwnProcess(devices.Get(session.DeviceId));

			OnUi(() => process.Volume = 37);
			WaitFor(() => session.VolumePercent == 37, "the volume didn't reach the session\n");

			OnUi(() => process.IsMuted = true);
			WaitFor(() => session.Volume.Mute, "the mute didn't reach the session\n");
			OnUi(() => process.IsMuted = false);
			WaitFor(() => !session.Volume.Mute, "the unmute didn't reach the session\n");
		}

		[TestMethod]
		public void WindowsVolumeAndMuteReachTheApp()
		{
			using var session = new TestAudioSession();
			using var devices = new LoadedDevices();
			var process = WaitForOwnProcess(devices.Get(session.DeviceId));

			session.Volume.Volume = 0.42f;
			WaitFor(() => process.Volume == 42, "the app didn't get the new volume\n");

			session.Volume.Mute = true;
			WaitFor(() => process.IsMuted, "the app didn't get the mute\n");
			session.Volume.Mute = false;
			WaitFor(() => !process.IsMuted, "the app didn't get the unmute\n");
		}

		[TestMethod]
		public void FastAppChangesEndOnTheLastValue()
		{
			using var session = new TestAudioSession();
			using var devices = new LoadedDevices();
			var process = WaitForOwnProcess(devices.Get(session.DeviceId));

			//like dragging the slider: the callbacks of earlier writes arrive while later writes are made
			for(int round = 0; round < 3; round++)
			{
				for(int volume = 10; volume <= 30; volume++)
				{
					int value = volume;
					OnUi(() => process.Volume = value);
				}
				Thread.Sleep(300);

				Assert.AreEqual(30, session.VolumePercent);
				Assert.AreEqual(30, OnUi(() => process.Volume), "the slider jumped back\n");
				OnUi(() => process.Volume = 50);
				Thread.Sleep(100);
			}
		}

		[TestMethod]
		public void TwoModelsOfTheSameSessionDontFight()
		{
			using var session = new TestAudioSession();
			using var first = new LoadedDevices();
			using var second = new LoadedDevices();
			var a = WaitForOwnProcess(first.Get(session.DeviceId));
			var b = WaitForOwnProcess(second.Get(session.DeviceId));

			OnUi(() => a.Volume = 25);
			WaitFor(() => b.Volume == 25, "the second model didn't follow the volume");
			OnUi(() => a.IsMuted = true);
			WaitFor(() => b.IsMuted, "the second model didn't follow the mute");
			OnUi(() => b.IsMuted = false);
			WaitFor(() => !a.IsMuted, "the first model didn't follow the unmute");
			Thread.Sleep(500);

			Assert.IsFalse(session.Volume.Mute);
			Assert.AreEqual(25, session.VolumePercent);
			Assert.IsFalse(OnUi(() => a.IsMuted || b.IsMuted));
		}

		[TestMethod]
		public void DeviceIsDescribed()
		{
			using var enumerator = new MMDeviceEnumerator();
			var mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			string id = mmDevice.ID;
			float endpointVolume = mmDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
			string name = mmDevice.FriendlyName;
			using var device = new AudioDevice(mmDevice);

			Assert.AreEqual(id, device.GetId());
			Assert.AreEqual(name, device.GetFriendlyName());
			Assert.IsFalse(string.IsNullOrEmpty(device.GetDeviceDesc()));
			var format = device.GetDeviceFormat();
			Assert.IsNotNull(format);
			Assert.IsGreaterThan(0, (int)format.Value.Channels);
			Assert.IsGreaterThan(0u, format.Value.SamplesPerSec);

			using var devices = new LoadedDevices();
			var model = devices.Get(id);
			Assert.AreEqual((int)Math.Round(endpointVolume * 100), OnUi(() => model.Master.Volume));
			Assert.AreEqual(name, OnUi(() => model.Master.DeviceFriendlyName));
		}

		[TestMethod]
		public void DefaultDeviceIdMatchesWindows()
		{
			using var enumerator = new MMDeviceEnumerator();
			using var mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

			var wrapper = new DeviceEnumerator(new HotkeyService());

			Assert.AreEqual(mmDevice.ID, wrapper.GetDefaultDeviceId());
			Assert.IsGreaterThan(0u, wrapper.GetDeviceCount());
		}
	}
}
