using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Model;
using static Volumey.Model.AppRoutingRules;

namespace Volumey.Tests;

[TestClass]
public class AppRoutingRulesTests
{
	private const string Here = "{0.0.0.00000000}.{here}";
	private const string Other = "{0.0.0.00000000}.{other}";
	private const string Gone = "{0.0.0.00000000}.{gone}";
	private static readonly string[] Active = [Here, Other];

	private static readonly RouteHere[] AllRoutesHere =
	[
		new RouteHere.FollowsDefault(true), new RouteHere.FollowsDefault(false), new RouteHere.PinnedHere(),
		new RouteHere.PinnedElsewhere(true), new RouteHere.PinnedElsewhere(false)
	];

	private static IEnumerable<PlacementFacts> AllFacts()
		=> from systemSounds in new[] { false, true }
		   from playsNow in new[] { false, true }
		   from route in AllRoutesHere
		   from elsewhere in new[] { false, true }
		   select new PlacementFacts(systemSounds, playsNow, route, elsewhere);

	private static PlacementFacts Facts(RouteHere route, bool playsNow = false, bool elsewhere = true, bool systemSounds = false)
		=> new(systemSounds, playsNow, route, elsewhere);

	[TestMethod]
	public void SystemSoundsAlwaysPlaysHere()
	{
		Assert.IsTrue(PlaysHere(Facts(new RouteHere.PinnedElsewhere(true), systemSounds: true)));
		Assert.IsTrue(PlaysHere(Facts(new RouteHere.FollowsDefault(false), systemSounds: true)));
	}

	[TestMethod]
	public void AnAppPlayingHereNowPlaysHere()
	{
		//an app pinned elsewhere that still plays here (it opened this device itself) keeps its row
		Assert.IsTrue(PlaysHere(Facts(new RouteHere.PinnedElsewhere(true), playsNow: true)));
		Assert.IsTrue(PlaysHere(Facts(new RouteHere.FollowsDefault(false), playsNow: true)));
	}

	[TestMethod]
	public void AnAppPinnedHerePlaysHere()
		=> Assert.IsTrue(PlaysHere(Facts(new RouteHere.PinnedHere())));

	[TestMethod]
	public void AnIdleAppPinnedToAnotherActiveDeviceDoesNotPlayHere()
		=> Assert.IsFalse(PlaysHere(Facts(new RouteHere.PinnedElsewhere(true))));

	[TestMethod]
	public void AnAppPinnedToAnInactiveDevicePlaysWhereItIs()
		=> Assert.IsTrue(PlaysHere(Facts(new RouteHere.PinnedElsewhere(false))));

	[TestMethod]
	public void AnIdleLeftoverRowOnANonDefaultDeviceDoesNotPlayHere()
		=> Assert.IsFalse(PlaysHere(Facts(new RouteHere.FollowsDefault(false), elsewhere: true)));

	[TestMethod]
	public void AnAppOnlyOnANonDefaultDeviceKeepsItsRow()
		=> Assert.IsTrue(PlaysHere(Facts(new RouteHere.FollowsDefault(false), elsewhere: false)));

	[TestMethod]
	public void AnAppFollowingTheDefaultPlaysOnTheDefaultDevice()
		=> Assert.IsTrue(PlaysHere(Facts(new RouteHere.FollowsDefault(true), elsewhere: true)));

	[TestMethod]
	public void PlaysHereAnswersEveryFactAndHidesOnlyIdleRowsOfAppsPlayingElsewhere()
	{
		foreach(var facts in AllFacts())
		{
			bool hidden = !facts.IsSystemSounds && !facts.PlaysNowOnThisDevice
				&& (facts.Route is RouteHere.PinnedElsewhere { DeviceIsActive: true }
					|| facts.Route is RouteHere.FollowsDefault { ThisIsDefault: false } && facts.HasModelOnOtherDevice);
			Assert.AreEqual(!hidden, PlaysHere(facts), facts.ToString());
		}
	}

	[TestMethod]
	public void TheRouteIsSeenFromEachDevice()
	{
		Assert.AreEqual(new RouteHere.FollowsDefault(true), Of(new AppRoute.FollowsDefault(), Here, Here, Active));
		Assert.AreEqual(new RouteHere.FollowsDefault(false), Of(new AppRoute.FollowsDefault(), Here, Other, Active));
		Assert.AreEqual(new RouteHere.FollowsDefault(false), Of(new AppRoute.FollowsDefault(), Here, null, Active));
		Assert.AreEqual(new RouteHere.PinnedHere(), Of(new AppRoute.Pinned(Here), Here, Other, Active));
		Assert.AreEqual(new RouteHere.PinnedElsewhere(true), Of(new AppRoute.Pinned(Other), Here, Here, Active));
		Assert.AreEqual(new RouteHere.PinnedElsewhere(false), Of(new AppRoute.Pinned(Gone), Here, Here, Active));
	}

	[TestMethod]
	public void DeviceIdsFromWindowsAreComparedWithoutCase()
	{
		Assert.AreEqual(new RouteHere.PinnedHere(), Of(new AppRoute.Pinned(Here.ToUpperInvariant()), Here, Other, Active));
		Assert.AreEqual(new RouteHere.PinnedElsewhere(true), Of(new AppRoute.Pinned(Other.ToUpperInvariant()), Here, Here, Active));
		Assert.AreEqual(Other, HomeDevice(new AppRoute.Pinned(Other.ToUpperInvariant()), Here, [Here, Other]));
	}

	[TestMethod]
	public void ThePinnedDevicesRowIsHome()
		=> Assert.AreEqual(Other, HomeDevice(new AppRoute.Pinned(Other), Here, [Here, Other]));

	[TestMethod]
	public void WithoutARowOnThePinnedDeviceTheDefaultDevicesRowIsHome()
		=> Assert.AreEqual(Here, HomeDevice(new AppRoute.Pinned(Gone), Here, [Other, Here]));

	[TestMethod]
	public void AnAppFollowingTheDefaultIsHomeOnTheDefaultDeviceElseOnItsFirstDevice()
	{
		Assert.AreEqual(Here, HomeDevice(new AppRoute.FollowsDefault(), Here, [Other, Here]));
		Assert.AreEqual(Other, HomeDevice(new AppRoute.FollowsDefault(), Gone, [Other, Here]));
		Assert.AreEqual(Other, HomeDevice(new AppRoute.FollowsDefault(), null, [Other, Here]));
	}

	[TestMethod]
	public void AnAppWithoutRowsHasNoHome()
	{
		Assert.IsNull(HomeDevice(new AppRoute.FollowsDefault(), Here, []));
		Assert.IsNull(HomeDevice(new AppRoute.Pinned(Here), Here, []));
	}

	[TestMethod]
	public void AVolumeCopyAppliesOnlyOnItsTargetWhileTheAppPlaysThereAndItsSourceExists()
	{
		var copy = new VolumeCopy(Here, Other);
		Assert.IsTrue(AppliesTo(copy, Other, playsNow: true, sourceExists: true));
		Assert.IsFalse(AppliesTo(copy, Here, playsNow: true, sourceExists: true));
		Assert.IsFalse(AppliesTo(copy, Other, playsNow: false, sourceExists: true));
		Assert.IsFalse(AppliesTo(copy, Other, playsNow: true, sourceExists: false));
	}

	private static readonly Guid ParamA = new("a1a1a1a1-0000-0000-0000-000000000001");
	private static readonly Guid ParamB = new("b2b2b2b2-0000-0000-0000-000000000002");
	private const string PathA = @"C:\Apps\a.exe";
	private const string PathB = @"C:\Apps\b.exe";

	[TestMethod]
	public void TheSameGroupingParameterOrTheSameFileIsOneApp()
	{
		Assert.IsTrue(SameApp(new AppIdentity(ParamA, PathA), new AppIdentity(ParamA, PathB)));
		Assert.IsTrue(SameApp(new AppIdentity(ParamA, PathA), new AppIdentity(ParamB, PathA)));
		Assert.IsFalse(SameApp(new AppIdentity(ParamA, PathA), new AppIdentity(ParamB, PathB)));
		//an app whose path can't be read has its process ID as its path
		Assert.IsTrue(SameApp(new AppIdentity(ParamA, "1234"), new AppIdentity(ParamA, "5678")));
	}

	[TestMethod]
	public void NoGroupingParameterOrNoFileIsNothingShared()
	{
		Assert.IsFalse(SameApp(new AppIdentity(Guid.Empty, PathA), new AppIdentity(Guid.Empty, PathB)));
		Assert.IsFalse(SameApp(new AppIdentity(ParamA, null), new AppIdentity(ParamB, null)));
		Assert.IsFalse(SameApp(new AppIdentity(ParamA, ""), new AppIdentity(ParamB, "")));
	}

	[TestMethod]
	public void AppsLinkedThroughAnotherModelAreOneAppInAnyOrder()
	{
		AppIdentity[] chain = [new(ParamA, PathA), new(ParamA, PathB), new(ParamB, PathB)];
		AppIdentity unrelated = new(Guid.NewGuid(), @"C:\Apps\c.exe");
		foreach(var order in Permutations([0, 1, 2, 3]))
		{
			var models = order.Select(i => i == 3 ? unrelated : chain[i]).ToArray();
			var groups = GroupApps(models);
			Assert.AreEqual(2, groups.Count, string.Join(",", order));
			var chainGroup = groups.Single(g => g.Count == 3);
			CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, chainGroup.Select(i => order[i]).ToArray());
		}
	}

	[TestMethod]
	public void EveryModelIsInExactlyOneGroupListedInInputOrder()
	{
		AppIdentity[] models = [new(ParamA, PathA), new(ParamB, PathB), new(ParamA, @"C:\Apps\c.exe"), new(Guid.NewGuid(), PathB)];
		var groups = GroupApps(models);

		CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, groups.SelectMany(g => g).OrderBy(i => i).ToArray());
		CollectionAssert.AreEqual(new[] { 0, 2 }, groups[0].ToArray());
		CollectionAssert.AreEqual(new[] { 1, 3 }, groups[1].ToArray());
		Assert.AreEqual(0, GroupApps([]).Count);
	}

	private static IEnumerable<int[]> Permutations(int[] items)
		=> items.Length <= 1
			? [items]
			: items.SelectMany(item => Permutations(items.Where(i => i != item).ToArray()).Select(rest => rest.Prepend(item).ToArray()));

	[TestMethod]
	public void AnAppIsSentAgainOnlyToItsReturningDeviceWhenItDoesntPlayThere()
	{
		string[] returned = [Other];
		Assert.IsTrue(ReappliesRoute(new AppRoute.Pinned(Other), returned, playsNowOnPinnedDevice: false));
		Assert.IsTrue(ReappliesRoute(new AppRoute.Pinned(Other.ToUpperInvariant()), returned, playsNowOnPinnedDevice: false));
		Assert.IsFalse(ReappliesRoute(new AppRoute.Pinned(Other), returned, playsNowOnPinnedDevice: true));
		Assert.IsFalse(ReappliesRoute(new AppRoute.Pinned(Here), returned, playsNowOnPinnedDevice: false));
		Assert.IsFalse(ReappliesRoute(new AppRoute.FollowsDefault(), returned, playsNowOnPinnedDevice: false));
		Assert.IsFalse(ReappliesRoute(new AppRoute.Pinned(Other), [], playsNowOnPinnedDevice: false));
	}

	private static readonly AppRoute Previous = new AppRoute.Pinned(Here);

	[TestMethod]
	public void OneDoneWriteIsAMove()
	{
		Assert.AreEqual(new MoveOutcome.Moved(Previous), OutcomeOf([new RouteWrite.Done(), new RouteWrite.ProcessGone()], Previous));
		Assert.AreEqual(new MoveOutcome.Moved(Previous), OutcomeOf([new RouteWrite.Refused(5), new RouteWrite.Done()], Previous));
	}

	[TestMethod]
	public void OnlyGoneProcessesMeanTheAppIsGone()
	{
		Assert.AreEqual(new MoveOutcome.AppGone(), OutcomeOf([new RouteWrite.ProcessGone(), new RouteWrite.ProcessGone()], Previous));
		Assert.AreEqual(new MoveOutcome.AppGone(), OutcomeOf([], Previous));
	}

	[TestMethod]
	public void ARefusalWithoutADoneWriteIsARefusal()
		=> Assert.AreEqual(new MoveOutcome.Refused(), OutcomeOf([new RouteWrite.Refused(5), new RouteWrite.ProcessGone()], Previous));
}
