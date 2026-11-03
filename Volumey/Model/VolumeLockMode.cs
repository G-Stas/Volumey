namespace Volumey.Model;

/// <summary>How an app's volume is held, the parts of the mixer's lock bar</summary>
public enum VolumeLockMode
{
	/// <summary>The volume is free</summary>
	Off,

	/// <summary>The volume is held until the app exits or Volumey closes; nothing is saved</summary>
	UntilClosed,

	/// <summary>The volume is held, and held again each time the app starts: the lock is saved</summary>
	Always
}
