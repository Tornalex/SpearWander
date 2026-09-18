using UnityEngine;
using System.Collections.Generic;

namespace SpearWander.Abilities
{
    public static class DeadEnemyTracker
    {
        private static Dictionary<string, HashSet<int>> _deadEnemies = new Dictionary<string, HashSet<int>>();
        private static Dictionary<string, int> _roomEnemyCounts = new Dictionary<string, int>();

        public static void RegisterRoom(string roomName)
        {
            if (_roomEnemyCounts.ContainsKey(roomName)) 
            {
                return;
            }

            var enemies = Object.FindObjectsByType<BaseEnemy>(FindObjectsInactive.Exclude);
            int count = 0;
            foreach (var enemy in enemies)
            {
                enemy.enemyIndex = count;
                enemy.roomName = roomName;
                count++;
            }
            _roomEnemyCounts[roomName] = count;
        }

        public static void MarkDead(string roomName, int index)
        {
            if (!_deadEnemies.ContainsKey(roomName))
                _deadEnemies[roomName] = new HashSet<int>();
            _deadEnemies[roomName].Add(index);
        }

        public static bool IsDead(string roomName, int index)
        {
            return _deadEnemies.TryGetValue(roomName, out var set) && set.Contains(index);
        }

        public static void ClearRoom(string roomName)
        {
            _deadEnemies.Remove(roomName);
            _roomEnemyCounts.Remove(roomName);
        }

        public static void ClearAll()
        {
            _deadEnemies.Clear();
            _roomEnemyCounts.Clear();
        }
    }
}