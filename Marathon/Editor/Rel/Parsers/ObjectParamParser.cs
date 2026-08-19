using UnityEngine;
using System.Collections.Generic;
using Marathon.IO;

namespace SilentTools
{
    public static class ObjectParamParser
    {
        public static ObjectParamData Parse(BinaryReaderEx reader, uint fileSize, uint baseAddr, uint headerLoc)
        {
            ObjectParamData data = new ObjectParamData();
            if (headerLoc + 8 > fileSize) return data;

            reader.JumpTo(headerLoc);
            int objCount = reader.ReadInt32();
            int rawTocPtr = reader.ReadInt32();
            uint tocLoc = RelResolver.ResolveOffset(rawTocPtr, fileSize, baseAddr);

            if (objCount <= 0 || objCount > 5000 || tocLoc >= fileSize)
                return data;

            for (int i = 0; i < objCount; i++)
            {
                if (tocLoc + i * 8 + 8 > fileSize) break;
                reader.JumpTo(tocLoc + i * 8);
                int objId = reader.ReadInt32();
                int rawObjPtr = reader.ReadInt32();
                uint objLoc = RelResolver.ResolveOffset(rawObjPtr, fileSize, baseAddr);

                if (objLoc + 20 > fileSize) continue;

                reader.JumpTo(objLoc);
                int rawP1 = reader.ReadInt32();
                int rawP2 = reader.ReadInt32();
                int rawP3 = reader.ReadInt32();
                int rawP4 = reader.ReadInt32();
                int rawP5 = reader.ReadInt32();

                uint p1 = RelResolver.ResolveOffset(rawP1, fileSize, baseAddr);
                uint p2 = RelResolver.ResolveOffset(rawP2, fileSize, baseAddr);
                uint p3 = RelResolver.ResolveOffset(rawP3, fileSize, baseAddr);
                uint p4 = RelResolver.ResolveOffset(rawP4, fileSize, baseAddr);
                uint p5 = RelResolver.ResolveOffset(rawP5, fileSize, baseAddr);

                ObjectParamEntry entry = new ObjectParamEntry();

                // Group 1: State / Interaction entries (8 bytes each)
                if (p1 > 0 && p1 + 8 <= fileSize)
                {
                    reader.JumpTo(p1);
                    int g1Count = reader.ReadInt32();
                    uint g1Loc = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);

                    if (g1Count > 0 && g1Count < 1000 && g1Loc + g1Count * 8 <= fileSize)
                    {
                        reader.JumpTo(g1Loc);
                        for (int j = 0; j < g1Count; j++)
                        {
                            entry.GroupOneEntries.Add(new ObjectGroup1Entry
                            {
                                Byte1 = reader.ReadByte(),
                                Byte2 = reader.ReadByte(),
                                Byte3 = reader.ReadByte(),
                                Byte4 = reader.ReadByte(),
                                Byte5 = reader.ReadByte(),
                                Byte6 = reader.ReadByte(),
                                Byte7 = reader.ReadByte(),
                                Byte8 = reader.ReadByte()
                            });
                        }
                    }
                }

                // Group 2: Hitbox
                if (p2 > 0 && p2 + 36 <= fileSize)
                {
                    reader.JumpTo(p2);
                    entry.Hitbox = new ObjectHitbox
                    {
                        HitboxShape = reader.ReadInt32(),
                        UnknownFloat2 = reader.ReadSingle(),
                        UnknownFloat3 = reader.ReadSingle(),
                        UnknownFloat4 = reader.ReadSingle(),
                        UnknownInt5 = reader.ReadInt32(),
                        UnknownFloat6 = reader.ReadSingle(),
                        UnusedValue7 = reader.ReadInt32(),
                        UnusedValue8 = reader.ReadInt32(),
                        UnknownInt9 = reader.ReadInt32()
                    };
                }

                // Group 3: Animations (0x30 bytes per record)
                if (p3 > 0 && p3 + 8 <= fileSize)
                {
                    reader.JumpTo(p3);
                    int aCount = reader.ReadInt32();
                    uint aLoc = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);

                    if (aCount > 0 && aCount < 1000 && aLoc + aCount * 0x30 <= fileSize)
                    {
                        for (int j = 0; j < aCount; j++)
                        {
                            reader.JumpTo(aLoc + j * 0x30);
                            ObjectAnimationReference anim = new ObjectAnimationReference
                            {
                                UnknownIdentifier1 = reader.ReadInt32()
                            };
                            int str1Ptr = reader.ReadInt32();
                            anim.UnknownFloat1 = reader.ReadSingle();
                            anim.UnknownFloat2 = reader.ReadSingle();
                            anim.UnknownFloat3 = reader.ReadSingle();
                            anim.UnknownIdentifier2 = reader.ReadInt32();
                            int str2Ptr = reader.ReadInt32();
                            anim.UnknownFloat4 = reader.ReadSingle();
                            anim.UnknownFloat5 = reader.ReadSingle();
                            anim.UnknownFloat6 = reader.ReadSingle();
                            anim.UnknownInt1 = reader.ReadInt32();
                            anim.UnknownInt2 = reader.ReadInt32();

                            uint str1Loc = RelResolver.ResolveOffset(str1Ptr, fileSize, baseAddr);
                            if (str1Loc > 0 && str1Loc < fileSize)
                            {
                                reader.JumpTo(str1Loc);
                                anim.TexAnimName = reader.ReadNullTerminatedString();
                            }

                            uint str2Loc = RelResolver.ResolveOffset(str2Ptr, fileSize, baseAddr);
                            if (str2Loc > 0 && str2Loc < fileSize)
                            {
                                reader.JumpTo(str2Loc);
                                anim.BoneAnimName = reader.ReadNullTerminatedString();
                            }

                            entry.Animations.Add(anim);
                        }
                    }
                }

                // Group 4: Particle & Sound Event Bindings
                if (p4 > 0 && p4 + 10 <= fileSize)
                {
                    reader.JumpTo(p4);
                    int sub1Ptr = reader.ReadInt32();
                    int sub2Ptr = reader.ReadInt32();
                    byte c1 = reader.ReadByte();
                    byte c2 = reader.ReadByte();
                    ushort mystery = reader.ReadUInt16();

                    ObjectParticleSoundReferenceList psRef = new ObjectParticleSoundReferenceList
                    {
                        MysteryData = mystery
                    };

                    uint loc1 = RelResolver.ResolveOffset(sub1Ptr, fileSize, baseAddr);
                    if (c1 > 0 && loc1 > 0 && loc1 + c1 * 24 <= fileSize)
                    {
                        for (int j = 0; j < c1; j++)
                        {
                            reader.JumpTo(loc1 + j * 24);
                            int s1 = reader.ReadInt32();
                            int s2 = reader.ReadInt32();
                            int e3 = reader.ReadInt32();
                            int e4 = reader.ReadInt32();
                            int e5 = reader.ReadInt32();
                            int u6 = reader.ReadInt32();

                            ParticleBinding pb = new ParticleBinding
                            {
                                EmptyInt3 = e3,
                                EmptyInt4 = e4,
                                EmptyInt5 = e5,
                                UsedInt6 = u6
                            };

                            uint s1Loc = RelResolver.ResolveOffset(s1, fileSize, baseAddr);
                            if (s1Loc > 0 && s1Loc < fileSize) { reader.JumpTo(s1Loc); pb.ParticleName = reader.ReadNullTerminatedString(); }

                            uint s2Loc = RelResolver.ResolveOffset(s2, fileSize, baseAddr);
                            if (s2Loc > 0 && s2Loc < fileSize) { reader.JumpTo(s2Loc); pb.EventName = reader.ReadNullTerminatedString(); }

                            psRef.ParticleBindings.Add(pb);
                        }
                    }

                    uint loc2 = RelResolver.ResolveOffset(sub2Ptr, fileSize, baseAddr);
                    if (c2 > 0 && loc2 > 0 && loc2 + c2 * 24 <= fileSize)
                    {
                        for (int j = 0; j < c2; j++)
                        {
                            reader.JumpTo(loc2 + j * 24);
                            int sid = reader.ReadInt32();
                            int sPtr = reader.ReadInt32();
                            float f2 = reader.ReadSingle();
                            int e3 = reader.ReadInt32();
                            int e4 = reader.ReadInt32();
                            int e5 = reader.ReadInt32();

                            SoundBinding sb = new SoundBinding
                            {
                                SoundId = sid,
                                UnknownFloat2 = f2,
                                EmptyInt3 = e3,
                                EmptyInt4 = e4,
                                EmptyInt5 = e5
                            };

                            uint sLoc = RelResolver.ResolveOffset(sPtr, fileSize, baseAddr);
                            if (sLoc > 0 && sLoc < fileSize) { reader.JumpTo(sLoc); sb.EventName = reader.ReadNullTerminatedString(); }

                            psRef.SoundBindings.Add(sb);
                        }
                    }

                    entry.ParticleSoundReferences = psRef;
                }

                // Group 5: Model References
                if (p5 > 0 && p5 + 8 <= fileSize)
                {
                    reader.JumpTo(p5);
                    int mCount = reader.ReadInt32();
                    uint mLoc = RelResolver.ResolveOffset(reader.ReadInt32(), fileSize, baseAddr);

                    if (mCount > 0 && mCount < 100 && mLoc + mCount * 8 <= fileSize)
                    {
                        for (int j = 0; j < mCount; j++)
                        {
                            reader.JumpTo(mLoc + j * 8);
                            int mid = reader.ReadInt32();
                            int mleafPtr = reader.ReadInt32();

                            if (mid != -1 && mleafPtr != 0)
                            {
                                uint mleafLoc = RelResolver.ResolveOffset(mleafPtr, fileSize, baseAddr);
                                if (mleafLoc > 0 && mleafLoc + 8 <= fileSize)
                                {
                                    reader.JumpTo(mleafLoc);
                                    int strPtr = reader.ReadInt32();
                                    float dist = reader.ReadSingle();

                                    string mName = "";
                                    uint strLoc = RelResolver.ResolveOffset(strPtr, fileSize, baseAddr);
                                    if (strLoc > 0 && strLoc < fileSize)
                                    {
                                        reader.JumpTo(strLoc);
                                        mName = reader.ReadNullTerminatedString();
                                    }

                                    entry.Models.Add(new ObjectModelReference
                                    {
                                        Id = mid,
                                        FileName = mName,
                                        RenderDistance = dist
                                    });
                                }
                            }
                        }
                    }
                }

                data.ObjectDefinitions[objId] = entry;
            }

            return data;
        }
    }
}