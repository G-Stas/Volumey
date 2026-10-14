#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Volumey.DataProvider.Settings;
using Volumey.Model;
using Volumey.View.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Volumey.View;

/// <summary>
/// The on-screen display: a pill per app or device whose volume changed (at most <see cref="MaxPills"/>), in a corner of a screen,
/// each closing after the display time unless the mouse is over it. UI thread only.
/// </summary>
public sealed partial class OsdWindow
{
	private const int MaxPills = 3;
	private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(150));

	private sealed class Entry
	{
		public required OsdPill Pill;
		public DispatcherTimer? Timer;
		public bool Closing;
	}

	private readonly Dictionary<string, Entry> entries = new();
	private NotificationPosition position;
	private int horizontalIndent;
	private int verticalIndent;

	public OsdWindow()
	{
		InitializeComponent();
		SourceInitialized += OnSourceInitialized;
		//WPF resizes the window to the suggested rectangle when it lands on a screen with another scale; put it back over the work area
		DpiChanged += (_, _) => Dispatcher.InvokeAsync(ApplyWorkArea);
	}

	//never activated, not even by a click on a pill (a game in front keeps the focus), and not in Alt+Tab
	private void OnSourceInitialized(object? sender, EventArgs e)
	{
		var hwnd = new HWND(new WindowInteropHelper(this).Handle);
		var style = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
		style |= (nint)(WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
		PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style);
		ApplyWorkArea();
	}

	private (int left, int top, int width, int height)? workArea;

	/// <summary>
	/// The work area of the OSD's screen, in physical pixels. Set with SetWindowPos, not Left/Top: WPF converts those with the scale
	/// of the screen the window is on now, which puts the OSD off on a screen with another scale
	/// </summary>
	public void SetWorkArea(int left, int top, int width, int height)
	{
		workArea = (left, top, width, height);
		ApplyWorkArea();
	}

	private void ApplyWorkArea()
	{
		var hwnd = new WindowInteropHelper(this).Handle;
		if(hwnd == IntPtr.Zero || workArea is not { } area)
			return;
		PInvoke.SetWindowPos(new HWND(hwnd), HWND.Null, area.left, area.top, area.width, area.height,
			SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
	}

	public void SetPosition(NotificationPosition newPosition)
	{
		position = newPosition;
		UpdatePlacement();
	}

	public void SetIndents(int horizontal, int vertical)
	{
		horizontalIndent = horizontal;
		verticalIndent = vertical;
		UpdatePlacement();
	}

	private void UpdatePlacement()
	{
		bool top = position is NotificationPosition.TopLeft or NotificationPosition.TopCenter or NotificationPosition.TopRight;
		Pills.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
		Pills.HorizontalAlignment = position switch
		{
			NotificationPosition.TopLeft or NotificationPosition.BottomLeft => HorizontalAlignment.Left,
			NotificationPosition.TopRight or NotificationPosition.BottomRight => HorizontalAlignment.Right,
			_ => HorizontalAlignment.Center
		};
		bool center = Pills.HorizontalAlignment == HorizontalAlignment.Center;
		//the distances are to the pill itself: its margin (room for the shadow) is part of them.
		//The distance from the side doesn't apply to a centered OSD
		double h = center ? 0 : Math.Max(0, horizontalIndent - OsdPill.ShadowMargin);
		double v = Math.Max(0, verticalIndent - OsdPill.ShadowMargin);
		Pills.Margin = new Thickness(h, top ? v : 0, h, top ? 0 : v);
	}

	/// <summary>
	/// Shows the session's pill, or keeps an already shown one open for another <paramref name="displayTime"/>
	/// </summary>
	/// <param name="displayTime">null: until <see cref="Close"/></param>
	public void Show(string key, IManagedMasterAudioSession session, TimeSpan? displayTime)
	{
		if(entries.TryGetValue(key, out var entry) && !entry.Closing)
		{
			RestartTimer(entry);
			return;
		}
		if(entry != null)
			Remove(key, entry);

		while(entries.Count(e => !e.Value.Closing) >= MaxPills)
		{
			var oldest = entries.First(e => !e.Value.Closing);
			Close(oldest.Key);
		}

		var pill = new OsdPill(session) { Opacity = 0 };
		entry = new Entry { Pill = pill };
		if(displayTime is { } time)
		{
			entry.Timer = new DispatcherTimer { Interval = time };
			entry.Timer.Tick += (_, _) => Close(key);
			//the pill stays while the mouse is over it, e.g. to drag its slider
			pill.MouseEnter += (_, _) => entry.Timer.Stop();
			pill.MouseLeave += (_, _) => RestartTimer(entry);
			entry.Timer.Start();
		}
		entries[key] = entry;
		//newer pills further from the corner
		if(Pills.VerticalAlignment == VerticalAlignment.Bottom)
			Pills.Children.Insert(0, pill);
		else
			Pills.Children.Add(pill);

		if(!IsVisible)
			Show();
		pill.BeginAnimation(OpacityProperty, new DoubleAnimation(1, FadeDuration));
	}

	private static void RestartTimer(Entry entry)
	{
		if(entry.Timer == null || entry.Pill.IsMouseOver)
			return;
		entry.Timer.Stop();
		entry.Timer.Start();
	}

	public bool IsShown(string key) => entries.TryGetValue(key, out var entry) && !entry.Closing;

	/// <summary>
	/// Fades the session's pill out, if it's shown
	/// </summary>
	public void Close(string key)
	{
		if(!entries.TryGetValue(key, out var entry) || entry.Closing)
			return;
		entry.Closing = true;
		entry.Timer?.Stop();
		var fade = new DoubleAnimation(0, FadeDuration);
		fade.Completed += (_, _) => Remove(key, entry);
		entry.Pill.BeginAnimation(OpacityProperty, fade);
	}

	private void Remove(string key, Entry entry)
	{
		entry.Timer?.Stop();
		Pills.Children.Remove(entry.Pill);
		//a new pill for the same key may have replaced this one meanwhile
		if(entries.TryGetValue(key, out var current) && current == entry)
			entries.Remove(key);
		if(entries.Count == 0)
			Hide();
	}
}
