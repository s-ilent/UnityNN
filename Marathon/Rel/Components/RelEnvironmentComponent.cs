using UnityEngine;

namespace SilentTools
{
    [DisallowMultipleComponent]
    public class RelEnvironmentComponent : MonoBehaviour
    {
        public LndFogData fog = new LndFogData();
        public LndLightData playerLight1 = new LndLightData();
        public LndLightData playerLight2 = new LndLightData();
        public LndLightData playerLightAmbient = new LndLightData();
        public LndGradientData topGradient = new LndGradientData();
        public LndGradientData bottomGradient = new LndGradientData();
        public Vector3 sunPosition;

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
            if (playerLightAmbient != null)
            {
                RenderSettings.ambientLight = playerLightAmbient.LightColor;
            }
            Debug.Log("[RelEnvironmentComponent] Applied REL environment fog and ambient light to scene RenderSettings.");
        }
    }
}