using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Event system for LED visualization components to communicate.
    /// Every event carries the source LEDGraphManager so subscribers can
    /// filter and only react to their own graph in multi-graph scenes.
    /// </summary>
    public static class LEDVisualizationEvents
    {
        public static System.Action<LEDGraphManager, Color[]> OnColorsUpdated;
        public static System.Action<LEDGraphManager, List<Vector3>, List<Vector2Int>, List<int>> OnGeometryUpdated;
        public static System.Action<string> OnAnimationChanged;
        public static System.Action<bool> OnAnimationPlayStateChanged;

        private static void SafeInvoke<T1, T2>(ref System.Action<T1, T2> eventField, T1 a1, T2 a2)
        {
            if (eventField == null) return;
            foreach (var d in eventField.GetInvocationList())
            {
                try
                {
                    ((System.Action<T1, T2>)d)(a1, a2);
                }
                catch (System.MissingMemberException) { eventField -= (System.Action<T1, T2>)d; }
                catch (MissingReferenceException) { eventField -= (System.Action<T1, T2>)d; }
            }
        }

        private static void SafeInvoke<T>(ref System.Action<T> eventField, T arg)
        {
            if (eventField == null) return;
            foreach (var d in eventField.GetInvocationList())
            {
                try
                {
                    ((System.Action<T>)d)(arg);
                }
                catch (System.MissingMemberException) { eventField -= (System.Action<T>)d; }
                catch (MissingReferenceException) { eventField -= (System.Action<T>)d; }
            }
        }

        private static void SafeInvoke<T1, T2, T3, T4>(
            ref System.Action<T1, T2, T3, T4> eventField, T1 a1, T2 a2, T3 a3, T4 a4)
        {
            if (eventField == null) return;
            foreach (var d in eventField.GetInvocationList())
            {
                try
                {
                    ((System.Action<T1, T2, T3, T4>)d)(a1, a2, a3, a4);
                }
                catch (System.MissingMemberException) { eventField -= (System.Action<T1, T2, T3, T4>)d; }
                catch (MissingReferenceException) { eventField -= (System.Action<T1, T2, T3, T4>)d; }
            }
        }

        public static void TriggerColorsUpdated(LEDGraphManager source, Color[] colors)
        {
            SafeInvoke(ref OnColorsUpdated, source, colors);
        }
        
        public static void TriggerGeometryUpdated(LEDGraphManager source, List<Vector3> nodePositions, List<Vector2Int> edgeConnections, List<int> sourceNodes)
        {
            SafeInvoke(ref OnGeometryUpdated, source, nodePositions, edgeConnections, sourceNodes);
        }
        
        public static void TriggerAnimationChanged(string animationName)
        {
            SafeInvoke(ref OnAnimationChanged, animationName);
        }
        
        public static void TriggerAnimationPlayStateChanged(bool isPlaying)
        {
            SafeInvoke(ref OnAnimationPlayStateChanged, isPlaying);
        }
    }
}
