using UnityEngine;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 玩家生成：持有角色预制体数组（0=荧 1=芙宁娜）与出生点，按 playerId 交替分配。
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("PVP 角色预制体：0=荧 1=芙宁娜（建议用带 PlayerModel 的角色 prefab，disableStateMachine 由 NetClient 设置）")]
        public GameObject[] characterPrefabs;

        [Tooltip("出生点（按 playerId 交替取模）")]
        public Transform[] spawnPoints;

        /// <summary>按 characterId 取角色预制体。</summary>
        public GameObject GetCharacterPrefab(int characterId)
        {
            if (characterPrefabs == null || characterId < 0 || characterId >= characterPrefabs.Length)
                return null;
            return characterPrefabs[characterId];
        }

        /// <summary>生成本地玩家（禁状态机由 NetClient 处理），返回其 PlayerModel。</summary>
        public PlayerModel SpawnLocal(int characterId, int playerId)
        {
            GameObject prefab = GetCharacterPrefab(characterId);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] characterId={characterId} 无预制体");
                return null;
            }
            Transform sp = GetSpawnPoint(playerId);
            // 与服务器 GetGroundY 对齐，避免不同角色首帧发生垂直硬校正。
            float groundY = characterId == 1 ? 0.15f : 0.025f;
            Vector3 spawnPos = new Vector3(sp.position.x, groundY, sp.position.z);
            GameObject go = Instantiate(prefab, spawnPos, sp.rotation);
            go.name = $"Local_{playerId}";
            return go.GetComponent<PlayerModel>();
        }

        private Transform GetSpawnPoint(int playerId)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
                return spawnPoints[(playerId - 1) % spawnPoints.Length];
            return transform;
        }
    }
}
