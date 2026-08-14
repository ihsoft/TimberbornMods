// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.XRay.Settings;
using Timberborn.EntitySystem;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

sealed class TransparentNaturalResourceModelService : IPostLoadableSingleton {

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _eventBus.Register(this);
    _objectTransparencySettings.PlantColor.ValueChanged += (_, _) => UpdateMaterialColor();
    _objectTransparencySettings.PlantTransparency.ValueChanged += (_, _) => UpdateMaterialColor();
  }

  #endregion

  #region API

  public bool IsActive { get; private set; }

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
    foreach (var model in _entityComponentRegistry.GetEnabled<NaturalResourceModel>()) {
      MakeTransparent(model);
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

  TransparentNaturalResourceModelService(
      EntityComponentRegistry entityComponentRegistry, RendererFactory rendererFactory,
      ObjectTransparencySettings objectTransparencySettings, EntitySelectionService entitySelectionService,
      EventBus eventBus) {
    _entityComponentRegistry = entityComponentRegistry;
    _rendererFactory = rendererFactory;
    _objectTransparencySettings = objectTransparencySettings;
    _entitySelectionService = entitySelectionService;
    _eventBus = eventBus;
  }

  void MakeTransparent(NaturalResourceModel model) {
    if (_entitySelectionService.IsAnythingSelected
        && _entitySelectionService.SelectedObject.GameObject == model.GameObject) {
      return;
    }
    foreach (var renderer in model.GameObject.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      if (_rendererStates.ContainsKey(renderer)) {
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
          $"XRay_{original.name}", _objectTransparencySettings.PlantFillColor,
          _rendererFactory.WaterRendererQueue + 1);
      _transparentMaterials.Add(original, transparent);
    }
    return transparent;
  }

  void UpdateMaterialColor() {
    RemoveDestroyedMaterials();
    var color = _objectTransparencySettings.PlantFillColor;
    foreach (var material in _transparentMaterials.Values) {
      _rendererFactory.SetMaterialColor(material, color);
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
        RestoreRenderer(renderer, state);
      }
    }
    _rendererStates.Clear();
  }

  void RestoreModel(GameObject model) {
    foreach (var renderer in model.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      if (_rendererStates.Remove(renderer, out var state) && renderer) {
        RestoreRenderer(renderer, state);
      }
    }
  }

  void RemoveModelState(GameObject model) {
    foreach (var renderer in model.GetComponentsInChildren<Renderer>(includeInactive: true)) {
      _rendererStates.Remove(renderer);
    }
  }

  static void RestoreRenderer(Renderer renderer, RendererState state) {
    renderer.sharedMaterials = state.Materials;
    renderer.shadowCastingMode = state.ShadowCastingMode;
    renderer.receiveShadows = state.ReceiveShadows;
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
    var model = e.Entity.GetComponent<NaturalResourceModel>();
    if (model != null) {
      MakeTransparent(model);
    }
  }

  [OnEvent]
  public void OnEntityDeleted(EntityDeletedEvent e) {
    var model = e.Entity.GetComponent<NaturalResourceModel>();
    if (model == null) {
      return;
    }
    RemoveModelState(model.GameObject);
    RemoveDestroyedMaterials();
  }

  [OnEvent]
  public void OnSelectableObjectSelected(SelectableObjectSelectedEvent e) {
    var model = e.SelectableObject.GetComponent<NaturalResourceModel>();
    if (model != null) {
      RestoreModel(model.GameObject);
    }
  }

  [OnEvent]
  public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent e) {
    if (!IsActive || !e.SelectableObject) {
      return;
    }
    var model = e.SelectableObject.GetComponent<NaturalResourceModel>();
    if (model != null) {
      MakeTransparent(model);
    }
  }

  #endregion
}
