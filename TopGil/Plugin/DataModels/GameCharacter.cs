using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;

namespace TopGil;

/// <summary>
/// Game character object.
/// </summary>
[Serializable]
public class GameCharacterBase
{
    // NB! Order is for nicer visual representation in JSON files
    // Also note, the Order enumeration continues in the derived class "GameCharacter"
    [JsonProperty(Order = 1)]
    public string? Name { get; set; }

    [JsonProperty(Order = 2)]
    public Guid UniqueId { get; set; } = Guid.Empty;  // Homemade unique identifier

    [JsonProperty(Order = 3)]
    public uint HomeWorldId { get; set; }

    [JsonProperty(Order = 4)]
    public uint Gil { get; set; } = 0;

    public static GameCharacterBase CreateNew(string name, uint homeworldId, uint gil = 0)
    {
        return new GameCharacterBase
        {
            UniqueId = Guid.NewGuid(),
            Name = name,
            HomeWorldId = homeworldId,
            Gil = gil
        };
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GameCharacterBase other)
            return false;

        return UniqueId == other.UniqueId &&
               Name == other.Name &&
               HomeWorldId == other.HomeWorldId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(UniqueId, Name, HomeWorldId);
    }
}



/// <summary>
/// Game character with a list of retainers.
/// </summary>
[Serializable]
public class GameCharacter : GameCharacterBase
{
    [JsonProperty(Order = 6)]
    public List<GameRetainer> Retainers { get; set; } = new();

    [JsonProperty(Order = 5)]
    public string LastVisited { get; set; } = DateTime.MinValue.ToString();

    public static new GameCharacter CreateNew(string name, uint homeworldId, uint gil = 0)
    {
        return new GameCharacter
        {
            UniqueId = Guid.NewGuid(),
            Name = name,
            HomeWorldId = homeworldId,
            Gil = gil,
            LastVisited = NumberFormatter.FormatDateTimeToString(DateTime.Now)
        };
    }

    /// <summary>
    /// Total gil for the character and all its retainers
    /// </summary>
    [JsonIgnore]
    public long TotalGil
    {
        get
        {
            uint totalGil = Gil;

            foreach (GameRetainer retainer in Retainers)
            {
                totalGil += retainer.Gil;
            }

            return totalGil;
        }
    }

    public GameCharacter()
    {
    }

    //public GameCharacter(string name, uint homeWorldId, uint gil)
    //{
    //    Name = name;
    //    HomeWorldId = homeWorldId;
    //    Gil = gil;
    //}

    //public GameCharacter(GilCharacter character)
    //{
    //    Name = character.Name;
    //    HomeWorldId = character.HomeWorldId;
    //    Gil = character.Gil;
    //}

    public override bool Equals(object? obj)
    {
        if (obj is not GameCharacter other)
            return false;

        return base.Equals(other) && // Compare base class properties
               Retainers.SequenceEqual(other.Retainers);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Retainers);
    }
}
