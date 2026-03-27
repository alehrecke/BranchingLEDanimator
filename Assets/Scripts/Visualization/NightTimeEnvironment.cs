using UnityEngine;
using UnityEngine.Rendering;

namespace BranchingLEDAnimator.Visualization
{
    /// <summary>
    /// Sets up a nighttime environment optimized for LED animation visualization
    /// Creates dark atmosphere with subtle lighting to make LEDs pop
    /// </summary>
    public class NightTimeEnvironment : MonoBehaviour
    {
        [Header("Environment Settings")]
        [SerializeField] private bool applyOnStart = false; // DISABLED - restore default lighting
        [SerializeField] private bool createCustomSkybox = true;
        
        [Header("Skybox Colors")]
        [SerializeField] private Color skyboxTop = new Color(0.02f, 0.02f, 0.08f, 1f); // Deep night blue
        [SerializeField] private Color skyboxMiddle = new Color(0.01f, 0.01f, 0.04f, 1f); // Darker blue
        [SerializeField] private Color skyboxBottom = new Color(0.005f, 0.005f, 0.02f, 1f); // Almost black
        
        [Header("Lighting")]
        [SerializeField] private Color ambientColor = new Color(0.05f, 0.05f, 0.1f, 1f); // Very dim blue ambient
        [SerializeField] private float ambientIntensity = 0.2f;
        [SerializeField] private Color fogColor = new Color(0.02f, 0.02f, 0.08f, 1f); // Match skybox
        [SerializeField] private bool enableFog = false; // Disable fog to avoid obscuring LEDs
        [SerializeField] private float fogDensity = 0.005f; // Reduced density
        
        [Header("Camera Settings")]
        [SerializeField] private Color cameraBackgroundColor = new Color(0.01f, 0.01f, 0.03f, 1f); // Very dark background
        [SerializeField] private bool adjustCameraBackground = true;
        
        [Header("Post Processing")]
        [SerializeField] private bool enableBloom = true;
        [SerializeField] private float bloomIntensity = 1.5f;
        [SerializeField] private float bloomThreshold = 0.8f;
        
        // Note: Bloom settings are reserved for future post-processing implementation
        
        private Material customSkyboxMaterial;
        private Light mainLight;
        
        void Start()
        {
            if (applyOnStart)
            {
                ApplyNightTimeEnvironment();
            }
        }
        
        /// <summary>
        /// Apply all nighttime environment settings
        /// </summary>
        [ContextMenu("Apply Nighttime Environment")]
        public void ApplyNightTimeEnvironment()
        {
            Debug.Log("🌙 Applying nighttime environment for LED visualization...");
            
            SetupSkybox();
            SetupLighting();
            SetupFog();
            SetupCamera();
            
            Debug.Log("✓ Nighttime environment applied!");
        }
        
        /// <summary>
        /// Create and apply custom gradient skybox
        /// </summary>
        private void SetupSkybox()
        {
            if (!createCustomSkybox) return;
            
            // Create custom skybox material
            Shader skyboxShader = Shader.Find("Skybox/Gradient");
            if (skyboxShader == null)
            {
                // Fallback to procedural skybox if gradient not available
                skyboxShader = Shader.Find("Skybox/Procedural");
            }
            
            if (skyboxShader != null)
            {
                customSkyboxMaterial = new Material(skyboxShader);
                
                if (skyboxShader.name.Contains("Gradient"))
                {
                    // Configure gradient skybox
                    customSkyboxMaterial.SetColor("_Color1", skyboxTop);
                    customSkyboxMaterial.SetColor("_Color2", skyboxMiddle);
                    customSkyboxMaterial.SetColor("_Color3", skyboxBottom);
                    customSkyboxMaterial.SetFloat("_Exponent1", 1f);
                    customSkyboxMaterial.SetFloat("_Exponent2", 1f);
                    customSkyboxMaterial.SetFloat("_Intensity", 0.8f);
                }
                else if (skyboxShader.name.Contains("Procedural"))
                {
                    // Configure procedural skybox for night
                    customSkyboxMaterial.SetColor("_SkyTint", skyboxTop);
                    customSkyboxMaterial.SetColor("_GroundColor", skyboxBottom);
                    customSkyboxMaterial.SetFloat("_Exposure", 0.3f);
                    customSkyboxMaterial.SetFloat("_AtmosphereThickness", 0.5f);
                    customSkyboxMaterial.SetFloat("_SunSize", 0.02f);
                    customSkyboxMaterial.SetFloat("_SunSizeConvergence", 2f);
                }
                
                RenderSettings.skybox = customSkyboxMaterial;
                DynamicGI.UpdateEnvironment();
                
                Debug.Log("✓ Custom nighttime skybox applied");
            }
            else
            {
                // Fallback: set solid color background
                RenderSettings.skybox = null;
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = skyboxBottom;
                
                Debug.Log("✓ Solid color background applied (skybox shader not found)");
            }
        }
        
        /// <summary>
        /// Setup ambient lighting for nighttime
        /// </summary>
        private void SetupLighting()
        {
            // Set ambient lighting
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientColor;
            RenderSettings.ambientEquatorColor = ambientColor * 0.7f;
            RenderSettings.ambientGroundColor = ambientColor * 0.3f;
            RenderSettings.ambientIntensity = ambientIntensity;
            
            // Find or create main light
            mainLight = FindFirstObjectByType<Light>();
            if (mainLight == null)
            {
                GameObject lightObj = new GameObject("Main Light");
                lightObj.transform.SetParent(transform);
                mainLight = lightObj.AddComponent<Light>();
                mainLight.type = LightType.Directional;
            }
            
            if (mainLight != null)
            {
                // Configure main light for subtle nighttime illumination
                mainLight.color = new Color(0.7f, 0.8f, 1f, 1f); // Cool moonlight color
                mainLight.intensity = 0.3f; // Very dim
                mainLight.shadows = LightShadows.Soft;
                
                // Position light like moonlight
                mainLight.transform.rotation = Quaternion.Euler(30f, -30f, 0f);
                
                Debug.Log("✓ Nighttime lighting configured");
            }
        }
        
        /// <summary>
        /// Setup atmospheric fog
        /// </summary>
        private void SetupFog()
        {
            if (!enableFog) return;
            
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            
            Debug.Log("✓ Atmospheric fog enabled");
        }
        
        /// <summary>
        /// Setup camera for nighttime viewing
        /// </summary>
        private void SetupCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            
            if (adjustCameraBackground)
            {
                mainCamera.backgroundColor = cameraBackgroundColor;
            }
            
            // Adjust camera settings for better LED visibility
            mainCamera.allowHDR = true; // Enable HDR for bloom effects
            
            Debug.Log("✓ Camera configured for nighttime");
        }
        
        /// <summary>
        /// Reset to Unity default environment
        /// </summary>
        [ContextMenu("Reset to Default Environment")]
        public void ResetToDefault()
        {
            // Reset skybox
            RenderSettings.skybox = null;
            
            // Reset lighting
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            
            // Reset fog
            RenderSettings.fog = false;
            
            // Reset camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = new Color(0.19f, 0.3f, 0.47f, 0f); // Unity default
            }
            
            // Reset main light
            if (mainLight != null)
            {
                mainLight.color = Color.white;
                mainLight.intensity = 1f;
                mainLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            
            Debug.Log("✓ Environment reset to Unity defaults");
        }
        
        void OnValidate()
        {
            // Apply changes in real-time when values are adjusted in inspector
            if (Application.isPlaying)
            {
                ApplyNightTimeEnvironment();
            }
        }
        
        void OnDestroy()
        {
            // Clean up custom materials
            if (customSkyboxMaterial != null)
            {
                DestroyImmediate(customSkyboxMaterial);
            }
        }
    }
}
