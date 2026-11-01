namespace Volumey.Model;

/// <summary>How the volume of an app is held, the states of the lock button of the mixer's app rows</summary>
public enum VolumeLockMode
{
	/// <summary>The volume is free</summary>
	None,

	/// <summary>The volume is held until the app exits or Volumey closes; nothing is saved</summary>
	Session,

	/// <summary>The volume is held, and held again each time the app starts: the lock is saved</summary>
	Persistent
}
