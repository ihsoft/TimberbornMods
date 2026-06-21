namespace Timberborn.WorldPersistence;

public interface IPersistentEntity {
  void Save(Timberborn.Persistence.IEntitySaver entitySaver);
  void Load(Timberborn.Persistence.IEntityLoader entityLoader);
}
