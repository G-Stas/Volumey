#nullable enable
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using log4net;
using Volumey.Helper;
using Volumey.Localization;
using Volumey.Services;
using Volumey.ViewModel;
using Wpf.Ui.Controls;

namespace Volumey.View.DialogContent;

/// <summary>
/// Store build: the donation add-ons as buttons (the Store's own purchase dialog opens over the window when one is clicked), and a thank-you after a purchase
/// </summary>
public sealed partial class SupportDialog
{
	private static readonly ILog Logger = LogManager.GetLogger(typeof(SupportDialog));

	private readonly SupportViewModel vm;
	private bool isBuying;

	internal SupportDialog(SupportViewModel vm)
	{
		this.vm = vm;
		DataContext = vm;
		//WPF-UI's dialog style is implicit, which doesn't reach a subclass (without it: no dialog chrome, no buttons)
		SetResourceReference(StyleProperty, typeof(ContentDialog));
		InitializeComponent();
		//the dialog scrolls to the button it focuses when it opens, which hides the heart and the title in a short window
		Loaded += (_, _) => Dispatcher.BeginInvoke(ScrollToTop, DispatcherPriority.ContextIdle);
	}

	private void ScrollToTop()
	{
		DependencyObject? element = Offer;
		while(element != null && element is not ScrollViewer)
			element = VisualTreeHelper.GetParent(element);
		(element as ScrollViewer)?.ScrollToHome();
	}

	private void OnTierClick(object sender, RoutedEventArgs e)
	{
		if(sender is FrameworkElement { DataContext: SupportTier tier } && !isBuying)
			BuyAsync(tier).Forget(Logger, "Failed to process a support purchase");
	}

	//runs on the UI thread: the awaits resume there
	private async System.Threading.Tasks.Task BuyAsync(SupportTier tier)
	{
		isBuying = true;
		TierList.IsEnabled = false;
		//the dialog stays while the Store's purchase window is open
		SetCloseButtonEnabled(false);
		try
		{
			var result = await vm.PurchaseAsync(tier);
			Status.Visibility = result == SupportPurchaseResult.Failed ? Visibility.Visible : Visibility.Collapsed;
			if(result == SupportPurchaseResult.Succeeded)
			{
				//the dialog turns into a thank-you
				Title = null;
				Offer.Visibility = Visibility.Collapsed;
				Thanks.Visibility = Visibility.Visible;
				CloseButtonText = TranslationSource.Instance["Support_Close"] ?? string.Empty;
			}
		}
		finally
		{
			TierList.IsEnabled = true;
			SetCloseButtonEnabled(true);
			isBuying = false;
		}
	}

	private void SetCloseButtonEnabled(bool enabled)
	{
		if(GetTemplateChild("CloseButton") is UIElement close)
			close.IsEnabled = enabled;
	}

	//the dialog doesn't close while a purchase is in progress
	protected override void OnButtonClick(ContentDialogButton button)
	{
		if(!isBuying)
			base.OnButtonClick(button);
	}
}
