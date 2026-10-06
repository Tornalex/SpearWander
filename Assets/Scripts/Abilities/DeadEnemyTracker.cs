using System.Collections.Generic;

namespace SpearWander.Abilities
{
    public static class DeadEnemyTracker
    {
        private static readonly Dictionary<string, HashSet<string>> _deadEnemies = new();

        public static void MarkDead(string roomName, string stableIdentity)
        {
            if (!_deadEnemies.ContainsKey(roomName))
                _deadEnemies[roomName] = new HashSet<string>();
            _deadEnemies[roomName].Add(stableIdentity);
        }

        public static bool IsDead(string roomName, string stableIdentity)
        {
            return _deadEnemies.TryGetValue(roomName, out var set) && set.Contains(stableIdentity);
        }

        public static void ClearRoom(string roomName)
        {
            _deadEnemies.Remove(roomName);
        }

        public static void ClearAll()
        {
            _deadEnemies.Clear();
        }
    }
}
