using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider.Settings;

namespace Volumey.Tests;

[TestClass]
public class SettingsSaveStatusTests
{
	[TestCleanup]
	public void Cleanup() => SettingsSaveStatus.Report(null);

	[TestMethod]
	public void TheReasonIsFoundInsideTheSettingsLibrarysException()
	{
		//JsonSettings wraps the exception of the write
		static Exception Wrapped(Exception inner) => new Exception("Failed writing the file", inner);

		Assert.AreEqual(SaveFailure.DiskFull, SettingsSaveStatus.Classify(Wrapped(new IOException("full", unchecked((int)0x80070070)))));
		Assert.AreEqual(SaveFailure.DiskFull, SettingsSaveStatus.Classify(Wrapped(new IOException("full", unchecked((int)0x80070027)))));
		Assert.AreEqual(SaveFailure.InUse, SettingsSaveStatus.Classify(Wrapped(new IOException("in use", unchecked((int)0x80070020)))));
		Assert.AreEqual(SaveFailure.AccessDenied, SettingsSaveStatus.Classify(Wrapped(new UnauthorizedAccessException())));
		Assert.AreEqual(SaveFailure.Other, SettingsSaveStatus.Classify(Wrapped(new IOException("other", unchecked((int)0x80070017)))));
		Assert.AreEqual(SaveFailure.Other, SettingsSaveStatus.Classify(new InvalidOperationException()));
	}

	[TestMethod]
	public void OnlyAChangedStateIsAnnounced()
	{
		var announced = new System.Collections.Generic.List<SaveFailure>();
		void OnChanged(SaveFailure f) => announced.Add(f);
		SettingsSaveStatus.Changed += OnChanged;
		try
		{
			var full = new IOException("full", unchecked((int)0x80070070));
			SettingsSaveStatus.Report(full);
			SettingsSaveStatus.Report(full);
			SettingsSaveStatus.Report(null);
			SettingsSaveStatus.Report(null);

			CollectionAssert.AreEqual(new[] { SaveFailure.DiskFull, SaveFailure.None }, announced);
		}
		finally
		{
			SettingsSaveStatus.Changed -= OnChanged;
		}
	}
}
