// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.DependencyContainerSystem;
using TimberApi.ToolGroupSystem;
using TimberApi.ToolSystem;
using Timberborn.ConstructionMode;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.ToolSystem;
using ToolGroupSpecification = TimberApi.ToolGroupSystem.ToolGroupSpecification;

namespace Automation.Utils {

/// <summary>System that manages bindings to support TimberAPI tools and groups specifications.</summary>
/// <remarks>Use this system to keep code short and clear when no fancy setups are needed.</remarks>
/// <example>
/// Define a tool/group specification as explained in the TimberAPI docs. Then, bind the specification(s):
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
    public string Id => _specification.Id;
    public string GroupId => _specification.GroupId;
    public int Order => _specification.Order;
    public string Section => _specification.Section;
    public bool DevMode => _specification.DevMode;
    #endregion

    ToolGroupSpecification _specification;

    /// <summary>Initializes the tool group. Do all logic here instead of the constructor.</summary>
    protected virtual void Initialize() {
    }

    internal void InitializeGroup(ToolGroupSpecification specification) {
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
    protected ToolSpecification ToolSpecification { get; private set; }

    /// <summary>Parsed tool information from the specification.</summary>
    /// <remarks>
    /// The actual type of the tool information class is what was passed during the tool registration in
    /// <see cref="CustomToolSystem.BindTool{TTool, TInfo}"/>. The descendant class can safely do the upcast.
    /// </remarks>
    /// <value>
    /// The tool information or <c>null</c> if the tool was registered via
    /// <see cref="CustomToolSystem.BindTool{TTool}"/>.
    /// </value>
    protected ToolInformation ToolInformation { get; private set; }

    protected ILoc Loc  { get; private set; }

    /// <summary>Initializes the tool. Do all logic here instead of the constructor.</summary>
    protected virtual void Initialize() {
    }
    #endregion

    #region Implementation
    [Inject]
    public void InjectDependencies(ILoc loc) {
      Loc = loc;
    }

    internal void InitializeTool(ToolGroup toolGroup, ToolSpecification toolSpecification,
                                 ToolInformation toolInformation = null) {
      ToolGroup = toolGroup;
      ToolSpecification = toolSpecification;
      ToolInformation = toolInformation;
      Initialize();
    }
    #endregion
  }

  /// <summary>Class base for the tool information classes.</summary>
  /// <seealso cref="CustomToolSystem.BindTool{TTool, TInfo}"/>
  public abstract class ToolInformation {
    public abstract void Load(IObjectLoader objectLoader);
  }
  #region API

  /// <summary>Registers a simple tool group that just contains other tools.</summary>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="groupTypeName">The tool group type as specified in the TimberAPI specification.</param>
  /// <seealso cref="CustomToolGroup"/>
  public static void BindGroupTrivial(IContainerDefinition containerDefinition, string groupTypeName) {
    containerDefinition.MultiBind<IToolGroupFactory>().ToInstance(new ToolGroupFactory<CustomToolGroup>(groupTypeName));
  }

  /// <summary>Registers a tool group that enables constructions mode when opened.</summary>
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
  /// <p>Call this method from the configurator to define the tool groups of your mod. Each tool class can be bound only
  /// once, or an exception will be thrown.</p>
  /// <p>The registered class will be created via Bindito. Implement a method, attributed with <c>[Inject]</c>, to have
  /// extra injections provided.</p>
  /// </remarks>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="groupTypeName">
  /// The tool group type as specified in the TimberAPI specification. Can be omitted, in which case the class full name
  /// will be used. The same name cannot be bound to different classes.
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
  /// be used. The same name cannot be bound to different classes.
  /// </param>
  /// <typeparam name="TTool">the class that implements the tool.</typeparam>
  public static void BindTool<TTool>(IContainerDefinition containerDefinition, string typeName = null)
      where TTool : CustomTool {
    containerDefinition.Bind<TTool>().AsTransient();
    containerDefinition.MultiBind<IToolFactory>().ToInstance(new ToolFactory<TTool>(typeName ?? typeof(TTool).FullName));
  }

  /// <summary>Registers a custom tool with tool information.</summary>
  /// <remarks>
  /// <p>Call this method from the configurator to define the tools of your mod. Each tool class can be bound only once,
  /// or an exception will be thrown.</p>
  /// <p>The registered class will be created via Bindito. Implement a method, attributed with <c>[Inject]</c>, to have
  /// extra injections provided.</p>
  /// </remarks>
  /// <param name="containerDefinition">The configurator interface.</param>
  /// <param name="typeName">
  /// The tool type as specified in the TimberAPI specification. Can be omitted, in which case the class full name will
  /// be used. The same name cannot be bound to different classes.
  /// </param>
  /// <typeparam name="TTool">the class that implements the tool.</typeparam>
  /// <typeparam name="TInfo">tha class that implements holds the tool information</typeparam>
  public static void BindTool<TTool, TInfo>(IContainerDefinition containerDefinition, string typeName = null)
      where TTool : CustomTool where TInfo : ToolInformation, new() {
    containerDefinition.Bind<TTool>().AsTransient();
    containerDefinition.MultiBind<IToolFactory>()
        .ToInstance(new ToolFactory<TTool, TInfo>(typeName ?? typeof(TTool).FullName));
  }
  #endregion

  #region Implementation
  sealed class ToolGroupFactory<TToolGroup> : IToolGroupFactory where TToolGroup : CustomToolGroup {
    public string Id { get; }

    public ToolGroupFactory(string id) {
      Id = id;
    }

    public IToolGroup Create(ToolGroupSpecification toolGroupSpecification) {
      CustomToolGroup group;
      if (typeof(TToolGroup) == typeof(CustomToolGroup)) {
        group = new CustomToolGroup();
      } else if (typeof(TToolGroup) == typeof(CustomToolGroupWithConstructionModeEnabler)) {
        group = new CustomToolGroupWithConstructionModeEnabler();
      } else {
        group = DependencyContainer.GetInstance<TToolGroup>();
      }
      group.InitializeGroup(toolGroupSpecification);
      return group;
    }
  }

  sealed class ToolFactory<TTool> : IToolFactory where TTool : CustomTool {
    public string Id { get; }
    
    public ToolFactory(string id) {
      Id = id;
    }

    public Tool Create(ToolSpecification toolSpecification, ToolGroup toolGroup = null) {
      var tool = DependencyContainer.GetInstance<TTool>();
      tool.InitializeTool(toolGroup, toolSpecification);
      return tool;
    }
  }

  sealed class ToolFactory<TTool, TInfo> : IToolFactory where TTool : CustomTool where TInfo : ToolInformation, new() {
    public string Id { get; }

    public ToolFactory(string id) {
      Id = id;
    }

    public Tool Create(ToolSpecification toolSpecification, ToolGroup toolGroup = null) {
      var toolInformation = new TInfo();
      toolInformation.Load(ObjectLoader.CreateBasicLoader(toolSpecification.ToolInformation));
      var tool = DependencyContainer.GetInstance<TTool>();
      tool.InitializeTool(toolGroup, toolSpecification, toolInformation: toolInformation);
      return tool;
    }
  }
  #endregion
}

}
