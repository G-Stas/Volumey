using System.Windows.Data;

namespace Volumey.Localization;

public sealed class LocalizationExtension : Binding
{
	public LocalizationExtension(string name) : base($"[{name}]")
	{
		Mode = BindingMode.OneWay;
		Source = TranslationSource.Instance;
	}
}
