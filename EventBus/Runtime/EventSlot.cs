using System;
using System.Collections.Generic;

namespace GameCore.EventBus {
    internal interface IEventSlot {
        void Invoke(IEvent evt);
        bool IsEmpty();
        void Clear();
    }

    internal interface IEventSlot<T> : IEventSlot where T : IEvent {
        void Add(Action<T> handler);
        void Remove(Action<T> handler);
    }

    internal sealed class EventSlot<T> : IEventSlot<T> where T : IEvent {

        List<HandlerEntry> _entries = new();
        List<HandlerEntry> _pendingRemovals = new();
        bool _isInvoking;

        public void Add(Action<T> handler) {
            _entries.Add(new HandlerEntry { Handler = handler });
        }

        public void Remove(Action<T> handler) {
            for (int i = 0; i < _entries.Count; ++i) {
                if (_entries[i].Handler == handler) {
                    if (_isInvoking) {
                        _entries[i].Active = false;
                        _pendingRemovals.Add(_entries[i]);
                    } else {
                        _entries.RemoveAt(i);
                    }

                    return;
                }
            }
        }

        public void Invoke(IEvent evt) {
            _isInvoking = true;

            T tevt = (T)evt;
            for (int i = 0; i < _entries.Count; ++i) {
                if (!_entries[i].Active) continue;

                _entries[i].Handler(tevt);
            }

            ClearPendingRemovals();
            _isInvoking = false;
        }

        public bool IsEmpty() {
            return _entries.Count == 0;
        }

        public void Clear() {
            _entries.Clear();
            _pendingRemovals.Clear();
        }

        private void ClearPendingRemovals() {
            for (int i = 0; i < _pendingRemovals.Count; ++i) {
                _entries.Remove(_pendingRemovals[i]);
            }
            _pendingRemovals.Clear();
        }


        private sealed class HandlerEntry {
            public Action<T> Handler;
            public bool Active = true;
        }
    }
}
