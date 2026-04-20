using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simulates multiple people interacting with the PathFinderAnimation.
    /// People touch endpoints to start new paths, maintaining a target
    /// level of activity on the sculpture.
    /// </summary>
    public class PathFinderSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LEDGraphManager graphManager;
        [SerializeField] private PathFinderAnimation targetAnimation;
        
        [Header("Simulation Settings")]
        [Range(1, 12)]
        [SerializeField] private int numberOfPeople = 4;
        
        [SerializeField] private bool autoSimulate = true;
        
        [Header("Behavior Settings")]
        [Tooltip("How often someone starts a new path")]
        [SerializeField] private float newPathInterval = 1.5f;
        
        [Tooltip("Chance of starting a new path each interval")]
        [Range(0f, 1f)]
        [SerializeField] private float newPathChance = 0.7f;
        
        [Tooltip("Try to maintain this many simultaneous paths")]
        [Range(1, 6)]
        [SerializeField] private int targetActivePaths = 3;
        
        public enum SimulationMode
        {
            Cooperative,        // Lower activity, measured interactions
            Exploratory,        // People wander and start paths frequently
            Balanced,           // Moderate interaction rate
            HighActivity        // Many simultaneous paths, high interaction rate
        }
        
        [Header("Simulation Mode")]
        [SerializeField] private SimulationMode simulationMode = SimulationMode.Balanced;
        
        [Header("Debug")]
        [SerializeField] private bool showSimulationDebug = true;
        
        private class SimulatedPerson
        {
            public int id;
            public int targetEndpoint = -1;
            public float nextActionTime;
            public PersonState state = PersonState.Idle;
        }
        
        private enum PersonState
        {
            Idle,
            MovingToEndpoint,
            Resting
        }
        
        private List<SimulatedPerson> people = new List<SimulatedPerson>();
        private List<int> endpoints = new List<int>();
        private float lastNewPathAttempt = 0f;
        private bool initialized = false;
        
        void Start()
        {
            if (autoSimulate)
            {
                Invoke(nameof(Initialize), 1f);
            }
        }
        
        void Initialize()
        {
            if (targetAnimation == null)
            {
                Debug.LogError("PathFinderSimulator: No target animation assigned!");
                return;
            }
            
            endpoints = targetAnimation.GetEndpointNodes();
            if (endpoints.Count == 0)
            {
                Invoke(nameof(Initialize), 0.5f);
                return;
            }
            
            // Create simulated people
            people.Clear();
            for (int i = 0; i < numberOfPeople; i++)
            {
                var person = new SimulatedPerson
                {
                    id = i,
                    nextActionTime = Time.time + Random.Range(0.5f, 2f),
                    state = PersonState.Idle
                };
                people.Add(person);
            }
            
            initialized = true;
            
            if (showSimulationDebug)
            {
                Debug.Log($"🎮 PathFinder Simulator: {numberOfPeople} people, {endpoints.Count} endpoints, mode={simulationMode}");
            }
            
            // Start initial paths to reach target
            StartInitialPaths();
        }
        
        void StartInitialPaths()
        {
            int currentPaths = targetAnimation.GetActivePathCount();
            int pathsToStart = Mathf.Min(targetActivePaths - currentPaths, endpoints.Count / 2);
            
            for (int i = 0; i < pathsToStart; i++)
            {
                // Stagger the starts slightly
                float delay = i * 0.3f;
                Invoke(nameof(TriggerRandomEndpoint), delay);
            }
            
            if (showSimulationDebug && pathsToStart > 0)
            {
                Debug.Log($"🚀 Starting {pathsToStart} initial paths");
            }
        }
        
        void Update()
        {
            if (!initialized || !autoSimulate) return;
            
            endpoints = targetAnimation.GetEndpointNodes();
            if (endpoints.Count == 0) return;
            
            foreach (var person in people)
            {
                UpdatePerson(person);
            }
            
            if (Time.time - lastNewPathAttempt > newPathInterval)
            {
                lastNewPathAttempt = Time.time;
                TryStartNewPath();
            }
        }
        
        void UpdatePerson(SimulatedPerson person)
        {
            switch (person.state)
            {
                case PersonState.Idle:
                    if (ShouldExplore() || targetAnimation.GetActivePathCount() < targetActivePaths)
                    {
                        person.targetEndpoint = endpoints[Random.Range(0, endpoints.Count)];
                        person.state = PersonState.MovingToEndpoint;
                        float moveDelay = targetAnimation.GetActivePathCount() < targetActivePaths 
                            ? Random.Range(0.2f, 0.8f) 
                            : Random.Range(0.5f, 1.5f);
                        person.nextActionTime = Time.time + moveDelay;
                    }
                    break;
                    
                case PersonState.MovingToEndpoint:
                    if (Time.time >= person.nextActionTime)
                    {
                        if (person.targetEndpoint >= 0)
                        {
                            TouchEndpoint(person.targetEndpoint);
                            if (showSimulationDebug)
                                Debug.Log($"👤 Person {person.id} touched endpoint {person.targetEndpoint}");
                        }
                        
                        person.state = PersonState.Resting;
                        person.nextActionTime = Time.time + Random.Range(0.3f, 1f);
                        person.targetEndpoint = -1;
                    }
                    break;
                    
                case PersonState.Resting:
                    if (Time.time >= person.nextActionTime)
                    {
                        person.state = PersonState.Idle;
                    }
                    break;
            }
        }
        
        bool ShouldExplore()
        {
            switch (simulationMode)
            {
                case SimulationMode.Cooperative:
                    return Random.value < 0.3f;
                case SimulationMode.Exploratory:
                    return Random.value < 0.7f;
                case SimulationMode.Balanced:
                    return Random.value < 0.4f;
                case SimulationMode.HighActivity:
                    return Random.value < 0.6f;
                default:
                    return Random.value < 0.3f;
            }
        }
        
        void TryStartNewPath()
        {
            int activePaths = targetAnimation.GetActivePathCount();
            
            // Always try to maintain target number of paths
            if (activePaths < targetActivePaths)
            {
                // Higher chance when below target
                float urgency = 1f - ((float)activePaths / targetActivePaths);
                float adjustedChance = Mathf.Lerp(newPathChance, 1f, urgency);
                
                // Mode adjustments
                switch (simulationMode)
                {
                    case SimulationMode.Cooperative:
                        adjustedChance *= 0.7f;
                        break;
                    case SimulationMode.HighActivity:
                        adjustedChance = 1f; // Always try when below target
                        break;
                }
                
                if (Random.value < adjustedChance)
                {
                    TriggerRandomEndpoint();
                }
            }
            else if (simulationMode == SimulationMode.HighActivity || simulationMode == SimulationMode.Exploratory)
            {
                // Even at target, occasionally start more
                if (Random.value < newPathChance * 0.3f)
                {
                    TriggerRandomEndpoint();
                }
            }
        }
        
        void TriggerRandomEndpoint()
        {
            if (endpoints.Count == 0) return;
            
            int endpoint = endpoints[Random.Range(0, endpoints.Count)];
            
            if (showSimulationDebug)
                Debug.Log($"🚀 Simulator triggering endpoint {endpoint} (active paths: {targetAnimation.GetActivePathCount()})");
            
            TouchEndpoint(endpoint);
        }
        
        void TouchEndpoint(int endpoint)
        {
            if (!endpoints.Contains(endpoint)) return;
            
            Vector3 position = Vector3.zero;
            if (graphManager != null)
            {
                var positions = graphManager.NodePositions;
                if (positions != null && endpoint < positions.Count)
                {
                    position = positions[endpoint];
                }
            }
            
            GraphPlayerController.SimulatePress(graphManager, endpoint, position);
        }
        
        // Public controls
        public void StartSimulation()
        {
            autoSimulate = true;
            if (!initialized)
            {
                Initialize();
            }
        }
        
        public void StopSimulation()
        {
            autoSimulate = false;
        }
        
        public void SetNumberOfPeople(int count)
        {
            numberOfPeople = Mathf.Clamp(count, 1, 12);
            if (initialized)
            {
                Initialize();
            }
        }
        
        public void SetSimulationMode(SimulationMode mode)
        {
            simulationMode = mode;
        }
        
        [ContextMenu("Trigger Random Endpoint")]
        public void ForceRandomTrigger()
        {
            if (!initialized) Initialize();
            TriggerRandomEndpoint();
        }
        
        [ContextMenu("Trigger All Endpoints")]
        public void TriggerAllEndpoints()
        {
            if (!initialized) return;
            
            foreach (int endpoint in endpoints)
            {
                TouchEndpoint(endpoint);
            }
        }
    }
}
