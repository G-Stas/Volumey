using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Volumey.UITests.Infrastructure;

namespace Volumey.UITests;

/// <summary>
/// The About page: version and build, the update check (the GitHub build's AutoUpdater), the GitHub links (they open the default
/// browser; the tab the test opened is closed again) and the support links read from a saved links.json
/// </summary>
[TestClass]
public class AboutPageTests : UiTest
{
	private void OpenPage()
	{
		StartApp();
		Settings.Open("NavAbout", "VersionText");
	}

	[TestMethod]
	public void TheVersionAndBuildAreShown()
	{
		OpenPage();
		string version = FileVersionInfo.GetVersionInfo(VolumeyApp.ExePath).FileVersion!;

		string text = Settings.Find("VersionText").Name;

		Assert.IsTrue(text.Contains(version), $"\"{text}\" doesn't contain the version {version}");
		Assert.IsTrue(text.Contains("Development build"), $"\"{text}\" doesn't say it's a development build");
	}

	[TestMethod]
	public void CheckForUpdatesReportsTheResult()
	{
		OpenPage();
		//AutoUpdater (GitHub build) answers in a window of its own: "Update Unavailable", an update, or an error (reported when asked).
		//It's owned by the settings window, where UI Automation lists it, so it's found through Win32
		(IntPtr Handle, string Title, string Class)? Report()
			=> App.VisibleWindows().Where(w => w.Title is not ("Volumey" or "Volumey settings" or "Volumey OSD" or "")).Select(w => ((IntPtr, string, string)?)w)
			      .FirstOrDefault();

		Settings.Find("CheckUpdatesButton").AsButton().Invoke();

		(IntPtr Handle, string Title, string Class)? report = null;
		Assert.IsTrue(Eventually(() => (report = Report()) != null, 30000), "the update check showed nothing");
		TestContext.WriteLine("The update check said: " + report!.Value.Title);
		VolumeyApp.CloseWindow(report.Value.Handle);
	}

	/// <summary>A window of the default browser whose active tab shows the page (its title starts with the page's title)</summary>
	private AutomationElement? BrowserTab(string titleStart)
		=> App.Automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)).FirstOrDefault(w => w.Name.StartsWith(titleStart));

	private void OpensInTheBrowser(string linkId, string titleStart)
	{
		//a CI runner has no browser set up (it would show its first-run pages, not GitHub's)
		if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
			Assert.Inconclusive("No browser on the CI runner");
		OpenPage();
		bool wasOpen = BrowserTab(titleStart) != null;

		Settings.Find(linkId).AsButton().Invoke();

		AutomationElement? tab = null;
		Assert.IsTrue(Eventually(() => (tab = BrowserTab(titleStart)) != null, 20000), $"no browser window showing \"{titleStart}…\"");
		//the new tab is the browser's active one: closed when the test opened it
		if(!wasOpen)
		{
			tab!.AsWindow().SetForeground();
			Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
		}
	}

	[TestMethod]
	public void SourceCodeOpensTheRepository() => OpensInTheBrowser("SourceCodeLink", "G-Stas/Volumey: ");

	[TestMethod]
	public void ReportAProblemOpensTheIssues() => OpensInTheBrowser("IssuesLink", "Issues · G-Stas/Volumey");

	[TestMethod]
	public void SavedSupportLinksAreShown()
	{
		//links.json from GitHub is saved next to settings.json; a failed download (it isn't on main yet) keeps the saved one
		string dir = Path.Combine(Path.GetTempPath(), "Volumey-uitests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		Later(new DeleteDir(dir));
		File.WriteAllText(Path.Combine(dir, "links.json"), """
			{ "support": [ { "name": "Volumey UI test link", "url": "https://example.com/one" }, { "name": "Not https", "url": "http://example.com" } ] }
			""");
		File.WriteAllText(Path.Combine(dir, "settings.json"),
			new JsonObject { ["General"] = new JsonObject { ["Language"] = "en" } }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
		StartAppIn(dir);
		Settings.Open("NavAbout", "VersionText");

		var links = Settings.Find("SupportLinks");
		var names = links.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).Or(cf.ByControlType(ControlType.Hyperlink))).Select(l => l.Name).ToList();

		//named "Support the project: <name>" for screen readers
		Assert.IsTrue(names.Any(n => n.EndsWith("Volumey UI test link")), "the saved link isn't shown: " + string.Join(", ", names));
		Assert.IsFalse(names.Any(n => n.EndsWith("Not https")), "a link that isn't https is shown");
	}

	private sealed class DeleteDir(string dir) : IDisposable
	{
		public void Dispose()
		{
			try
			{
				Directory.Delete(dir, true);
			}
			catch(IOException)
			{
			}
		}
	}
}
