using IgorZ.CustomTools.Core;
using Timberborn.EntitySystem;
using Timberborn.EntityUndoSystem;
using Timberborn.SingletonSystem;

namespace CustomTools.Tests;

static class CustomToolsUndoServiceTests {
  public static void RegistersOnPostLoad() {
    var eventBus = new EventBus();
    var service = new CustomToolsUndoService(eventBus, new UndoableEntityFactory());

    service.PostLoad();

    Assert.Same(service, eventBus.RegisteredObject);
  }

  public static void CommitsAndUndoesCaptures() {
    var factory = new UndoableEntityFactory();
    var service = new CustomToolsUndoService(new EventBus(), factory);
    var first = new EntityComponent();
    var second = new EntityComponent();

    service.BeginCapture();
    service.OnEntityCreated(new EntityCreatedEvent(first));
    service.OnEntityCreated(new EntityCreatedEvent(second));
    service.CommitCapture();

    Assert.True(service.CanUndo);
    service.Undo();

    Assert.False(service.CanUndo);
    Assert.Equal(1, factory.GetUndoable(first).InitializeUndoableStateCalls);
    Assert.Equal(1, factory.GetUndoable(first).DeleteCalls);
    Assert.Equal(1, factory.GetUndoable(second).InitializeUndoableStateCalls);
    Assert.Equal(1, factory.GetUndoable(second).DeleteCalls);
    Assert.True(factory.GetUndoable(second).DeletedBefore(factory.GetUndoable(first)));
  }

  public static void AbortsCaptures() {
    var factory = new UndoableEntityFactory();
    var service = new CustomToolsUndoService(new EventBus(), factory);
    var entity = new EntityComponent();

    service.BeginCapture();
    service.OnEntityCreated(new EntityCreatedEvent(entity));
    service.AbortCapture();

    Assert.False(service.CanUndo);
    service.Undo();
    Assert.Equal(0, factory.GetUndoable(entity).DeleteCalls);
  }

  public static void ClearsUndoActions() {
    var factory = new UndoableEntityFactory();
    var service = new CustomToolsUndoService(new EventBus(), factory);
    var entity = new EntityComponent();

    service.BeginCapture();
    service.OnEntityCreated(new EntityCreatedEvent(entity));
    service.CommitCapture();
    service.Clear();

    Assert.False(service.CanUndo);
    service.Undo();
    Assert.Equal(0, factory.GetUndoable(entity).DeleteCalls);
  }

  public static void IgnoresCreationOutsideCapture() {
    var factory = new UndoableEntityFactory();
    var service = new CustomToolsUndoService(new EventBus(), factory);
    var entity = new EntityComponent();

    service.OnEntityCreated(new EntityCreatedEvent(entity));

    Assert.False(service.CanUndo);
    Assert.Equal(0, factory.CreateUninitializedCalls);
  }

  public static void KeepsNestedCaptureAsOneAction() {
    var factory = new UndoableEntityFactory();
    var service = new CustomToolsUndoService(new EventBus(), factory);
    var first = new EntityComponent();
    var second = new EntityComponent();

    service.BeginCapture();
    service.OnEntityCreated(new EntityCreatedEvent(first));
    service.BeginCapture();
    service.OnEntityCreated(new EntityCreatedEvent(second));
    service.CommitCapture();

    Assert.False(service.CanUndo);
    service.CommitCapture();
    Assert.True(service.CanUndo);

    service.Undo();

    Assert.Equal(1, factory.GetUndoable(first).DeleteCalls);
    Assert.Equal(1, factory.GetUndoable(second).DeleteCalls);
  }
}
