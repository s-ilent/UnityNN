using UnityEngine;
using System.Collections.Generic;

namespace SilentTools
{
    [DisallowMultipleComponent]
    public class ParticleEffectComponent : MonoBehaviour
    {
        [Header("Engine Metadata")]
        public int particleType;
        public List<string> externalBones = new List<string>();
        public List<string> resourceFiles = new List<string>();

        [Header("Counts")]
        public int emitterCount;
        public int behaviorCount;
        public int sequenceCueCount;
    }
}