// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>
/// A class that wants to behave as <see cref="BaseComponent"/>, except it's not a part of the blueprint.
/// </summary>
/// <remarks>
/// <p>
/// The descendants of this type are added to the game object dynamically. The v1.0 game doesn't naturally support
/// dynamic components. All the base components must be defined in the blueprints at the game start. However, this type
/// of components can be created dynamically via <see cref="AutomationBehavior.GetOrCreate&lt;T&gt;"/>.
/// </p>
/// <p>New components can be created, but they cannot be destroyed!</p>
/// <p>
/// Dynamic components behave similar to BaseComponents. Some base component callback interfaces are supported out of
/// the box. For the others, the client need to add own support. The supported interfaces:
/// <see cref="IAwakableComponent"/>, <see cref="IStartableComponent"/>, <see cref="IFinishedStateListener"/>,
/// <see cref="IInitializableEntity"/>, and <see cref="IDeletableEntity"/>.
/// </p>
/// </remarks>
public abstract class AbstractDynamicComponent : IStartableComponent {
  /// <summary>The automation owner object.</summary>
  /// <remarks>Handle all <see cref="BaseComponent"/> related logic via this object.</remarks>
  public AutomationBehavior AutomationBehavior { get; private set; }

  /// <summary>Indicates if this component is enabled and should get Unity/Timberborn events.</summary>
  /// <remarks>For now, only the <see cref="IStartableComponent"/> is subject to this state.</remarks>
  /// <seealso cref="AutomationBehavior.GetOrCreate"/>
  public bool Enabled { get; private set; } = true;

  /// <summary>Indicates if this component has started.</summary>
  public bool Started { get; private set; }

  /// <summary>A Unity MonoBehaviour object. In case of Unity functionality is needed.</summary>
  protected MonoBehaviour MonoBehaviour => AutomationBehavior._componentCache;

  /// <summary>
  /// Counterpart to the <see cref="BaseComponent.EnableComponent"/>, but only affects this dynamic component.
  /// </summary>
  public virtual void EnableComponent() {
    if (Enabled) {
      return;
    }
    Enabled = true;
    if (!Started && AutomationBehavior._componentCache.StartIsEnabled) {
      Start();
    }
  }

  /// <summary>
  /// Counterpart to the <see cref="BaseComponent.DisableComponent"/>, but only affects this dynamic component.
  /// </summary>
  public virtual void DisableComponent() {
    Enabled = false;
  }

  internal void Initialize(AutomationBehavior behavior) {
    AutomationBehavior = behavior;
  }

  /// <summary>Counterpart to the Unit Start() event.</summary>
  /// <remarks>
  /// Normally, it is called when the automation behavior object is started by Unity. If teh dynamic object is being
  /// created on already started behavior, then this method is called immediately after Awake(). The component must
  /// be enabled to get the call. When component becomes enabled, the start method will be called if not called before.
  /// </remarks>
  /// <seealso cref="Started"/>
  /// <seealso cref="Enabled"/>
  public virtual void Start() {
    Started = true;
  }
}
