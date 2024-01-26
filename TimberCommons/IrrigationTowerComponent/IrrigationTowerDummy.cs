// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationTowerComponent {

/// <summary>A temporary component which is used to load the old saves. It's NOOP.</summary>
public class IrrigationTower : BaseComponent {

  // ReSharper disable once InconsistentNaming
  [SerializeField] string _dummyComponentAction = "DELETE!";

  void Awake() {
    HostedDebugLog.Warning(
        this, "Dummy component created: type={0}, action: {1}", GetType().FullName, _dummyComponentAction);
  }
}

}
