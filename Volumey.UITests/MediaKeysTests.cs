using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The Media keys page: one hotkey per media key, changed in place and cleared with the X button
/// </summary>
[TestClass]
public class MediaKeysTests : UiTest
{
	private const string PlayPause = "Play / pause";
	private const string Next = "Next track";

	private void OpenPage(bool shareKeys = false)
	{
		StartApp(shareKeys ? new JsonObject { ["Hotkeys"] = new JsonObject { ["BlockInSystem"] = false } } : null);
		Settings.Open("NavMediaKeys", "MediaKeyList");
	}

	/// <summary>A media key's hotkey box (named after the key)</summary>
	private AutomationElement Box(string key)
		=> Settings.Find("MediaKeyList").FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit).And(cf.ByName(key)))
		   ?? throw new InvalidOperationException("No hotkey box for " + key);

	private static JsonNode? Saved(JsonNode settings, string systemKey)
		=> settings["Hotkeys"]?["System"]?.AsArray().FirstOrDefault(e => e?["SystemKey"]?.GetValue<string>() == systemKey)?["Replacement"];

	[TestMethod]
	public void AMediaKeyHotkeyIsSetChangedAndCleared()
	{
		OpenPage();
		var first = Combo.F(15);
		var second = Combo.F(16);

		Settings.TypeHotkey(Box(PlayPause), first);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaPlayPause"), first), "play/pause saved");
		Assert.IsTrue(first.IsHeldByAnotherApp(), "the hotkey isn't registered");

		Settings.TypeHotkey(Box(PlayPause), second);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaPlayPause"), second), "play/pause changed");
		Assert.IsTrue(second.IsHeldByAnotherApp(), "the new hotkey isn't registered");
		Assert.IsFalse(first.IsHeldByAnotherApp(), "the old hotkey is still registered");

		var clear = Settings.Find("MediaKeyList").FindFirstDescendant(cf => cf.ByName("Clear the hotkey for " + PlayPause))
		            ?? throw new InvalidOperationException("No clear button");
		clear.AsButton().Invoke();
		App.WaitForSettings(s => Saved(s, "MediaPlayPause") == null, "play/pause cleared");
		Assert.IsFalse(second.IsHeldByAnotherApp(), "a cleared hotkey is still registered");
	}

	[TestMethod]
	public void InBlockingModeAHotkeyInUseSaysItsAlreadyUsedInVolumey()
	{
		OpenPage();
		var combo = Combo.F(17);
		Settings.TypeHotkey(Box(PlayPause), combo);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaPlayPause"), combo), "play/pause saved");

		//Windows delivers a registered combination as Volumey's hotkey, not as keys: the box can't record it, only its key release
		Settings.TypeHotkey(Box(Next), combo);

		Assert.IsTrue(Settings.BoxSays(Box(Next), TakenInVolumey), "no message under the box");
		Assert.IsFalse(Settings.BoxSays(Box(Next), TakenByAnotherApp, 500), "Volumey's own hotkey was reported as another app's");
		Assert.IsNull(Saved(App.ReadSettings(), "MediaNextTrack"), "the same hotkey was saved twice");
		Assert.IsTrue(string.IsNullOrEmpty(Box(Next).AsTextBox().Text), "the box shows a hotkey");

		//the next key typed into the box clears the message
		Settings.TypeHotkey(Box(Next), Combo.F(18));
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaNextTrack"), Combo.F(18)), "next track saved");
		Assert.IsFalse(Settings.BoxSays(Box(Next), TakenInVolumey, 500), "the message stayed");
	}

	[TestMethod]
	public void EachHotkeySendsItsMediaKey() => SendsEachMediaKey(shareKeys: false);

	[TestMethod]
	public void InSharedModeEachHotkeySendsItsMediaKey() => SendsEachMediaKey(shareKeys: true);

	private void SendsEachMediaKey(bool shareKeys)
	{
		//WPF's key names, as saved
		var keys = new (string Name, Combo Combo, int VirtualKey)[]
		{
			("MediaPlayPause", Combo.F(13), MediaKeyCatcher.PlayPause),
			("MediaPreviousTrack", Combo.F(14), MediaKeyCatcher.PreviousTrack),
			("MediaNextTrack", Combo.F(15), MediaKeyCatcher.NextTrack),
			("MediaStop", Combo.F(16), MediaKeyCatcher.Stop)
		};
		StartApp(new JsonObject
		{
			["Hotkeys"] = new JsonObject
			{
				["BlockInSystem"] = !shareKeys,
				["System"] = new JsonArray(keys.Select(k => (JsonNode)new JsonObject { ["SystemKey"] = k.Name, ["Replacement"] = HotkeyJson(k.Combo) }).ToArray())
			}
		});
		//a hook can't be checked from outside: in shared mode the presses below show that it works
		if(shareKeys)
			Thread.Sleep(1000);
		else
			Assert.IsTrue(Eventually(() => keys.All(k => k.Combo.IsHeldByAnotherApp())), "the saved media key hotkeys aren't registered");

		//caught and swallowed before any player gets them
		using var catcher = new MediaKeyCatcher();
		foreach(var key in keys)
		{
			key.Combo.Press();
			Assert.IsTrue(catcher.WaitFor(key.VirtualKey), $"{key.Combo} didn't send {key.Name}");
		}
	}

	[TestMethod]
	public void InSharedModeADuplicateIsRejectedWithAnError()
	{
		OpenPage(shareKeys: true);
		var combo = Combo.F(18);
		Settings.TypeHotkey(Box(PlayPause), combo);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaPlayPause"), combo), "play/pause saved");

		Settings.TypeHotkey(Box(Next), combo);

		Assert.IsTrue(Settings.ShowsText(ErrorExists), "no error shown");
		Assert.IsNull(Saved(App.ReadSettings(), "MediaNextTrack"), "the same hotkey was saved twice");
	}

	[TestMethod]
	public void SavedMediaKeyHotkeysAreRegisteredAndShownAfterARestart()
	{
		OpenPage();
		var combo = Combo.F(19);
		Settings.TypeHotkey(Box(Next), combo);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaNextTrack"), combo), "next track saved");

		Restart();
		Settings.Open("NavMediaKeys", "MediaKeyList");

		Assert.IsTrue(combo.IsHeldByAnotherApp(), "the hotkey isn't registered after the restart");
		Assert.IsFalse(string.IsNullOrEmpty(Box(Next).AsTextBox().Text), "the box is empty after the restart");
	}
}
