using System;

namespace Volumey.Helper
{
	/// <summary>
	/// Contains information about a low-level keyboard input event.
	/// </summary>
	public class KeyboardHookEventInfo
	{
		/// <summary>
		/// A virtual-key code. The code must be a value in the range 1 to 254.
		/// </summary>
		public int vkCode;

		/// <summary>
		/// A hardware scan code for the key.
		/// </summary>
		public int scanCode;

		/// <summary>
		/// The extended-key flag, event-injected flags, context code, and transition-state flag.
		/// </summary>
		public int flags;

		/// <summary>
		/// The time stamp for this message
		/// </summary>
		public long time;

		/// <summary>
		/// Additional information associated with the message.
		/// </summary>
		public IntPtr extraInfo;
	}
}