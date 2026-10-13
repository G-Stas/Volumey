using System;
using Microsoft.Extensions.DependencyInjection;
using Volumey.ViewModel.Settings;

namespace Volumey.Services;

/// <summary>
/// The container's services for controls that XAML creates (the container can't pass them their services).
/// Set at startup; use on the UI thread.
/// </summary>
internal sealed class AppServices
{
	internal static AppServices Current { get; private set; }

	private readonly IServiceProvider provider;

	private AppServices(IServiceProvider provider)
	{
		this.provider = provider;
	}

	internal static void Use(IServiceProvider provider) => Current = new AppServices(provider);

	public IHotkeyService Hotkeys => provider.GetRequiredService<IHotkeyService>();
}
