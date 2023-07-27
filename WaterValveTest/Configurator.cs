using System.Linq;
using Bindito.Core;
using IgorZ.TimberCommons.WaterValveComponent;
using IgorZ.TimberDev.CustomInstantiator;
using IgorZ.TimberDev.Logging;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.PrefabSystem;
using Timberborn.WaterBuildings;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.Reflections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IgorZ.WaterValveTest {

[Configurator(SceneEntrypoint.InGame)]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<ThreadedLogsRecorder>().AsSingleton();
    CustomizableInstantiator.AddPatcher(PatchId, PatchWaterExtensionBuilding);
  }

  void PatchWaterExtensionBuilding(GameObject prefab) {
    var prefabComp = prefab.GetComponent<Prefab>();
    if (prefabComp == null) {
      return;
    }
    PatchValve(prefabComp, prefab);
    PatchPipe(prefabComp, prefab);
  }

  void PatchPipe(Prefab prefabComp, GameObject obj) {
    if (prefabComp == null || !prefabComp.Name.StartsWith("Pipe1X")) {
      return;
    }
    DebugEx.Warning("*** found prefab: {0}", prefabComp.Name);
    var all = obj.GetComponents<BaseComponent>();
    foreach (var c in all) {
      DebugEx.Warning("*** found component: {0}", c.GetType().FullName);
    }
    var dropComponents = new[] {
        "WaterMover", "MechanicalBuilding", "MechanicalNodeSpecification", "MechanicalNodeAnimator",
        "WaterInput", "WaterOutput", "ClusterElementSpecification", "WaterMoverParticleController",
        //"WaterObstacle", "FinishableWaterObstacle",
    };
    var waterMoverParticleSystemFieldFn =
        new ReflectedField<ParticleSystem>(
            //typeof(WaterInput).Assembly.GetType("Timberborn.WaterBuildings.WaterMoverParticleController"),
            "Timberborn.WaterBuildings.WaterMoverParticleController",
            "_particleSystem",
            throwOnFailure: true);
    var waterValveParticleSystemFieldFn =
        new ReflectedField<WaterValve, ParticleSystem>("_particleSystem", throwOnFailure: true);
    ParticleSystem particleSystem = null;
    foreach (var comp in obj.GetComponents<BaseComponent>()) {
      var componentName = comp.GetType().Name;
      if (dropComponents.Contains(componentName)) {
        DebugEx.Warning("*** Destroying component on prefab: name={0}, component={1}, enabled={2}",
                        obj.name, componentName, comp.enabled);
        if (componentName == "WaterMoverParticleController") {
          particleSystem = waterMoverParticleSystemFieldFn.Get(comp);
          DebugEx.Warning("*** got system: {0}", particleSystem);
        }
        Object.DestroyImmediate(comp);
      }
    }
    var valve = obj.AddComponent<WaterValve>();
    waterValveParticleSystemFieldFn.Set(valve, particleSystem);
  }

  void PatchValve(Prefab prefabComp, GameObject obj) {
    if (!prefabComp.Name.Contains("Valve1X")) {
      return;
    }
    var dropComponents = new[] {
        "WaterValve"
    };
    foreach (var comp in obj.GetComponents<BaseComponent>()) {
      if (dropComponents.Contains(comp.GetType().Name)) {
        DebugEx.Warning("*** Destroying component on prefab: name={0}, component={1}", obj.name, comp.GetType().Name);
        Object.DestroyImmediate(comp);
      }
    }
    obj.AddComponent<WaterValve>();
  }
}

}