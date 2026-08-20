using UnityEngine;

namespace Marathon.Formats.Particle
{
    public class ParticleSequenceCue
    {
        public int NextEntryTop { get; set; }
        public int NextEntryBottom { get; set; }
        public int EffectId { get; set; }
        public int TargetId { get; set; }
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public Vector3 Translation { get; set; }
        public Vector3 Rotation { get; set; }
        public int UserData1 { get; set; }
        public int UserData2 { get; set; }
        public int UserData3 { get; set; }
        public int UserData4 { get; set; }
    }
}