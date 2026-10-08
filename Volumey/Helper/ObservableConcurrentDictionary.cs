using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace Volumey.Helper
{
	/// <summary>
	/// Thread-safe dictionary that can be bound in WPF. Every change raises a collection reset and
	/// PropertyChanged for Count, Keys and Values on the synchronization context it was created on (normally the UI thread).
	/// Replaces the class of the same name from MSFT.ParallelExtensionsExtras (a .NET Framework-only package).
	/// </summary>
	public sealed class ObservableConcurrentDictionary<TKey, TValue> :
		IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		private readonly ConcurrentDictionary<TKey, TValue> dictionary = new ConcurrentDictionary<TKey, TValue>();
		private readonly SynchronizationContext context = SynchronizationContext.Current;

		public event NotifyCollectionChangedEventHandler CollectionChanged;
		public event PropertyChangedEventHandler PropertyChanged;

		public TValue this[TKey key]
		{
			get => this.dictionary[key];
			set
			{
				this.dictionary[key] = value;
				this.NotifyChanged();
			}
		}

		public ICollection<TKey> Keys => this.dictionary.Keys;
		public ICollection<TValue> Values => this.dictionary.Values;
		public int Count => this.dictionary.Count;
		public bool IsReadOnly => false;

		public void Add(TKey key, TValue value)
		{
			if(this.dictionary.TryAdd(key, value))
				this.NotifyChanged();
		}

		public bool Remove(TKey key)
		{
			bool removed = this.dictionary.TryRemove(key, out _);
			if(removed)
				this.NotifyChanged();
			return removed;
		}

		public void Clear()
		{
			this.dictionary.Clear();
			this.NotifyChanged();
		}

		public bool ContainsKey(TKey key) => this.dictionary.ContainsKey(key);
		public bool TryGetValue(TKey key, out TValue value) => this.dictionary.TryGetValue(key, out value);

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => this.Add(item.Key, item.Value);
		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
			=> ((ICollection<KeyValuePair<TKey, TValue>>) this.dictionary).Contains(item);
		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
			=> ((ICollection<KeyValuePair<TKey, TValue>>) this.dictionary).CopyTo(array, arrayIndex);
		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			bool removed = ((ICollection<KeyValuePair<TKey, TValue>>) this.dictionary).Remove(item);
			if(removed)
				this.NotifyChanged();
			return removed;
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => this.dictionary.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		private void NotifyChanged()
		{
			if(this.context == null || this.context == SynchronizationContext.Current)
				this.RaiseChanged();
			else
				this.context.Post(_ => this.RaiseChanged(), null);
		}

		private void RaiseChanged()
		{
			this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Count)));
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Keys)));
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Values)));
		}
	}
}
