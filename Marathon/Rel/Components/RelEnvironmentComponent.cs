// File: Marathon/Rel/Components/RelEnvironmentComponent.cs
using UnityEngine;

namespace UnityNN
{
    [DisallowMultipleComponent]
    public class RelEnvironmentComponent : MonoBehaviour
    {
        [Header("Distance & Atmosphere Fog")]
        public LndFogData fog = new LndFogData();

        [Header("Player & Ambient Lighting")]
        public LndLightData playerLight1 = new LndLightData();
        public LndLightData playerLight2 = new LndLightData();
        public LndLightData playerLightAmbient = new LndLightData();

        [Header("Atmosphere Height Gradients")]
        public LndGradientData topGradient = new LndGradientData();
        public LndGradientData bottomGradient = new LndGradientData();

        [Header("Sun & Celestial Lighting")]
        public Vector3 sunPosition;
        public float sunUnknown;

        [Header("Depth / Screen Blur Post-Processing")]
        public float blurStartDistance;
        public float blurDistance;
        public int blurPixelCount;
        public float blurOpacity;
        public float blurUnknown;

        [ContextMenu("Apply Environment To Unity Scene")]
        public void ApplyEnvironmentToScene()
        {
            if (fog != null)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = fog.NearPlane;
                RenderSettings.fogEndDistance = fog.FarPlane;
                RenderSettings.fogColor = fog.FogColor;
            }

            if (topGradient != null && bottomGradient != null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = topGradient.StartColor * topGradient.GradientMultiplier;
                RenderSettings.ambientEquatorColor = (playerLightAmbient != null && playerLightAmbient.LightColor != Color.black) 
                    ? playerLightAmbient.LightColor : topGradient.EndColor;
                RenderSettings.ambientGroundColor = bottomGradient.EndColor * bottomGradient.GradientMultiplier;
            }
            else if (playerLightAmbient != null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = playerLightAmbient.LightColor;
            }

            Debug.Log("[RelEnvironmentComponent] Applied complete REL environment fog, ambient lighting, and sky gradients to scene RenderSettings.");
        }
    }
}