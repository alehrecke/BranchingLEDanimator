using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simulates multiple people interacting with the Interactive Pan animation.
    /// Creates realistic touch patterns with varying hold durations and movement.
    /// </summary>
    public class InteractivePanSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LEDGraphManager graphManager;
        [SerializeField] private InteractivePanAnimation targetAnimation;
        
        [Header("Simulation Settings")]
        [Tooltip("Number of simulated people")]
        [Range(1, 10)]
        [SerializeField] private int numberOfPeople = 3;
        
        [Tooltip("Enable automatic simulation")]
        [SerializeField] private bool autoSimulate = true;
        
        [Header("Touch Behavior")]
        [Tooltip("Minimum time a person holds an endpoint")]
        [Range(0.5f, 10f)]
        [SerializeField] private float minHoldDuration = 1f;
        
        [Tooltip("Maximum time a person holds an endpoint")]
        [Range(1f, 20f)]
        [SerializeField] private float maxHoldDuration = 5f;
        
        [Tooltip("Minimum pause between touches")]
        [Range(0f, 5f)]
        [SerializeField] private float minPauseDuration = 0.2f;
        
        [Tooltip("Maximum pause between touches")]
        [Range(0.5f, 10f)]
        [SerializeField] private float maxPauseDuration = 2f;
        
        [Tooltip("Chance that a person moves to an adjacent endpoint vs random")]
        [Range(0f, 1f)]
        [SerializeField] private float adjacentMoveProbability = 0.6f;
        
        [Header("Group Behavior")]
        [Tooltip("Chance people will cluster on same side")]
        [Range(0f, 1f)]
        [SerializeField] private float clusterProbability = 0.3f;
        
        [Tooltip("Chance of synchronized touches (people touching at same time)")]
        [Range(0f, 1f)]
        [SerializeField] private float synchronizedTouchProbability = 0.2f;
        
        public enum InteractionStyle
        {
            Random,           // Completely random behavior
            Exploratory,      // People wander and explore
            Collaborative,    // People tend to touch same areas
            Oppositional,     // People touch opposite sides
            Wave              // People create wave-like patterns
        }
        
        [Header("Interaction Style")]
        [SerializeField] private InteractionStyle interactionStyle = InteractionStyle.Random;
        
        // Simulated person state
        private class SimulatedPerson
        {
            public int personId;
            public int currentEndpoint = -1;
            public bool isTouching = false;
            public float touchEndTime;
            public float nextTouchTime;
            public int lastEndpoint = -1;
            public float preferredAngle; // For wave/directional patterns
        }
        
        private List<SimulatedPerson> people = new List<SimulatedPerson>();
        private List<int> availableEndpoints = new List<int>();
        private Dictionary<int, List<int>> endpointAdjacency = new Dictionary<int, List<int>>();
        private Vector3 graphCenter;
        private bool initialized = false;
        
        void Start()
        {
            if (graphManager == null)
                graphManager = FindFirstObjectByType<LEDGraphManager>();
                
            if (targetAnimation == null)
                targetAnimation = FindFirstObjectByType<LEDAnimationSystem>()?.CurrentAnimation as InteractivePanAnimation;
        }
        
        void Update()
        {
            if (!autoSimulate) return;
            
            if (!initialized && graphManager != null && graphManager.DataLoaded)
            {
                Initialize();
            }
            
            if (initialized)
            {
                UpdateSimulation();
            }
        }
        
        private void Initialize()
        {
            // Find endpoints from graph
            availableEndpoints.Clear();
            endpointAdjacency.Clear();
            
            var positions = graphManager.NodePositions;
            var edges = graphManager.EdgeConnections;
            
            if (positions == null || positions.Count == 0) return;
            
            // Build adjacency
            Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
            foreach (var edge in edges)
            {
                if (!adjacency.ContainsKey(edge.x))
                    adjacency[edge.x] = new List<int>();
                if (!adjacency.ContainsKey(edge.y))
                    adjacency[edge.y] = new List<int>();
                
                adjacency[edge.x].Add(edge.y);
                adjacency[edge.y].Add(edge.x);
            }
            
            // Find endpoints (nodes with one connection)
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    availableEndpoints.Add(kvp.Key);
                    endpointAdjacency[kvp.Key] = kvp.Value;
                }
            }
            
            if (availableEndpoints.Count == 0) return;
            
            // Calculate center
            Vector3 sum = Vector3.zero;
            foreach (int ep in availableEndpoints)
            {
                sum += positions[ep];
            }
            graphCenter = sum / availableEndpoints.Count;
            
            // Initialize people
            people.Clear();
            for (int i = 0; i < numberOfPeople; i++)
            {
                var person = new SimulatedPerson
                {
                    personId = i,
                    currentEndpoint = -1,
                    isTouching = false,
                    nextTouchTime = Time.time + Random.Range(0f, maxPauseDuration),
                    preferredAngle = (float)i / numberOfPeople * 360f // Spread people around for wave mode
                };
                people.Add(person);
            }
            
            initialized = true;
        }
        
        private void UpdateSimulation()
        {
            float currentTime = Time.time;
            
            foreach (var person in people)
            {
                if (person.isTouching)
                {
                    // Check if should release
                    if (currentTime >= person.touchEndTime)
                    {
                        ReleaseTouchForPerson(person);
                    }
                }
                else
                {
                    // Check if should start touching
                    if (currentTime >= person.nextTouchTime)
                    {
                        StartTouchForPerson(person);
                    }
                }
            }
        }
        
        private void StartTouchForPerson(SimulatedPerson person)
        {
            int endpoint = ChooseEndpointForPerson(person);
            if (endpoint < 0) return;
            
            person.currentEndpoint = endpoint;
            person.isTouching = true;
            person.touchEndTime = Time.time + Random.Range(minHoldDuration, maxHoldDuration);
            
            // Fire the touch event using the static helper
            Vector3 position = graphManager.NodePositions[endpoint];
            GraphPlayerController.SimulatePress(graphManager, endpoint, position);
        }
        
        private void ReleaseTouchForPerson(SimulatedPerson person)
        {
            if (person.currentEndpoint >= 0)
            {
                Vector3 position = graphManager.NodePositions[person.currentEndpoint];
                GraphPlayerController.SimulateRelease(graphManager, person.currentEndpoint, position);
                
                person.lastEndpoint = person.currentEndpoint;
                person.currentEndpoint = -1;
            }
            
            person.isTouching = false;
            person.nextTouchTime = Time.time + Random.Range(minPauseDuration, maxPauseDuration);
            
            // Check for synchronized touch
            if (Random.value < synchronizedTouchProbability)
            {
                // Find another person who's about to touch and sync them
                foreach (var other in people)
                {
                    if (other != person && !other.isTouching && other.nextTouchTime > Time.time)
                    {
                        other.nextTouchTime = person.nextTouchTime;
                        break;
                    }
                }
            }
        }
        
        private int ChooseEndpointForPerson(SimulatedPerson person)
        {
            if (availableEndpoints.Count == 0) return -1;
            
            var positions = graphManager.NodePositions;
            
            switch (interactionStyle)
            {
                case InteractionStyle.Exploratory:
                    return ChooseExploratoryEndpoint(person);
                    
                case InteractionStyle.Collaborative:
                    return ChooseCollaborativeEndpoint(person);
                    
                case InteractionStyle.Oppositional:
                    return ChooseOppositionalEndpoint(person);
                    
                case InteractionStyle.Wave:
                    return ChooseWaveEndpoint(person);
                    
                case InteractionStyle.Random:
                default:
                    return ChooseRandomEndpoint(person);
            }
        }
        
        private int ChooseRandomEndpoint(SimulatedPerson person)
        {
            // Try adjacent first
            if (person.lastEndpoint >= 0 && Random.value < adjacentMoveProbability)
            {
                int adjacent = FindAdjacentEndpoint(person.lastEndpoint);
                if (adjacent >= 0) return adjacent;
            }
            
            // Random endpoint
            return availableEndpoints[Random.Range(0, availableEndpoints.Count)];
        }
        
        private int ChooseExploratoryEndpoint(SimulatedPerson person)
        {
            // Prefer endpoints they haven't visited recently
            // For simplicity, just avoid last endpoint and prefer adjacent
            if (person.lastEndpoint >= 0 && Random.value < adjacentMoveProbability)
            {
                int adjacent = FindAdjacentEndpoint(person.lastEndpoint);
                if (adjacent >= 0 && adjacent != person.lastEndpoint) return adjacent;
            }
            
            // Pick random different endpoint
            int attempts = 0;
            while (attempts < 10)
            {
                int ep = availableEndpoints[Random.Range(0, availableEndpoints.Count)];
                if (ep != person.lastEndpoint) return ep;
                attempts++;
            }
            
            return availableEndpoints[Random.Range(0, availableEndpoints.Count)];
        }
        
        private int ChooseCollaborativeEndpoint(SimulatedPerson person)
        {
            // Check where others are touching
            List<int> touchedEndpoints = new List<int>();
            foreach (var other in people)
            {
                if (other != person && other.currentEndpoint >= 0)
                {
                    touchedEndpoints.Add(other.currentEndpoint);
                }
            }
            
            if (touchedEndpoints.Count > 0 && Random.value < clusterProbability)
            {
                // Touch same area - pick adjacent to someone else's touch
                int otherEndpoint = touchedEndpoints[Random.Range(0, touchedEndpoints.Count)];
                int adjacent = FindAdjacentEndpoint(otherEndpoint);
                if (adjacent >= 0) return adjacent;
                
                // Or just pick nearby
                return FindNearestEndpoint(graphManager.NodePositions[otherEndpoint]);
            }
            
            return ChooseRandomEndpoint(person);
        }
        
        private int ChooseOppositionalEndpoint(SimulatedPerson person)
        {
            // Find where others are touching and go opposite
            Vector3 othersCenter = Vector3.zero;
            int otherCount = 0;
            
            foreach (var other in people)
            {
                if (other != person && other.currentEndpoint >= 0)
                {
                    othersCenter += graphManager.NodePositions[other.currentEndpoint];
                    otherCount++;
                }
            }
            
            if (otherCount > 0)
            {
                othersCenter /= otherCount;
                
                // Find endpoint opposite to others' center
                Vector3 oppositeDir = (graphCenter - othersCenter).normalized;
                Vector3 targetPos = graphCenter + oppositeDir * 100f; // Far in opposite direction
                
                return FindNearestEndpoint(targetPos);
            }
            
            return ChooseRandomEndpoint(person);
        }
        
        private int ChooseWaveEndpoint(SimulatedPerson person)
        {
            // Move around the circle based on preferred angle
            person.preferredAngle += Random.Range(20f, 60f); // Advance around
            if (person.preferredAngle > 360f) person.preferredAngle -= 360f;
            
            // Find endpoint closest to this angle from center
            float targetAngleRad = person.preferredAngle * Mathf.Deg2Rad;
            Vector3 targetDir = new Vector3(Mathf.Cos(targetAngleRad), 0, Mathf.Sin(targetAngleRad));
            Vector3 targetPos = graphCenter + targetDir * 100f;
            
            return FindNearestEndpoint(targetPos);
        }
        
        private int FindAdjacentEndpoint(int fromEndpoint)
        {
            // This is simplified - in reality we'd traverse the graph
            // For now, just find nearest different endpoint
            if (fromEndpoint < 0 || fromEndpoint >= graphManager.NodePositions.Count)
                return -1;
            
            Vector3 fromPos = graphManager.NodePositions[fromEndpoint];
            float minDist = float.MaxValue;
            int nearest = -1;
            
            foreach (int ep in availableEndpoints)
            {
                if (ep == fromEndpoint) continue;
                float dist = Vector3.Distance(fromPos, graphManager.NodePositions[ep]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = ep;
                }
            }
            
            return nearest;
        }
        
        private int FindNearestEndpoint(Vector3 position)
        {
            float minDist = float.MaxValue;
            int nearest = -1;
            
            foreach (int ep in availableEndpoints)
            {
                float dist = Vector3.Distance(position, graphManager.NodePositions[ep]);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = ep;
                }
            }
            
            return nearest;
        }
        
        // Public API
        
        [ContextMenu("Start Simulation")]
        public void StartSimulation()
        {
            autoSimulate = true;
            if (!initialized && graphManager != null && graphManager.DataLoaded)
            {
                Initialize();
            }
        }
        
        [ContextMenu("Stop Simulation")]
        public void StopSimulation()
        {
            autoSimulate = false;
            
            // Release all touches
            foreach (var person in people)
            {
                if (person.isTouching && person.currentEndpoint >= 0)
                {
                    Vector3 position = graphManager.NodePositions[person.currentEndpoint];
                    GraphPlayerController.SimulateRelease(graphManager, person.currentEndpoint, position);
                    person.isTouching = false;
                    person.currentEndpoint = -1;
                }
            }
        }
        
        [ContextMenu("Reset Simulation")]
        public void ResetSimulation()
        {
            StopSimulation();
            initialized = false;
            people.Clear();
        }
        
        /// <summary>
        /// Get number of people currently touching
        /// </summary>
        public int ActiveTouchCount
        {
            get
            {
                int count = 0;
                foreach (var person in people)
                {
                    if (person.isTouching) count++;
                }
                return count;
            }
        }
        
        /// <summary>
        /// Get currently touched endpoints
        /// </summary>
        public List<int> GetTouchedEndpoints()
        {
            List<int> touched = new List<int>();
            foreach (var person in people)
            {
                if (person.isTouching && person.currentEndpoint >= 0)
                {
                    touched.Add(person.currentEndpoint);
                }
            }
            return touched;
        }
        
        void OnDisable()
        {
            StopSimulation();
        }
    }
}
