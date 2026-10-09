using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace Volumey.Helper;

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
		get => dictionary[key];
		set
		{
			dictionary[key] = value;
			NotifyChanged();
		}
	}

	public ICollection<TKey> Keys => dictionary.Keys;
	public ICollection<TValue> Values => dictionary.Values;
	public int Count => dictionary.Count;
	public bool IsReadOnly => false;

	public void Add(TKey key, TValue value)
	{
		if(dictionary.TryAdd(key, value))
			NotifyChanged();
	}

	public bool Remove(TKey key)
	{
		bool removed = dictionary.TryRemove(key, out _);
		if(removed)
			NotifyChanged();
		return removed;
	}

	public void Clear()
	{
		dictionary.Clear();
		NotifyChanged();
	}

	public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
	public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);

	void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		=> ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Contains(item);
	void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		=> ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).CopyTo(array, arrayIndex);
	bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
	{
		bool removed = ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(item);
		if(removed)
			NotifyChanged();
		return removed;
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private void NotifyChanged()
	{
		if(context == null || context == SynchronizationContext.Current)
			RaiseChanged();
		else
			context.Post(_ => RaiseChanged(), null);
	}

	private void RaiseChanged()
	{
		CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Keys)));
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Values)));
	}
}
