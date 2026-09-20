using UnityEngine;
using UnityEngine.Tilemaps;

public class GrassDresser : MonoBehaviour
{
    public Tilemap groundTilemap;
    public Tilemap grassTilemap;
    public TileBase[] grassVariants;
    [Range(0f, 1f)] public float density = 0.6f;
    public int seed = 12345;

    [ContextMenu("Dress Grass")]
    void Dress()
    {
        grassTilemap.ClearAllTiles();
        Random.InitState(seed);

        foreach (var pos in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(pos)) continue;

            var above = pos + Vector3Int.up;
            if (groundTilemap.HasTile(above)) continue;

            if (Random.value > density) continue;

            var tile = grassVariants[Random.Range(0, grassVariants.Length)];
            grassTilemap.SetTile(above, tile);
        }
    }

    [ContextMenu("Clear Grass")]
    void Clear() => grassTilemap.ClearAllTiles();
}