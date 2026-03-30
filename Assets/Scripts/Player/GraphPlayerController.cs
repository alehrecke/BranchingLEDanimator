using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Player
{
    /// <summary>
    /// First/third person player controller for navigating around the LED graph
    /// Walks on a ground plane at the level of the graph's "feet" (low valence nodes)
    /// </summary>
    public class GraphPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 8f;
        public float runSpeed = 16f;
        public float acceleration = 10f;
        public float deceleration = 15f;
        public KeyCode runKey = KeyCode.LeftShift;
        
        [Header("Camera")]
        public CameraMode cameraMode = CameraMode.FirstPerson;
        public float mouseSensitivity = 2f;
        public float lookUpLimit = 80f;
        public float lookDownLimit = -60f;
        
        [Header("Third Person Settings")]
        [Tooltip("Enable to auto-calculate height/distance based on geometry size")]
        public bool autoFitCameraToGeometry = true;
        [Tooltip("Multiplier for auto-fit (1.5 = 50% padding around geometry)")]
        [Range(1f, 3f)]
        public float autoFitPadding = 1.5f;
        public float thirdPersonHeight = 30f;
        public float thirdPersonAngle = 45f; // Angle from vertical (0 = top down, 45 = 3/4 view)
        public float thirdPersonDistance = 40f; // Distance from graph center
        public bool thirdPersonFollowPlayer = false; // If true, camera follows player. If false, fixed on graph.
        public float cameraSmoothing = 10f;
        
        [Header("Ground Detection")]
        public float groundHeight = 0f;
        public bool autoDetectGround = true;
        public float playerHeight = 1.8f;
        public float groundPadding = 5f; // Extra space around the graph
        
        [Header("Ground Plane Visual")]
        public bool createGroundPlane = true;
        public Material groundMaterial;
        public Color groundColor = new Color(0.2f, 0.3f, 0.2f, 1f); // Grassy green
        private GameObject groundPlaneObject;
        
        [Header("Interaction")]
        public float interactionRadius = 3f;
        public KeyCode interactKey = KeyCode.E;
        public bool autoInteractOnTouch = true;
        
        [Header("Visual")]
        public bool showPlayerModel = true;
        public Color playerColor = new Color(1f, 0.5f, 0f, 1f); // Orange for visibility
        public float playerRadius = 1f; // Visual size of the player sphere
        private GameObject playerVisual;
        
        [Header("Trigger Settings")]
        public float touchRadius = 2f; // How close to trigger an endpoint (independent of visual size)
        [Tooltip("Extra distance before releasing a held endpoint (prevents rapid toggling)")]
        public float touchHysteresis = 2f; // Release radius = touchRadius + hysteresis
        
        [Header("References")]
        public LEDGraphManager graphManager;
        public Camera playerCamera;
        
        public enum CameraMode
        {
            FirstPerson,
            ThirdPerson,
            TopDown
        }
        
        // Internal state
        private Vector3 velocity = Vector3.zero;
        private float rotationX = 0f;
        private float rotationY = 0f;
        private List<int> nearbyEndpoints = new List<int>();
        private int closestEndpoint = -1;
        private float closestDistance = float.MaxValue;
        
        // Events for game integration -- source identifies which graph the endpoint belongs to
        public delegate void EndpointInteraction(LEDGraphManager source, int endpointIndex, Vector3 position);
        public static event EndpointInteraction OnEndpointTouched;
        public static event EndpointInteraction OnEndpointInteract;
        public static event EndpointInteraction OnEndpointPressed;
        public static event EndpointInteraction OnEndpointReleased;
        public static event EndpointInteraction OnEndpointHeld;
        
        public static void SimulatePress(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            OnEndpointPressed?.Invoke(source, endpointIndex, position);
            OnEndpointTouched?.Invoke(source, endpointIndex, position);
        }
        
        public static void SimulateRelease(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            OnEndpointReleased?.Invoke(source, endpointIndex, position);
        }
        
        public static void SimulateHold(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            OnEndpointHeld?.Invoke(source, endpointIndex, position);
        }
        
        // Track currently held endpoints (keyed by graph+node)
        private HashSet<long> currentlyHeldEndpoints = new HashSet<long>();
        
        // Per-endpoint tracking across all graphs
        private struct EndpointEntry
        {
            public LEDGraphManager graph;
            public int nodeIndex;
            public Vector3 position;
        }
        private List<EndpointEntry> allEndpoints = new List<EndpointEntry>();
        private List<LEDGraphManager> allGraphManagers = new List<LEDGraphManager>();
        
        // Cached data (legacy -- kept for single-graph compat)
        private List<int> endpointNodes = new List<int>();
        private List<Vector3> endpointPositions = new List<Vector3>();
        private bool endpointsAnalyzed = false;
        
        private static long EndpointKey(LEDGraphManager g, int node) =>
            ((long)g.GetInstanceID() << 32) | (uint)node;
        
        // Graph bounds
        private Bounds graphBounds;
        private Vector3 graphCenter;
        
        void Start()
        {
            SetupPlayer();
            SetupCamera();
            lastTouchTime = new Dictionary<long, float>();
            
            // Discover all graph managers in the scene
            allGraphManagers.Clear();
            allGraphManagers.AddRange(FindObjectsByType<LEDGraphManager>(FindObjectsSortMode.None));
            
            if (graphManager == null && allGraphManagers.Count > 0)
            {
                graphManager = allGraphManagers[0];
            }
            
            // Lock cursor for first person, free cursor for other modes
            if (cameraMode == CameraMode.FirstPerson)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        
        void SetupPlayer()
        {
            // Remove old capsule collider if present
            var oldCapsule = GetComponent<CapsuleCollider>();
            if (oldCapsule != null) DestroyImmediate(oldCapsule);
            
            // Add sphere collider
            var collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.radius = playerRadius;
            collider.center = new Vector3(0, playerRadius, 0); // Center at sphere center
            
            // Add rigidbody for physics (optional)
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
            }
            
            // Create visible player model
            CreatePlayerVisual();
        }
        
        void CreatePlayerVisual()
        {
            // Remove old visual if exists
            if (playerVisual != null)
            {
                DestroyImmediate(playerVisual);
            }
            
            if (!showPlayerModel) return;
            
            // Create sphere mesh for the player - sits on ground
            playerVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            playerVisual.name = "PlayerModel";
            playerVisual.transform.SetParent(transform);
            // Position sphere so it sits ON the ground (center at radius height)
            playerVisual.transform.localPosition = new Vector3(0, playerRadius, 0);
            playerVisual.transform.localRotation = Quaternion.identity;
            playerVisual.transform.localScale = new Vector3(playerRadius * 2f, playerRadius * 2f, playerRadius * 2f);
            
            // Remove collider from visual (we have our own)
            var visualCollider = playerVisual.GetComponent<Collider>();
            if (visualCollider != null) DestroyImmediate(visualCollider);
            
            // Apply bright material
            var renderer = playerVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = playerColor;
                // Make it emissive so it's visible
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", playerColor * 0.5f);
                renderer.material = mat;
            }
            
            Debug.Log($"🔵 Created player sphere (visual radius={playerRadius}, touch radius={touchRadius})");
        }
        
        void SetupCamera()
        {
            if (playerCamera == null)
            {
                // Try to find or create camera
                playerCamera = Camera.main;
                
                if (playerCamera == null)
                {
                    var camObj = new GameObject("PlayerCamera");
                    playerCamera = camObj.AddComponent<Camera>();
                    playerCamera.tag = "MainCamera";
                }
            }
            
            // Parent camera for first person
            if (cameraMode == CameraMode.FirstPerson)
            {
                playerCamera.transform.SetParent(transform);
                playerCamera.transform.localRotation = Quaternion.identity;
                // Height will be set by UpdateCameraHeight()
            }
        }
        
        void Update()
        {
            // Analyze endpoints once all graphs have loaded
            if (!endpointsAnalyzed)
            {
                bool anyLoaded = false;
                foreach (var gm in allGraphManagers)
                {
                    if (gm != null && gm.DataLoaded) { anyLoaded = true; break; }
                }
                if (anyLoaded)
                {
                    AnalyzeAllEndpoints();
                    CalculateGraphBounds();
                    
                    if (autoDetectGround) DetectGroundLevel();
                    if (createGroundPlane) CreateGroundPlane();
                    
                    SpawnPlayerInFrontOfGraph();
                }
            }
            
            HandleInput();
            UpdateMovement();
            UpdateCamera();
            CheckEndpointProximity();
            
            // Escape to unlock cursor
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            // Click to re-lock cursor
            if (Input.GetMouseButtonDown(0) && cameraMode == CameraMode.FirstPerson)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        
        void AnalyzeAllEndpoints()
        {
            allEndpoints.Clear();
            endpointNodes.Clear();
            endpointPositions.Clear();
            
            foreach (var gm in allGraphManagers)
            {
                if (gm == null || !gm.DataLoaded) continue;
                
                var nodePositions = gm.NodePositions;
                var edges = gm.EdgeConnections;
                
                var valences = new int[nodePositions.Count];
                foreach (var edge in edges)
                {
                    if (edge.x < valences.Length) valences[edge.x]++;
                    if (edge.y < valences.Length) valences[edge.y]++;
                }
                
                for (int i = 0; i < valences.Length; i++)
                {
                    if (valences[i] == 1)
                    {
                        Vector3 worldPos = gm.transform.TransformPoint(nodePositions[i]);
                        allEndpoints.Add(new EndpointEntry { graph = gm, nodeIndex = i, position = worldPos });
                        endpointNodes.Add(i);
                        endpointPositions.Add(worldPos);
                    }
                }
            }
            
            Debug.Log($"🦶 Found {allEndpoints.Count} endpoint 'feet' across {allGraphManagers.Count} graph(s)");
            endpointsAnalyzed = true;
        }
        
        void CalculateGraphBounds()
        {
            bool first = true;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            
            foreach (var gm in allGraphManagers)
            {
                if (gm == null || !gm.DataLoaded) continue;
                foreach (var pos in gm.NodePositions)
                {
                    Vector3 worldPos = gm.transform.TransformPoint(pos);
                    if (first) { min = worldPos; max = worldPos; first = false; }
                    else { min = Vector3.Min(min, worldPos); max = Vector3.Max(max, worldPos); }
                }
            }
            
            if (first) return; // no data
            
            graphBounds = new Bounds((min + max) / 2f, max - min);
            graphCenter = graphBounds.center;
            
            Debug.Log($"📐 Graph bounds (all graphs): Center={graphCenter}, Size={graphBounds.size}");
            
            // Auto-fit camera to geometry
            if (autoFitCameraToGeometry)
            {
                AutoFitCameraToGeometry();
            }
        }
        
        /// <summary>
        /// Automatically calculate camera height and distance to fit the entire geometry in view
        /// </summary>
        void AutoFitCameraToGeometry()
        {
            // Get the maximum dimension of the geometry
            float maxHorizontalSize = Mathf.Max(graphBounds.size.x, graphBounds.size.z);
            float verticalSize = graphBounds.size.y;
            
            // Calculate camera distance needed to see the entire horizontal extent
            // Using basic trigonometry with a 60° FOV (common default)
            float fov = playerCamera != null ? playerCamera.fieldOfView : 60f;
            float halfFovRad = (fov * 0.5f) * Mathf.Deg2Rad;
            
            // Distance needed to see horizontal extent (with padding)
            float horizontalDistance = (maxHorizontalSize * autoFitPadding * 0.5f) / Mathf.Tan(halfFovRad);
            
            // Height should be enough to see vertical extent plus some offset
            float calculatedHeight = verticalSize * autoFitPadding + graphBounds.center.y;
            
            // For third person, we want to see the whole structure
            // Use the angle to calculate the actual distance/height relationship
            float angleRad = thirdPersonAngle * Mathf.Deg2Rad;
            
            // The camera needs to be far enough back AND high enough
            float neededDistance = horizontalDistance * Mathf.Cos(angleRad);
            float neededHeight = horizontalDistance * Mathf.Sin(angleRad) + calculatedHeight;
            
            // Apply calculated values (take the larger of calculated vs current for safety)
            thirdPersonDistance = Mathf.Max(neededDistance, maxHorizontalSize * autoFitPadding);
            thirdPersonHeight = Mathf.Max(neededHeight, calculatedHeight);
            
            Debug.Log($"📷 Auto-fit camera: Height={thirdPersonHeight:F1}, Distance={thirdPersonDistance:F1} " +
                      $"(geometry size: {maxHorizontalSize:F1}x{verticalSize:F1})");
        }
        
        /// <summary>
        /// Manually trigger camera auto-fit (useful from inspector or other scripts)
        /// </summary>
        [ContextMenu("Re-fit Camera to Geometry")]
        public void RefitCameraToGeometry()
        {
            if (graphManager != null && graphManager.DataLoaded)
            {
                CalculateGraphBounds();
                Debug.Log("📷 Camera re-fitted to geometry bounds");
            }
            else
            {
                Debug.LogWarning("⚠️ Cannot fit camera - graph data not loaded");
            }
        }
        
        void DetectGroundLevel()
        {
            float lowestY = float.MaxValue;
            int totalNodes = 0;
            
            foreach (var gm in allGraphManagers)
            {
                if (gm == null || !gm.DataLoaded) continue;
                foreach (var pos in gm.NodePositions)
                {
                    Vector3 worldPos = gm.transform.TransformPoint(pos);
                    if (worldPos.y < lowestY) lowestY = worldPos.y;
                    totalNodes++;
                }
            }
            
            if (totalNodes == 0) return;
            groundHeight = lowestY;
            Debug.Log($"🌍 Auto-detected ground level: Y = {groundHeight} (from {totalNodes} nodes across {allGraphManagers.Count} graphs)");
        }
        
        void CreateGroundPlane()
        {
            // Remove old ground plane if exists
            if (groundPlaneObject != null)
            {
                DestroyImmediate(groundPlaneObject);
            }
            
            // Create ground plane sized to graph bounds + padding
            groundPlaneObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundPlaneObject.name = "GroundPlane";
            groundPlaneObject.transform.SetParent(transform.parent);
            
            // Position at ground level
            groundPlaneObject.transform.position = new Vector3(graphCenter.x, groundHeight, graphCenter.z);
            
            // Scale to cover graph area (Unity plane is 10x10 by default)
            float scaleX = (graphBounds.size.x + groundPadding * 2f) / 10f;
            float scaleZ = (graphBounds.size.z + groundPadding * 2f) / 10f;
            groundPlaneObject.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
            
            // Apply material/color
            var renderer = groundPlaneObject.GetComponent<Renderer>();
            if (groundMaterial != null)
            {
                renderer.material = groundMaterial;
            }
            else
            {
                // Create simple colored material
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = groundColor;
                renderer.material = mat;
            }
            
            // Disable collider (we handle ground ourselves)
            var collider = groundPlaneObject.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            
            Debug.Log($"🌿 Created ground plane at Y={groundHeight}, size=({scaleX * 10f}, {scaleZ * 10f})");
        }
        
        void SpawnPlayerInFrontOfGraph()
        {
            // Calculate spawn position: in front of the graph, at ground level
            // "In front" = negative Z from the graph center (or the side with most open space)
            
            float spawnDistance = Mathf.Max(graphBounds.size.x, graphBounds.size.z) * 0.5f + 5f;
            
            // Spawn on the -Z side of the graph, DIRECTLY on the ground plane
            Vector3 spawnPos = new Vector3(
                graphCenter.x,
                groundHeight,  // Player feet at ground level
                graphCenter.z - spawnDistance
            );
            
            transform.position = spawnPos;
            
            // Face towards the graph center
            Vector3 lookDir = graphCenter - spawnPos;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
                rotationY = transform.eulerAngles.y;
            }
            
            // Update camera position for first person
            UpdateCameraHeight();
            
            Debug.Log($"🎮 Player spawned at {spawnPos} (ground={groundHeight}), facing graph center");
        }
        
        void UpdateCameraHeight()
        {
            if (playerCamera != null && cameraMode == CameraMode.FirstPerson)
            {
                // Camera at eye level (playerHeight is total height, eyes are near top)
                playerCamera.transform.localPosition = new Vector3(0, playerHeight * 0.9f, 0);
            }
        }
        
        void HandleInput()
        {
            // Mouse look
            if (Cursor.lockState == CursorLockMode.Locked || cameraMode == CameraMode.ThirdPerson)
            {
                rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
                rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                rotationX = Mathf.Clamp(rotationX, lookDownLimit, lookUpLimit);
            }
            
            // Manual interaction
            if (Input.GetKeyDown(interactKey) && closestEndpoint >= 0)
            {
                TriggerEndpointInteraction(closestEndpoint);
            }
        }
        
        void UpdateMovement()
        {
            // Get input
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            
            Vector3 forward;
            Vector3 right;
            
            // Calculate movement direction based on camera mode
            if (cameraMode == CameraMode.FirstPerson)
            {
                // First person: move relative to player facing
                forward = transform.forward;
                right = transform.right;
            }
            else
            {
                // Third person / Top down: move relative to CAMERA view
                if (playerCamera != null)
                {
                    forward = playerCamera.transform.forward;
                    right = playerCamera.transform.right;
                }
                else
                {
                    forward = Vector3.forward;
                    right = Vector3.right;
                }
            }
            
            // Keep movement on ground plane (flatten to XZ)
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            Vector3 targetDirection = (forward * vertical + right * horizontal).normalized;
            
            // Apply speed
            bool isRunning = Input.GetKey(runKey);
            float targetSpeed = targetDirection.magnitude > 0.1f ? (isRunning ? runSpeed : walkSpeed) : 0f;
            Vector3 targetVelocity = targetDirection * targetSpeed;
            
            // Smooth acceleration/deceleration
            float smoothFactor = targetSpeed > velocity.magnitude ? acceleration : deceleration;
            velocity = Vector3.Lerp(velocity, targetVelocity, smoothFactor * Time.deltaTime);
            
            // Apply movement - player feet stay on ground
            Vector3 newPosition = transform.position + velocity * Time.deltaTime;
            newPosition.y = groundHeight; // Player origin at ground level
            transform.position = newPosition;
            
            // Rotate player to face movement direction (for third person visibility)
            if (cameraMode != CameraMode.FirstPerson && targetDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
            else if (cameraMode == CameraMode.FirstPerson)
            {
                // First person: rotate based on mouse
                transform.rotation = Quaternion.Euler(0, rotationY, 0);
            }
            
            // Keep camera at correct height (in case playerHeight changes)
            UpdateCameraHeight();
        }
        
        void UpdateCamera()
        {
            if (playerCamera == null) return;
            
            switch (cameraMode)
            {
                case CameraMode.FirstPerson:
                    // Ensure camera is parented to player for first person
                    if (playerCamera.transform.parent != transform)
                    {
                        playerCamera.transform.SetParent(transform);
                        playerCamera.transform.localPosition = new Vector3(0, playerHeight * 0.9f, 0);
                    }
                    // Camera looks where player looks (X rotation for up/down)
                    playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
                    break;
                    
                case CameraMode.ThirdPerson:
                    // Fixed 3/4 view looking at graph (or optionally following player)
                    Vector3 lookTarget = thirdPersonFollowPlayer ? transform.position : graphCenter;
                    
                    // Calculate camera position: offset from target at specified angle
                    // Angle is from vertical, so 45° gives a nice 3/4 view
                    float angleRad = thirdPersonAngle * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(
                        Mathf.Sin(angleRad) * thirdPersonDistance * 0.7f,  // X offset (slight side view)
                        thirdPersonHeight,                                  // Y height
                        -Mathf.Sin(angleRad) * thirdPersonDistance          // Z offset (in front)
                    );
                    
                    Vector3 targetCamPos = lookTarget + offset;
                    
                    playerCamera.transform.position = Vector3.Lerp(
                        playerCamera.transform.position, 
                        targetCamPos, 
                        cameraSmoothing * Time.deltaTime
                    );
                    
                    // Look at the center of the graph (or player)
                    Vector3 lookPoint = lookTarget;
                    lookPoint.y = groundHeight; // Look at ground level
                    playerCamera.transform.LookAt(lookPoint);
                    
                    // Unparent camera in third person mode
                    if (playerCamera.transform.parent == transform)
                    {
                        playerCamera.transform.SetParent(null);
                    }
                    break;
                    
                case CameraMode.TopDown:
                    // Fixed overhead view centered on graph
                    Vector3 topDownPos = graphCenter + new Vector3(0, thirdPersonHeight * 1.5f, 0);
                    playerCamera.transform.position = Vector3.Lerp(
                        playerCamera.transform.position,
                        topDownPos,
                        cameraSmoothing * Time.deltaTime
                    );
                    playerCamera.transform.LookAt(graphCenter);
                    
                    // Unparent camera
                    if (playerCamera.transform.parent == transform)
                    {
                        playerCamera.transform.SetParent(null);
                    }
                    break;
            }
        }
        
        void CheckEndpointProximity()
        {
            nearbyEndpoints.Clear();
            closestEndpoint = -1;
            closestDistance = float.MaxValue;
            
            Vector3 playerPos = transform.position;
            HashSet<long> currentlyTouching = new HashSet<long>();
            
            for (int i = 0; i < allEndpoints.Count; i++)
            {
                var ep = allEndpoints[i];
                long key = EndpointKey(ep.graph, ep.nodeIndex);
                
                float dx = ep.position.x - playerPos.x;
                float dz = ep.position.z - playerPos.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                
                if (distance < interactionRadius)
                {
                    nearbyEndpoints.Add(ep.nodeIndex);
                    
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestEndpoint = ep.nodeIndex;
                    }
                    
                    if (autoInteractOnTouch)
                    {
                        bool wasHeld = currentlyHeldEndpoints.Contains(key);
                        float releaseRadius = touchRadius + touchHysteresis;
                        bool shouldBeHeld = distance < touchRadius || (wasHeld && distance < releaseRadius);
                        
                        if (shouldBeHeld)
                        {
                            currentlyTouching.Add(key);
                            
                            if (!wasHeld)
                            {
                                Debug.Log($"🎹 Endpoint {ep.nodeIndex} PRESSED on {ep.graph.name} (distance: {distance:F2})");
                                OnEndpointPressed?.Invoke(ep.graph, ep.nodeIndex, ep.position);
                                OnEndpointTouched?.Invoke(ep.graph, ep.nodeIndex, ep.position);
                            }
                            else
                            {
                                OnEndpointHeld?.Invoke(ep.graph, ep.nodeIndex, ep.position);
                            }
                        }
                    }
                }
            }
            
            // Check for released endpoints
            foreach (long heldKey in currentlyHeldEndpoints)
            {
                if (!currentlyTouching.Contains(heldKey))
                {
                    // Find the endpoint entry for this key
                    for (int i = 0; i < allEndpoints.Count; i++)
                    {
                        var ep = allEndpoints[i];
                        if (EndpointKey(ep.graph, ep.nodeIndex) == heldKey)
                        {
                            Debug.Log($"🎹 Endpoint {ep.nodeIndex} RELEASED on {ep.graph.name}");
                            OnEndpointReleased?.Invoke(ep.graph, ep.nodeIndex, ep.position);
                            break;
                        }
                    }
                }
            }
            
            currentlyHeldEndpoints = currentlyTouching;
        }
        
        private Dictionary<long, float> lastTouchTime = new Dictionary<long, float>();
        private float touchCooldown = 0.5f;
        
        void TriggerEndpointInteraction(int endpointIndex)
        {
            // Find the graph this endpoint belongs to (use closest match)
            for (int i = 0; i < allEndpoints.Count; i++)
            {
                var ep = allEndpoints[i];
                if (ep.nodeIndex == endpointIndex)
                {
                    Debug.Log($"🎯 Player interacted with endpoint {endpointIndex} on {ep.graph.name}");
                    OnEndpointInteract?.Invoke(ep.graph, ep.nodeIndex, ep.position);
                    return;
                }
            }
        }
        
        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            // Draw player sphere (visual size)
            Gizmos.color = playerColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * playerRadius, playerRadius);
            
            // Draw touch radius (trigger area)
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, touchRadius);
            
            // Draw interaction radius (larger awareness area)
            Gizmos.color = new Color(0, 1, 1, 0.15f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
            
            // Highlight closest endpoint
            if (closestEndpoint >= 0)
            {
                int idx = endpointNodes.IndexOf(closestEndpoint);
                if (idx >= 0 && idx < endpointPositions.Count)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, endpointPositions[idx]);
                    Gizmos.DrawWireSphere(endpointPositions[idx], 1f);
                }
            }
        }
        
        // Public API
        public int GetClosestEndpoint() => closestEndpoint;
        public float GetClosestDistance() => closestDistance;
        public List<int> GetNearbyEndpoints() => nearbyEndpoints;
        public bool IsNearEndpoint(int nodeIndex) => nearbyEndpoints.Contains(nodeIndex);
        
        [ContextMenu("Teleport to Random Endpoint")]
        public void TeleportToRandomEndpoint()
        {
            if (endpointPositions.Count > 0)
            {
                int idx = Random.Range(0, endpointPositions.Count);
                Vector3 pos = endpointPositions[idx];
                transform.position = new Vector3(pos.x, groundHeight, pos.z);
            }
        }
        
        [ContextMenu("Reset to Ground Level")]
        public void ResetToGroundLevel()
        {
            Vector3 pos = transform.position;
            pos.y = groundHeight;
            transform.position = pos;
            Debug.Log($"🔄 Reset player to ground level: Y = {groundHeight}");
        }
        
        [ContextMenu("Debug Player Info")]
        public void DebugPlayerInfo()
        {
            Debug.Log($"📍 Player Position: {transform.position}");
            Debug.Log($"🌍 Ground Height: {groundHeight}");
            Debug.Log($"👤 Player Height Setting: {playerHeight}");
            if (playerCamera != null)
            {
                Debug.Log($"📷 Camera World Pos: {playerCamera.transform.position}");
                Debug.Log($"📷 Camera Local Pos: {playerCamera.transform.localPosition}");
            }
            Debug.Log($"📐 Graph Center: {graphCenter}");
            Debug.Log($"📐 Graph Bounds: {graphBounds}");
        }
    }
}

