using System.Collections.Generic;
using System.Collections.Immutable;

namespace DeathrunManager.Extensions;

public class StorageExtension
{
    public sealed class ImmutableStorage<T>
    {
        private ImmutableList<T> _items = ImmutableList<T>.Empty;

        public void Add(T item)
        {
            ImmutableInterlocked.Update(ref _items, list => list.Add(item));
        }

        public void Remove(T item)
        {
            ImmutableInterlocked.Update(ref _items, list => list.Remove(item));
        }

        public IReadOnlyCollection<T> Items => _items;

        public IEnumerable<T> Enumerate() => _items;
    }
    
}