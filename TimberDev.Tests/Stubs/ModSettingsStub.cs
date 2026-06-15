using System;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityEngine;

namespace ModSettings.Core {
  [Flags]
  public enum ModSettingsContext {
    MainMenu = 1,
    Game = 2,
  }

  public class ModSetting<T> where T : notnull {
    public event EventHandler ValueChanged;

    public T Value { get; private set; }
    public ModSettingDescriptor Descriptor { get; }

    public ModSetting(T defaultValue, ModSettingDescriptor descriptor) {
      Value = defaultValue;
      Descriptor = descriptor;
    }

    public void SetValue(T value) {
      Value = value;
      ValueChanged?.Invoke(this, EventArgs.Empty);
    }
  }

  public sealed class ModSettingDescriptor {
    public string LocKey { get; private set; }
    public string TooltipLocKey { get; private set; }

    public static ModSettingDescriptor CreateLocalized(string locKey) {
      return new ModSettingDescriptor { LocKey = locKey };
    }

    public ModSettingDescriptor SetLocalizedTooltip(string locKey) {
      TooltipLocKey = locKey;
      return this;
    }
  }

  public sealed class ModSettingsOwnerRegistry {
  }

  public abstract class ModSettingsOwner {
    protected abstract string ModId { get; }
    public abstract string HeaderLocKey { get; }
    public abstract int Order { get; }
    public abstract ModSettingsContext ChangeableOn { get; }

    protected ModSettingsOwner(
        ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository) {
    }

    protected virtual void OnAfterLoad() {
    }
  }
}

namespace ModSettings.Common {
  public sealed class ColorModSetting {
    public event EventHandler ValueChanged;

    public Color Color { get; private set; }

    public ColorModSetting(Color defaultValue) {
      Color = defaultValue;
    }

    public void SetColor(Color color) {
      Color = color;
      ValueChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.Modding {
  public sealed class ModRepository {
  }
}

namespace Timberborn.SettingsSystem {
  public interface ISettings {
  }

  public sealed class TestSettings : ISettings {
  }
}
