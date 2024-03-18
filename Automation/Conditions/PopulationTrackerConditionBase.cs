// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Automation.Core;
using Timberborn.GameDistricts;

namespace Automation.Conditions {

/// <summary>A base condition class that tracks population changes in the district.</summary>
public abstract class PopulationTrackerConditionBase : AutomationConditionBase {

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return behavior.GetComponentFast<DistrictBuilding>();
  }

  /// <inheritdoc/>
  public override void SyncState() {
    if (DistrictCenter && !IsPreview) {
      OnPopulationChanged();
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    var districtBuilding = Behavior.GetComponentFast<DistrictBuilding>();
    districtBuilding.ReassignedDistrict += OnReassignedDistrict;
    districtBuilding.ReassignedConstructionDistrict += OnReassignedConstructionDistrict;
    UpdateDistrict();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    var districtBuilding = Behavior.GetComponentFast<DistrictBuilding>();
    districtBuilding.ReassignedDistrict -= OnReassignedDistrict;
    districtBuilding.ReassignedConstructionDistrict -= OnReassignedConstructionDistrict;
    UpdateDistrict();
  }

  #endregion

  #region API
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>District center of the owning building. It can be <c>null</c>.</summary>
  protected DistrictCenter DistrictCenter { get; private set; }

  /// <summary>District population of the district or <c>null</c> if no district assigned to the building.</summary>
  protected DistrictPopulation DistrictPopulation => DistrictCenter ? DistrictCenter.DistrictPopulation : null;

  /// <summary>Tells if the owner objects is a preview building.</summary>
  protected bool IsPreview => !Behavior.BlockObject.Finished;

  /// <summary>A callback that is called every time the citizen's list on the district is changed.</summary>
  protected abstract void OnPopulationChanged();

  /// <summary>A callback that is called when building is assigned to another district or is removed from any.</summary>
  /// <param name="oldDistrict">The district center that was previous known.</param>
  protected abstract void OnBuildingDistrictCenterChange(DistrictCenter oldDistrict);

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region Implementation

  /// <summary>Updates <see cref="DistrictCenter"/> to the current value. It handles the old center dispose.</summary>
  /// <returns>The district center that was active before or <c>null</c>.</returns>
  DistrictCenter UpdateDistrict() {
    var oldDistrict = DistrictCenter;
    if (DistrictCenter) {
      DistrictCenter.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
      DistrictCenter.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
    }
    if (Behavior) {
      DistrictCenter = Behavior.GetComponentFast<DistrictBuilding>().District;
      if (!DistrictCenter) {
        DistrictCenter = Behavior.GetComponentFast<DistrictBuilding>().ConstructionDistrict;
      }
    } else {
      DistrictCenter = null;
    }
    if (DistrictCenter) {
      DistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
      DistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
    }
    return oldDistrict;
  }  

  #endregion

  #region Event handlers

  void OnCitizenAssigned(object sender, CitizenAssignedEventArgs args) {
    OnPopulationChanged();
  }

  void OnCitizenUnassigned(object sender, CitizenUnassignedEventArgs args) {
    OnPopulationChanged();
  }

  void OnReassignedDistrict(object sender, EventArgs e) {
    OnBuildingDistrictCenterChange(UpdateDistrict());
    if (DistrictCenter && !IsPreview) {
      OnPopulationChanged();
    }
  }

  void OnReassignedConstructionDistrict(object sender, EventArgs e) {
    OnBuildingDistrictCenterChange(UpdateDistrict());
  }

  #endregion
}

}
