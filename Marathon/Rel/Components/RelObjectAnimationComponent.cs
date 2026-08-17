using UnityEngine;
using System;
using System.Collections.Generic;

namespace SilentTools
{
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