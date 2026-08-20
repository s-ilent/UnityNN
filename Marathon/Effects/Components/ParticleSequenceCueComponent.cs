using UnityEngine;

namespace SilentTools
{
    [DisallowMultipleComponent]
    public class ParticleSequenceCueComponent : MonoBehaviour
    {
        public int effectId;
        public int targetId;
        public int startTime;
        public int endTime;
        public int nextEntryTop;
        public int nextEntryBottom;
        public int userData1;
        public int userData2;
        public int userData3;
        public int userData4;
    }
}