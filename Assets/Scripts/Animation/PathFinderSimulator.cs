using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Simulates multiple people interacting with the PathFinderAnimation.
    /// People will try to "catch" destination endpoints and redirect paths,
    /// while also starting new paths from idle endpoints.
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
        [Tooltip("How likely a person is to successfully catch a destination")]
        [Range(0f, 1f)]
        [SerializeField] private float catchSuccessRate = 0.8f;
        
        [Tooltip("Reaction time range after fill arrives (seconds)")]
        [SerializeField] private Vector2 reactionTimeRange = new Vector2(0.1f, 0.8f);
        
        [Tooltip("How often someone starts a new path from an idle endpoint")]
        [SerializeField] private float newPathInterval = 1.5f;
        
        [Tooltip("Chance of starting a new path each interval")]
        [Range(0f, 1f)]
        [SerializeField] private float newPathChance = 0.7f;
        
        [Tooltip("Try to maintain this many simultaneous paths")]
        [Range(1, 6)]
        [SerializeField] private int targetActivePaths = 3;
        
        public enum SimulationMode
        {
            Cooperative,        // People focus on catching destinations
            Exploratory,        // People wander and start paths randomly
            Balanced,           // Mix of catching and starting new paths
            HighActivity        // Many simultaneous paths, high interaction rate
        }
        
        [Header("Simulation Mode")]
        [SerializeField] private SimulationMode simulationMode = SimulationMode.Balanced;
        
        [Header("Debug")]
        [SerializeField] private bool showSimulationDebug = true;
        
        // Simulated person
        private class SimulatedPerson
        {
            public int id;
            public int watchingEndpoint = -1;
            public float nextActionTime;
            public bool isWaitingToReact;
            public PersonState state = PersonState.Idle;
        }
        
        private enum PersonState
        {
            Idle,               // Looking for something to do
            WatchingDestination,// Waiting to catch a destination
            MovingToEndpoint,   // Moving to touch an endpoint
            Resting             // Taking a break
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
            
            // Get current waiting destinations
            var waitingDestinations = targetAnimation.GetWaitingDestinations();
            
            // Update each person
            foreach (var person in people)
            {
                UpdatePerson(person, waitingDestinations);
            }
            
            // Periodically try to start new paths
            if (Time.time - lastNewPathAttempt > newPathInterval)
            {
                lastNewPathAttempt = Time.time;
                TryStartNewPath();
            }
        }
        
        void UpdatePerson(SimulatedPerson person, List<int> waitingDestinations)
        {
            switch (person.state)
            {
                case PersonState.Idle:
                    // Look for something to do
                    if (waitingDestinations.Count > 0 && ShouldWatchDestination())
                    {
                        // Try to watch an unwatched destination
                        var unwatched = waitingDestinations
                            .Where(d => !people.Any(p => p.watchingEndpoint == d && p != person))
                            .ToList();
                        
                        if (unwatched.Count > 0)
                        {
                            person.watchingEndpoint = unwatched[Random.Range(0, unwatched.Count)];
                            person.state = PersonState.WatchingDestination;
                            person.isWaitingToReact = false;
                            
                            if (showSimulationDebug)
                            {
                                Debug.Log($"👤 Person {person.id} watching destination {person.watchingEndpoint}");
                            }
                        }
                    }
                    else if (ShouldExplore() || targetAnimation.GetActivePathCount() < targetActivePaths)
                    {
                        // Pick a random idle endpoint to touch
                        var idleEndpoints = endpoints
                            .Where(e => !targetAnimation.IsEndpointActive(e))
                            .ToList();
                        
                        if (idleEndpoints.Count > 0)
                        {
                            person.watchingEndpoint = idleEndpoints[Random.Range(0, idleEndpoints.Count)];
                            person.state = PersonState.MovingToEndpoint;
                            // Faster movement when below target paths
                            float moveDelay = targetAnimation.GetActivePathCount() < targetActivePaths 
                                ? Random.Range(0.2f, 0.8f) 
                                : Random.Range(0.5f, 1.5f);
                            person.nextActionTime = Time.time + moveDelay;
                        }
                    }
                    break;
                    
                case PersonState.WatchingDestination:
                    // Check if our destination is still waiting
                    if (!waitingDestinations.Contains(person.watchingEndpoint))
                    {
                        // Destination was taken or timed out
                        person.state = PersonState.Idle;
                        person.watchingEndpoint = -1;
                        break;
                    }
                    
                    // Set up reaction if not already
                    if (!person.isWaitingToReact)
                    {
                        person.isWaitingToReact = true;
                        person.nextActionTime = Time.time + Random.Range(reactionTimeRange.x, reactionTimeRange.y);
                    }
                    
                    // Try to catch
                    if (Time.time >= person.nextActionTime)
                    {
                        if (Random.value < catchSuccessRate)
                        {
                            TouchEndpoint(person.watchingEndpoint);
                            if (showSimulationDebug)
                            {
                                Debug.Log($"👤 Person {person.id} caught destination {person.watchingEndpoint}!");
                            }
                        }
                        else
                        {
                            if (showSimulationDebug)
                            {
                                Debug.Log($"👤 Person {person.id} missed destination {person.watchingEndpoint}");
                            }
                        }
                        
                        person.state = PersonState.Resting;
                        person.nextActionTime = Time.time + Random.Range(0.3f, 0.8f);
                        person.watchingEndpoint = -1;
                        person.isWaitingToReact = false;
                    }
                    break;
                    
                case PersonState.MovingToEndpoint:
                    if (Time.time >= person.nextActionTime)
                    {
                        if (person.watchingEndpoint >= 0 && !targetAnimation.IsEndpointActive(person.watchingEndpoint))
                        {
                            TouchEndpoint(person.watchingEndpoint);
                            if (showSimulationDebug)
                            {
                                Debug.Log($"👤 Person {person.id} started new path from {person.watchingEndpoint}");
                            }
                        }
                        
                        person.state = PersonState.Resting;
                        person.nextActionTime = Time.time + Random.Range(0.3f, 1f);
                        person.watchingEndpoint = -1;
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
        
        bool ShouldWatchDestination()
        {
            switch (simulationMode)
            {
                case SimulationMode.Cooperative:
                    return true;
                case SimulationMode.Exploratory:
                    return Random.value < 0.3f;
                case SimulationMode.Balanced:
                    return Random.value < 0.6f;
                case SimulationMode.HighActivity:
                    return Random.value < 0.5f;
                default:
                    return true;
            }
        }
        
        bool ShouldExplore()
        {
            switch (simulationMode)
            {
                case SimulationMode.Cooperative:
                    return Random.value < 0.2f;
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
            // Find idle endpoints
            var idleEndpoints = endpoints
                .Where(e => !targetAnimation.IsEndpointActive(e))
                .ToList();
            
            if (showSimulationDebug)
            {
                Debug.Log($"🔍 TriggerRandomEndpoint: {idleEndpoints.Count} idle endpoints out of {endpoints.Count}, active paths: {targetAnimation.GetActivePathCount()}");
            }
            
            if (idleEndpoints.Count == 0)
            {
                if (showSimulationDebug) Debug.Log($"⚠️ No idle endpoints available");
                return;
            }
            
            int endpoint = idleEndpoints[Random.Range(0, idleEndpoints.Count)];
            
            if (showSimulationDebug)
            {
                Debug.Log($"🚀 Simulator triggering endpoint {endpoint}");
            }
            
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
            
            GraphPlayerController.SimulatePress(endpoint, position);
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
        
        [ContextMenu("Trigger All Waiting Destinations")]
        public void TriggerAllWaiting()
        {
            if (!initialized) return;
            
            var waiting = targetAnimation.GetWaitingDestinations();
            foreach (int dest in waiting)
            {
                TouchEndpoint(dest);
            }
        }
    }
}
