using System;
using System.Collections.Generic;

namespace GameCore.EventBus {

    internal interface IEventPool : IDisposable {
        T Rent<T>() where T : PooledEvent, new();
        void Release(PooledEvent pooledEvent);
        void Prewarm<T>(int count) where T : PooledEvent, new();
        bool Disposed { get; }
    }

    internal sealed class EventPool : IEventPool {

        private const int INITIAL_CAPACITY = 32;
        private const int MAX_POOL_SIZE = 128;

        private readonly Dictionary<int, Stack<PooledEvent>> _pools = new();

        private bool _disposed;

        public bool Disposed => _disposed;

        public T Rent<T>() where T : PooledEvent, new() {
            if (_disposed) throw new ObjectDisposedException(nameof(EventPool));

            if (_pools.TryGetValue(EventTypeId<T>.TypeId, out var pool) && pool.Count > 0) {
                T evt = (T)pool.Pop();
                evt.Reset();
                return evt;
            }

            return PooledEvent.Create<T>(this);
        }

        public void Release(PooledEvent evt) {
            if (_disposed) return;

            if (!_pools.TryGetValue(evt.TypeId, out var pool)) {
                pool = new Stack<PooledEvent>(INITIAL_CAPACITY);
                _pools[evt.TypeId] = pool;
            }

            if (pool.Count >= MAX_POOL_SIZE) return;

            pool.Push(evt);
        }

        public void Prewarm<T>(int count) where T : PooledEvent, new() {
            if (_disposed) throw new ObjectDisposedException(nameof(EventPool));

            int eventType = EventTypeId<T>.TypeId;
            if (!_pools.TryGetValue(eventType, out var pool)) {
                pool = new Stack<PooledEvent>(Math.Min(count, MAX_POOL_SIZE));
                _pools[eventType] = pool;
            }

            for (int i = 0; i < Math.Min(count, MAX_POOL_SIZE - pool.Count); i++) {
                pool.Push(PooledEvent.Create<T>(this));
            }
        }

        public void Dispose() {
            if (_disposed) return;

            _disposed = true;
            _pools.Clear();
        }
    }
}
