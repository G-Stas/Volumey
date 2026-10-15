using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Tests;

[TestClass]
public class MasterVolumeNotificationHandlerTests
{
	[TestMethod]
	public void TheMixersOwnVolumeChangesAreSkipped()
		=> Assert.IsTrue(MasterVolumeNotificationHandler.IsOwnVolumeChange(GuidValue.Internal.VolumeGUID));

	[TestMethod]
	public void BalanceChangesAndOutsideChangesReachTheMixer()
	{
		//raising a channel above the device volume raises the device volume, which the mixer has to show
		Assert.IsFalse(MasterVolumeNotificationHandler.IsOwnVolumeChange(GuidValue.Internal.BalanceGUID));
		//hotkeys write with an empty context so their change still shows an OSD; Windows' own changes have their own contexts
		Assert.IsFalse(MasterVolumeNotificationHandler.IsOwnVolumeChange(Guid.Empty));
		Assert.IsFalse(MasterVolumeNotificationHandler.IsOwnVolumeChange(Guid.NewGuid()));
	}
}
