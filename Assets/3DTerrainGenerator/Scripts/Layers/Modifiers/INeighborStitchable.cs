using UnityEngine;
using System.Collections.Generic;

namespace TerrainGenerator
{
    // Common Enum for Stitching
    public enum EdgeSide { Left, Right, Top, Bottom }

    public interface INeighborStitchable
    {
        // Return data representing the edge state on the given side.
        // Can be List<PathPoint>, float[] array of pixels, etc.
        object GetEdgeData(EdgeSide side);

        // Accept data from a neighbor to influence generation on this side.
        void InjectEdgeData(object data, EdgeSide side);
        
        // Notify if a neighbor exists on a given side (even if it has no data)
        void SetNeighborExistence(EdgeSide side, bool exists);
        
        // Clear any injected data (for clean re-runs)
        void ClearStitching();
    }

    // Common Data Packet for Path Generators
    public class PathStitchData
    {
        // We reference the type from TDPathGenerator to maintain compatibility with existing serialization
        // (If we moved PathPoint out, we'd break Unity serialization for existing prefabs)
        public List<TowerDefensePathGenerator.PathPoint> starts = new List<TowerDefensePathGenerator.PathPoint>();
        public List<TowerDefensePathGenerator.PathPoint> ends = new List<TowerDefensePathGenerator.PathPoint>();
    }
}
