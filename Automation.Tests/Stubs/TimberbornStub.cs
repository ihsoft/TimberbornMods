using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.BaseComponentSystem {
  public class BaseComponent {
    readonly Dictionary<System.Type, object> _components = new();

    public string Name { get; set; }
    public List<object> AllComponents { get; } = [];
    public MonoBehaviour _componentCache = new();

    public void SetComponent<T>(T component) where T : class {
      _components[typeof(T)] = component;
      if (!AllComponents.Contains(component)) {
        AllComponents.Add(component);
      }
    }

    public T GetComponent<T>() where T : class {
      return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    public T GetComponentInChildren<T>() where T : class {
      return GetComponent<T>();
    }

    public static bool operator !(BaseComponent component) {
      return component == null;
    }

    public static implicit operator bool(BaseComponent component) {
      return component != null;
    }

    public static bool operator true(BaseComponent component) {
      return component != null;
    }

    public static bool operator false(BaseComponent component) {
      return component == null;
    }
  }

  public interface IAwakableComponent {
    void Awake();
  }
}

namespace Timberborn.AutomationBuildings {
  public sealed class Lever : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsOn { get; private set; }

    public void SwitchOn() {
      IsOn = true;
    }

    public void SwitchOff() {
      IsOn = false;
    }
  }
}

namespace Timberborn.Automation {
  public enum AutomatorState {
    Off,
    On,
  }

  public sealed class Automator : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsTransmitter { get; init; }
    public AutomatorState State { get; set; }
    public string AutomatorName { get; set; }
  }
}

namespace Timberborn.Bots {
  public sealed class BotSpec : Timberborn.BaseComponentSystem.BaseComponent {
  }
}

namespace Timberborn.BlockSystem {
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;

  public sealed class BlockObject : BaseComponent {
    public bool IsFinished { get; set; }
    public UnityEngine.Vector3Int Coordinates { get; init; }

    public void GetComponents<T>(List<T> components) where T : class {
      foreach (var component in AllComponents) {
        if (component is T typedComponent) {
          components.Add(typedComponent);
        }
      }
    }
  }

  public sealed class BlockObjectSpec : BaseComponent {
    public Timberborn.BlockObjectTools.Blueprint Blueprint { get; init; } = new();
  }

  public sealed class BlockService {
    readonly Dictionary<UnityEngine.Vector3Int, BlockObject> _bottomObjects = [];
    readonly Dictionary<UnityEngine.Vector3Int, List<BlockObject>> _objects = [];

    public void SetBottomObjectAt(UnityEngine.Vector3Int coordinates, BlockObject blockObject) {
      _bottomObjects[coordinates] = blockObject;
    }

    public BlockObject GetBottomObjectAt(UnityEngine.Vector3Int coordinates) {
      return _bottomObjects.TryGetValue(coordinates, out var blockObject) ? blockObject : null;
    }

    public void SetObjectsAt(UnityEngine.Vector3Int coordinates, params BlockObject[] blockObjects) {
      _objects[coordinates] = [..blockObjects];
    }

    public List<BlockObject> GetObjectsAt(UnityEngine.Vector3Int coordinates) {
      return _objects.TryGetValue(coordinates, out var blockObjects) ? blockObjects : [];
    }
  }

  public interface IBlockOccupancyService {
    bool OccupantPresentOnArea(BlockObject blockObject, float maxDistance);
  }

  public interface IFinishedStateListener {
    void OnEnterFinishedState();
    void OnExitFinishedState();
  }
}

namespace Timberborn.BlockObjectTools {
  using System.Collections.Generic;
  using Timberborn.BlockSystem;

  public sealed class BlockObjectTool {
    public Template Template { get; init; } = new();
    public bool _placedAnythingThisFrame = true;

    public void Place(List<Placement> placements) {
    }
  }

  public sealed class Placement {
    public UnityEngine.Vector3Int Coordinates { get; }

    public Placement(UnityEngine.Vector3Int coordinates) {
      Coordinates = coordinates;
    }
  }

  public sealed class Template {
    public Blueprint Blueprint { get; init; } = new();
  }

  public sealed class Blueprint {
    public string Name { get; init; }
  }
}

namespace Timberborn.BuilderPrioritySystem {
  using System;
  using Timberborn.BaseComponentSystem;
  using Timberborn.PrioritySystem;

  public sealed class PriorityChangedEventArgs : EventArgs {
  }

  public sealed class BuilderPrioritizable : BaseComponent {
    public event EventHandler<PriorityChangedEventArgs> PriorityChanged;
    public Priority Priority { get; private set; }

    public void SetPriority(Priority priority) {
      Priority = priority;
      PriorityChanged?.Invoke(this, new PriorityChangedEventArgs());
    }
  }
}

namespace Timberborn.Buildings {
  public sealed class PausableBuilding : Timberborn.BaseComponentSystem.BaseComponent {
    public bool Pausable { get; init; } = true;
    public bool Paused { get; private set; }

    public bool IsPausable() {
      return Pausable;
    }

    public void Pause() {
      Paused = true;
    }

    public void Resume() {
      Paused = false;
    }
  }
}

namespace Timberborn.BuildingsNavigation {
  using System;
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;

  public sealed class RangeChangedEventArgs : EventArgs {
  }

  public sealed class BuildingTerrainRange : BaseComponent {
    readonly List<UnityEngine.Vector3Int> _range = [];

    public event EventHandler<RangeChangedEventArgs> RangeChanged;

    public void SetRange(params UnityEngine.Vector3Int[] coordinates) {
      _range.Clear();
      _range.AddRange(coordinates);
    }

    public IEnumerable<UnityEngine.Vector3Int> GetRange() {
      return _range;
    }

    public void RaiseRangeChanged() {
      RangeChanged?.Invoke(this, new RangeChangedEventArgs());
    }
  }
}

namespace Timberborn.Cutting {
  using System;
  using Timberborn.BaseComponentSystem;

  public sealed class Cuttable : BaseComponent {
    public event EventHandler WasCut;

    public void Cut() {
      WasCut?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.ConstructionSites {
  public static class ConstructionSiteInventoryInitializer {
    public const string InventoryComponentName = "ConstructionSite";
  }

  public sealed class ConstructionSite : Timberborn.BaseComponentSystem.BaseComponent {
    public event System.EventHandler OnConstructionSiteProgressed;
    public float BuildTimeProgress { get; init; }

    public void Progress() {
      OnConstructionSiteProgressed?.Invoke(this, System.EventArgs.Empty);
    }
  }
}

namespace Timberborn.DuplicationSystem {
  public interface IDuplicable {
    bool IsDuplicable { get; }
  }

  public interface IDuplicable<T> : IDuplicable {
    void DuplicateFrom(T source);
  }
}

namespace Timberborn.DwellingSystem {
  using Timberborn.BaseComponentSystem;

  public readonly struct DwellingStatistics {
    public int FreeBeds { get; init; }
    public int OccupiedBeds { get; init; }
  }

  public sealed class DistrictDwellingStatisticsProvider : BaseComponent {
    public DwellingStatistics Statistics { get; set; }

    public DwellingStatistics GetDwellingStatistics() {
      return Statistics;
    }
  }
}

namespace Timberborn.EntitySystem {
  public sealed class EntityComponent : Timberborn.BaseComponentSystem.BaseComponent {
    public object EntityId { get; set; } = "entity";
  }

  public sealed class Citizen : Timberborn.BaseComponentSystem.BaseComponent {
  }

  public interface IInitializableEntity {
    void InitializeEntity();
  }

  public interface IDeletableEntity {
    void DeleteEntity();
  }

  public sealed class EntityInitializedEvent {
    public Timberborn.BaseComponentSystem.BaseComponent Entity { get; }

    public EntityInitializedEvent(Timberborn.BaseComponentSystem.BaseComponent entity) {
      Entity = entity;
    }
  }

  public sealed class EntityDeletedEvent {
    public Timberborn.BaseComponentSystem.BaseComponent Entity { get; }

    public EntityDeletedEvent(Timberborn.BaseComponentSystem.BaseComponent entity) {
      Entity = entity;
    }
  }
}

namespace Timberborn.Emptying {
  using System;
  using Timberborn.BaseComponentSystem;

  public sealed class Emptiable : BaseComponent {
    public event EventHandler MarkedForEmptying;
    public event EventHandler UnmarkedForEmptying;
    public bool IsMarkedForEmptying { get; private set; }
    public int MarkForEmptyingCalls { get; private set; }
    public int UnmarkForEmptyingCalls { get; private set; }

    public void MarkForEmptyingWithoutStatus() {
      IsMarkedForEmptying = true;
      MarkForEmptyingCalls++;
      MarkedForEmptying?.Invoke(this, EventArgs.Empty);
    }

    public void UnmarkForEmptying() {
      IsMarkedForEmptying = false;
      UnmarkForEmptyingCalls++;
      UnmarkedForEmptying?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.Goods {
  public readonly struct GoodAmount {
    public string GoodId { get; }
    public int Amount { get; }

    public GoodAmount(string goodId, int amount) {
      GoodId = goodId;
      Amount = amount;
    }
  }

  public sealed class GoodSpec {
    public string Id { get; init; }
    public LocalizedText PluralDisplayName { get; init; }
  }

  public interface IGoodService {
    GoodSpec GetGood(string id);
    GoodSpec GetGoodOrNull(string id);
  }

  public sealed class LocalizedText {
    public string Value { get; }

    public LocalizedText(string value) {
      Value = value;
    }
  }

  public readonly struct StorableGood {
    public string GoodId { get; }

    public StorableGood(string goodId) {
      GoodId = goodId;
    }
  }

  public readonly struct StorableGoodAmount {
    public StorableGood StorableGood { get; }
    public int Amount { get; }

    public StorableGoodAmount(StorableGood storableGood, int amount) {
      StorableGood = storableGood;
      Amount = amount;
    }
  }
}

namespace Timberborn.GoodStackSystem {
  using System;
  using Timberborn.BaseComponentSystem;

  public sealed class GoodStack : BaseComponent {
    public event EventHandler GoodStackDisabled;
    public StackInventory Inventory { get; } = new();

    public void DisableStack() {
      GoodStackDisabled?.Invoke(this, EventArgs.Empty);
    }
  }

  public sealed class StackInventory {
    public bool Enabled { get; set; } = true;
    public bool IsEmpty { get; set; }
  }
}

namespace Timberborn.Growing {
  using System;
  using Timberborn.BaseComponentSystem;

  public sealed class Growable : BaseComponent {
    public event EventHandler HasGrown;

    public void Grow() {
      HasGrown?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.GameDistricts {
  using System;
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;
  using Timberborn.EntitySystem;

  public sealed class DistrictBuilding : BaseComponent {
    public event EventHandler ReassignedDistrict;
    public event EventHandler ReassignedConstructionDistrict;
    public DistrictCenter District { get; private set; }
    public DistrictCenter InstantDistrict { get; private set; }
    public DistrictCenter ConstructionDistrict { get; private set; }

    public void SetDistrict(DistrictCenter district) {
      District = district;
      ReassignedDistrict?.Invoke(this, EventArgs.Empty);
    }

    public void SetInstantDistrict(DistrictCenter district) {
      InstantDistrict = district;
    }

    public void SetConstructionDistrict(DistrictCenter district) {
      ConstructionDistrict = district;
      ReassignedConstructionDistrict?.Invoke(this, EventArgs.Empty);
    }
  }

  public sealed class DistrictCenter : BaseComponent {
    public DistrictPopulation DistrictPopulation { get; } = new();
    public DistrictBuildingRegistry DistrictBuildingRegistry { get; } = new();
  }

  public sealed class DistrictPopulation {
    public event EventHandler<CitizenAssignedEventArgs> CitizenAssigned;
    public event EventHandler<CitizenUnassignedEventArgs> CitizenUnassigned;
    public List<Citizen> Beavers { get; } = [];
    public List<Citizen> Bots { get; } = [];

    public void AssignBeaver(Citizen citizen) {
      Beavers.Add(citizen);
      CitizenAssigned?.Invoke(this, new CitizenAssignedEventArgs(citizen));
    }

    public void AssignBot(Citizen citizen) {
      Bots.Add(citizen);
      CitizenAssigned?.Invoke(this, new CitizenAssignedEventArgs(citizen));
    }

    public void Unassign(Citizen citizen) {
      Beavers.Remove(citizen);
      Bots.Remove(citizen);
      CitizenUnassigned?.Invoke(this, new CitizenUnassignedEventArgs(citizen));
    }
  }

  public sealed class CitizenAssignedEventArgs : EventArgs {
    public Citizen Citizen { get; }

    public CitizenAssignedEventArgs(Citizen citizen) {
      Citizen = citizen;
    }
  }

  public sealed class CitizenUnassignedEventArgs : EventArgs {
    public Citizen Citizen { get; }

    public CitizenUnassignedEventArgs(Citizen citizen) {
      Citizen = citizen;
    }
  }

  public sealed class DistrictBuildingRegistry {
    public event EventHandler<FinishedBuildingRegisteredEventArgs> FinishedBuildingRegistered;
    public event EventHandler<FinishedBuildingUnregisteredEventArgs> FinishedBuildingUnregistered;

    public void RegisterFinishedBuilding() {
      FinishedBuildingRegistered?.Invoke(this, new FinishedBuildingRegisteredEventArgs());
    }

    public void UnregisterFinishedBuilding() {
      FinishedBuildingUnregistered?.Invoke(this, new FinishedBuildingUnregisteredEventArgs());
    }
  }

  public sealed class FinishedBuildingRegisteredEventArgs : EventArgs {
  }

  public sealed class FinishedBuildingUnregisteredEventArgs : EventArgs {
  }
}

namespace Timberborn.Hauling {
  public sealed class HaulPrioritizable : Timberborn.BaseComponentSystem.BaseComponent {
    public bool Prioritized { get; set; }
  }
}

namespace Timberborn.HazardousWeatherSystem {
  public sealed class HazardousWeather {
    public string Id { get; init; }
  }

  public sealed class HazardousWeatherService {
    public HazardousWeather CurrentCycleHazardousWeather { get; init; }
  }

  public sealed class HazardousWeatherStartedEvent {
  }

  public sealed class HazardousWeatherEndedEvent {
  }
}

namespace Timberborn.Localization {
  public interface ILoc {
    string T(string key, params object[] args);
  }
}

namespace Timberborn.PrioritySystem {
  public enum Priority {
    VeryLow,
    Low,
    Normal,
    High,
    VeryHigh,
  }
}

namespace Timberborn.PowerManagement {
  public enum ClutchMode {
    Engaged,
    Disengaged,
    Automated,
  }

  public sealed class Clutch : Timberborn.BaseComponentSystem.BaseComponent {
    public ClutchMode Mode { get; private set; }

    public void SetMode(ClutchMode value) {
      Mode = value;
    }
  }
}

namespace Timberborn.SingletonSystem {
  public interface ILoadableSingleton {
    void Load();
  }

  public interface IPostLoadableSingleton {
  }

  public sealed class EventBus {
    public readonly List<object> RegisteredObjects = [];

    public void Register(object obj) {
      RegisteredObjects.Add(obj);
    }

    public void Unregister(object obj) {
      RegisteredObjects.Remove(obj);
    }
  }

  [System.AttributeUsage(System.AttributeTargets.Method)]
  public sealed class OnEventAttribute : System.Attribute {
  }
}

namespace Timberborn.GameCycleSystem {
  public sealed class CycleDayStartedEvent {
  }
}

namespace Timberborn.TimeSystem {
  using System;

  public interface IDayNightCycle {
    int DayNumber { get; }
    float HoursPassedToday { get; }
  }

  public interface ITimeTrigger {
    bool InProgress { get; }
    void Resume();
    void Pause();
  }

  public interface ITimeTriggerFactory {
    ITimeTrigger Create(Action action, float delayInDays);
  }
}

namespace Timberborn.WorkSystem {
  public sealed class WorkingHoursManager {
    public bool AreWorkingHours { get; set; }
  }

  public sealed class WorkingHoursTransitionedEvent {
  }

  public sealed class WorkingHoursChangedEvent {
  }
}

namespace Timberborn.ScienceSystem {
  public sealed class ScienceService {
    public int SciencePoints { get; private set; }

    public void AddPoints(int amount) {
      SciencePoints += amount;
    }

    public void SubtractPoints(int amount) {
      SciencePoints -= amount;
    }
  }
}

namespace Timberborn.Common {
  public readonly struct ReadOnlyHashSet<T> {
    public readonly HashSet<T> _set;
    public int Count => _set.Count;

    public ReadOnlyHashSet(HashSet<T> set) {
      _set = set;
    }

    public HashSet<T>.Enumerator GetEnumerator() {
      return _set.GetEnumerator();
    }

    public bool Contains(T item) {
      return _set.Contains(item);
    }
  }

  public static class DictionaryExtensions {
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) {
      return dictionary.TryGetValue(key, out var value) ? value : default;
    }

    public static TValue GetOrAdd<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary, TKey key, System.Func<TValue> factory) {
      if (!dictionary.TryGetValue(key, out var value)) {
        value = factory();
        dictionary.Add(key, value);
      }
      return value;
    }
  }

  public static class CollectionExtensions {
    public static void AddRange<T>(this ISet<T> set, IEnumerable<T> values) {
      foreach (var value in values) {
        set.Add(value);
      }
    }
  }
}

namespace Timberborn.Conditions {
}

namespace Timberborn.Coordinates {
  public static class Vector3IntExtensions {
    public static string XY(this UnityEngine.Vector3Int coordinates) {
      return $"{coordinates.x},{coordinates.y}";
    }
  }
}

namespace Timberborn.Explosions {
  using Timberborn.BaseComponentSystem;

  public sealed class Dynamite : BaseComponent {
    public int Depth { get; init; } = 1;
    public int TriggerCalls { get; private set; }

    public void Trigger() {
      TriggerCalls++;
    }
  }
}

namespace Timberborn.MapIndexSystem {
  public sealed class MapIndexService {
    public int CoordinatesToIndex3D(UnityEngine.Vector3Int coordinates) {
      return 0;
    }
  }
}

namespace Timberborn.Multithreading {
  using System;

  public readonly struct ParallelizerHandle {
  }

  public interface IParallelizerSingleTask {
    void Run();
  }

  public interface IParallelizerLoopTask {
    void Run(int index);
  }

  public interface IParallelizer {
    long LastTaskTimestamp { get; }
    int NumberOfThreads { get; }

    ParallelizerHandle Schedule<T>(in T task) where T : struct, IParallelizerSingleTask;
    ParallelizerHandle Schedule<T>(in T task, ParallelizerHandle dependency)
        where T : struct, IParallelizerSingleTask;
    ParallelizerHandle Schedule<T>(in T task, ReadOnlySpan<ParallelizerHandle> dependencies)
        where T : struct, IParallelizerSingleTask;
    ParallelizerHandle Schedule<T>(int fromInclusive, int toExclusive, int batchSize, in T task)
        where T : struct, IParallelizerLoopTask;
    ParallelizerHandle Schedule<T>(
        int fromInclusive, int toExclusive, int batchSize, in T task, ParallelizerHandle dependency)
        where T : struct, IParallelizerLoopTask;
    ParallelizerHandle Schedule<T>(
        int fromInclusive, int toExclusive, int batchSize, in T task, ReadOnlySpan<ParallelizerHandle> dependencies)
        where T : struct, IParallelizerLoopTask;

    void StartScheduling();
    void StopScheduling();
    void Wait();
    void ThrowIfAnyPendingTasks();
  }
}

namespace Timberborn.InventorySystem {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Timberborn.BaseComponentSystem;
  using Timberborn.Goods;

  public sealed class Inventories : BaseComponent {
    readonly List<Inventory> _allInventories = [];

    public IReadOnlyList<Inventory> AllInventories => _allInventories;

    public void AddInventory(Inventory inventory) {
      _allInventories.Add(inventory);
    }
  }

  public sealed class Inventory : BaseComponent {
    readonly List<StorableGoodAmount> _allowedGoods = [];
    readonly HashSet<string> _inputGoods = [];
    readonly HashSet<string> _outputGoods = [];
    readonly Dictionary<string, int> _amounts = [];
    public object _goodDisallower;

    public Inventory(bool isYielderInventory = false) {
      if (isYielderInventory) {
        _goodDisallower = new Timberborn.Yielding.InRangeYielderGoodAllower();
      }
    }

    public string ComponentName { get; init; } = "Storage";
    public bool IsOutput => _outputGoods.Count > 0;
    public IReadOnlyList<StorableGoodAmount> AllowedGoods => _allowedGoods;
    public IReadOnlyCollection<string> InputGoods => _inputGoods;
    public IReadOnlyCollection<string> OutputGoods => _outputGoods;
    public event EventHandler<InventoryStockChangedEventArgs> InventoryStockChanged;

    public void AddInputGood(string goodId, int capacity) {
      _inputGoods.Add(goodId);
      _allowedGoods.Add(new StorableGoodAmount(new StorableGood(goodId), capacity));
    }

    public void AddOutputGood(string goodId, int capacity) {
      _outputGoods.Add(goodId);
      _allowedGoods.Add(new StorableGoodAmount(new StorableGood(goodId), capacity));
    }

    public void SetAmount(string goodId, int amount) {
      _amounts[goodId] = amount;
      InventoryStockChanged?.Invoke(this, new InventoryStockChangedEventArgs(new GoodAmount(goodId, amount)));
    }

    public int AmountInStock(string goodId) {
      return _amounts.GetValueOrDefault(goodId);
    }

    public int LimitedAmount(string goodId) {
      return _allowedGoods.First(x => x.StorableGood.GoodId == goodId).Amount;
    }

    public void GetCapacity(List<GoodAmount> capacity) {
      foreach (var good in _allowedGoods) {
        capacity.Add(new GoodAmount(good.StorableGood.GoodId, good.Amount));
      }
    }
  }

  public readonly struct InventoryStockChangedEventArgs {
    public GoodAmount GoodAmount { get; }

    public InventoryStockChangedEventArgs(GoodAmount goodAmount) {
      GoodAmount = goodAmount;
    }
  }
}

namespace Timberborn.Forestry {
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;

  public sealed class LumberjackFlagWorkplaceBehavior : BaseComponent {
  }

  public sealed class TreeCuttingArea {
    readonly HashSet<UnityEngine.Vector3Int> _disabledCoordinates = [];

    public void SetInCuttingArea(UnityEngine.Vector3Int coordinates, bool inArea) {
      if (inArea) {
        _disabledCoordinates.Remove(coordinates);
      } else {
        _disabledCoordinates.Add(coordinates);
      }
    }

    public bool IsInCuttingArea(UnityEngine.Vector3Int coordinates) {
      return !_disabledCoordinates.Contains(coordinates);
    }
  }

  public sealed class TreeCuttingAreaChangedEvent {
  }
}

namespace Timberborn.NaturalResourcesLifecycle {
  using System;
  using Timberborn.BaseComponentSystem;

  public sealed class LivingNaturalResource : BaseComponent {
    public event EventHandler Died;
    public event EventHandler ReversedDeath;
    public bool Alive { get; private set; } = true;

    public void Die() {
      Alive = false;
      Died?.Invoke(this, EventArgs.Empty);
    }

    public void ReverseDeath() {
      Alive = true;
      ReversedDeath?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.Planting {
  using System;
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;
  using Timberborn.BlockSystem;
  using Timberborn.Common;

  public readonly struct PlantingSpot {
    public UnityEngine.Vector3Int Coordinates { get; }
    public string ResourceToPlant { get; }
    public BlockObject PlantingBlocker { get; }

    public PlantingSpot(UnityEngine.Vector3Int coordinates, string resourceToPlant, BlockObject plantingBlocker) {
      Coordinates = coordinates;
      ResourceToPlant = resourceToPlant;
      PlantingBlocker = plantingBlocker;
    }
  }

  public sealed class PlantingService {
    readonly Dictionary<UnityEngine.Vector3Int, PlantingSpot> _plantingSpots = [];
    readonly Dictionary<UnityEngine.Vector3Int, string> _resources = [];
    public readonly HashSet<UnityEngine.Vector3Int> _reservedCoordinates = [];

    public void SetSpot(UnityEngine.Vector3Int coordinates, string resource, bool hasSpot = true) {
      _resources[coordinates] = resource;
      if (hasSpot) {
        _plantingSpots[coordinates] = new PlantingSpot(coordinates, resource, null);
      } else {
        _plantingSpots.Remove(coordinates);
      }
    }

    public PlantingSpot? GetSpotAt(UnityEngine.Vector3Int coordinates) {
      return _plantingSpots.TryGetValue(coordinates, out var spot) ? spot : null;
    }

    public string GetResourceAt(UnityEngine.Vector3Int coordinates) {
      return _resources.GetValueOrDefault(coordinates);
    }

    public PlantingSpot CreatePlantingSpot(UnityEngine.Vector3Int coordinates, string resourceToPlant) {
      return new PlantingSpot(coordinates, resourceToPlant, null);
    }
  }

  public sealed class PlantingSpotFinder : BaseComponent {
    readonly HashSet<UnityEngine.Vector3Int> _blockedCoordinates = [];

    public void SetCanPlantAt(UnityEngine.Vector3Int coordinates, bool canPlant) {
      if (canPlant) {
        _blockedCoordinates.Remove(coordinates);
      } else {
        _blockedCoordinates.Add(coordinates);
      }
    }

    public bool CanPlantAt(PlantingSpot plantingSpot, object prioritizedPlantableSpec) {
      return !_blockedCoordinates.Contains(plantingSpot.Coordinates);
    }
  }

  public sealed class InRangePlantingCoordinates : BaseComponent {
    readonly HashSet<UnityEngine.Vector3Int> _coordinates = [];

    public void SetCoordinates(params UnityEngine.Vector3Int[] coordinates) {
      _coordinates.Clear();
      foreach (var coordinate in coordinates) {
        _coordinates.Add(coordinate);
      }
    }

    public ReadOnlyHashSet<UnityEngine.Vector3Int> GetCoordinates() {
      return new ReadOnlyHashSet<UnityEngine.Vector3Int>(_coordinates);
    }
  }

  public sealed class PlantingCoordinatesSetEvent {
    public UnityEngine.Vector3Int Coordinates { get; }
    public string Resource { get; }

    public PlantingCoordinatesSetEvent(UnityEngine.Vector3Int coordinates, string resource) {
      Coordinates = coordinates;
      Resource = resource;
    }
  }

  public sealed class PlantingCoordinatesUnsetEvent {
    public UnityEngine.Vector3Int Coordinates { get; }
    public string Resource { get; }

    public PlantingCoordinatesUnsetEvent(UnityEngine.Vector3Int coordinates, string resource) {
      Coordinates = coordinates;
      Resource = resource;
    }
  }
}

namespace Timberborn.StatusSystem {
  public sealed class StatusSubject : Timberborn.BaseComponentSystem.BaseComponent {
    public readonly List<StatusToggle> RegisteredStatuses = [];

    public void RegisterStatus(StatusToggle statusToggle) {
      RegisteredStatuses.Add(statusToggle);
    }
  }

  public sealed class StatusToggle {
    public bool Active { get; private set; }
    public bool IsActive => Active;

    public static StatusToggle CreatePriorityStatusWithAlertAndFloatingIcon(
        string icon, string description, string alert) {
      return new StatusToggle();
    }

    public static StatusToggle CreatePriorityStatusWithFloatingIcon(string icon, string description) {
      return new StatusToggle();
    }

    public static StatusToggle CreateNormalStatusWithAlertAndFloatingIcon(
        string icon, string description, string alert) {
      return new StatusToggle();
    }

    public static StatusToggle CreateNormalStatusWithFloatingIcon(string icon, string description) {
      return new StatusToggle();
    }

    public static StatusToggle CreateNormalStatus(string icon, string description) {
      return new StatusToggle();
    }

    public void Activate() {
      Active = true;
    }

    public void Deactivate() {
      Active = false;
    }

    public void Toggle(bool active) {
      Active = active;
    }
  }
}

namespace Timberborn.TickSystem {
  public interface ITickableSingleton {
    void Tick();
  }

  public interface IParallelTickableSingleton {
    void StartParallelTick();
  }

  public interface ILateTickable {
  }

  public abstract class TickableComponent : Timberborn.BaseComponentSystem.BaseComponent {
    public bool Enabled { get; private set; } = true;

    public void DisableComponent() {
      Enabled = false;
    }

    public void EnableComponent() {
      Enabled = true;
    }

    public virtual void Tick() {
    }
  }
}

namespace Timberborn.TerrainSystem {
  public interface ITerrainService {
    bool UnsafeCellIsTerrain(int index);
    int GetTerrainHeightBelow(UnityEngine.Vector3Int coordinates);
  }
}

namespace Timberborn.ToolButtonSystem {
  using System.Collections.Generic;

  public sealed class ToolButtonService {
    public List<ToolButton> ToolButtons { get; } = [];
  }

  public sealed class ToolButton {
    public object Tool { get; init; }
  }
}

namespace Timberborn.WaterBuildings {
  public sealed class Floodgate : Timberborn.BaseComponentSystem.BaseComponent {
    public float Height { get; set; }
    public int MaxHeight { get; init; } = 1;
    public int SetHeightCalls { get; private set; }

    public void SetHeight(float height) {
      Height = height;
      SetHeightCalls++;
    }
  }

  public sealed class FillValve : Timberborn.BaseComponentSystem.BaseComponent {
    public int MinTargetHeight { get; init; }
    public int MaxTargetHeight { get; init; } = 1;
    public float TargetHeight { get; private set; }
    public bool TargetHeightEnabled { get; private set; }
    public int SetTargetHeightCalls { get; private set; }
    public int SetTargetHeightEnabledCalls { get; private set; }

    public void SetTargetHeightEnabledAndSynchronize(bool value) {
      TargetHeightEnabled = value;
      SetTargetHeightEnabledCalls++;
    }

    public void SetTargetHeightAndSynchronize(float value) {
      TargetHeight = value;
      SetTargetHeightCalls++;
    }
  }

  public sealed class ThrottlingValve : Timberborn.BaseComponentSystem.BaseComponent {
    public float MaxOutflowLimit { get; init; } = 1;
    public float OutflowLimit { get; private set; }
    public bool OutflowLimitEnabled { get; private set; }
    public int SetOutflowLimitCalls { get; private set; }
    public int SetOutflowLimitEnabledCalls { get; private set; }

    public void SetOutflowLimitEnabledAndSynchronize(bool value) {
      OutflowLimitEnabled = value;
      SetOutflowLimitEnabledCalls++;
    }

    public void SetOutflowLimitAndSynchronize(float value) {
      OutflowLimit = value;
      SetOutflowLimitCalls++;
    }
  }

  public sealed class StreamGauge : Timberborn.BaseComponentSystem.BaseComponent {
    public float WaterLevel { get; init; }
    public float ContaminationLevel { get; init; }
    public float WaterCurrent { get; init; }
  }
}

namespace Timberborn.WaterSourceSystem {
  public sealed class WaterSourceRegulator : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsOpen { get; private set; }

    public void Open() {
      IsOpen = true;
    }

    public void Close() {
      IsOpen = false;
    }
  }
}

namespace Timberborn.Ruins {
  public sealed class ScavengerWorkplaceBehavior : Timberborn.BaseComponentSystem.BaseComponent {
  }
}

namespace Timberborn.ResourceCountingSystem {
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;

  public readonly struct ResourceCount {
    public int AvailableStock { get; init; }
    public int InputOutputCapacity { get; init; }
  }

  public sealed class DistrictResourceCounter : BaseComponent {
    public readonly StockCounter _stockCounter = new();
    public readonly CapacityCounter _capacityCounter = new();

    public ResourceCount GetResourceCount(string goodId) {
      return new ResourceCount {
          AvailableStock = _stockCounter.GetInputOutputStock(goodId) + _stockCounter.GetOutputStock(goodId),
          InputOutputCapacity = _capacityCounter.GetInputOutputCapacity(goodId),
      };
    }
  }

  public sealed class StockCounter {
    public readonly Dictionary<string, int> _inputOutputStock = [];
    public readonly Dictionary<string, int> _outputStock = [];

    public void SetInputOutputStock(string goodId, int value) {
      _inputOutputStock[goodId] = value;
    }

    public void SetOutputStock(string goodId, int value) {
      _outputStock[goodId] = value;
    }

    public int GetInputOutputStock(string goodId) {
      return _inputOutputStock.GetValueOrDefault(goodId);
    }

    public int GetOutputStock(string goodId) {
      return _outputStock.GetValueOrDefault(goodId);
    }
  }

  public sealed class CapacityCounter {
    public readonly Dictionary<string, int> _inputOutputCapacity = [];
    public readonly Dictionary<string, int> _outputCapacity = [];

    public void SetInputOutputCapacity(string goodId, int value) {
      _inputOutputCapacity[goodId] = value;
    }

    public void SetOutputCapacity(string goodId, int value) {
      _outputCapacity[goodId] = value;
    }

    public int GetInputOutputCapacity(string goodId) {
      return _inputOutputCapacity.GetValueOrDefault(goodId);
    }
  }
}

namespace Timberborn.WeatherSystem {
  public sealed class WeatherService {
    public bool IsHazardousWeather { get; init; }
  }
}

namespace Timberborn.Yielding {
  using System;
  using System.Collections.Generic;
  using Timberborn.BaseComponentSystem;
  using Timberborn.NaturalResourcesLifecycle;

  public sealed class InRangeYielderGoodAllower {
  }

  public sealed class YieldRemovingBuilding : BaseComponent {
    readonly HashSet<object> _disallowedSpecs = [];

    public void Disallow(object yielderSpec) {
      _disallowedSpecs.Add(yielderSpec);
    }

    public bool IsAllowed(object yielderSpec) {
      return !_disallowedSpecs.Contains(yielderSpec);
    }
  }

  public sealed class Yielder : BaseComponent {
    public event EventHandler YieldDecreased;
    public event EventHandler YieldAdded;
    public object YielderSpec { get; init; } = new();
    public bool IsYielding { get; private set; } = true;

    public void SetYielding(bool isYielding) {
      IsYielding = isYielding;
    }

    public void DecreaseYield() {
      YieldDecreased?.Invoke(this, EventArgs.Empty);
    }

    public void AddYield() {
      YieldAdded?.Invoke(this, EventArgs.Empty);
    }

    public bool IsAlive() {
      return GetComponent<LivingNaturalResource>()?.Alive ?? true;
    }
  }
}

namespace Timberborn.YielderFinding {
}

namespace Timberborn.WorkSystem {
  using System;
  using Timberborn.PrioritySystem;

  public sealed class WorkerChangedEventArgs : EventArgs {
  }

  public sealed class Workplace : Timberborn.BaseComponentSystem.BaseComponent {
    public event EventHandler<WorkerChangedEventArgs> WorkerAssigned;
    public event EventHandler<WorkerChangedEventArgs> WorkerUnassigned;
    public int NumberOfAssignedWorkers { get; init; }
    public int MaxWorkers { get; init; }
    public int DesiredWorkers { get; set; }
    public int UnassignWorkerIfOverstaffedCalls { get; private set; }

    public void UnassignWorkerIfOverstaffed() {
      UnassignWorkerIfOverstaffedCalls++;
      WorkerUnassigned?.Invoke(this, new WorkerChangedEventArgs());
    }

    public void AssignWorker() {
      WorkerAssigned?.Invoke(this, new WorkerChangedEventArgs());
    }
  }

  public sealed class WorkplacePriority : Timberborn.BaseComponentSystem.BaseComponent {
    public Priority Priority { get; set; }
    public int SetPriorityCalls { get; private set; }

    public void SetPriority(Priority priority) {
      Priority = priority;
      SetPriorityCalls++;
    }
  }
}

namespace Timberborn.Workshops {
  public sealed class RecipeSpec {
    public string Id { get; init; }
    public string DisplayLocKey { get; init; }
  }

  public sealed class Manufactory : Timberborn.BaseComponentSystem.BaseComponent {
    public RecipeSpec[] ProductionRecipes { get; init; } = [];
    public RecipeSpec CurrentRecipe { get; set; }
    public int SetRecipeCalls { get; private set; }

    public void SetRecipe(RecipeSpec recipe) {
      CurrentRecipe = recipe;
      SetRecipeCalls++;
    }
  }
}
