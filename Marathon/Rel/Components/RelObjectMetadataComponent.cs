using UnityEngine;

namespace SilentTools
{
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
}