using System;
using System.Diagnostics;
using log4net;

namespace Volumey.Helper;

/// <summary>
/// Writes WPF's data binding errors to the log. A broken binding otherwise only leaves a control empty;
/// the UI tests fail on a logged <see cref="Prefix"/>
/// </summary>
internal sealed class BindingErrorListener : TraceListener
{
	internal const string Prefix = "Binding error:";

	private readonly Action<string> write;

	internal BindingErrorListener(Action<string> write)
	{
		this.write = write;
	}

	/// <summary>
	/// Listens to <see cref="PresentationTraceSources.DataBindingSource"/> for errors and logs them
	/// </summary>
	internal static void Attach()
	{
		var logger = LogManager.GetLogger(typeof(BindingErrorListener));
		//WPF traces bindings only under a debugger or after a refresh; the refresh also creates the sources anew, so it comes first
		PresentationTraceSources.Refresh();
		var source = PresentationTraceSources.DataBindingSource;
		source.Listeners.Add(new BindingErrorListener(logger.Error));
		//keeps a lower level a debugger has set (its output shows warnings too); only errors are written
		source.Switch.Level |= SourceLevels.Error;
	}

	internal static string Format(int id, string message) => $"{Prefix} {id}: {message}";

	public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
	{
		if(eventType <= TraceEventType.Error)
			write(Format(id, message));
	}

	public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
	{
		if(eventType <= TraceEventType.Error)
			write(Format(id, args is { Length: > 0 } ? string.Format(format, args) : format));
	}

	//WPF reports binding failures as trace events; plain writes carry no event type
	public override void Write(string message) { }

	public override void WriteLine(string message) { }
}
