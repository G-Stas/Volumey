using System;
using System.Threading.Tasks;
using log4net;

namespace Volumey.Helper
{
	internal static class TaskExtensions
	{
		/// <summary>
		/// Lets an event handler start async work without awaiting it. An exception is logged instead of ending the app:
		/// an exception that escapes an <c>async void</c> method ends the process (that's how the sleep crash #57 closed the app).
		/// Event handlers call this instead of being <c>async void</c> themselves; this is the only <c>async void</c> method left.
		/// </summary>
		internal static async void Forget(this Task task, ILog logger, string failure)
		{
			try
			{
				await task;
			}
			catch(Exception e)
			{
				try { logger.Error(failure, e); }
				catch { /*logging must not end the app either*/ }
			}
		}
	}
}
