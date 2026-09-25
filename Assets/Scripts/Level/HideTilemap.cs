using UnityEngine;
using UnityEngine.Tilemaps;

public class HideTilemap : MonoBehaviour
{
    void Start()
    {
        Tilemap tilemap = GetComponent<Tilemap>(); if (tilemap != null)
        {
            Color color = tilemap.color;
            color.a = 0f;
            tilemap.color = color;
        }
    }
}