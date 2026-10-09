using System;
using System.Windows;
using System.Windows.Threading;

namespace Volumey.Services
{
	/// <summary>
	/// <see cref="IAppShell"/> over the WPF <see cref="Application"/>.
	/// </summary>
	internal sealed class AppShell : IAppShell
	{
		private readonly Application application;

		public AppShell(Application application)
		{
			this.application = application;
			this.application.SessionEnding += (s, e) => this.SessionEnding?.Invoke();
			this.application.Exit += (s, e) => this.Exit?.Invoke();
		}

		public Dispatcher Dispatcher => this.application.Dispatcher;

		public bool StartMinimized => Startup.StartMinimized;

		public event Action SessionEnding;

		public event Action Exit;

		public void Shutdown() => this.application.Shutdown();
	}
}
