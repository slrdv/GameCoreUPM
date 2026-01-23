using System;
using System.Collections.Generic;

namespace GameCore.EventBus {

    public interface IEventBus {
        void Subscribe<T>(Action<T> handler) where T : IEvent;
        void Unsubscribe<T>(Action<T> handler) where T : IEvent;
        void Publish(IEvent evt);
        T RentEvent<T>() where T : PooledEvent, new();
        void Prewarm<T>(int count) where T : PooledEvent, new();
    }

    /// <summary>
    /// Default implementation of EventBus.
    /// 
    /// Contract:
    /// - Main-thread-only.
    /// - Not thread-safe.
    /// - Designed for high-frequency usage.
    /// 
    /// Subscription rules:
    /// - Handlers are invoked in the order they were subscribed.
    /// - Subscribing the same handler multiple times results in multiple invocations.
    /// - Unsubscribe removes only one matching subscription.
    /// 
    /// Disposal:
    /// - After Dispose, Subscribe throws.
    /// - Publish and Unsubscribe become no-ops.
    /// - Releasing pooled events after disposal is allowed and ignored.
    /// 
    /// Pooled events:
    /// - Publish automatically retains pooled events for the duration of dispatch.
    /// - Handlers that store the event beyond Publish MUST call Retain.
    /// </summary>
    public sealed class EventBus : IEventBus, IDisposable {

        private readonly Dictionary<int, IEventSlot> _slots = new();
        private readonly IEventPool _pool;

        private bool _disposed;

        public bool Disposed => _disposed;

        public EventBus() {
            _pool = new EventPool();
        }

        public void Subscribe<T>(Action<T> handler) where T : IEvent {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBus));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            int typeId = EventTypeId<T>.TypeId;
            if (!_slots.TryGetValue(typeId, out var slot)) {
                slot = new EventSlot<T>();
                _slots[typeId] = slot;
            }

            ((IEventSlot<T>)slot).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent {
            if (_disposed) return;
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (_slots.TryGetValue(EventTypeId<T>.TypeId, out var slot)) {
                ((IEventSlot<T>)slot).Remove(handler);
            }
        }

        public void Publish(IEvent evt) {
            if (_disposed || evt == null) return;

            int typeId = evt.TypeId;

#if DEBUG
            if (typeId == 0) {
                throw new InvalidOperationException("Invalid event: TypeId is not set.");
            }
#endif

            IPooledEvent pooledEvent = evt as IPooledEvent;
            pooledEvent?.Retain();

            if (_slots.TryGetValue(typeId, out var slot)) {
                slot.Invoke(evt);
            }

            pooledEvent?.Release();
        }

        public T RentEvent<T>() where T : PooledEvent, new() {
            return _pool.Rent<T>();
        }

        public void Prewarm<T>(int count) where T : PooledEvent, new() {
            _pool.Prewarm<T>(count);
        }

        public void Dispose() {
            if (_disposed) return;

            _disposed = true;
            _slots.Clear();
            _pool.Dispose();
        }
    }
}
