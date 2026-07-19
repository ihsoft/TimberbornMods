using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.LinkedBuildingSystem;
using Timberborn.Stockpiles;
using UnityEngine;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.DualDistrictStorage;

sealed class DualDistrictStorage : BaseComponent, IAwakableComponent, IFinishedStateListener,
    ILateUpdatableComponent, IDeletableEntity {
  readonly MirrorOperationLock _mirrorOperationLock = new();
  readonly DualDistrictStorageRegistry _registry;

  Inventory _inventory;
  SingleGoodAllower _singleGoodAllower;
  DualDistrictStorage _linked;
  GameObject _normalModel;
  GameObject _mirroredRoofModel;
  MeshFilter _liquidMeshFilter;
  Mesh _liquidHalfMesh;
  bool _liquidMeshPreparationFailed;
  bool _registered;
  bool _finished;
  bool _usesModelVariants;

  internal Inventory Inventory => _inventory;

  public DualDistrictStorage(DualDistrictStorageRegistry registry) {
    _registry = registry;
  }

  public void Awake() {
    _inventory = GetComponent<Stockpile>().Inventory;
    _singleGoodAllower = GetComponent<SingleGoodAllower>();
    _normalModel = GameObject.transform.Find("#Finished/NormalModel")?.gameObject;
    _mirroredRoofModel = GameObject.transform.Find("#Finished/MirroredRoofModel")?.gameObject;
    if (_normalModel != null && _mirroredRoofModel != null) {
      _usesModelVariants = true;
      ShowModelVariant(mirroredRoof: false);
    } else if (_normalModel != null || _mirroredRoofModel != null) {
      HostedDebugLog.Warning(this, "Found only one of the normal and mirrored finished models.");
    }
    GetComponent<LinkedBuilding>().BuildingLinked += OnBuildingLinked;
  }

  public void OnEnterFinishedState() {
    _finished = true;
    _inventory.InventoryEnabled += OnInventoryEnabled;
    _inventory.InventoryChanged += OnInventoryChanged;
    _inventory.InventoryStockChanged += OnInventoryStockChanged;
    _singleGoodAllower.DisallowedGoodsChanged += OnDisallowedGoodsChanged;
    TryInitializePair();
  }

  public void LateUpdate() {
    if (!_finished || _usesModelVariants) {
      return;
    }

    if (_liquidHalfMesh == null && !_liquidMeshPreparationFailed) {
      PrepareLiquidHalfVisualization();
    }
    if (_liquidHalfMesh != null && _liquidMeshFilter.sharedMesh != _liquidHalfMesh) {
      _liquidMeshFilter.sharedMesh = _liquidHalfMesh;
    }
  }

  void PrepareLiquidHalfVisualization() {
    if (_usesModelVariants) {
      return;
    }

    _liquidMeshFilter ??= GameObject.transform.Find("#Finished/GoodVisualization")?.GetComponent<MeshFilter>();
    if (_liquidMeshFilter == null || _liquidMeshFilter.sharedMesh == null) {
      return;
    }

    var sourceMesh = _liquidMeshFilter.sharedMesh;
    var sourceVertices = sourceMesh.vertices;
    var sourceTriangles = sourceMesh.triangles;
    if (sourceVertices.Length == 0 || sourceTriangles.Length == 0) {
      return;
    }

    var sourceNormals = sourceMesh.normals;
    var sourceTangents = sourceMesh.tangents;
    var sourceUvs = sourceMesh.uv;
    var sourceColors = sourceMesh.colors;
    var hasNormals = sourceNormals.Length == sourceVertices.Length;
    var hasTangents = sourceTangents.Length == sourceVertices.Length;
    var hasUvs = sourceUvs.Length == sourceVertices.Length;
    var hasColors = sourceColors.Length == sourceVertices.Length;
    var outputVertices = new List<LiquidVertex>();
    var outputTriangles = new List<int>();
    for (var i = 0; i < sourceTriangles.Length; i += 3) {
      var polygon = new List<LiquidVertex>(4) {
          GetLiquidVertex(sourceTriangles[i]),
          GetLiquidVertex(sourceTriangles[i + 1]),
          GetLiquidVertex(sourceTriangles[i + 2]),
      };
      polygon = ClipLiquidPolygon(polygon);
      for (var vertexIndex = 1; vertexIndex + 1 < polygon.Count; vertexIndex++) {
        AddOutputVertex(polygon[0]);
        AddOutputVertex(polygon[vertexIndex]);
        AddOutputVertex(polygon[vertexIndex + 1]);
      }
    }

    if (outputTriangles.Count == 0) {
      _liquidMeshPreparationFailed = true;
      HostedDebugLog.Error(
          this,
          $"Could not split the liquid plane at Z=0: source had {sourceTriangles.Length / 3} triangles.");
      return;
    }

    var positions = new List<Vector3>(outputVertices.Count);
    var normals = new List<Vector3>(outputVertices.Count);
    var tangents = new List<Vector4>(outputVertices.Count);
    var uvs = new List<Vector2>(outputVertices.Count);
    var colors = new List<Color>(outputVertices.Count);
    foreach (var vertex in outputVertices) {
      positions.Add(vertex.Position + Vector3.forward * 0.5f);
      normals.Add(vertex.Normal);
      tangents.Add(vertex.Tangent);
      uvs.Add(vertex.Uv);
      colors.Add(vertex.Color);
    }

    _liquidHalfMesh = UnityEngine.Object.Instantiate(sourceMesh);
    _liquidHalfMesh.name = "DualDistrictTank.LiquidHalf";
    _liquidHalfMesh.Clear();
    _liquidHalfMesh.SetVertices(positions);
    if (hasNormals) {
      _liquidHalfMesh.SetNormals(normals);
    }
    if (hasTangents) {
      _liquidHalfMesh.SetTangents(tangents);
    }
    if (hasUvs) {
      _liquidHalfMesh.SetUVs(0, uvs);
    }
    if (hasColors) {
      _liquidHalfMesh.SetColors(colors);
    }
    _liquidHalfMesh.SetTriangles(outputTriangles, 0);
    _liquidHalfMesh.RecalculateBounds();
    _liquidMeshFilter.sharedMesh = _liquidHalfMesh;

    LiquidVertex GetLiquidVertex(int index) {
      return new LiquidVertex(
          sourceVertices[index],
          hasNormals ? sourceNormals[index] : default,
          hasTangents ? sourceTangents[index] : default,
          hasUvs ? sourceUvs[index] : default,
          hasColors ? sourceColors[index] : default);
    }

    void AddOutputVertex(LiquidVertex vertex) {
      outputTriangles.Add(outputVertices.Count);
      outputVertices.Add(vertex);
    }
  }

  static List<LiquidVertex> ClipLiquidPolygon(List<LiquidVertex> input) {
    var output = new List<LiquidVertex>(4);
    var previous = input[^1];
    var previousInside = previous.Position.z <= 0.0001f;
    foreach (var current in input) {
      var currentInside = current.Position.z <= 0.0001f;
      if (currentInside != previousInside) {
        var interpolation = -previous.Position.z / (current.Position.z - previous.Position.z);
        output.Add(LiquidVertex.Lerp(previous, current, interpolation));
      }
      if (currentInside) {
        output.Add(current);
      }
      previous = current;
      previousInside = currentInside;
    }
    return output;
  }

  readonly struct LiquidVertex {
    internal readonly Vector3 Position;
    internal readonly Vector3 Normal;
    internal readonly Vector4 Tangent;
    internal readonly Vector2 Uv;
    internal readonly Color Color;

    internal LiquidVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv, Color color) {
      Position = position;
      Normal = normal;
      Tangent = tangent;
      Uv = uv;
      Color = color;
    }

    internal static LiquidVertex Lerp(LiquidVertex first, LiquidVertex second, float interpolation) {
      return new LiquidVertex(
          Vector3.LerpUnclamped(first.Position, second.Position, interpolation),
          Vector3.LerpUnclamped(first.Normal, second.Normal, interpolation).normalized,
          Vector4.LerpUnclamped(first.Tangent, second.Tangent, interpolation),
          Vector2.LerpUnclamped(first.Uv, second.Uv, interpolation),
          UnityEngine.Color.LerpUnclamped(first.Color, second.Color, interpolation));
    }
  }

  public void OnExitFinishedState() {
    _finished = false;
    _inventory.InventoryEnabled -= OnInventoryEnabled;
    _inventory.InventoryChanged -= OnInventoryChanged;
    _inventory.InventoryStockChanged -= OnInventoryStockChanged;
    _singleGoodAllower.DisallowedGoodsChanged -= OnDisallowedGoodsChanged;
    UnregisterPair();
  }

  void OnBuildingLinked(object sender, LinkedBuilding linkedBuilding) {
    _linked = linkedBuilding.GetComponent<DualDistrictStorage>();
    ApplyPairModelVariants();
    TryInitializePair();
  }

  public void DeleteEntity() {
    if (_liquidHalfMesh != null) {
      UnityEngine.Object.Destroy(_liquidHalfMesh);
    }
  }

  void OnInventoryEnabled(object sender, EventArgs e) {
    TryInitializePair();
  }

  void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorStockReservation(e.GoodId, ReservedStock(e.GoodId));
    }
  }

  void OnInventoryStockChanged(object sender, InventoryStockChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorStock(e.GoodAmount);
    }
  }

  void OnDisallowedGoodsChanged(object sender, DisallowedGoodsChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorAllowedGood(_singleGoodAllower.AllowedGood);
    }
  }

  void MirrorStock(GoodAmount change) {
    using (_mirrorOperationLock.Lock()) {
      if (change.Amount > 0) {
        _inventory.GiveExistingIgnoringCapacityReservation(change);
      } else if (change.Amount < 0) {
        _inventory.TakeExisting(new GoodAmount(change.GoodId, -change.Amount));
      }
    }
  }

  void MirrorStockReservation(string goodId, int expectedReservation) {
    using (_mirrorOperationLock.Lock()) {
      var reservationDelta = expectedReservation - ReservedStock(goodId);
      if (reservationDelta > 0) {
        _inventory.ReserveStock(new GoodAmount(goodId, reservationDelta));
      } else if (reservationDelta < 0) {
        _inventory.UnreserveStock(new GoodAmount(goodId, -reservationDelta));
      }
    }
  }

  void MirrorAllowedGood(string goodId) {
    using (_mirrorOperationLock.Lock()) {
      _singleGoodAllower.Allow(goodId);
    }
  }

  int ReservedStock(string goodId) {
    return _inventory.AmountInStock(goodId) - _inventory.UnreservedAmountInStock(goodId);
  }

  void TryInitializePair() {
    if (!IsOperationalPair()) {
      return;
    }

    var primary = PrimaryHalf();
    var secondary = primary._linked;
    primary.VerifyReplicasMatch(secondary);
    if (!primary._registered) {
      primary._registry.Register(primary);
      primary._registered = true;
    }
  }

  void ApplyPairModelVariants() {
    if (_linked == null || !_usesModelVariants || !_linked._usesModelVariants) {
      return;
    }

    var primary = PrimaryHalf();
    var secondary = ReferenceEquals(primary, this) ? _linked : this;
    primary.ShowModelVariant(mirroredRoof: false);
    secondary.ShowModelVariant(mirroredRoof: true);
  }

  void ShowModelVariant(bool mirroredRoof) {
    if (_normalModel == null || _mirroredRoofModel == null) {
      HostedDebugLog.Warning(this, "Could not find the normal and mirrored finished models.");
      return;
    }

    _normalModel.SetActive(!mirroredRoof);
    _mirroredRoofModel.SetActive(mirroredRoof);
  }

  void VerifyReplicasMatch(DualDistrictStorage other) {
    if (_singleGoodAllower.AllowedGood != other._singleGoodAllower.AllowedGood
        || !StockMatches(other) || !ReservationsMatch(other)) {
      HostedDebugLog.Error(this, "Linked storage inventories are out of sync. No automatic recovery was attempted.");
    }
  }

  bool StockMatches(DualDistrictStorage other) {
    var goods = new HashSet<string>();
    AddStockGoods(_inventory, goods);
    AddStockGoods(other._inventory, goods);
    foreach (var goodId in goods) {
      if (_inventory.AmountInStock(goodId) != other._inventory.AmountInStock(goodId)) {
        return false;
      }
    }
    return true;
  }

  bool ReservationsMatch(DualDistrictStorage other) {
    var goods = new HashSet<string>();
    AddStockGoods(_inventory, goods);
    AddStockGoods(other._inventory, goods);
    foreach (var goodId in goods) {
      if (ReservedStock(goodId) != other.ReservedStock(goodId)) {
        return false;
      }
    }
    return true;
  }

  static void AddStockGoods(Inventory inventory, HashSet<string> goods) {
    foreach (var good in inventory.Stock) {
      goods.Add(good.GoodId);
    }
  }

  void UnregisterPair() {
    var primary = PrimaryHalfOrNull();
    if (primary?._registered == true) {
      primary._registry.Unregister(primary);
      primary._registered = false;
    }
  }

  DualDistrictStorage PrimaryHalf() {
    return GetComponent<EntityComponent>().EntityId.CompareTo(_linked.GetComponent<EntityComponent>().EntityId) <= 0
        ? this
        : _linked;
  }

  DualDistrictStorage PrimaryHalfOrNull() {
    return _linked == null ? null : PrimaryHalf();
  }

  bool IsOperationalPair() {
    return _inventory.Enabled && _linked?._inventory.Enabled == true;
  }
}
