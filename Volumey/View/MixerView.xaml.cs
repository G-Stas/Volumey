using System;
using System.Collections.Specialized;
using System.Windows;

namespace Volumey.View;

public partial class MixerView
{
	/// <summary>
	/// Occurs when currently displayed sessions count changes.
	/// </summary>
	public event Action<object, NotifyCollectionChangedEventArgs> CollectionChanged;
	private readonly INotifyCollectionChanged itemsCollection;
	public MixerView()
	{
		InitializeComponent();

		if(SessionsList.ItemsControl.Items.SourceCollection is INotifyCollectionChanged items)
		{
			items.CollectionChanged += OnItemsCollectionChanged;
			itemsCollection = items;
		}
		Unloaded += OnUnloaded;
	}

	private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		CollectionChanged?.Invoke(this, e);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		Unloaded -= OnUnloaded;
		if(itemsCollection != null)
			itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
		CollectionChanged = null;
	}
}
