using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Model;
using static Volumey.IntegrationTests.AudioTestEnvironment;

namespace Volumey.IntegrationTests;

/// <summary>
/// App routes read from Windows. Nothing is set: a route set here would stay in Windows for the test executable.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AppRoutingTests
{
	// C1
	[TestMethod]
	public void AnAppWithASessionFollowsTheDefaultDevice()
	{
		RequireOutputDevice();
		//Windows answers only for processes that have used audio; others get E_INVALIDARG like a process that doesn't exist
		using var session = new TestAudioSession();
		var routing = new AudioPolicyRouting();

		Assert.IsTrue(routing.IsAvailable, "Windows' audio policy factory wasn't found");
		Assert.AreEqual(new AppRoute.FollowsDefault(), routing.GetRoute(TestAudioSession.ProcessId));
	}

	[TestMethod]
	public void WindowsNamesADeviceByItsId()
	{
		RequireOutputDevice();
		using var session = new TestAudioSession();

		Assert.IsFalse(string.IsNullOrEmpty(DeviceEnumerator.GetDeviceName(session.DeviceId)));
		Assert.IsNull(DeviceEnumerator.GetDeviceName("{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}"));
	}

	// C2
	[TestMethod]
	public void SystemSoundsHasNoRoute()
	{
		var routing = new AudioPolicyRouting();

		Assert.IsTrue(routing.IsAvailable, "Windows' audio policy factory wasn't found");
		Assert.IsNull(routing.GetRoute(0));
	}
}
