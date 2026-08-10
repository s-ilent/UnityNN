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
            Debug.Log("Applied REL environment fog and ambient light to scene RenderSettings.");
        }
    }

    public class RelObjectMetadataComponent : MonoBehaviour
    {
        public int objID;
        public string objectName = "";
        public Vector3 originalPosition;
        public Vector3 originalRotation;
        public int headerInt1;
        public int headerInt2;
        public int headerInt3;
        public byte[] metadata;
    }

    public class RelEnemySpawnComponent : MonoBehaviour
    {
        public int spawnIndex;
        public short monsterNum;
        public string enemyName = "";
        public short element;
        public short count;
        public short levelModifier;
    }
}