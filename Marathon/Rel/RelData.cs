using UnityEngine;
using System.Collections.Generic;

namespace SilentTools
{
    public enum RelFileType
    {
        Unknown,
        SetLayout,        // set_r*.rel / LndSet.rel
        LndEffect,        // LndEffect.rel
        LndEnemyLight,    // LndEnemyLight.rel
        FogBank,          // FogBank.rel
        LndCommon,        // LndCommon.rel
        StageRouteBlock,  // LndBlock.rel / LndRoute.rel
        EnemyLayout,      // enemy*.rel / enemy*.xnr
        QuestList,        // filelist.rel (Quest list)
        Collision,        // collision.xnr / collision.rel
        FileList          // *filelist.rel (16-category filename list)
    }

    public class FileListCategoryData
    {
        public int CategoryIndex { get; set; }
        public List<string> FileNames { get; set; } = new List<string>();
    }

    public class FileListData
    {
        public List<FileListCategoryData> Categories { get; set; } = new List<FileListCategoryData>();
    }

    public class CollisionPolygon
    {
        public uint Flags { get; set; }
        public ushort[] VertexIndices { get; set; } = new ushort[4];
        public Vector4 Plane { get; set; }
    }

    public class CollisionMeshData
    {
        public List<Vector3> Vertices { get; set; } = new List<Vector3>();
        public List<CollisionPolygon> Polygons { get; set; } = new List<CollisionPolygon>();
    }

    public class LndLightData
    {
        public Vector3 Direction { get; set; }
        public Color LightColor { get; set; } = Color.white;
    }

    public class LndGradientData
    {
        public float StartHeight { get; set; }
        public float EndHeight { get; set; }
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public float GradientMultiplier { get; set; }
        public float DestinationMultiplier { get; set; }
    }

    public class LndFogData
    {
        public float NearPlane { get; set; }
        public float FarPlane { get; set; }
        public float InitialIntensity { get; set; }
        public float RampUp { get; set; }
        public Color FogColor { get; set; } = Color.gray;
    }

    public class LndEffectData
    {
        public LndLightData PlayerLight1 { get; set; } = new LndLightData();
        public LndLightData PlayerLight2 { get; set; } = new LndLightData();
        public LndLightData PlayerLightAmbient { get; set; } = new LndLightData();
        public LndGradientData TopGradient { get; set; } = new LndGradientData();
        public LndGradientData BottomGradient { get; set; } = new LndGradientData();
        public LndFogData Fog { get; set; } = new LndFogData();

        public Vector3 SunPosition { get; set; }
        public float SunUnknown { get; set; }

        public float BlurStartDistance { get; set; }
        public float BlurUnknown { get; set; }
        public int BlurPixelCount { get; set; }
        public float BlurDistance { get; set; }
        public float BlurOpacity { get; set; }
    }

    public class LndEnemyLightData
    {
        public LndLightData Light1 { get; set; } = new LndLightData();
        public LndLightData Light2 { get; set; } = new LndLightData();
        public LndLightData LightAmbient { get; set; } = new LndLightData();
    }

    public class LndCommonData
    {
        public string NblFilenameFragment { get; set; } = "";
        public string XntFilenameFragment1 { get; set; } = "";
        public string XntFilenameFragment2 { get; set; } = "";
        public float UnknownFloat { get; set; }
    }

    public class SetObjectEntry
    {
        public int HeaderInt1 { get; set; }
        public int HeaderInt2 { get; set; }
        public int HeaderInt3 { get; set; }
        public short HeaderShort1 { get; set; }
        public short ObjID { get; set; }
        public int UnkInt1 { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public byte[] Metadata { get; set; }
    }

    public class SetListHeader
    {
        public int UnusedInt1 { get; set; }
        public Vector4 BoundSphere { get; set; }
        public short UnusedShort1 { get; set; }
        public short UnknownShort1 { get; set; }
        public int UnusedInt2 { get; set; }
        public short ListIndex { get; set; }
        public short UnknownPairedShort1 { get; set; }
        public short UnknownPairedShort2 { get; set; }
        public List<SetObjectEntry> Objects { get; set; } = new List<SetObjectEntry>();
    }

    public class SetMapListing
    {
        public short mapNumber; // Backwards compatibility field matching PSULib
        public short MapNumber { get => mapNumber; set => mapNumber = value; }
        public List<SetListHeader> Headers { get; set; } = new List<SetListHeader>();
    }

    public class SetFileData
    {
        public short AreaID { get; set; }
        public List<SetMapListing> MapData { get; set; } = new List<SetMapListing>();
    }

    public class EnemyMonsterEntryData
    {
        public short MonsterNum { get; set; }
        public short Element { get; set; }
        public byte KingBuff { get; set; }
        public byte Buff1 { get; set; }
        public byte Buff2 { get; set; }
        public byte Buff3 { get; set; }
        public byte Buff4 { get; set; }
        public byte UnkByte1 { get; set; }
        public short SpawnAnimation { get; set; }
        public short UnkShort2 { get; set; }
        public short SpawnDelay { get; set; }
        public short Count { get; set; }
        public short UnkShort3 { get; set; }
        public short UnkShort4 { get; set; }
        public short UnknownShort5 { get; set; }
        public short LevelModifier { get; set; }
        public short LevelCapUnused { get; set; }
        public short UnkShort7 { get; set; }
        public short UnkShort8 { get; set; }
        public int UnkInt1 { get; set; }
        public string EnemyName { get; set; } = "";
    }

    public class EnemyLayoutData
    {
        public List<List<EnemyMonsterEntryData>> Spawns { get; set; } = new List<List<EnemyMonsterEntryData>>();
    }

    public class QuestListingData
    {
        public int QuestNumber { get; set; }
        public string FileName { get; set; } = "";
    }

    public static class SetObjectDefinitions
    {
        public static readonly Dictionary<int, string> Definitions = new Dictionary<int, string>
        {
            { 4, "TObjUnbreak" }, { 5, "TObjSwitchContact" }, { 6, "TObjColliEffect" }, { 9, "TObjColliEvent" },
            { 10, "TObjColliBlock" }, { 12, "TObjBreak" }, { 14, "TObjUnitTransporter" }, { 17, "TObjFence" },
            { 18, "TObjNpc" }, { 20, "TObjDoor" }, { 22, "TObjSwitchTerminal" }, { 23, "TEnemyGateway" },
            { 24, "TEnemyGateway" }, { 25, "TEnemyGateway" }, { 26, "TObjColliStartWithEvent" },
            { 27, "TObjColliGoalWithEvent" }, { 28, "TObjColliPositionFlag" }, { 29, "TObjColliPath" },
            { 31, "TObjKey" }, { 33, "TObjColliCamera" }, { 35, "TBossGate" }, { 37, "TObjScenario" },
            { 39, "TObjDamage" }, { 40, "TObjSavePoint" }, { 41, "TObjTentacle" }, { 42, "TMyRoomSetter" },
            { 43, "TObjFixedBattery" }, { 44, "TObjBurstTrap" }, { 45, "TObjColliShopCounter" },
            { 46, "TObjColliQuestCounter" }, { 47, "TObjColliJobCounter" }, { 48, "TObjBossTransporter" },
            { 49, "TObjCheckPoint" }, { 50, "TObjCureMachine" }, { 51, "TObjColliSearch" }, { 52, "TObjSeedFlower" },
            { 53, "TObjNameBoard" }, { 54, "TObjSeedCore" }, { 55, "TObjPhotonPoint" }, { 56, "TObjColliChair" },
            { 57, "TObjItem" }, { 58, "TObjColliVehiclePos" }, { 59, "TObjImageBoard" }, { 60, "TObjServerTransporter" },
            { 61, "TObjLODModel" }, { 62, "TObjCureMachinePP" }, { 63, "TObjColliDressRoom" }, { 64, "TObjRadarMapMarker" },
            { 65, "TMyRoomSetterIlminas" }, { 66, "TSacredLotCounter" }, { 67, "TRouletteCounter" }, { 68, "TSlotMachineCounter" },
            { 69, "TMyRoomSetterCamera" }, { 70, "TObjFixedBatteryIlminas" }, { 71, "TObjBurstTrapIlminas" }, { 72, "TObjDrop" },
            { 73, "TObjColliAttack" }, { 74, "TObjTrapPath" }, { 75, "TObjMoving" }, { 76, "TObjEnergyPole" },
            { 77, "TObjBattleBaseBattery" }, { 78, "TObjBattleBaseMainUnit" }, { 79, "TObjBurstTrapSimple" },
            { 80, "TObjPSP2Catapult" }, { 81, "TObjEnemyPheromone" }, { 82, "TObjFixedPlayerBattery" }, { 83, "TObjBattleWall" },
            { 84, "TObjSwing" }, { 85, "TObjRolling" }, { 86, "TObjRoulette" }, { 87, "TObjAnimation" },
            { 88, "TObjSwitchParty" }, { 89, "TObjChat" }, { 90, "TObjGraveWindow" }, { 91, "TObjPress" },
            { 92, "TObjNeedle" }, { 93, "TObjCureMachineAccess" }, { 94, "TObjEnergyPoleEx" }, { 95, "TObjCityBoard" },
            { 96, "TObjMesetaDrop" }, { 97, "TObjColliCityFlag" }
        };

        public static string GetDefinitionName(int objID)
        {
            if (Definitions.TryGetValue(objID, out string name))
                return name;
            return $"UnknownObject_{objID}";
        }
    }
}