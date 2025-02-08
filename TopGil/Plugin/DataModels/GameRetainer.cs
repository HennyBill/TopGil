using System;

namespace TopGil;

/// <summary>
/// Game retainer object.
/// </summary>
public class GameRetainer
{
    public string? Name { get; set; }
    public ulong RetainerId { get; set; } // Game internal retainer identifier
    public uint Gil { get; set; }
    public Guid OwnerCharacterId { get; set; } // Reference to GameCharacter.UniqueId
    public string LastVisited { get; set; } = DateTime.MinValue.ToString();

    public override bool Equals(object? obj)
    {
        if (obj is not GameRetainer other)
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
