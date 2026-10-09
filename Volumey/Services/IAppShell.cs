using System;
using System.Windows.Threading;

namespace Volumey.Services;

/// <summary>
/// The running application: its UI dispatcher, lifetime events and shutdown.
/// </summary>
public interface IAppShell
{
	Dispatcher Dispatcher { get; }

	/// <summary>
	/// The app was started with the minimized argument (e.g. on Windows startup)
	/// </summary>
	bool StartMinimized { get; }

	/// <summary>
	/// Windows is logging off or shutting down
	/// </summary>
	event Action SessionEnding;

	/// <summary>
	/// The application is exiting
	/// </summary>
	event Action Exit;

	void Shutdown();
}
