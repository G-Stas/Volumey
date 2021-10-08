using System;
using System.Collections.Specialized;
using System.Windows;

namespace Volumey.View
{
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

			if(this.SessionsList.ItemsControl.Items.SourceCollection is INotifyCollectionChanged items)
			{
				items.CollectionChanged += OnItemsCollectionChanged;
				this.itemsCollection = items;
			}
			this.Unloaded += OnUnloaded;
		}

		private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			this.CollectionChanged?.Invoke(this, e);
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			this.Unloaded -= OnUnloaded;
			if(this.itemsCollection != null)
				this.itemsCollection.CollectionChanged -= OnItemsCollectionChanged;
			this.CollectionChanged = null;
		}
	}
}