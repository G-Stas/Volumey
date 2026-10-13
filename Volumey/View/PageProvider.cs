using System;
using Wpf.Ui.Abstractions;

namespace Volumey.View;

/// <summary>
/// The settings pages for WPF-UI's navigation, from the container (each page exists once and keeps its view model)
/// </summary>
internal sealed class PageProvider : INavigationViewPageProvider
{
	private readonly IServiceProvider services;

	internal PageProvider(IServiceProvider services)
	{
		this.services = services;
	}

	public object GetPage(Type pageType) => services.GetService(pageType);
}
