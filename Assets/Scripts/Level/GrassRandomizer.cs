using UnityEngine;
using UnityEngine.Tilemaps;

public class GrassRandomizer : MonoBehaviour
{
    public Tilemap groundTilemap;
    public GameObject[] grassPrefabs;
    [Range(0f, 1f)] public float density = 0.5f;
    public int seed = 12345;
    public Transform parent;

    [ContextMenu("Scatter Grass")]
    void Scatter()
    {
        Random.InitState(seed);

        foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(pos)) continue;

            var above = pos + Vector3Int.up;
            if (groundTilemap.HasTile(above)) continue;

            if (Random.value > density) continue;

            var prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
            var worldPos = groundTilemap.GetCellCenterWorld(above);
            worldPos.y = groundTilemap.CellToWorld(above).y;

#if UNITY_EDITOR
            var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = worldPos;
#endif
        }
    }

    [ContextMenu("Clear Scattered Grass")]
    void Clear()
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);
    }
}