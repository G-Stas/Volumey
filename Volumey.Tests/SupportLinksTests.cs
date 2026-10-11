using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.DataProvider;

namespace Volumey.Tests;

[TestClass]
public class SupportLinksTests
{
	private const string TwoLinks = """{ "support": [ { "name": "Boosty", "url": "https://boosty.to/stasg" }, { "name": "Ko-fi", "url": "https://ko-fi.com/x" } ] }""";

	private string dir;
	private string cache;

	[TestInitialize]
	public void SetUp()
	{
		dir = Path.Combine(Path.GetTempPath(), "VolumeyTests", Guid.NewGuid().ToString("N"));
		cache = Path.Combine(dir, SupportLinks.CacheFileName);
	}

	[TestCleanup]
	public void CleanUp()
	{
		if(Directory.Exists(dir))
			Directory.Delete(dir, true);
	}

	[TestMethod]
	public void TheRepositoryFileIsValid()
	{
		//links.json at the repository root is what the app downloads
		string repoFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "links.json");
		if(!File.Exists(repoFile))
			Assert.Inconclusive("links.json not found next to the repository");
		Assert.IsNotNull(SupportLinks.Parse(File.ReadAllText(repoFile)));
	}

	[TestMethod]
	public void OnlyNamedHttpsLinksAreTaken()
	{
		var links = SupportLinks.Parse("""
			{ "support": [
				{ "name": "Boosty", "url": "https://boosty.to/stasg" },
				{ "name": "Plain", "url": "http://example.com" },
				{ "name": "Script", "url": "file:///C:/Windows/notepad.exe" },
				{ "name": "", "url": "https://example.com" },
				{ "url": "https://example.com" },
				{ "name": "Relative", "url": "/donate" }
			] }
			""");

		var link = Assert.ContainsSingle(links);
		Assert.AreEqual("Boosty", link.Name);
		Assert.AreEqual("https://boosty.to/stasg", link.Url);
	}

	[TestMethod]
	public void AFileWithoutValidLinksGivesNothing()
	{
		Assert.IsNull(SupportLinks.Parse("not json"));
		Assert.IsNull(SupportLinks.Parse("[]"));
		Assert.IsNull(SupportLinks.Parse("""{ "support": [] }"""));
		Assert.IsNull(SupportLinks.Parse("""{ "support": [ { "name": "x", "url": "http://x" } ] }"""));
	}

	[TestMethod]
	public void AtMostFiveLinksAreTaken()
	{
		string items = string.Join(",", Enumerable.Range(1, 8).Select(i => $$"""{ "name": "L{{i}}", "url": "https://example.com/{{i}}" }"""));
		Assert.HasCount(5, SupportLinks.Parse($$"""{ "support": [ {{items}} ] }"""));
	}

	[TestMethod]
	public void WithoutASavedCopyThereAreNoLinks()
	{
		var links = new SupportLinks(cache, () => Task.FromResult(""));

		Assert.IsEmpty(links.Links);
	}

	[TestMethod]
	public async Task DownloadedLinksAreTakenAndSaved()
	{
		var links = new SupportLinks(cache, () => Task.FromResult(TwoLinks));

		await links.RefreshAsync();

		Assert.HasCount(2, links.Links);
		Assert.AreEqual("Ko-fi", links.Links[1].Name);
		//the next start reads the saved copy before downloading
		var nextStart = new SupportLinks(cache, () => throw new HttpRequestException("offline"));
		Assert.HasCount(2, nextStart.Links);
	}

	[TestMethod]
	public async Task AFailedOrInvalidDownloadKeepsTheLinks()
	{
		var offline = new SupportLinks(cache, () => throw new HttpRequestException("offline"));
		await offline.RefreshAsync();
		Assert.IsEmpty(offline.Links);

		var invalid = new SupportLinks(cache, () => Task.FromResult("<html>rate limited</html>"));
		await invalid.RefreshAsync();
		Assert.IsEmpty(invalid.Links);
		Assert.IsFalse(File.Exists(cache), "an invalid file isn't saved");

		//a saved copy stays when the download fails
		await new SupportLinks(cache, () => Task.FromResult(TwoLinks)).RefreshAsync();
		var savedThenOffline = new SupportLinks(cache, () => throw new HttpRequestException("offline"));
		await savedThenOffline.RefreshAsync();
		Assert.HasCount(2, savedThenOffline.Links);
	}
}
