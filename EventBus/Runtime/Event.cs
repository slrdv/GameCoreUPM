using System;

namespace GameCore.EventBus {

    public interface IEvent {
        int TypeId { get; }
    }

    public interface IPooledEvent : IEvent {
        int RefCount { get; }
        void Retain();
        void Release();
    }


    internal static class EventTypeIdGenerator {
        private static int _nextId = 0;

        public static int GetId() {
            return ++_nextId;
        }
    }


    internal static class EventTypeId<T> {
        public static readonly int TypeId = EventTypeIdGenerator.GetId();
    }

    /// <summary>
    /// Base class for non-pooled events.
    /// 
    /// Contract:
    /// - Events are immutable by convention during Publish.
    /// - EventBase instances may be freely allocated.
    /// - Each concrete event type has a unique TypeId for the current session.
    /// - Each event type has a unique TypeId assigned at runtime.
    /// - TypeId is stable only for the current application session.
    /// - TypeId must not be persisted or relied upon across runs.
    /// </summary>
    public abstract class EventBase : IEvent {

        private int _typeId;

        public int TypeId => _typeId;

        public static T Create<T>() where T : EventBase, new() {
            T evt = new T();
            evt.SetTypeId(EventTypeId<T>.TypeId);
            return evt;
        }

        internal void SetTypeId(int typeId) {
            _typeId = typeId;
        }

        internal protected EventBase() { }
    }

    /// <summary>
    /// Base class for pooled events.
    /// PooledEvent managed by an internal object pool.
    /// 
    /// Contract:
    /// - Instances are managed by the EventBus pool.
    /// - Do NOT instantiate pooled events directly.
    /// - Use EventBus.RentEvent to obtain an instance.
    /// - The event is returned to the pool when its reference count reaches zero.
    /// 
    /// Lifetime rules:
    /// - RefCount is managed manually via Retain/Release.
    /// - EventBus automatically retains the event during Publish.
    /// - Handlers that keep the event MUST call Retain.
    /// - Each Retain MUST be matched with exactly one Release.
    /// - Double Release or Release without Retain is a usage error.
    /// 
    /// Reset:
    /// - OnReset is called before the event is reused.
    /// - Derived events should override Reset to clear all event-specific state.
    /// </summary>
    public abstract class PooledEvent : EventBase, IPooledEvent {

        private int _refCount;
        private IEventPool _pool;

        internal static T Create<T>(IEventPool pool) where T : PooledEvent, new() {
            T evt = new T();
            evt.SetTypeId(EventTypeId<T>.TypeId);
            evt._pool = pool;

            return evt;
        }

        internal protected PooledEvent() { }

        public int RefCount => _refCount;

        public void Retain() {
            ++_refCount;
        }

        public void Release() {
            if (_pool == null) return;

#if DEBUG
            if (_refCount == 0) {
                throw new InvalidOperationException($"{GetType().Name} has already been released");
            }
#endif

            if (--_refCount == 0) {
                _pool.Release(this);
            }
        }

        internal void Reset() {
            ResetRefCount();
            OnReset();
        }

        protected virtual void OnReset() { }

        private void ResetRefCount() {
            _refCount = 0;
        }
    }
}