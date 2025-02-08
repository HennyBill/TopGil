using Dalamud.Configuration;

using System;
using System.Collections.Generic;

namespace TopGil;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 100;

    public bool DebugEnabled { get; set; } = false;
    public string LastUpdate { get; set; } = DateTime.MinValue.ToString();


    public List<GameCharacter> Characters { get; set; } = new();


    public void Save()
    {
        LastUpdate = NumberFormatter.FormatDateTimeToString(DateTime.Now);

        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

