using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace TopGil;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 100;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;


    //public ulong TimerDelayMS { get; set; } = 60000;
    public bool DebugEnabled { get; set; } = false;
    public DateTime LastUpdate { get; set; } = DateTime.MinValue;

    public List<GilCharacterWithRetainers> Characters { get; set; } = new();


    // the below exist just to make saving less cumbersome
    public void Save()
    {
        LastUpdate = DateTime.Now;

        Plugin.PluginInterface.SavePluginConfig(this);
    }

    // Static method to load the configuration
    //public static Configuration Load()
    //{
    //    var config = Plugin.PluginInterface.GetPluginConfig() as Configuration;
    //    if (config == null)
    //    {
    //        config = new Configuration();
    //        config.Save();
    //    }
    //    return config;
    //}
}

