namespace Timberborn.EntitySystem;

public class EntityComponent {
  public bool Exists { get; set; } = true;

  public static implicit operator bool(EntityComponent entity) {
    return entity?.Exists == true;
  }
}

public class EntityCreatedEvent {
  public EntityComponent Entity { get; }

  public EntityCreatedEvent(EntityComponent entity) {
    Entity = entity;
  }
}
