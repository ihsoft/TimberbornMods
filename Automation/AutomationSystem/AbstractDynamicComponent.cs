// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>
/// A class that wants to behave as <see cref="BaseComponent"/>, except it's not a part of the blueprint.
/// </summary>
/// <remarks>
/// <p>
/// The descendants of this type are added to the game object dynamically via
/// <see cref="AutomationBehavior.GetOrCreate&lt;T&gt;"/>.
/// </p>
/// <p>New components can be created, but they cannot be destroyed!</p>
/// <p>
/// Dynamic components behave similar to BaseComponents. Some base component callback interfaces are supported out of
/// the box. For the others, the client needs to add their own support. The supported interfaces:
/// <see cref="IAwakableComponent"/>, <see cref="IFinishedStateListener"/>, <see cref="IInitializableEntity"/>,
/// <see cref="IDeletableEntity"/>, and <see cref="IPersistentEntity"/>.
/// </p>
/// </remarks>
public abstract class AbstractDynamicComponent {
  /// <summary>The automation owner object.</summary>
  /// <remarks>Handle all <see cref="BaseComponent"/> related logic via this object.</remarks>
  public AutomationBehavior AutomationBehavior { get; private set; }

  /// <summary>Indicates if this component is enabled and should get Unity/Timberborn events.</summary>
  /// <seealso cref="AutomationBehavior.GetOrCreate"/>
  public bool Enabled { get; private set; } = true;

  /// <summary>A Unity MonoBehaviour object. In the case of Unity, functionality is needed.</summary>
  protected MonoBehaviour MonoBehaviour => AutomationBehavior._componentCache;

  /// <summary>
  /// Counterpart to the <see cref="BaseComponent.EnableComponent"/>, but only affects this dynamic component.
  /// </summary>
  public virtual void EnableComponent() {
    Enabled = true;
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
}
