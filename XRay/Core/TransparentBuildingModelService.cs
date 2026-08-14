// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.XRay.Settings;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.StockpileVisualization;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

sealed class TransparentBuildingModelService : IPostLoadableSingleton {

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _eventBus.Register(this);
    _objectTransparencySettings.BuildingColor.ValueChanged += (_, _) => UpdateMaterialColor();
    _objectTransparencySettings.BuildingTransparency.ValueChanged += (_, _) => UpdateMaterialColor();
  }

  #endregion

  #region API

  public bool IsActive { get; private set; }

  public bool PassThroughSurfaceObjects => IsActive;

  public void SetActive(bool active) {
    if (active == IsActive) {
      return;
    }
    IsActive = active;
    if (!active) {
      RestoreAll();
      return;
    }
    RemoveDestroyedMaterials();
    foreach (var building in _entityComponentRegistry.GetEnabled<Building>()) {
      ApplyToBuilding(building);
    }
  }

  #endregion

  #region Implementation

  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly RendererFactory _rendererFactory;
  readonly ObjectTransparencySettings _objectTransparencySettings;
  readonly EntitySelectionService _entitySelectionService;
  readonly EventBus _eventBus;

  readonly Dictionary<Material, Material> _transparentMaterials = new();
  readonly Dictionary<Renderer, RendererState> _rendererStates = new();
  readonly Dictionary<GoodVisualization, bool> _hiddenGoodVisualizations = new();

  TransparentBuildingModelService(
      EntityComponentRegistry entityComponentRegistry, RendererFactory rendererFactory,
      ObjectTransparencySettings objectTransparencySettings, EntitySelectionService entitySelectionService,
      EventBus eventBus) {
    _entityComponentRegistry = entityComponentRegistry;
    _rendererFactory = rendererFactory;
    _objectTransparencySettings = objectTransparencySettings;
    _entitySelectionService = entitySelectionService;
    _eventBus = eventBus;
  }

  void ApplyToBuilding(Building building) {
    if (_entitySelectionService.IsAnythingSelected
        && _entitySelectionService.SelectedObject.GameObject == building.GameObject) {
      return;
    }
    var buildingModel = building.GetComponent<BuildingModel>();
    if (buildingModel == null) {
      return;
    }
    var goodVisualizationRenderer = HideGoodVisualization(building);
    MakeTransparent(buildingModel.FinishedModel, goodVisualizationRenderer);
    MakeTransparent(buildingModel.UnfinishedModel);
    MakeTransparent(buildingModel.FinishedUncoveredModel);
  }

  Renderer HideGoodVisualization(Building building) {
    var goodVisualization = building.GetComponent<GoodVisualization>();
    if (goodVisualization == null || _hiddenGoodVisualizations.ContainsKey(goodVisualization)) {
      return goodVisualization?._meshRenderer;
    }
    var visualization = goodVisualization._visualization;
    _hiddenGoodVisualizations.Add(goodVisualization, visualization.activeSelf);
    visualization.SetActive(false);
    return goodVisualization._meshRenderer;
  }

  void MakeTransparent(GameObject model, Renderer excludedRenderer = null) {
    if (!model) {
      return;
    }
    foreach (var renderer in model.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      if (renderer == excludedRenderer || _rendererStates.ContainsKey(renderer)) {
        continue;
      }
      var originalMaterials = renderer.sharedMaterials;
      var transparentMaterials = new Material[originalMaterials.Length];
      for (var i = 0; i < originalMaterials.Length; i++) {
        var original = originalMaterials[i];
        transparentMaterials[i] = original ? GetTransparentMaterial(original) : null;
      }
      _rendererStates.Add(
          renderer, new RendererState(originalMaterials, renderer.shadowCastingMode, renderer.receiveShadows));
      renderer.sharedMaterials = transparentMaterials;
      renderer.shadowCastingMode = ShadowCastingMode.Off;
      renderer.receiveShadows = false;
    }
  }

  Material GetTransparentMaterial(Material original) {
    if (!_transparentMaterials.TryGetValue(original, out var transparent)) {
      transparent = _rendererFactory.CreateTransparencyMaterial(
          $"XRay_{original.name}", _objectTransparencySettings.BuildingFillColor,
          _rendererFactory.WaterRendererQueue + 1);
      _transparentMaterials.Add(original, transparent);
    }
    return transparent;
  }

  void UpdateMaterialColor() {
    RemoveDestroyedMaterials();
    foreach (var material in _transparentMaterials.Values) {
      _rendererFactory.SetMaterialColor(material, _objectTransparencySettings.BuildingFillColor);
    }
  }

  void RemoveDestroyedMaterials() {
    var destroyedMaterials = new List<Material>();
    foreach (var (original, transparent) in _transparentMaterials) {
      if (!original || !transparent) {
        if (transparent) {
          Object.Destroy(transparent);
        }
        destroyedMaterials.Add(original);
      }
    }
    foreach (var original in destroyedMaterials) {
      _transparentMaterials.Remove(original);
    }
  }

  void RestoreAll() {
    foreach (var (renderer, state) in _rendererStates) {
      if (renderer) {
        renderer.sharedMaterials = state.Materials;
        renderer.shadowCastingMode = state.ShadowCastingMode;
        renderer.receiveShadows = state.ReceiveShadows;
      }
    }
    _rendererStates.Clear();
    foreach (var (goodVisualization, wasActive) in _hiddenGoodVisualizations) {
      if (goodVisualization) {
        goodVisualization._visualization.SetActive(wasActive);
      }
    }
    _hiddenGoodVisualizations.Clear();
  }

  void RestoreBuilding(GameObject building) {
    foreach (var renderer in building.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      if (_rendererStates.Remove(renderer, out var state) && renderer) {
        renderer.sharedMaterials = state.Materials;
        renderer.shadowCastingMode = state.ShadowCastingMode;
        renderer.receiveShadows = state.ReceiveShadows;
      }
    }
    var goodVisualization = building.GetComponent<GoodVisualization>();
    if (goodVisualization != null && _hiddenGoodVisualizations.Remove(goodVisualization, out var wasActive)) {
      goodVisualization._visualization.SetActive(wasActive);
    }
  }

  void RemoveBuildingState(GameObject building) {
    foreach (var renderer in building.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      _rendererStates.Remove(renderer);
    }
    var goodVisualization = building.GetComponent<GoodVisualization>();
    if (goodVisualization != null) {
      _hiddenGoodVisualizations.Remove(goodVisualization);
    }
  }

  readonly record struct RendererState(
      Material[] Materials, ShadowCastingMode ShadowCastingMode, bool ReceiveShadows);

  #endregion

  #region Events

  [OnEvent]
  public void OnEntityInitialized(EntityInitializedEvent e) {
    if (!IsActive) {
      return;
    }
    var building = e.Entity.GetComponent<Building>();
    if (building != null) {
      ApplyToBuilding(building);
    }
  }

  [OnEvent]
  public void OnEnteredFinishedState(EnteredFinishedStateEvent e) {
    if (!IsActive) {
      return;
    }
    var building = e.BlockObject.GetComponent<Building>();
    if (building != null) {
      ApplyToBuilding(building);
    }
  }

  [OnEvent]
  public void OnEntityDeleted(EntityDeletedEvent e) {
    var building = e.Entity.GetComponent<Building>();
    if (building == null) {
      return;
    }
    RemoveBuildingState(building.GameObject);
    RemoveDestroyedMaterials();
  }

  [OnEvent]
  public void OnSelectableObjectSelected(SelectableObjectSelectedEvent e) {
    var building = e.SelectableObject.GetComponent<Building>();
    if (building != null) {
      RestoreBuilding(building.GameObject);
    }
  }

  [OnEvent]
  public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent e) {
    if (!IsActive || !e.SelectableObject) {
      return;
    }
    var building = e.SelectableObject.GetComponent<Building>();
    if (building != null) {
      ApplyToBuilding(building);
    }
  }

  #endregion
}
