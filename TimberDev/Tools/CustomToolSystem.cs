// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.DependencyContainerSystem;
using TimberApi.Tools.ToolGroupSystem;
using TimberApi.Tools.ToolSystem;
using Timberborn.ConstructionMode;
using Timberborn.Localization;
using Timberborn.ToolSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Tools;

/// <summary>System that manages bindings to support TimberAPI tools and groups specifications.</summary>
/// <remarks>Use this system to keep code short and clear when no fancy setups are needed.</remarks>
/// <example>
/// Define a tool/group specification as explained in the TimberAPI docs. Then, bind the specification:
/// <code>
/// [Configurator(SceneEntrypoint.InGame)]
/// class Configurator : IConfigurator {
///   public void Configure(IContainerDefinition containerDefinition) {
///     CustomToolSystem.BindGroupWithConstructionModeEnabler(containerDefinition, "MyGroup");
///     CustomToolSystem.BindTool&lt;MyTool1>(containerDefinition);
///     CustomToolSystem.BindTool&lt;MyTool2>(containerDefinition, "CustomTypeName");
///   }
/// }
/// </code>
/// </example>
public static class CustomToolSystem {

  /// <summary>Base class for all custom tool groups.</summary>
  public class CustomToolGroup : ToolGroup, IToolGroup {

    #region IToolGroup implementation

    /// <summary>The unique ID of the tool. It is defined in the specification.</summary>
    public string Id => _specification.Id;

    /// <summary>The tool group ID to attach this tool to. It is defined in the specification.</summary>
    public string GroupId => _specification.GroupId;

    /// <summary>The tool order in the tool group. It is defined in the specification.</summary>
    public int Order => _specification.Order;

    /// <summary>The tool's section (whatever it is). It is defined in the specification.</summary>
    public string Section => _specification.Section;

    /// <summary>
    /// Indicates if the tool must only be available in the dev mode. It is defined in the specification.
    /// </summary>
    public bool DevMode => _specification.DevMode;

    #endregion

    TimberApiToolGroupSpec _specification;

    /// <summary>Initializes the tool group. Do all logic here instead of the constructor.</summary>
    protected virtual void Initialize() {
    }

    internal void InitializeGroup(TimberApiToolGroupSpec specification) {
      Icon = specification.Icon;
      DisplayNameLocKey = specification.NameLocKey;
      _specification = specification;
      Initialize();
    }
  }

  /// <summary>Base class for all tool groups that need construction mode enabled.</summary>
  public class CustomToolGroupWithConstructionModeEnabler : CustomToolGroup, IConstructionModeEnabler {
  }

  /// <summary>Base class for all custom tools.</summary>
  public abstract class CustomTool : Tool {

    #region API

    /// <summary>TimberAPI tool specification.</summary>
    protected ToolSpec ToolSpec { get; private set; }

    /// <summary>Shortcut to <see cref="ILoc"/>.</summary>
    protected ILoc Loc { get; private set; }

    /// <summary>Initializes the tool. Do all logic here instead of the constructor.</summary>
    protected virtual void Initialize() {
    }

    #endregion

    #region Tool implementation

    /// <inheritdoc/>
    public override bool DevModeTool => ToolSpec.DevMode;

    #endregion

    #region Implementation

    /// <summary>Injects the dependencies. It has to be public to work.</summary>
    [Inject]
    public void InjectDependencies(ILoc loc) {
      Loc = loc;
    }

    internal void InitializeTool(ToolGroup toolGroup, ToolSpec toolSpec) {
      ToolGroup = toolGroup;
      ToolSpec = toolSpec;
      Initialize();
    }
    #endregion
  }

  #region API

  /// <summary>Registers a simple tool group that just contains other tools.</summary>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="groupTypeName">The tool group type as specified in the TimberAPI specification.</param>
  /// <seealso cref="CustomToolGroup"/>
  public static void BindGroupTrivial(IContainerDefinition containerDefinition, string groupTypeName) {
    containerDefinition.MultiBind<IToolGroupFactory>().ToInstance(new ToolGroupFactory<CustomToolGroup>(groupTypeName));
  }

  /// <summary>Registers a tool group that enables construction mode when opened.</summary>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="groupTypeName">The tool group type as specified in the TimberAPI specification.</param>
  /// <seealso cref="CustomToolGroupWithConstructionModeEnabler"/>
  public static void BindGroupWithConstructionModeEnabler(IContainerDefinition containerDefinition,
                                                          string groupTypeName) {
    containerDefinition.MultiBind<IToolGroupFactory>()
        .ToInstance(new ToolGroupFactory<CustomToolGroupWithConstructionModeEnabler>(groupTypeName));
  }

  /// <summary>Registers an arbitrary class as a tool group.</summary>
  /// <remarks>
  /// <p>
  /// Call this method from the configurator to define the tool groups of your mod. Each tool class can be bound only
  /// once, or an exception will be thrown.
  /// </p>
  /// <p>
  /// The registered class will be created via Bindito. Implement a method, attributed with <c>[Inject]</c>, to have
  /// extra injections provided.
  /// </p>
  /// </remarks>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="groupTypeName">
  /// The tool group type as specified in the TimberAPI specification. Can be omitted, in which case the class full name
  /// will be used. The same name can't be bound to different classes.
  /// </param>
  /// <typeparam name="TToolGroup">the class that implements the tool group.</typeparam>
  /// <seealso cref="CustomToolGroup"/>
  /// <seealso cref="CustomToolGroupWithConstructionModeEnabler"/>
  public static void BindToolGroup<TToolGroup>(IContainerDefinition containerDefinition, string groupTypeName = null)
      where TToolGroup : CustomToolGroup {
    containerDefinition.Bind<TToolGroup>().AsTransient();
    containerDefinition.MultiBind<IToolGroupFactory>()
        .ToInstance(new ToolGroupFactory<TToolGroup>(groupTypeName ?? typeof(TToolGroup).FullName));
  }

  /// <summary>Registers a custom tool.</summary>
  /// <remarks>
  /// <p>Call this method from the configurator to define the tools of your mod. Each tool class can be bound only once,
  /// or an exception will be thrown.</p>
  /// <p>The registered class will be created via Bindito. Implement a method, attributed with <c>[Inject]</c>, to have
  /// extra injections provided.</p>
  /// </remarks>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="typeName">
  /// The tool type as specified in the TimberAPI specification. Can be omitted, in which case the class full name will
  /// be used. The same name can't be bound to different classes.
  /// </param>
  /// <typeparam name="TTool">the class that implements the tool.</typeparam>
  public static void BindTool<TTool>(IContainerDefinition containerDefinition, string typeName = null)
      where TTool : CustomTool {
    containerDefinition.Bind<TTool>().AsTransient();
    containerDefinition.MultiBind<IToolFactory>().ToInstance(new ToolFactory<TTool>(typeName ?? typeof(TTool).FullName));
  }

  #endregion

  #region Implementation

  sealed class ToolGroupFactory<TToolGroup>(string id) : IToolGroupFactory
      where TToolGroup : CustomToolGroup {
    public string Id { get; } = id;

    public IToolGroup Create(TimberApiToolGroupSpec toolGroupSpecification) {
      CustomToolGroup group;
      if (typeof(TToolGroup) == typeof(CustomToolGroup)) {
        group = new CustomToolGroup();
      } else if (typeof(TToolGroup) == typeof(CustomToolGroupWithConstructionModeEnabler)) {
        group = new CustomToolGroupWithConstructionModeEnabler();
      } else {
        group = StaticBindings.DependencyContainer.GetInstance<TToolGroup>();
      }
      group.InitializeGroup(toolGroupSpecification);
      return group;
    }
  }

  sealed class ToolFactory<TTool>(string id) : IToolFactory
      where TTool : CustomTool {
    public string Id { get; } = id;

    public Tool Create(ToolSpec toolSpec, ToolGroup toolGroup = null) {
      var tool = DependencyContainer.GetInstance<TTool>();
      tool.InitializeTool(toolGroup, toolSpec);
      return tool;
    }
  }

  #endregion
}
