using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.CoreAudioWrapper.Wrapper;

namespace Volumey.Tests;

[TestClass]
public class AudioPolicyRoutingTests
{
	private static readonly Guid Windows11 = new("ab3d4648-e242-459f-b02f-541c70306324");
	private static readonly Guid Windows10 = new("2a59116d-6c4f-45e0-a74f-707e3fef9258");
	private static readonly Guid AgileObject = new("00000035-0000-0000-c000-000000000046");
	private static readonly Guid Unknown = new("32aa8e18-6496-4e24-9f94-b800e7eccc45");

	private const string DeviceId = "{0.0.0.00000000}.{8f2c1a4e-0b6d-4c39-9e1f-5a7d3b2c6e10}";
	private const string PolicyId = @"\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{8f2c1a4e-0b6d-4c39-9e1f-5a7d3b2c6e10}#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

	// U1
	[TestMethod]
	public void TheKnownInterfaceIsPicked()
	{
		Assert.AreEqual(Windows11, AudioPolicyRouting.PickInterface([AgileObject, Windows11]));
		Assert.AreEqual(Windows10, AudioPolicyRouting.PickInterface([AgileObject, Windows10]));
	}

	// U1
	[TestMethod]
	public void AnUnknownInterfaceIsNotPicked()
	{
		Assert.IsNull(AudioPolicyRouting.PickInterface([AgileObject, Unknown]));
		Assert.IsNull(AudioPolicyRouting.PickInterface([]));
	}

	// U1
	[TestMethod]
	public void WithBothKnownInterfacesTheWindows11OneIsPicked()
	{
		Assert.AreEqual(Windows11, AudioPolicyRouting.PickInterface([Windows10, AgileObject, Windows11]));
	}

	// U2
	[TestMethod]
	public void ADeviceIdIsWrappedForThePolicy()
	{
		Assert.AreEqual(PolicyId, AudioPolicyRouting.ToPolicyId(DeviceId));
	}

	// U2
	[TestMethod]
	public void AnAlreadyWrappedIdStaysAsItIs()
	{
		Assert.AreEqual(PolicyId, AudioPolicyRouting.ToPolicyId(PolicyId));
		Assert.AreEqual(PolicyId.ToLowerInvariant(), AudioPolicyRouting.ToPolicyId(PolicyId.ToLowerInvariant()));
	}

	// U2
	[TestMethod]
	public void APolicyIdIsUnwrapped()
	{
		Assert.AreEqual(DeviceId, AudioPolicyRouting.FromPolicyId(PolicyId));
	}

	// U2
	[TestMethod]
	public void APolicyIdInAnotherCaseIsUnwrapped()
	{
		Assert.AreEqual(DeviceId.ToUpperInvariant(), AudioPolicyRouting.FromPolicyId(PolicyId.ToUpperInvariant()));
	}

	// U2
	[TestMethod]
	public void NoPolicyIdIsNoDevice()
	{
		Assert.IsNull(AudioPolicyRouting.FromPolicyId(""));
		Assert.IsNull(AudioPolicyRouting.FromPolicyId(null));
	}

	// U2
	[TestMethod]
	public void AnUnknownFormStaysAsItIs()
	{
		Assert.AreEqual(DeviceId, AudioPolicyRouting.FromPolicyId(DeviceId));
		const string capture = @"\\?\SWD#MMDEVAPI#{0.0.1.00000000}.{guid}#{2eef81be-33fa-4800-9670-1cd474972c3f}";
		Assert.AreEqual(capture, AudioPolicyRouting.FromPolicyId(capture));
	}
}
