using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.DataProvider;
using Volumey.Model;
using Volumey.Services;
using Volumey.ViewModel;

namespace Volumey.Tests.ViewModels;

[TestClass]
public class AppOutputTests : ViewModelTestBase
{
	private const string Game = "{0.0.0.00000000}.{game}";
	private const string Chat = "{0.0.0.00000000}.{chat}";
	private const string Speakers = "{0.0.0.00000000}.{speakers}";
	private static readonly (string, string)[] Active = [(Game, "Headphones (Game)"), (Chat, "Headphones (Chat)")];

	// U12
	[TestMethod]
	public void TheDefaultDeviceComesFirstThenEveryActiveDevice()
	{
		var options = AppOutputOptions.Build(Active, "Headphones (Game)", new AppRoute.FollowsDefault(), null);

		CollectionAssert.AreEqual(new[]
		{
			new AppOutputOption(null, "", "Headphones (Game)", true),
			new AppOutputOption(Game, "Headphones (Game)", null, true),
			new AppOutputOption(Chat, "Headphones (Chat)", null, true)
		}, options.ToArray());
		Assert.AreEqual(0, AppOutputOptions.IndexOf(options, new AppRoute.FollowsDefault()));
	}

	// U12
	[TestMethod]
	public void AnAppPinnedToAnActiveDeviceAddsNothing()
	{
		var options = AppOutputOptions.Build(Active, "Headphones (Game)", new AppRoute.Pinned(Chat), "Headphones (Chat)");

		Assert.AreEqual(3, options.Count);
		Assert.AreEqual(2, AppOutputOptions.IndexOf(options, new AppRoute.Pinned(Chat)));
		Assert.AreEqual(2, AppOutputOptions.IndexOf(options, new AppRoute.Pinned(Chat.ToUpperInvariant())));
	}

	// U12
	[TestMethod]
	public void AnUnpluggedPinnedDeviceComesLastAndCantBePicked()
	{
		var options = AppOutputOptions.Build(Active, "Headphones (Game)", new AppRoute.Pinned(Speakers), "Speakers");

		Assert.AreEqual(new AppOutputOption(Speakers, "Speakers", null, false), options[^1]);
		Assert.AreEqual(3, AppOutputOptions.IndexOf(options, new AppRoute.Pinned(Speakers)));
	}

	// U12
	[TestMethod]
	public void ADeviceWindowsCantNameHasNoNameAndTheDefaultsDetail()
	{
		var options = AppOutputOptions.Build(Active, "Headphones (Game)", new AppRoute.Pinned(Speakers), null);

		//the closed box shows "Default device · <default>" for it: Windows plays the app there meanwhile
		Assert.AreEqual(new AppOutputOption(Speakers, "", "Headphones (Game)", false), options[^1]);
	}

	private Mock<IAppRouting> routing;
	private DeviceProviderViewModel vm;
	private AudioProcessModel app;

	private void Arrange()
	{
		app = CreateProcess("Spotify");
		SetDefaultDevice(CreateDevice(Game, "Headphones (Game)", app));
		ActiveDevices.Add(CreateDevice(Chat, "Headphones (Chat)"));
		routing = new Mock<IAppRouting>();
		routing.Setup(r => r.IsAvailable).Returns(true);
		vm = new DeviceProviderViewModel(DeviceProvider.Object, new Mock<IAppShell>().Object, routing.Object);
	}

	private void WillAnswer(MoveOutcome outcome)
		=> routing.Setup(r => r.MoveAsync(It.IsAny<AudioProcessModel>(), It.IsAny<AppRoute>())).ReturnsAsync(outcome);

	private static readonly AppOutputOption ChatOption = new(Chat, "Headphones (Chat)", null, true);

	private async Task PickChat() => await vm.PickOutputAsync(app, ChatOption);

	// U18
	[TestMethod]
	public async Task AMovedPickShowsTheNoticeWithUndo()
	{
		Arrange();
		WillAnswer(new MoveOutcome.Moved(new AppRoute.FollowsDefault()));

		await PickChat();

		Assert.IsTrue(vm.Notice.CanUndo);
		StringAssert.Contains(vm.Notice.Text, "Spotify");
		StringAssert.Contains(vm.Notice.Text, "Headphones (Chat)");
		routing.Verify(r => r.MoveAsync(app, new AppRoute.Pinned(Chat)));
	}

	// U18
	[TestMethod]
	public async Task PickingDefaultDeviceNamesTheCurrentDefault()
	{
		Arrange();
		WillAnswer(new MoveOutcome.Moved(new AppRoute.Pinned(Chat)));
		app.SetPlacement(new AppRoute.Pinned(Chat), "Headphones (Chat)", true);

		await vm.PickOutputAsync(app, new AppOutputOption(null, "", "Headphones (Game)", true));

		StringAssert.Contains(vm.Notice.Text, "Headphones (Game)");
		routing.Verify(r => r.MoveAsync(app, new AppRoute.FollowsDefault()));
	}

	// U18
	[TestMethod]
	public async Task PickingTheCurrentChoiceDoesNothing()
	{
		Arrange();

		await vm.PickOutputAsync(app, new AppOutputOption(null, "", "Headphones (Game)", true));

		Assert.IsNull(vm.Notice);
		routing.Verify(r => r.MoveAsync(It.IsAny<AudioProcessModel>(), It.IsAny<AppRoute>()), Times.Never);
	}

	// U18
	[TestMethod]
	public async Task ARefusedPickShowsTheFailureWithoutUndo()
	{
		Arrange();
		WillAnswer(new MoveOutcome.Refused());

		await PickChat();

		Assert.IsFalse(vm.Notice.CanUndo);
		StringAssert.Contains(vm.Notice.Text, "Spotify");
		StringAssert.Contains(vm.Notice.Text, "Headphones (Chat)");
	}

	// U18
	[TestMethod]
	public async Task APickOfAGoneDeviceOrAGoneAppShowsNothing()
	{
		Arrange();
		WillAnswer(new MoveOutcome.DeviceGone());
		await PickChat();
		Assert.IsNull(vm.Notice);

		WillAnswer(new MoveOutcome.AppGone());
		await PickChat();
		Assert.IsNull(vm.Notice);
	}

	private async Task UndoAfterAMove(MoveOutcome undoOutcome)
	{
		Arrange();
		WillAnswer(new MoveOutcome.Moved(new AppRoute.FollowsDefault()));
		await PickChat();
		app.SetPlacement(new AppRoute.Pinned(Chat), "Headphones (Chat)", false);
		WillAnswer(undoOutcome);

		await vm.UndoMoveCommand.ExecuteAsync(null);

		routing.Verify(r => r.MoveAsync(app, new AppRoute.FollowsDefault()));
	}

	// U18
	[TestMethod]
	public async Task ADoneUndoClosesTheNotice()
	{
		await UndoAfterAMove(new MoveOutcome.Moved(new AppRoute.Pinned(Chat)));
		Assert.IsNull(vm.Notice);
	}

	// U18
	[TestMethod]
	public async Task ARefusedUndoShowsTheFailure()
	{
		await UndoAfterAMove(new MoveOutcome.Refused());
		Assert.IsFalse(vm.Notice.CanUndo);
		StringAssert.Contains(vm.Notice.Text, "Spotify");
		StringAssert.Contains(vm.Notice.Text, "Headphones (Game)");
	}

	// U18
	[TestMethod]
	public async Task AnUndoToADeviceThatLeftShowsTheFailure()
	{
		await UndoAfterAMove(new MoveOutcome.DeviceGone());
		Assert.IsFalse(vm.Notice.CanUndo);
	}

	// U18
	[TestMethod]
	public async Task AnUndoOfAnExitedAppShowsNothing()
	{
		await UndoAfterAMove(new MoveOutcome.AppGone());
		Assert.IsNull(vm.Notice);
	}

	[TestMethod]
	public void TheBoxListsTheAppsChoice()
	{
		Arrange();
		app.SetPlacement(new AppRoute.Pinned(Chat), "Headphones (Chat)", true);

		var (options, selected) = vm.OutputOptionsOf(app);

		Assert.AreEqual(3, options.Count);
		Assert.AreEqual(Chat, options[selected].DeviceId);
	}
}
