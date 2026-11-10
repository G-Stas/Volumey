using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Volumey.Services;
using Volumey.ViewModel;

namespace Volumey.Tests;

[TestClass]
public class SupportViewModelTests
{
	private sealed class FakeStore : ISupportStore
	{
		public List<SupportTier> Tiers { get; set; } = [];
		public SupportPurchaseResult Result { get; set; } = SupportPurchaseResult.Succeeded;
		public bool Throws { get; set; }
		public int ReadFailures { get; set; }
		public int Reads { get; private set; }
		public int Fulfilled { get; private set; }
		public bool FulfillThrows { get; set; }
		public IntPtr LastWindow { get; private set; }

		public Task<IReadOnlyList<SupportTier>> GetTiersAsync()
		{
			if(Reads++ < ReadFailures)
				throw new InvalidOperationException("Store unreachable");
			return Task.FromResult<IReadOnlyList<SupportTier>>(Tiers.ToList());
		}

		public Task<SupportPurchaseResult> PurchaseAsync(SupportTier tier, IntPtr windowHandle)
		{
			LastWindow = windowHandle;
			if(Throws)
				throw new InvalidOperationException("Store failure");
			return Task.FromResult(Result);
		}

		public Task FulfillPendingAsync(IReadOnlyList<SupportTier> tiers)
		{
			Fulfilled++;
			return FulfillThrows ? throw new InvalidOperationException("Store failure") : Task.CompletedTask;
		}
	}

	private static readonly SupportTier Small = new("1", "Small", "$1");
	private static readonly SupportTier Big = new("2", "Big", "$5");

	[TestMethod]
	public async Task NothingIsOfferedWithoutAddOns()
	{
		var vm = new SupportViewModel(new FakeStore(), read: false);
		await vm.RefreshAsync();
		Assert.IsFalse(vm.IsAvailable);
		Assert.AreEqual(0, vm.Tiers.Count);
	}

	[TestMethod]
	public async Task AddOnsMakeTheSupportAvailable()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small, Big] }, read: false);
		await vm.RefreshAsync();
		Assert.IsTrue(vm.IsAvailable);
		CollectionAssert.AreEqual(new[] { "1", "2" }, vm.Tiers.Select(t => t.StoreId).ToArray());
	}

	[TestMethod]
	public async Task UnusedPurchasesAreReportedWhenTheAddOnsAreRead()
	{
		var store = new FakeStore { Tiers = [Small] };
		var vm = new SupportViewModel(store, read: false);
		await vm.RefreshAsync();
		Assert.AreEqual(1, store.Fulfilled);
	}

	[TestMethod]
	public async Task AFailedReportDoesNotHideTheHeart()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small], FulfillThrows = true }, read: false);
		await vm.RefreshAsync();
		Assert.IsTrue(vm.IsAvailable);
	}

	[TestMethod]
	public async Task APurchaseOpensOverTheWindow()
	{
		var store = new FakeStore { Tiers = [Small, Big] };
		var vm = new SupportViewModel(store, read: false);
		vm.Attach(new IntPtr(42));
		await vm.RefreshAsync();
		var result = await vm.PurchaseAsync(Small);
		Assert.AreEqual(SupportPurchaseResult.Succeeded, result);
		Assert.AreEqual(new IntPtr(42), store.LastWindow);
	}

	[TestMethod]
	public async Task ACancelledPurchaseChangesNothing()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small], Result = SupportPurchaseResult.Cancelled }, read: false);
		await vm.RefreshAsync();
		Assert.AreEqual(SupportPurchaseResult.Cancelled, await vm.PurchaseAsync(Small));
	}

	[TestMethod]
	public async Task AStoreFailureIsReportedNotThrown()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small], Throws = true }, read: false);
		await vm.RefreshAsync();
		Assert.AreEqual(SupportPurchaseResult.Failed, await vm.PurchaseAsync(Small));
	}

	[TestMethod]
	public async Task TheCommandAsksTheWindowForTheDialog()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small] }, read: false);
		int requests = 0;
		vm.DialogRequested += () => requests++;
		vm.OpenDialogCommand.Execute(null);
		Assert.AreEqual(1, requests);
		await vm.RefreshAsync();
	}

	[TestMethod]
	public async Task AnUnreachableStoreIsTriedAgainUntilItAnswers()
	{
		var store = new FakeStore { Tiers = [Small], ReadFailures = 2 };
		var vm = new SupportViewModel(store, read: false);
		await vm.ReadWithRetriesAsync([TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]);
		Assert.AreEqual(3, store.Reads);
		Assert.IsTrue(vm.IsAvailable);
	}

	[TestMethod]
	public async Task AnUnreachableStoreIsGivenUpOnAfterTheLastRetry()
	{
		var store = new FakeStore { Tiers = [Small], ReadFailures = 100 };
		var vm = new SupportViewModel(store, read: false);
		await vm.ReadWithRetriesAsync([TimeSpan.Zero, TimeSpan.Zero]);
		Assert.AreEqual(3, store.Reads);
		Assert.IsFalse(vm.IsAvailable);
	}

	[TestMethod]
	public async Task AStoreWithoutAddOnsIsNotTriedAgain()
	{
		var store = new FakeStore();
		var vm = new SupportViewModel(store, read: false);
		await vm.ReadWithRetriesAsync([TimeSpan.Zero, TimeSpan.Zero]);
		Assert.AreEqual(1, store.Reads);
	}

	[TestMethod]
	public async Task TheMiddleTierIsSuggested()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small, new("3", "Middle", "$3"), Big] }, read: false);
		await vm.RefreshAsync();
		CollectionAssert.AreEqual(new[] { false, true, false }, vm.Tiers.Select(t => t.IsHighlighted).ToArray());
	}

	[TestMethod]
	public async Task OfTwoTiersTheCheaperIsSuggested()
	{
		var vm = new SupportViewModel(new FakeStore { Tiers = [Small, Big] }, read: false);
		await vm.RefreshAsync();
		CollectionAssert.AreEqual(new[] { true, false }, vm.Tiers.Select(t => t.IsHighlighted).ToArray());
	}
}
