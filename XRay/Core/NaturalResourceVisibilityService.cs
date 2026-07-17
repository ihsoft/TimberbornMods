// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.XRay.Settings;
using Timberborn.Debugging;
using Timberborn.EntitySystem;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.NaturalResourcesUI;
using Timberborn.SingletonSystem;

namespace IgorZ.XRay.Core;

sealed class NaturalResourceVisibilityService : IPostLoadableSingleton {

  readonly EntityComponentRegistry _entityComponentRegistry;
  readonly MeshSettings _meshSettings;
  readonly NaturalResourcesModelToggler _naturalResourcesModelToggler;

  internal static NaturalResourceVisibilityService Instance { get; private set; }

  public bool IsActive { get; private set; }

  NaturalResourceVisibilityService(
      EntityComponentRegistry entityComponentRegistry, MeshSettings meshSettings,
      IEnumerable<IDevModule> devModules) {
    Instance = this;
    _entityComponentRegistry = entityComponentRegistry;
    _meshSettings = meshSettings;
    _naturalResourcesModelToggler = devModules.OfType<NaturalResourcesModelToggler>().Single();
  }

  public void PostLoad() {
    _meshSettings.HideNaturalResources.ValueChanged += (_, _) => RefreshAllModels();
  }

  public void Activate() {
    IsActive = true;
    RefreshAllModels();
  }

  public void Deactivate() {
    IsActive = false;
    RefreshAllModels();
  }

  internal void RefreshAfterDebugToggle() {
    if (ShouldHideForXRay) {
      HideAllModels();
    }
  }

  internal void RefreshModel(NaturalResourceModel model) {
    if (ShouldHideForXRay || _naturalResourcesModelToggler._naturalResourcesHidden) {
      model.Hide();
    }
  }

  bool ShouldHideForXRay => IsActive && _meshSettings.HideNaturalResources.Value;

  void RefreshAllModels() {
    if (ShouldHideForXRay || _naturalResourcesModelToggler._naturalResourcesHidden) {
      HideAllModels();
    } else {
      foreach (var model in GetModels()) {
        model.Show();
      }
    }
  }

  void HideAllModels() {
    foreach (var model in GetModels()) {
      model.Hide();
    }
  }

  IEnumerable<NaturalResourceModel> GetModels() =>
      _entityComponentRegistry.GetEnabled<NaturalResourceModel>();
}
