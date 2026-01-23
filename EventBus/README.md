# Event Bus

A lightweight, allocation-free event bus with optional event pooling.  
Designed for **main-thread usage** with explicit lifetime control for edge cases.

---

## Features

- Type-safe event subscriptions
- Zero allocations on publish
- Optional event pool
- Explicit retain / release model

---

## Requirements

- **Unity 2021.3+** (recommended)

---

## Basic Event

### Define event

```csharp
public sealed class PlayerDiedEvent : EventBase {
    public int PlayerId;
}
```

### Subscribe

```csharp
eventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);

void OnPlayerDied(PlayerDiedEvent evt) {
    Debug.Log(evt.PlayerId);
}
```

### Publish

```csharp
var evt = EventBase.Create<PlayerDiedEvent>();
evt.PlayerId = 42;

eventBus.Publish(evt);
```

---

## Pooled Event

### Define pooled event

```csharp
public sealed class DamageEvent : PooledEvent {
    public int damage;

    protected override void OnReset() {
        damage = default;
    }
}
```

### Prewarm pool (optional)

```csharp
eventBus.Prewarm<DamageEvent>(32);
```

### Publish pooled event (no retain)

```csharp
var evt = eventBus.RentEvent<DamageEvent>();
evt.damage = 8;

eventBus.Publish(evt); 
// Automatically returned to pool
```

### Publish pooled event (with retain)
Use this pattern when the event must outlive the Publish() call but is still processed on the same thread, for example across frames or inside a coroutine.

#### Handler

```csharp
eventBus.Subscribe<DamageEvent>(OnDamage);

void OnDamage(DamageEvent evt) {
    // Extend event lifetime beyond Publish()
    evt.Retain();

    StartCoroutine(ProcessDamageCoroutine(evt));
}

IEnumerator ProcessDamageCoroutine(DamageEvent evt) {
    ...

    // Release when finished using the event
    evt.Release();
}
```

#### Publish

```csharp
var evt = eventBus.RentEvent<DamageEvent>();
evt.damage = 8;

eventBus.Publish(evt);
// Returned to pool only after all releases
```

---

## Event Lifetime Rules
- Events are fully initialized before Publish 
- Handlers must not mutate shared event data 
- Pooled events return to the pool when RefCount == 0 
- Retain() is optional - use only when needed