using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xv2CoreLib.Resource;
using YAXLib;

namespace Xv2CoreLib.BCS
{
    public class Parser
    {
        string saveLocation;
        byte[] rawBytes;
        private readonly List<int> recordOffsets = new List<int>();
        private int lastStringEnd;
        public BCS_File bcsFile { get; private set; } = new BCS_File();


        public Parser(string location, bool _writeXml = false)
        {
            saveLocation = location;
            rawBytes = File.ReadAllBytes(location);
            Parse();
            if (_writeXml)
            {
                YAXSerializer serializer = new YAXSerializer(typeof(BCS_File));
                serializer.SerializeToFile(bcsFile, saveLocation + ".xml");
            }
        }

        public Parser(byte[] _bytes)
        {
            rawBytes = _bytes;
            Parse();
        }

        public BCS_File GetBcsFile()
        {
            return bcsFile;
        }

        private void Parse()
        {
            switch(BitConverter.ToInt16(rawBytes, 6))
            {
                case 72:
                    bcsFile.Version = Version.XV1;
                    break;
                case 0: //Some Xv2 BCS files dont have a signature (first 8 bytes all null)
                case 76:
                    bcsFile.Version = Version.XV2;
                    break;
                default:
                    throw new InvalidDataException("BCS: Unknown BCS version!");
            }

            bcsFile.HasSignature = BitConverter.ToInt64(rawBytes, 0) != 0;

            //counts
            int partsetCount = BitConverter.ToUInt16(rawBytes, 12);
            int partcolorsCount = BitConverter.ToUInt16(rawBytes, 14);
            int bodyCount = BitConverter.ToUInt16(rawBytes, 16);

            //offsets
            int partsetOffset;
            int partcolorsOffset;
            int bodyOffset;
            int skeleton2Offset;
            int skeleton1Offset;

            if(bcsFile.Version == Version.XV1)
            {
                partsetOffset = BitConverter.ToInt32(rawBytes, 20);
                partcolorsOffset = BitConverter.ToInt32(rawBytes, 24);
                bodyOffset = BitConverter.ToInt32(rawBytes, 28);
                skeleton2Offset = 0;
                skeleton1Offset = 64; //XV1 BCS files have a skeleton instance directly in the header
                bcsFile.F_48 = BitConverter_Ex.ToFloat32Array(rawBytes, 36, 7);

                byte[] _I_44 = rawBytes.GetRange(32, 4);
                bcsFile.Race = (Race)_I_44[0];
                bcsFile.Gender = (Gender)_I_44[1];
            }
            else
            {
                partsetOffset = BitConverter.ToInt32(rawBytes, 24);
                partcolorsOffset = BitConverter.ToInt32(rawBytes, 28);
                bodyOffset = BitConverter.ToInt32(rawBytes, 32);
                skeleton2Offset = BitConverter.ToInt32(rawBytes, 36);
                skeleton1Offset = BitConverter.ToInt32(rawBytes, 40);
                bcsFile.F_48 = BitConverter_Ex.ToFloat32Array(rawBytes, 48, 7);

                bcsFile.I_46 = BitConverter.ToUInt16(rawBytes, 46);
                byte[] _I_44 = rawBytes.GetRange(44, 4);
                bcsFile.Race = (Race)_I_44[0];
                bcsFile.Gender = (Gender)_I_44[1];
            }

            recordOffsets.Add(rawBytes.Length);
            foreach (int table in new[] { partcolorsOffset, bodyOffset, skeleton1Offset, skeleton2Offset })
                if (table > 0) recordOffsets.Add(BitConverter.ToInt32(rawBytes, table));
            for (int partSetIndex = 0; partSetIndex < partsetCount; partSetIndex++)
            {
                int partSetStart = BitConverter.ToInt32(rawBytes, partsetOffset + partSetIndex * 4);
                if (partSetStart == 0) continue;
                recordOffsets.Add(partSetStart);
                int partTable = partSetStart + BitConverter.ToInt32(rawBytes, partSetStart + 24);
                for (int partIndex = 0; partIndex < 10; partIndex++)
                {
                    int partStart = BitConverter.ToInt32(rawBytes, partTable + partIndex * 4);
                    if (partStart > 0) recordOffsets.Add(partSetStart + partStart);
                }
            }

            //PartSets
            int actualIndex = 0;

            if(partsetCount > 0)
            {
                for (int i = 0; i < partsetCount; i++)
                {
                    int thisPartsetOffset = BitConverter.ToInt32(rawBytes, partsetOffset);

                    if(thisPartsetOffset != 0)
                    {
                        bcsFile.PartSets.Add(new PartSet());
                        bcsFile.PartSets[actualIndex].ID = i;

                        if (BitConverter.ToInt32(rawBytes, thisPartsetOffset + 20) != 10)
                        {
                            throw new Exception(string.Format("Part count mismatch on PartSet {0} (Expected 10, but found {1})\nThis BCS file cannot be parsed.", i, BitConverter.ToInt32(rawBytes, thisPartsetOffset + 20)));
                        }

                        int tableOffset = thisPartsetOffset + BitConverter.ToInt32(rawBytes, thisPartsetOffset + 24);
                        
                        bcsFile.PartSets[actualIndex].FaceBase = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 0), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].FaceForehead = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 4), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].FaceEye = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 8), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].FaceNose = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 12), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].FaceEar = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 16), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].Hair = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 20), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].Bust = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 24), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].Pants = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 28), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].Rist = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 32), thisPartsetOffset);
                        bcsFile.PartSets[actualIndex].Boots = ParsePart(BitConverter.ToInt32(rawBytes, tableOffset + 36), thisPartsetOffset);

                        actualIndex++;
                    }

                    partsetOffset += 4;
                }
            }

            //PartColors
            if(partcolorsCount > 0)
            {
                for(int i = 0; i < partcolorsCount; i++)
                {
                    int thisPartColorOffset = BitConverter.ToInt32(rawBytes, partcolorsOffset);

                    if(thisPartColorOffset != 0)
                    {
                        bcsFile.PartColors.Add(new PartColor()
                        {
                            Index = i.ToString(),
                            Name = ReadString(BitConverter.ToInt32(rawBytes, thisPartColorOffset + 0) + thisPartColorOffset),
                            ColorsList = ParseColors(BitConverter.ToInt32(rawBytes, thisPartColorOffset + 12) + thisPartColorOffset, BitConverter.ToInt16(rawBytes, thisPartColorOffset + 10))
                        });
                    }

                    partcolorsOffset += 4;
                }
            }

            //BodyScales
            if(bodyCount > 0)
            {
                for(int i = 0; i < bodyCount; i++)
                {
                    int thisBodyScaleOffset = BitConverter.ToInt32(rawBytes, bodyOffset);

                    if(thisBodyScaleOffset != 0)
                    {
                        Body body = ParseBody(thisBodyScaleOffset, i);

                        bcsFile.Bodies.Add(body);
                    }

                    bodyOffset += 4;
                }

            }

            if(skeleton1Offset != 0)
            {
                bcsFile.SkeletonData1 = ParseSkeleton(bcsFile.Version == Version.XV1 ? skeleton1Offset : BitConverter.ToInt32(rawBytes, skeleton1Offset));
            }

            if(skeleton2Offset != 0)
            {
                bcsFile.SkeletonData2 = ParseSkeleton(BitConverter.ToInt32(rawBytes, skeleton2Offset));
                int skeletonCount = BitConverter.ToUInt16(rawBytes, 18);
                if (skeletonCount > 1)
                {
                    bcsFile.ExtraSkeletonData2 = new List<SkeletonData>();
                    for (int skeletonIndex = 1; skeletonIndex < skeletonCount; skeletonIndex++)
                        bcsFile.ExtraSkeletonData2.Add(ParseSkeleton(BitConverter.ToInt32(rawBytes, skeleton2Offset + skeletonIndex * 4)));
                }
            }
            if (lastStringEnd > 0)
            {
                if (rawBytes.Skip(lastStringEnd).Any(value => value != 0))
                    throw new InvalidDataException("BCS has nonzero data after its last string.");
                bcsFile.TrailingZeroCount = rawBytes.Length - lastStringEnd;
            }
        }


        private string ReadString(int offset)
        {
            int end = Array.IndexOf(rawBytes, (byte)0, offset);
            if (end < 0) throw new InvalidDataException("BCS string has no end.");
            recordOffsets.Add(offset);
            lastStringEnd = Math.Max(lastStringEnd, end + 1);
            return StringEx.GetString(rawBytes, offset, false);
        }

        //Part Parsers
        private Part ParsePart(int offset, int partOffset)
        {
            if(offset != 0)
            {
                offset += partOffset;
                
                //validation
                int i_28 = BitConverter.ToInt32(rawBytes, offset + 28);
                int i_32 = BitConverter.ToInt32(rawBytes, offset + 32);

                if (i_28 > 0x3FF)
                    throw new InvalidDataException($"Unexpected I_28 value: {i_28}.");

                if (i_32 > 0x3FF)
                    throw new InvalidDataException($"Unexpected I_32 value: {i_32}.");

                var part = new Part()
                {
                    Model = BitConverter.ToInt16(rawBytes, offset + 0),
                    Model2 = BitConverter.ToInt16(rawBytes, offset + 2),
                    Texture = BitConverter.ToInt16(rawBytes, offset + 4),
                    Shader = BitConverter.ToInt16(rawBytes, offset + 16),
                    Flags = (Part.PartFlags)BitConverter.ToUInt32(rawBytes, offset + 24),
                    HideFlags = (PartTypeFlags)BitConverter.ToInt32(rawBytes, offset + 28),
                    HideMatFlags = (PartTypeFlags)BitConverter.ToInt32(rawBytes, offset + 32),
                    F_36 = BitConverter.ToSingle(rawBytes, offset + 36),
                    F_40 = BitConverter.ToSingle(rawBytes, offset + 40),
                    I_44 = BitConverter.ToInt32(rawBytes, offset + 44),
                    I_48 = BitConverter.ToInt32(rawBytes, offset + 48),
                    CharaCode = StringEx.GetString(rawBytes, offset + 52, false, StringEx.EncodingType.ASCII, 4),
                    EmdPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 56), offset),
                    EmmPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 60), offset),
                    EmbPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 64), offset),
                    EanPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 68), offset),
                    ColorSelectors = ParseColorSelector(BitConverter.ToInt32(rawBytes, offset + 20) + offset, BitConverter.ToInt16(rawBytes, offset + 18)),
                    PhysicsParts = ParsePhysicsObject(BitConverter.ToInt32(rawBytes, offset + 76) + offset, BitConverter.ToInt16(rawBytes, offset + 74))
                };

                if(bcsFile.Version == Version.XV2)
                {
                    int hideCount = BitConverter.ToUInt16(rawBytes, offset + 82);
                    if (hideCount > 0)
                    {
                        int hideOffset = BitConverter.ToInt32(rawBytes, offset + 84);
                        if (hideOffset < 0 || hideOffset > rawBytes.Length - offset)
                            throw new InvalidDataException("BCS hide exception offset is outside the file.");
                        int hideStart = hideOffset + offset;
                        int hideEnd = recordOffsets.Where(value => value > hideStart).DefaultIfEmpty(rawBytes.Length).Min();
                        if (hideEnd > rawBytes.Length || hideEnd - hideStart < hideCount * 8 || (hideEnd - hideStart) % 4 != 0)
                            throw new InvalidDataException("BCS hide exception block does not fit its records.");
                        part.HideExceptions = ParseHideExceptions(hideStart, hideCount);
                        part.HideExceptionDataSize = hideEnd - hideStart;
                        var usedOffsets = new HashSet<int>();
                        for (int recordIndex = 0; recordIndex < hideCount; recordIndex++)
                        {
                            usedOffsets.Add(recordIndex * 8);
                            usedOffsets.Add(recordIndex * 8 + 4);
                            HideException record = part.HideExceptions[recordIndex];
                            for (int modelIndex = 0; modelIndex < record.Models.Count; modelIndex++)
                                usedOffsets.Add(recordIndex * 8 + record.ListOffset.Value + modelIndex * 4);
                        }
                        part.UnusedHideExceptionValues = new List<HideExceptionValue>();
                        for (int valueOffset = 0; valueOffset < part.HideExceptionDataSize; valueOffset += 4)
                        {
                            if (!usedOffsets.Contains(valueOffset))
                                part.UnusedHideExceptionValues.Add(new HideExceptionValue { Offset = valueOffset, Value = BitConverter.ToInt32(rawBytes, hideStart + valueOffset) });
                        }
                        int nextList = hideCount * 8;
                        bool separateLists = true;
                        for (int recordIndex = 0; recordIndex < hideCount; recordIndex++)
                        {
                            HideException record = part.HideExceptions[recordIndex];
                            separateLists &= recordIndex * 8 + record.ListOffset.Value == nextList;
                            nextList += record.Models.Count * 4;
                        }
                        if (separateLists && nextList == part.HideExceptionDataSize)
                        {
                            part.HideExceptionDataSize = null;
                            foreach (HideException record in part.HideExceptions) record.ListOffset = null;
                        }
                        if (part.UnusedHideExceptionValues.Count == 0) part.UnusedHideExceptionValues = null;
                    }
                }

                return part;

            } 
            else
            {
                return null;
            }
            
        }

        private AsyncObservableCollection<ColorSelector> ParseColorSelector(int offset, int count)
        {
            AsyncObservableCollection<ColorSelector> colorSelectors = new AsyncObservableCollection<ColorSelector>();

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    colorSelectors.Add(new ColorSelector()
                    {
                        PartColorGroup = BitConverter.ToUInt16(rawBytes, offset + 0),
                        ColorIndex = BitConverter.ToUInt16(rawBytes, offset + 2)
                    });
                    offset += 4;
                }
            }

            return colorSelectors;
        }

        private AsyncObservableCollection<PhysicsPart> ParsePhysicsObject(int offset, int count)
        {
            AsyncObservableCollection<PhysicsPart> physicsObjects = new AsyncObservableCollection<PhysicsPart>();

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    //validation
                    int i_28 = BitConverter.ToInt32(rawBytes, offset + 28);
                    int i_32 = BitConverter.ToInt32(rawBytes, offset + 32);

                    if (i_28 > 0x200)
                        throw new InvalidDataException($"Unexpected I_28 value: {i_28}.");

                    if (i_32 > 0x200)
                        throw new InvalidDataException($"Unexpected I_32 value: {i_32}.");

                    physicsObjects.Add(new PhysicsPart()
                    {
                        Model1 = BitConverter.ToInt16(rawBytes, offset + 0),
                        Model2 = BitConverter.ToInt16(rawBytes, offset + 2),
                        Texture = BitConverter.ToInt16(rawBytes, offset + 4),
                        Flags = (Part.PartFlags)BitConverter.ToInt32(rawBytes, offset + 24),
                        HideFlags = (PartTypeFlags)BitConverter.ToInt32(rawBytes, offset + 28),
                        HideMatFlags = (PartTypeFlags)BitConverter.ToInt32(rawBytes, offset + 32),
                        CharaCode = StringEx.GetString(rawBytes, offset + 36, false, StringEx.EncodingType.ASCII, 4),
                        EmdPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 40), offset),
                        EmmPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 44), offset),
                        EmbPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 48), offset),
                        EanPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 52), offset),
                        BoneToAttach = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 56), offset),
                        ScdPath = GetStringWrapper(BitConverter.ToInt32(rawBytes, offset + 60), offset)
                    });
                    offset += 72;
                }
            }

            return physicsObjects;
        }
        
        private List<HideException> ParseHideExceptions(int offset, int count)
        {
            if (count <= 0) return null;
            if (offset < 0 || offset > rawBytes.Length || count > (rawBytes.Length - offset) / 8)
                throw new InvalidDataException("BCS hide exception records are outside the file.");

            List<HideException> hideExceptions = new List<HideException>();

            for (int i = 0; i < count; i++)
            {
                int recordOffset = offset + (i * 8);
                int modelCount = BitConverter.ToUInt16(rawBytes, recordOffset + 2);
                int listOffset = BitConverter.ToInt32(rawBytes, recordOffset + 4);
                if (listOffset < 0 || listOffset > rawBytes.Length - recordOffset || modelCount > (rawBytes.Length - recordOffset - listOffset) / 4)
                    throw new InvalidDataException("BCS hide exception model list is outside the file.");

                hideExceptions.Add(new HideException()
                {
                    PartType = BitConverter.ToUInt16(rawBytes, recordOffset),
                    ListOffset = listOffset,
                    Models = new List<int>(BitConverter_Ex.ToInt32Array(rawBytes, recordOffset + listOffset, modelCount))
                });
            }

            return hideExceptions;
        }

        //Color Parsers
        private AsyncObservableCollection<Colors> ParseColors (int offset, int count)
        {
            AsyncObservableCollection<Colors> colors = new AsyncObservableCollection<Colors>();

            if (count > 0)
            {
                for(int i = 0; i < count; i++)
                {
                    Colors entry = new Colors()
                    {
                        ID = i,
                        Color1 = new LB_Common.Numbers.CustomColor(BitConverter.ToSingle(rawBytes, offset + 0),
                                                                   BitConverter.ToSingle(rawBytes, offset + 4),
                                                                   BitConverter.ToSingle(rawBytes, offset + 8),
                                                                   BitConverter.ToSingle(rawBytes, offset + 12)),
                        Color2 = new LB_Common.Numbers.CustomColor(BitConverter.ToSingle(rawBytes, offset + 16),
                                                                   BitConverter.ToSingle(rawBytes, offset + 20),
                                                                   BitConverter.ToSingle(rawBytes, offset + 24),
                                                                   BitConverter.ToSingle(rawBytes, offset + 28)),
                        Color3 = new LB_Common.Numbers.CustomColor(BitConverter.ToSingle(rawBytes, offset + 32),
                                                                   BitConverter.ToSingle(rawBytes, offset + 36),
                                                                   BitConverter.ToSingle(rawBytes, offset + 40),
                                                                   BitConverter.ToSingle(rawBytes, offset + 44)),
                        Color4 = new LB_Common.Numbers.CustomColor(BitConverter.ToSingle(rawBytes, offset + 48),
                                                                   BitConverter.ToSingle(rawBytes, offset + 52),
                                                                   BitConverter.ToSingle(rawBytes, offset + 56),
                                                                   BitConverter.ToSingle(rawBytes, offset + 60))
                    };

                    if(!entry.IsNull())
                        colors.Add(entry);

                    offset += 80;
                }
            }

            return colors;
        }
        
        //Body Parsers
        private Body ParseBody(int offset, int _index)
        {
            Body body = new Body();
            body.ID = _index;

            int bodyCount = BitConverter.ToInt16(rawBytes, offset + 2);
            int bodyOffset = BitConverter.ToInt32(rawBytes, offset + 4) + offset;
            
            for(int i = 0; i < bodyCount; i++)
            {
                body.BodyScales.Add(new BoneScale()
                {
                    ScaleX = BitConverter.ToSingle(rawBytes, bodyOffset + 0),
                    ScaleY = BitConverter.ToSingle(rawBytes, bodyOffset + 4),
                    ScaleZ = BitConverter.ToSingle(rawBytes, bodyOffset + 8),
                    BoneName = ReadString(BitConverter.ToInt32(rawBytes, bodyOffset + 12) + bodyOffset)
                });
                bodyOffset += 16;
            }

            return body;
        }

        //Skeleton Parsers
        private SkeletonData ParseSkeleton (int offset)
        {
            SkeletonData skeleton = new SkeletonData();

            if (offset != 0)
            {
                int relativeTo = bcsFile.Version == Version.XV1 ? 32 : offset;

                skeleton.I_00 = BitConverter.ToInt16(rawBytes, offset);
                int boneCount = BitConverter.ToInt16(rawBytes, offset + 2);
                int boneOffset = BitConverter.ToInt32(rawBytes, offset + 4) + relativeTo;

                if (boneCount > 0)
                {
                    skeleton.Bones = new AsyncObservableCollection<Bone>();

                    for (int i = 0; i < boneCount; i++)
                    {
                        if(bcsFile.Version == Version.XV1)
                        {
                            skeleton.Bones.Add(new Bone()
                            {
                                I_00 = BitConverter.ToInt32(rawBytes, boneOffset + 0),
                                I_04 = BitConverter.ToInt32(rawBytes, boneOffset + 4),
                                F_12 = BitConverter.ToSingle(rawBytes, boneOffset + 16),
                                F_16 = BitConverter.ToSingle(rawBytes, boneOffset + 20),
                                F_20 = BitConverter.ToSingle(rawBytes, boneOffset + 24),
                                F_24 = BitConverter.ToSingle(rawBytes, boneOffset + 28),
                                F_28 = BitConverter.ToSingle(rawBytes, boneOffset + 32),
                                F_32 = BitConverter.ToSingle(rawBytes, boneOffset + 36),
                                F_36 = BitConverter.ToSingle(rawBytes, boneOffset + 40),
                                F_40 = BitConverter.ToSingle(rawBytes, boneOffset + 44),
                                F_44 = BitConverter.ToSingle(rawBytes, boneOffset + 48),
                                BoneName = ReadString(BitConverter.ToInt32(rawBytes, boneOffset + 12) + boneOffset)
                            });
                        }
                        else
                        {
                            skeleton.Bones.Add(new Bone()
                            {
                                I_00 = BitConverter.ToInt32(rawBytes, boneOffset + 0),
                                I_04 = BitConverter.ToInt32(rawBytes, boneOffset + 4),
                                F_12 = BitConverter.ToSingle(rawBytes, boneOffset + 12),
                                F_16 = BitConverter.ToSingle(rawBytes, boneOffset + 16),
                                F_20 = BitConverter.ToSingle(rawBytes, boneOffset + 20),
                                F_24 = BitConverter.ToSingle(rawBytes, boneOffset + 24),
                                F_28 = BitConverter.ToSingle(rawBytes, boneOffset + 28),
                                F_32 = BitConverter.ToSingle(rawBytes, boneOffset + 32),
                                F_36 = BitConverter.ToSingle(rawBytes, boneOffset + 36),
                                F_40 = BitConverter.ToSingle(rawBytes, boneOffset + 40),
                                F_44 = BitConverter.ToSingle(rawBytes, boneOffset + 44),
                                BoneName = ReadString(BitConverter.ToInt32(rawBytes, boneOffset + 48) + boneOffset)
                            });
                        }

                        boneOffset += 52;
                    }
                }
            }

            return skeleton;
        }

        //Utility
        
        private string GetStringWrapper(int relativeOffset, int mainOffset)
        {
            if(relativeOffset != 0)
            {
                return ReadString(relativeOffset + mainOffset);
            }
            else
            {
                return string.Empty;
            }
        }

    }
}
