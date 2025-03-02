using CounterStrikeSharp.API.Core.Capabilities;
using MAULActainShared.plugin;

namespace SharpTimer.Mixins;

public class Mixin {
  public static PluginCapability<IActain> ActainCapability { get; } =
    new("maulactain:core");

  public static IActain? Actain {
    get {
      try { return ActainCapability.Get(); } catch (KeyNotFoundException) {
        return null;
      }
    }
  }
}