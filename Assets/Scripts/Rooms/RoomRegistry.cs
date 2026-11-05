using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomRegistry", menuName = "Project Spear/Rooms/Room Registry")]
public class RoomRegistry : ScriptableObject
{
    [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

    public IReadOnlyList<RoomDefinition> Rooms => rooms;

#if UNITY_EDITOR
    public bool Add(RoomDefinition definition)
    {
        if (definition == null || rooms.Contains(definition))
            return false;

        rooms.Add(definition);
        return true;
    }

    public bool Remove(RoomDefinition definition)
    {
        return rooms.Remove(definition);
    }
#endif
}
