using UnityEngine;
using System.Collections.Generic;

public class TileInfo : MonoBehaviour
{
    // This dictionary will store direct references to the GameObjects of adjacent walls.
    public Dictionary<Direction, GameObject> WallObjects = new Dictionary<Direction, GameObject>();
}