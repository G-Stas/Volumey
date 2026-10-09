using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Volumey.CoreAudioWrapper.Wrapper;
using Volumey.Localization;
using Volumey.Model;
using Volumey.View.Converters;
using Volumey.ViewModel.Settings;

namespace Volumey.Tests
{
	[TestClass]
	public class ConvertersTests
	{
		private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

		[TestInitialize]
		public void Initialize() => TranslationSource.SetDefaultLanguage();

		private static object Convert(IValueConverter converter, object value, object parameter = null, System.Type targetType = null)
			=> converter.Convert(value, targetType ?? typeof(object), parameter, Culture);

		private static object Convert(IMultiValueConverter converter, params object[] values)
			=> converter.Convert(values, typeof(object), null, Culture);

		private static MasterSessionModel CreateMaster(string name, int volume, bool muted)
			=> new MasterSessionModel(name, "desc", volume, muted, "id", null,
			                          new Mock<IAudioSessionVolume>().Object, new Mock<IMasterVolumeNotificationHandler>().Object, new HotkeyService());

		[TestMethod]
		public void BoolToVisibilityHidesOnTrue()
		{
			//the converter is used to hide elements when a flag is set
			var converter = new BoolToVisibilityConverter();
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, true));
			Assert.AreEqual(Visibility.Visible, Convert(converter, false));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, "not a bool"));
		}

		[TestMethod]
		public void InverseBoolInvertsBools()
		{
			var converter = new InverseBoolConverter();
			Assert.AreEqual(false, Convert(converter, true));
			Assert.AreEqual(true, Convert(converter, false));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, null));
		}

		[TestMethod]
		public void HotkeysRegisteredDisablesEditing()
		{
			var converter = new HotkeysRegisteredToEnabledConverter();
			Assert.AreEqual(false, Convert(converter, true));
			Assert.AreEqual(true, Convert(converter, false));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, 1));
		}

		[TestMethod]
		public void WindowVisibilityMapsToHidden()
		{
			var converter = new WindowVisibilityConverter();
			Assert.AreEqual(Visibility.Visible, Convert(converter, true));
			Assert.AreEqual(Visibility.Hidden, Convert(converter, false));
			Assert.AreEqual(Visibility.Visible, Convert(converter, null));
		}

		[TestMethod]
		public void DeviceViewPositionMapsToDock()
		{
			var converter = new DeviceViewPositionToDockConverter();
			Assert.AreEqual(Dock.Bottom, Convert(converter, true));
			Assert.AreEqual(Dock.Top, Convert(converter, false));
			Assert.AreEqual(Dock.Top, Convert(converter, null));
		}

		[TestMethod]
		public void CollectionCountHidesAtLimit()
		{
			var converter = new CollectionCountToVisibilityConverter { CountLimit = 1 };
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, 1));
			Assert.AreEqual(Visibility.Visible, Convert(converter, 2));
			Assert.AreEqual(Visibility.Visible, Convert(converter, "x"));
		}

		[TestMethod]
		public void PropertyIsNullDisables()
		{
			var converter = new PropertyIsNullToEnabledConverter();
			Assert.AreEqual(false, Convert(converter, null));
			Assert.AreEqual(true, Convert(converter, new object()));
		}

		[TestMethod]
		public void AnyPropertyIsNullDisables()
		{
			var converter = new AnyPropertyIsNullToEnabledConverter();
			Assert.AreEqual(true, Convert((IMultiValueConverter)converter, 1, "a"));
			Assert.AreEqual(false, Convert((IMultiValueConverter)converter, 1, null));
			Assert.AreEqual(false, converter.Convert((object[])null, typeof(bool), null, Culture));
			Assert.AreEqual(false, Convert((IValueConverter)converter, null));
			Assert.AreEqual(true, Convert((IValueConverter)converter, 1));
		}

		[TestMethod]
		public void AnyPropertyIsTrue()
		{
			var converter = new AnyPropertyIsTrueConverter();
			Assert.AreEqual(true, Convert(converter, false, true));
			Assert.AreEqual(false, Convert(converter, false, null, "true"));
		}

		[TestMethod]
		public void MultipleVisibilitiesCollapseIfAnyCollapsed()
		{
			var converter = new MultipleVisibilityToSingleVisibilityConverter();
			Assert.AreEqual(Visibility.Visible, Convert(converter, Visibility.Visible, Visibility.Hidden));
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, Visibility.Visible, Visibility.Collapsed));
		}

		[TestMethod]
		public void MutedMapsToGlyph()
		{
			var converter = new MutedToGlyphConverter();
			Assert.AreEqual('', Convert(converter, true));
			Assert.AreEqual('', Convert(converter, false));
			Assert.AreEqual(string.Empty, Convert(converter, null));
		}

		[TestMethod]
		public void NoDeviceTogglesMasterAndLabel()
		{
			var converter = new NoDeviceToVisibilityConverter();
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, true, "Master"));
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, true, "Separator"));
			Assert.AreEqual(Visibility.Visible, Convert(converter, true, "Label"));
			Assert.AreEqual(Visibility.Visible, Convert(converter, false, "Master"));
			Assert.AreEqual(Visibility.Collapsed, Convert(converter, false, "Label"));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, true, null));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, true, "Other"));
		}

		[STATestMethod]
		public void BrushMapsToColor()
		{
			var converter = new BrushToColorConverter();
			Assert.AreEqual(Colors.Red, Convert(converter, new SolidColorBrush(Colors.Red)));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, null));
		}

		[TestMethod]
		public void DefaultDeviceGetsAnIconAndHidesProperties()
		{
			var device = OutputDeviceModelTests.GetDeviceMock("1", "One", new Mock<IDeviceStateNotificationHandler>().Object);
			var other = OutputDeviceModelTests.GetDeviceMock("2", "Two", new Mock<IDeviceStateNotificationHandler>().Object);

			var visibility = new DeviceIsDefaultToVisibilityPropertiesConverter();
			Assert.AreEqual(Visibility.Collapsed, visibility.Convert(new object[] { device, device }, typeof(Visibility), null, Culture));
			Assert.AreEqual(Visibility.Visible, visibility.Convert(new object[] { other, device }, typeof(Visibility), null, Culture));
			Assert.AreEqual(false, visibility.Convert(new object[] { device, device }, typeof(bool), null, Culture));
			Assert.AreEqual(true, visibility.Convert(new object[] { other, device }, typeof(bool), null, Culture));
			Assert.AreEqual(Binding.DoNothing, visibility.Convert(new object[] { other, null }, typeof(bool), null, Culture));
		}

		[STATestMethod]
		public void DefaultDeviceIconOnlyForTheDefault()
		{
			var device = OutputDeviceModelTests.GetDeviceMock("1", "One", new Mock<IDeviceStateNotificationHandler>().Object);
			var other = OutputDeviceModelTests.GetDeviceMock("2", "Two", new Mock<IDeviceStateNotificationHandler>().Object);

			var icon = new DeviceIsDefaultToIconConverter();
			Assert.IsNotNull(Convert(icon, device, device));
			Assert.IsNull(Convert(icon, other, device));
		}

		[TestMethod]
		public void LocalizationKeyReturnsTheTranslation()
		{
			var converter = new LocalizationKeyConverter();
			Assert.AreEqual(TranslationSource.Instance[TranslationSource.MutedCaptionKey], Convert(converter, TranslationSource.MutedCaptionKey));
			Assert.IsNull(Convert(converter, 5));
		}

		[TestMethod]
		public void TrayTooltipShowsVolumeOrMuteOrNoDevice()
		{
			var converter = new TrayTooltipConverter();
			string muted = TranslationSource.Instance[TranslationSource.MutedCaptionKey];
			string noDevice = TranslationSource.Instance[TranslationSource.NoDeviceCaptionKey];

			Assert.AreEqual("Speakers: 42%", Convert(converter, false, CreateMaster("Speakers", 42, false)));
			Assert.AreEqual($"Speakers: {muted}", Convert(converter, false, CreateMaster("Speakers", 42, true)));
			Assert.AreEqual(noDevice, Convert(converter, true, null));
			Assert.AreEqual(Binding.DoNothing, Convert(converter, false, null));
			Assert.AreEqual(Binding.DoNothing, converter.Convert(null, typeof(string), null, Culture));
		}

		[TestMethod]
		public void TrayIconShowsTheDeviceIconWhenEnabled()
		{
			var converter = new TrayIconConverter();
			using var icon = (Icon)SystemIcons.Application.Clone();

			Assert.AreSame(icon, Convert(converter, false, 50, false, null, true, icon));
			//the same icon again doesn't update the tray
			Assert.AreEqual(Binding.DoNothing, Convert(converter, false, 50, false, null, true, icon));
			//without a theme there is no volume icon to show
			Assert.AreEqual(Binding.DoNothing, Convert(converter, false, 50, false, null, false, icon));
		}
	}
}
