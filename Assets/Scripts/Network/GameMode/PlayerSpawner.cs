using UnityEngine;
using UnityEngine.AI;

namespace ElementWar.Net
{
    /// <summary>
    /// PVP 玩家生成：持有角色预制体数组（0=荧 1=芙宁娜）与出生点，按 playerId 交替分配。
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("PVP 角色预制体：0=荧 1=芙宁娜（建议用带 PlayerModel 的角色 prefab，disableStateMachine 由 NetClient 设置）")]
        public GameObject[] characterPrefabs;

        [Tooltip("旧场景出生点引用。PVP 现由服务器 Welcome 下发权威出生位置，不再读取该数组。")]
        public Transform[] spawnPoints;

        /// <summary>按 characterId 取角色预制体。</summary>
        public GameObject GetCharacterPrefab(int characterId)
        {
            if (characterPrefabs == null || characterId < 0 || characterId >= characterPrefabs.Length)
                return null;
            return characterPrefabs[characterId];
        }

        /// <summary>生成本地玩家（禁状态机由 NetClient 处理），返回其 PlayerModel。</summary>
        public PlayerModel SpawnLocal(int characterId, int playerId, Vector3 spawnPos, float spawnBodyYawDeg)
        {
            GameObject prefab = GetCharacterPrefab(characterId);
            if (prefab == null)
            {
                Debug.LogError($"[PlayerSpawner] characterId={characterId} 无预制体");
                return null;
            }
            // 角色 prefab 自带 NavMeshAgent；PVP 场景未烘焙 NavMesh，直接 Instantiate 会在 OnEnable
            // 期间刷 "no valid NavMesh"。先在失活容器中生成并关闭所有 Agent，再激活角色。
            var staging = new GameObject("PVP_LocalSpawnStaging");
            staging.SetActive(false);
            GameObject go = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, spawnBodyYawDeg, 0f), staging.transform);
            go.name = $"Local_{playerId}";
            var model = go.GetComponent<PlayerModel>();
            if (model != null) model.disableStateMachine = true;
            DisableNavMeshAgents(go);
            go.transform.SetParent(null, true);
            go.SetActive(true);
            Destroy(staging);
            return model;
        }

        private static void DisableNavMeshAgents(GameObject go)
        {
            foreach (var agent in go.GetComponentsInChildren<NavMeshAgent>(true))
                agent.enabled = false;
        }

    }
}
