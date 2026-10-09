using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.IntegrationTests
{
	/// <summary>
	/// Runs a WPF application on its own UI thread, as the app does, so models marshal their work to a real dispatcher.
	/// </summary>
	[TestClass]
	public static class AudioTestEnvironment
	{
		private static Application application;

		[AssemblyInitialize]
		public static void Initialize(TestContext context)
		{
			//keep the machine's settings untouched
			SettingsProvider.UseSettings(new VolumeySettings());

			using var started = new ManualResetEventSlim();
			var thread = new Thread(() =>
			{
				application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
				started.Set();
				application.Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.IsBackground = true;
			thread.Start();
			started.Wait();
		}

		[AssemblyCleanup]
		public static void Cleanup() => application?.Dispatcher.InvokeShutdown();

		/// <summary>
		/// Runs the function on the UI thread.
		/// </summary>
		public static T OnUi<T>(Func<T> function) => application.Dispatcher.Invoke(function);

		public static void OnUi(Action action) => application.Dispatcher.Invoke(action);

		/// <summary>
		/// Skips the test on machines without an output device, such as CI runners.
		/// </summary>
		public static void RequireOutputDevice()
		{
			using var enumerator = new MMDeviceEnumerator();
			if(!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
				Assert.Inconclusive("No audio output device");
		}

		/// <summary>
		/// Waits until the condition, checked on the UI thread, is true.
		/// </summary>
		public static void WaitFor(Func<bool> condition, string message, int timeoutMs = 3000)
		{
			var watch = Stopwatch.StartNew();
			while(!OnUi(condition))
			{
				if(watch.ElapsedMilliseconds > timeoutMs)
					Assert.Fail(message);
				Thread.Sleep(20);
			}
		}
	}

	/// <summary>
	/// An audio session of the test process on the default output device, playing silence.
	/// Its volume and mute stand in for changes made in the Windows mixer.
	/// </summary>
	public sealed class TestAudioSession : IDisposable
	{
		private readonly WasapiPlayer player;

		public TestAudioSession()
		{
			this.player = new WasapiPlayerBuilder().WithSharedMode().Build();
			this.player.Init(new SilenceProvider(this.player.DeviceMixFormat));
			this.player.Play();
			//Windows remembers the volume and mute of the test executable from earlier runs
			SetVolumeAndWait(1f);
		}

		/// <summary>
		/// Sets the session's volume and mute, as the Windows mixer would, and waits until Windows keeps them.
		/// When a stream starts, Windows restores the volume it remembers for the executable asynchronously,
		/// which can overwrite a value set right after the start (that made the tests flaky).
		/// </summary>
		public void SetVolumeAndWait(float volume, bool mute = false)
		{
			void Apply()
			{
				this.Volume.Mute = mute;
				this.Volume.Volume = volume;
			}

			Apply();
			var total = Stopwatch.StartNew();
			var unchanged = Stopwatch.StartNew();
			while(total.ElapsedMilliseconds < 3000)
			{
				Thread.Sleep(20);
				if(Math.Abs(this.Volume.Volume - volume) > 0.005f || this.Volume.Mute != mute)
				{
					Apply();
					unchanged.Restart();
				}
				else if(unchanged.ElapsedMilliseconds >= 400)
					return;
			}
			throw new InvalidOperationException($"Windows didn't keep the test session's volume {volume} (mute {mute})");
		}

		public static uint ProcessId => (uint)Environment.ProcessId;

		public string DeviceId => this.player.DeviceId;

		public SimpleAudioVolume Volume => this.player.SessionVolume;

		public int VolumePercent => (int)Math.Round(this.Volume.Volume * 100);

		public void Dispose() => this.player.Dispose();
	}
}
