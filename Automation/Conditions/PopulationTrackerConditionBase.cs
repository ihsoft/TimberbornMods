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
    return behavior.GetComponentFast<DistrictBuilding>() != null;
  }

  /// <inheritdoc/>
  public override void SyncState() {
    OnPopulationChanged();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    var districtBuilding = Behavior.GetComponentFast<DistrictBuilding>();
    districtBuilding.ReassignedDistrict += OnReassignedDistrict;
    DistrictCenter = districtBuilding.District;
    if (DistrictCenter == null) {
      return;
    }
    DistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
    DistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    var districtBuilding = Behavior.GetComponentFast<DistrictBuilding>();
    districtBuilding.ReassignedDistrict -= OnReassignedDistrict;
    if (DistrictCenter == null) {
      return;
    }
    DistrictCenter.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
    DistrictCenter.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
  }

  #endregion

  #region API

  /// <summary>District center of the owning building. It can be <c>null</c>.</summary>
  protected DistrictCenter DistrictCenter { get; private set; }

  /// <summary>District population of the district or <c>null</c> if no district assigned to the building.</summary>
  protected DistrictPopulation DistrictPopulation => DistrictCenter != null ? DistrictCenter.DistrictPopulation : null;

  /// <summary>A callback that is called every time the citizen's list on the district is changed.</summary>
  protected abstract void OnPopulationChanged();

  /// <summary>A callback that is called when building is assigned to another district or is removed from any.</summary>
  /// <param name="oldDistrict">The district center that was previous known.</param>
  protected abstract void OnBuildingDistrictCenterChange(DistrictCenter oldDistrict);

  #endregion

  #region Event handlers

  void OnCitizenAssigned(object sender, CitizenAssignedEventArgs args) {
    OnPopulationChanged();
  }

  void OnCitizenUnassigned(object sender, CitizenUnassignedEventArgs args) {
    OnPopulationChanged();
  }

  void OnReassignedDistrict(object sender, EventArgs e) {
    var oldDistrict = DistrictCenter;
    DistrictCenter = Behavior.GetComponentFast<DistrictBuilding>().District;
    if (oldDistrict != null) {
      oldDistrict.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
      oldDistrict.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
    }
    if (DistrictCenter != null) {
      DistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
      DistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
    }
    OnBuildingDistrictCenterChange(oldDistrict);
  }

  #endregion
}

}
