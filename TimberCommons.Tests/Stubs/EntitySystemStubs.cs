using Timberborn.BaseComponentSystem;

namespace Timberborn.EntitySystem;

public sealed class EntityInitializedEvent {
  public BaseComponent Entity { get; }

  public EntityInitializedEvent(BaseComponent entity) {
    Entity = entity;
  }
}
