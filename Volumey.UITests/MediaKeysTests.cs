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
	public void InBlockingModeAHotkeyInUseCantBeTypedIntoAnotherBox()
	{
		OpenPage();
		var combo = Combo.F(17);
		Settings.TypeHotkey(Box(PlayPause), combo);
		App.WaitForSettings(s => IsHotkey(Saved(s, "MediaPlayPause"), combo), "play/pause saved");

		//Windows delivers a registered combination as Volumey's hotkey, not as keys: the box can't record it
		Settings.TypeHotkey(Box(Next), combo);

		Thread.Sleep(800);
		Assert.IsNull(Saved(App.ReadSettings(), "MediaNextTrack"), "the same hotkey was saved twice");
		Assert.IsTrue(string.IsNullOrEmpty(Box(Next).AsTextBox().Text), "the box shows a hotkey");
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
