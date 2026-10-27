using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;
using Volumey.DataProvider.Settings;

namespace Volumey.Tests;

[TestClass]
public sealed class MixerProcessOrderTests
{
	[TestMethod]
	public void ApplyRestoresSavedOrderAndKeepsUnseenSourcesInArrivalOrder()
	{
		var processes = new ObservableCollection<string> { "system-sounds", "browser.exe", "music.exe", "chat.exe" };

		MixerProcessOrder.Apply(processes, key => key, new[] { "music.exe", "browser.exe" });

		CollectionAssert.AreEqual(new[] { "music.exe", "browser.exe", "system-sounds", "chat.exe" }, processes.ToArray());
	}

	[TestMethod]
	public void ApplyKeepsNewSourcesAfterSavedSourcesAndPreservesTheirRelativeOrder()
	{
		var processes = new ObservableCollection<string> { "new-one.exe", "saved.exe", "new-two.exe" };

		MixerProcessOrder.Apply(processes, key => key, new[] { "saved.exe", "gone.exe" });

		CollectionAssert.AreEqual(new[] { "saved.exe", "new-one.exe", "new-two.exe" }, processes.ToArray());
	}

	[TestMethod]
	public void SaveOrderAddsCurrentSourcesAndRetainsTemporarilyMissingSources()
	{
		string[] saved = MixerProcessOrder.SaveOrder(new[] { "third.exe", "first.exe" }, key => key,
			new[] { "first.exe", "second.exe", "third.exe", "removed.exe" });

		CollectionAssert.AreEqual(new[] { "third.exe", "first.exe", "second.exe", "removed.exe" }, saved);
	}

	[TestMethod]
	public void RemovedSourceDoesNotBreakOrderAndReturnsToItsSavedPosition()
	{
		var processes = new ObservableCollection<string> { "second.exe", "first.exe", "third.exe" };
		string[] savedOrder = { "first.exe", "second.exe", "third.exe" };
		processes.Remove("second.exe");
		MixerProcessOrder.Apply(processes, key => key, savedOrder);
		CollectionAssert.AreEqual(new[] { "first.exe", "third.exe" }, processes.ToArray());

		processes.Add("second.exe");
		MixerProcessOrder.Apply(processes, key => key, savedOrder);
		CollectionAssert.AreEqual(new[] { "first.exe", "second.exe", "third.exe" }, processes.ToArray());
	}

	[TestMethod]
	public void PreferredOrderIsPersistedAndReloaded()
	{
		string dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
		try
		{
			var settings = SettingsProvider.LoadSettings(dir);
			settings.Mixer.ProcessOrder = new[] { "music.exe", "browser.exe" };
			settings.Flush();

			var reloaded = SettingsProvider.LoadSettings(dir);
			CollectionAssert.AreEqual(new[] { "music.exe", "browser.exe" }, reloaded.Mixer.ProcessOrder.ToArray());
		}
		finally
		{
			if(Directory.Exists(dir))
				Directory.Delete(dir, true);
		}
	}
}
