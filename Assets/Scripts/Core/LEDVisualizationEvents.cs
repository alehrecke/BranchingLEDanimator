using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Event system for LED visualization components to communicate
    /// </summary>
    public static class LEDVisualizationEvents
    {
        /// <summary>
        /// Called when LED colors are updated by animation system
        /// </summary>
        public static System.Action<Color[]> OnColorsUpdated;
        
        /// <summary>
        /// Called when geometry data is imported or changed
        /// </summary>
        public static System.Action<List<Vector3>, List<Vector2Int>, List<int>> OnGeometryUpdated;
        
        /// <summary>
        /// Called when animation type changes
        /// </summary>
        public static System.Action<string> OnAnimationChanged;
        
        /// <summary>
        /// Called when animation starts/stops
        /// </summary>
        public static System.Action<bool> OnAnimationPlayStateChanged;
        
        /// <summary>
        /// Trigger color update event
        /// </summary>
        public static void TriggerColorsUpdated(Color[] colors)
        {
            OnColorsUpdated?.Invoke(colors);
        }
        
        /// <summary>
        /// Trigger geometry update event
        /// </summary>
        public static void TriggerGeometryUpdated(List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            OnGeometryUpdated?.Invoke(nodePositions, edgeConnections, sourceNodes);
        }
        
        /// <summary>
        /// Trigger animation change event
        /// </summary>
        public static void TriggerAnimationChanged(string animationName)
        {
            OnAnimationChanged?.Invoke(animationName);
        }
        
        /// <summary>
        /// Trigger animation play state change event
        /// </summary>
        public static void TriggerAnimationPlayStateChanged(bool isPlaying)
        {
            OnAnimationPlayStateChanged?.Invoke(isPlaying);
        }
    }
}
