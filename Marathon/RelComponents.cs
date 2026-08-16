// File: Marathon/RelComponents.cs
using UnityEngine;
using System;
using System.Collections.Generic;

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
    
    public class RelObjectParamComponent : MonoBehaviour
    {
        public int objID;
        public string objectName = "";
        public int groupOneCount;
        public int modelCount;
        public int animationCount;
        public int particleBindingCount;
        public int soundBindingCount;
    }

    public class RelObjectHitboxComponent : MonoBehaviour
    {
        public int hitboxShape;
        public Vector3 dimensions;
        public float radius;
        public int paramInt5;
        public int paramInt9;
    }

    public class RelObjectParticleInfoComponent : MonoBehaviour
    {
        public int particleIndex;
        public string particleName = "";
        public string particleFileName = "";
        public float mysteryFloat;
        public int mysteryInt;
    }

    [Serializable]
    public class ObjectAnimationEntryData
    {
        public int id1;
        public int id2;
        public string boneAnimName = "";
        public string texAnimName = "";
        public AnimationClip boneClip;
        public AnimationClip materialClip;
        public float paramFloat1;
        public float paramFloat2;
        public float paramFloat3;
        public float paramFloat4;
        public float paramFloat5;
        public float paramFloat6;
    }

    public class RelObjectAnimationComponent : MonoBehaviour
    {
        public int objID = -1;
        public List<ObjectAnimationEntryData> animations = new List<ObjectAnimationEntryData>();
    }
}