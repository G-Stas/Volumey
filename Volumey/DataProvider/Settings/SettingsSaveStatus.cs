#nullable enable
using System;
using System.IO;

namespace Volumey.DataProvider.Settings;

/// <summary>Why the settings file couldn't be written</summary>
public enum SaveFailure
{
	None,
	DiskFull,
	AccessDenied,
	InUse,
	Other,
}

/// <summary>
/// Whether the last write of the settings file worked, for the settings window's error bar. The settings stay in use when a write
/// fails; the next change writes the whole file again.
/// </summary>
public static class SettingsSaveStatus
{
	private static readonly object Lock = new object();
	private static SaveFailure current;

	public static SaveFailure Current
	{
		get { lock(Lock) return current; }
	}

	/// <summary>Raised when <see cref="Current"/> changes, on the thread that wrote the file (a timer thread or the UI thread)</summary>
	public static event Action<SaveFailure>? Changed;

	/// <param name="error">Null after a write that worked</param>
	internal static void Report(Exception? error)
	{
		var failure = error == null ? SaveFailure.None : Classify(error);
		lock(Lock)
		{
			if(failure == current)
				return;
			current = failure;
		}
		Changed?.Invoke(failure);
	}

	private const int ErrorAccessDenied = 5;
	private const int ErrorSharingViolation = 32;
	private const int ErrorLockViolation = 33;
	private const int ErrorHandleDiskFull = 39;
	private const int ErrorDiskFull = 112;

	/// <summary>JsonSettings wraps the exception of the write in its own</summary>
	internal static SaveFailure Classify(Exception error)
	{
		for(var e = error; e != null; e = e.InnerException)
		{
			if(e is UnauthorizedAccessException)
				return SaveFailure.AccessDenied;
			if(e is IOException)
			{
				switch(e.HResult & 0xFFFF)
				{
					case ErrorDiskFull or ErrorHandleDiskFull:
						return SaveFailure.DiskFull;
					case ErrorSharingViolation or ErrorLockViolation:
						return SaveFailure.InUse;
					case ErrorAccessDenied:
						return SaveFailure.AccessDenied;
				}
			}
		}
		return SaveFailure.Other;
	}
}
