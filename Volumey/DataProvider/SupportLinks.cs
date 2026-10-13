using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json.Linq;

namespace Volumey.DataProvider;

/// <summary>
/// A page where people can support the project (About page)
/// </summary>
public sealed record SupportLink(string Name, string Url);

/// <summary>
/// The support links, read from links.json on GitHub, so a new build isn't needed
/// to change them. The last copy is kept next to settings.json; without one there are no links (none are built in).
/// File format: <c>{ "support": [ { "name": "Boosty", "url": "https://boosty.to/stasg" } ] }</c>. UI thread only.
/// </summary>
public sealed class SupportLinks
{
	internal const string FeedUrl = "https://raw.githubusercontent.com/G-Stas/Volumey/main/links.json";
	internal const string CacheFileName = "links.json";
	private const int MaxLinks = 5;
	private const int MaxNameLength = 40;

	private static readonly ILog Logger = LogManager.GetLogger(typeof(SupportLinks));
	private static readonly Lazy<HttpClient> Http = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

	private readonly string cachePath;
	private readonly Func<Task<string>> download;

	public ObservableCollection<SupportLink> Links { get; }

	internal SupportLinks() : this(Path.Combine(SettingsProvider.SettingsDir, CacheFileName), () => Http.Value.GetStringAsync(FeedUrl)) { }

	internal SupportLinks(string cachePath, Func<Task<string>> download)
	{
		this.cachePath = cachePath;
		this.download = download;
		Links = new ObservableCollection<SupportLink>(ReadCache() ?? Array.Empty<SupportLink>());
	}

	/// <summary>
	/// Reads the file on GitHub and takes its links; keeps the current ones (the saved copy's, or none) if it can't be read or has none
	/// </summary>
	public async Task RefreshAsync()
	{
		string json;
		try
		{
			json = await download();
		}
		catch(Exception e)
		{
			Logger.Info($"Couldn't read the support links: {e.Message}");
			return;
		}

		var links = Parse(json);
		if(links == null)
		{
			Logger.Warn("The support links file has no valid links");
			return;
		}
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
			File.WriteAllText(cachePath, json);
		}
		catch(Exception e)
		{
			Logger.Warn("Couldn't save the support links", e);
		}
		if(Links.SequenceEqual(links))
			return;
		Links.Clear();
		foreach(var link in links)
			Links.Add(link);
	}

	private IReadOnlyList<SupportLink> ReadCache()
	{
		try
		{
			return File.Exists(cachePath) ? Parse(File.ReadAllText(cachePath)) : null;
		}
		catch(Exception e)
		{
			Logger.Warn("Couldn't read the saved support links", e);
			return null;
		}
	}

	/// <summary>
	/// The valid links of a links.json (a name and an https address each, at most <see cref="MaxLinks"/>), or null if there are none
	/// </summary>
	internal static IReadOnlyList<SupportLink> Parse(string json)
	{
		try
		{
			if(JToken.Parse(json) is not JObject root || root["support"] is not JArray items)
				return null;
			var links = new List<SupportLink>();
			foreach(var item in items.OfType<JObject>())
			{
				string name = (item["name"] as JValue)?.Value as string;
				string url = (item["url"] as JValue)?.Value as string;
				//only web pages: the app opens the address with the system's handler
				if(string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength
				   || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
					continue;
				links.Add(new SupportLink(name.Trim(), uri.AbsoluteUri));
				if(links.Count == MaxLinks)
					break;
			}
			return links.Count > 0 ? links : null;
		}
		catch(Exception)
		{
			return null;
		}
	}
}
