using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TopGil;

public class GilCharacter // TODO: why does game charactes have a unique id like retainers do? How should we handle character renaming?
{
    public Guid UniqueId { get; set; } = Guid.Empty;  // Homemade unique identifier
    public string? Name { get; set; }
    public uint HomeWorldId { get; set; }
    public uint Gil { get; set; } = 0;

    public override bool Equals(object? obj)
    {
        if (obj is not GilCharacter other)
            return false;

        return UniqueId == other.UniqueId &&
               Name == other.Name &&
               HomeWorldId == other.HomeWorldId &&
               Gil == other.Gil;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UniqueId, Name, HomeWorldId, Gil);
    }
}

/// <summary>
/// Character data
/// </summary>
public class GilCharacterWithRetainers : GilCharacter // TODO: cleanup, not using this class
{
    public List<GilRetainer> Retainers { get; set; } = new();

    /// <summary>
    /// Total gil for the character and all its retainers
    /// </summary>
    [JsonIgnore]
    public uint TotalGil
    {
        get
        {
            uint totalGil = Gil;
            foreach (var retainer in Retainers)
            {
                totalGil += retainer.Gil;
            }

            return totalGil;
        }
    }

    public GilCharacterWithRetainers()
    {
    }

    public GilCharacterWithRetainers(string name, uint homeWorldId, uint gil)
    {
        Name = name;
        HomeWorldId = homeWorldId;
        Gil = gil;
    }

    public GilCharacterWithRetainers(GilCharacter character)
    {
        Name = character.Name;
        HomeWorldId = character.HomeWorldId;
        Gil = character.Gil;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GilCharacterWithRetainers other)
            return false;

        return base.Equals(other) && // Compare base class properties
               Retainers.SequenceEqual(other.Retainers);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Retainers);
    }
}



/// <summary>
/// Retainer data
/// </summary>
public class GilRetainer
{
    public string? Name { get; set; }
    public ulong RetainerId { get; set; } // Game's internal retainer identifier
    public uint Gil { get; set; }
    public Guid OwnerCharacterId { get; set; } // -> GilCharacter.UniqueId

    public override bool Equals(object? obj)
    {
        if (obj is not GilRetainer other)
            return false;

        return Name == other.Name &&
               RetainerId == other.RetainerId &&
               Gil == other.Gil &&
               OwnerCharacterId == other.OwnerCharacterId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, RetainerId, Gil, OwnerCharacterId);
    }
}
