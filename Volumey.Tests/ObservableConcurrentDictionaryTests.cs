using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Helper;

namespace Volumey.Tests;

[TestClass]
public class ObservableConcurrentDictionaryTests
{
	[TestMethod]
	public void ChangesShouldNotifyBindings()
	{
		var dictionary = new ObservableConcurrentDictionary<string, int>();
		var collectionChanges = new List<NotifyCollectionChangedAction>();
		var propertyChanges = new List<string>();
		dictionary.CollectionChanged += (s, e) => collectionChanges.Add(e.Action);
		dictionary.PropertyChanged += (s, e) => propertyChanges.Add(e.PropertyName);

		dictionary.Add("a", 1);
		dictionary["a"] = 2;
		dictionary.Remove("a");

		Assert.HasCount(3, collectionChanges);
		Assert.IsTrue(collectionChanges.TrueForAll(a => a == NotifyCollectionChangedAction.Reset));
		CollectionAssert.IsSubsetOf(new[] { "Count", "Keys", "Values" }, propertyChanges);
	}

	[TestMethod]
	public void NoOpChangesShouldNotNotify()
	{
		var dictionary = new ObservableConcurrentDictionary<string, int> { { "a", 1 } };
		int changes = 0;
		dictionary.CollectionChanged += (s, e) => changes++;

		dictionary.Add("a", 5);
		dictionary.Remove("missing");

		Assert.AreEqual(0, changes);
		Assert.AreEqual(1, dictionary["a"]);
	}

	[TestMethod]
	public void ShouldWorkAsDictionary()
	{
		var dictionary = new ObservableConcurrentDictionary<string, int> { { "a", 1 }, { "b", 2 } };

		Assert.AreEqual(2, dictionary.Count);
		Assert.IsTrue(dictionary.ContainsKey("b"));
		Assert.IsTrue(dictionary.TryGetValue("a", out int value));
		Assert.AreEqual(1, value);
		CollectionAssert.AreEquivalent(new[] { "a", "b" }, new List<string>(dictionary.Keys));
	}
}
