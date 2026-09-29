using System.Collections.Generic;
using UnityEngine;

namespace PocketDrive
{
    // Street network as nodes (intersections, corners, dead ends) joined by straight two-way edges.
    // Traffic drives on the right of each edge, laneOffset metres from its centreline.
    [CreateAssetMenu(menuName = "Pocket Drive/Road Graph", fileName = "RoadGraph")]
    public sealed class RoadGraph : ScriptableObject
    {
        public Vector3[] nodes = new Vector3[0];
        public Vector2Int[] edges = new Vector2Int[0];
        public float laneOffset = 2f;

        List<int>[] neighbours;

        public IReadOnlyList<int> Neighbours(int node)
        {
            if (neighbours == null || neighbours.Length != nodes.Length) Build();
            return neighbours[node];
        }

        public int Degree(int node) => Neighbours(node).Count;

        void Build()
        {
            neighbours = new List<int>[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) neighbours[i] = new List<int>();
            foreach (var e in edges)
            {
                neighbours[e.x].Add(e.y);
                neighbours[e.y].Add(e.x);
            }
        }

        void OnValidate() => neighbours = null;
    }
}
