using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xv2CoreLib.Resource;
using YAXLib;

namespace Xv2CoreLib.BCS
{
    public class Deserializer
    {
        string saveLocation;
        BCS_File bcsFile;
        public List<byte> bytes { get; private set; } = new List<byte>() { 35, 66, 67, 83, 254, 255 };

        //Counts
        int PartSetCount { get; set; }
        int PartColorCount { get; set; }
        int BodyCount { get; set; }

        //Offset Lists
        List<int> PartSetTable = new List<int>();
        List<int> PartColorTable = new List<int>();
        List<int> BodyTable = new List<int>();
        int Skeleton2Table = 0;
        int Skeleton1Table = 0;

        //Strings
        private readonly List<HideExceptionValue> hideReferences = new List<HideExceptionValue>();

        List<StringWriter.StringInfo> stringInfo = new List<StringWriter.StringInfo>();

        public Deserializer(string location)
        {
            saveLocation = String.Format("{0}/{1}", Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location));
            YAXSerializer serializer = new YAXSerializer(typeof(BCS_File), YAXSerializationOptions.DontSerializeNullObjects);
            bcsFile = (BCS_File)serializer.DeserializeFromFile(location);
            bcsFile.SortEntries();

            Write();
            File.WriteAllBytes(saveLocation, bytes.ToArray());
        }

        public Deserializer(BCS_File _bcsFile, string location)
        {
            saveLocation = location;
            bcsFile = _bcsFile;
            _bcsFile.SortEntries();

            Write();
            File.WriteAllBytes(saveLocation, bytes.ToArray());
        }

        public Deserializer(BCS_File _bcsFile)
        {
            bcsFile = _bcsFile;
            _bcsFile.SortEntries();

            Write();
        }

        private void Write()
        {
            PartSetCount = bcsFile.PartSets?.Count > 0 ? bcsFile.PartSets.Max(x => x.ID) + 1 : 0;
            PartColorCount = bcsFile.PartColors?.Count > 0 ? bcsFile.PartColors.Max(x => x.ID) + 1 : 0;
            BodyCount = bcsFile.Bodies?.Count > 0 ? bcsFile.Bodies.Max(x => x.ID) + 1 : 0;

            if(PartSetCount > ushort.MaxValue)
            {
                throw new InvalidOperationException($"BCS: Some PartSet IDs exceeds the maximum of {ushort.MaxValue}.");
            }

            //Header
            int I_18 = (bcsFile.SkeletonData2?.Bones?.Count > 0) ? 1 + (bcsFile.ExtraSkeletonData2?.Count ?? 0) : 0;
            byte[] _i_44 = { (byte)bcsFile.Race, (byte)bcsFile.Gender, (byte)bcsFile.I_46, (byte)(bcsFile.I_46 >> 8) };
            int partSetOffset;
            int partColorOffset;
            int bodyOffset;

            if (bcsFile.Version == Version.XV1)
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)72));
                bytes.AddRange(BitConverter.GetBytes(0));
                bytes.AddRange(BitConverter.GetBytes((ushort)PartSetCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)PartColorCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)BodyCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)I_18));
                partSetOffset = 20;
                partColorOffset = 24;
                bodyOffset = 28;
                bytes.AddRange(new byte[12]);
                bytes.AddRange(_i_44);
                Assertion.AssertArraySize(bcsFile.F_48, 7, "BCS", "F_48");
                bytes.AddRange(BitConverter_Ex.GetBytes(bcsFile.F_48));

                if(bcsFile.SkeletonData1?.Bones?.Count > 0)
                {
                    bytes.AddRange(new byte[2]);
                    bytes.AddRange(BitConverter.GetBytes((ushort)bcsFile.SkeletonData1.Bones.Count));
                    bytes.AddRange(new byte[4]);
                }
                else
                {
                    bytes.AddRange(new byte[8]);
                }

            }
            else
            {
                bytes.AddRange(BitConverter.GetBytes((ushort)76));
                bytes.AddRange(BitConverter.GetBytes(0));
                bytes.AddRange(BitConverter.GetBytes((ushort)PartSetCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)PartColorCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)BodyCount));
                bytes.AddRange(BitConverter.GetBytes((ushort)I_18));
                partSetOffset = 24;
                partColorOffset = 28;
                bodyOffset = 32;
                bytes.AddRange(new byte[24]);
                bytes.AddRange(_i_44);
                Assertion.AssertArraySize(bcsFile.F_48, 7, "BCS", "F_48");
                bytes.AddRange(BitConverter_Ex.GetBytes(bcsFile.F_48));
            }


            //PartSet Table
            if(bcsFile.PartSets?.Count > 0)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), partSetOffset);

                for (int i = 0; i < PartSetCount; i++)
                {
                    if (bcsFile.PartSets.FirstOrDefault(x => x.ID == i) != null)
                    {
                        PartSetTable.Add(bytes.Count);
                        bytes.AddRange(new byte[4]);
                    }
                    else
                    {
                        //Null entry
                        bytes.AddRange(new byte[4]);
                    }
                }
            }

            //PartColor Table
            if (bcsFile.PartColors?.Count > 0)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), partColorOffset);

                for (int i = 0; i < PartColorCount; i++)
                {
                    if (bcsFile.PartColors.FirstOrDefault(x => x.ID == i) != null)
                    {
                        PartColorTable.Add(bytes.Count);
                        bytes.AddRange(new byte[4]);
                    }
                    else
                    {
                        //Null entry
                        bytes.AddRange(new byte[4]);
                    }
                }
            }

            //Body Table
            if (bcsFile.Bodies?.Count > 0)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), bodyOffset);

                for (int i = 0; i < BodyCount; i++)
                {
                    if(bcsFile.Bodies.FirstOrDefault(x => x.ID == i) != null)
                    {
                        BodyTable.Add(bytes.Count);
                        bytes.AddRange(new byte[4]);
                    }
                    else
                    {
                        //Null entry
                        bytes.AddRange(new byte[4]);
                    }
                }
            }
            
            if(bcsFile.Version == Version.XV2)
            {
                //Skeleton1 Table
                if (bcsFile.SkeletonData1?.Bones?.Count > 0)
                {
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), 40);
                    Skeleton1Table = bytes.Count;
                    bytes.AddRange(new byte[4]);
                }

                //Skeleton2 Table
                if (bcsFile.SkeletonData2?.Bones?.Count > 0)
                {
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), 36);
                    Skeleton2Table = bytes.Count;
                    bytes.AddRange(new byte[I_18 * 4]);
                }
            }

            //PartSets
            if (bcsFile.PartSets?.Count > 0)
            {
                for(int i = 0; i < bcsFile.PartSets.Count; i++)
                {
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), PartSetTable[i]);
                    int PartSetStart = bytes.Count;

                    bytes.AddRange(new byte[20]);
                    bytes.AddRange(BitConverter.GetBytes(10));
                    bytes.AddRange(BitConverter.GetBytes(32));
                    bytes.AddRange(new byte[4]);
                    int[] PartSetSubTable = new int[10] { bytes.Count + 0, bytes.Count + 4, bytes.Count + 8, bytes.Count + 12, bytes.Count + 16, bytes.Count + 20, bytes.Count + 24, bytes.Count + 28, bytes.Count + 32, bytes.Count + 36 };
                    bytes.AddRange(new byte[40]);

                    //Parts
                    WritePart(bcsFile.PartSets[i].FaceBase, PartSetSubTable[0], PartSetStart);
                    WritePart(bcsFile.PartSets[i].FaceForehead, PartSetSubTable[1], PartSetStart);
                    WritePart(bcsFile.PartSets[i].FaceEye, PartSetSubTable[2], PartSetStart);
                    WritePart(bcsFile.PartSets[i].FaceNose, PartSetSubTable[3], PartSetStart);
                    WritePart(bcsFile.PartSets[i].FaceEar, PartSetSubTable[4], PartSetStart);
                    WritePart(bcsFile.PartSets[i].Hair, PartSetSubTable[5], PartSetStart);
                    WritePart(bcsFile.PartSets[i].Bust, PartSetSubTable[6], PartSetStart);
                    WritePart(bcsFile.PartSets[i].Pants, PartSetSubTable[7], PartSetStart);
                    WritePart(bcsFile.PartSets[i].Rist, PartSetSubTable[8], PartSetStart);
                    WritePart(bcsFile.PartSets[i].Boots, PartSetSubTable[9], PartSetStart);

                }
            }

            //PartColors
            if(bcsFile.PartColors?.Count > 0)
            {
                List<int> PartColorOffsets = new List<int>();

                for (int i = 0; i < bcsFile.PartColors.Count; i++)
                {
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), PartColorTable[i]);
                    int PartColorStart = bytes.Count;
                    int colorCount = bcsFile.PartColors[i].ColorsList?.Count > 0 ? bcsFile.PartColors[i].ColorsList.Max(x => x.ID) + 1 : 0;

                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = PartColorStart,
                        StringToWrite = bcsFile.PartColors[i].Name
                    });

                    bytes.AddRange(new byte[10]);
                    bytes.AddRange(BitConverter.GetBytes((short)colorCount));
                    PartColorOffsets.Add(bytes.Count);
                    bytes.AddRange(new byte[4]);
                }

                for(int i = 0; i < bcsFile.PartColors.Count; i++)
                {
                    if(bcsFile.PartColors[i].ColorsList?.Count > 0)
                    {
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - PartColorOffsets[i] + 12), PartColorOffsets[i]);

                        //Allocate space for all the colors
                        byte[] colorBytes = new byte[(bcsFile.PartColors[i].ColorsList.Max(x => x.ID) + 1) * 80];

                        foreach(Colors colors in bcsFile.PartColors[i].ColorsList.OrderBy(x => x.SortID).Where(x => !x.IsNull()))
                        {
                            List<byte> colorBytesTemp = new List<byte>();

                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color1.R));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color1.G));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color1.B));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color1.A));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color2.R));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color2.G));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color2.B));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color2.A));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color3.R));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color3.G));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color3.B));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color3.A));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color4.R));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color4.G));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color4.B));
                            colorBytesTemp.AddRange(BitConverter.GetBytes(colors.Color4.A));

                            colorBytesTemp.AddRange(new byte[16]);

                            colorBytes = Utils.ReplaceRange(colorBytes, colorBytesTemp.ToArray(), colors.ID * 80);
                        }

                        bytes.AddRange(colorBytes);

                        /*
                        for (int a = 0; a < bcsFile.PartColors[i].ColorsList.Count; a++)
                        {
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color1_R));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color1_G));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color1_B));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color1_A));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color2_R));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color2_G));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color2_B));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color2_A));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color3_R));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color3_G));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color3_B));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color3_A));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color4_R));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color4_G));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color4_B));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color4_A));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color5_R));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color5_G));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color5_B));
                            bytes.AddRange(BitConverter.GetBytes(bcsFile.PartColors[i].ColorsList[a].Color5_A));
                        }
                        */
                    }
                }

            }

            //Bodies
            if (bcsFile.Bodies?.Count > 0)
            {
                List<int> BodyOffsets = new List<int>();

                int idx = 0;

                foreach(var body in bcsFile.Bodies.OrderBy(x => x.ID))
                {
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), BodyTable[idx]);

                    int bodyScalesCount = (body.BodyScales != null) ? body.BodyScales.Count : 0;
                    bytes.AddRange(new byte[2]);
                    bytes.AddRange(BitConverter.GetBytes((short)bodyScalesCount));
                    BodyOffsets.Add(bytes.Count);
                    bytes.AddRange(new byte[4]);

                    idx++;
                }

                idx = 0;

                foreach (var body in bcsFile.Bodies.OrderBy(x => x.ID))
                {
                    if (body.BodyScales?.Count > 0)
                    {
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - BodyOffsets[idx] + 4), BodyOffsets[idx]);

                        for (int a = 0; a < body.BodyScales.Count; a++)
                        {
                            bytes.AddRange(BitConverter.GetBytes(body.BodyScales[a].ScaleX));
                            bytes.AddRange(BitConverter.GetBytes(body.BodyScales[a].ScaleY));
                            bytes.AddRange(BitConverter.GetBytes(body.BodyScales[a].ScaleZ)); stringInfo.Add(new StringWriter.StringInfo()
                            {
                                Offset = bytes.Count,
                                RelativeOffset = bytes.Count - 12,
                                StringToWrite = body.BodyScales[a].BoneName
                            });
                            bytes.AddRange(new byte[4]);
                        }

                    }

                    idx++;
                }
            }

            if(bcsFile.Version == Version.XV1)
            {
                WriteSkeleton(bcsFile.SkeletonData1, 68);
            }
            else
            {
                //Skeleton1
                WriteSkeleton(bcsFile.SkeletonData1, Skeleton1Table);

                //Skeleton2
                if (I_18 > 0)
                {
                    int skeletonHeaders = bytes.Count;
                    bytes.AddRange(new byte[I_18 * 8]);
                    WriteSkeleton(bcsFile.SkeletonData2, Skeleton2Table, skeletonHeaders);
                    for (int skeletonIndex = 1; skeletonIndex < I_18; skeletonIndex++)
                        WriteSkeleton(bcsFile.ExtraSkeletonData2[skeletonIndex - 1], Skeleton2Table + skeletonIndex * 4, skeletonHeaders + skeletonIndex * 8);
                }
            }

            if (!bcsFile.HasSignature)
                bytes = Utils.ReplaceRange(bytes, new byte[8], 0);

            //Strings
            bytes = StringWriter.WritePointerStrings(stringInfo, bytes);
            if (bcsFile.TrailingZeroCount < 0)
                throw new InvalidDataException("BCS trailing zero count cannot be negative.");
            bytes.AddRange(new byte[bcsFile.TrailingZeroCount]);
            byte[] writtenBytes = hideReferences.Count > 0 ? bytes.ToArray() : null;
            foreach (HideExceptionValue reference in hideReferences)
            {
                if (reference.Offset < 0 || reference.Offset > bytes.Count - 4 || BitConverter.ToInt32(writtenBytes, reference.Offset) != reference.Value)
                    throw new InvalidDataException("BCS hide exception overlaps changed data. Clear HideExceptionDataSize, UnusedHideExceptionValues, and each ListOffset to write separate model lists.");
            }
        }

        //Part Writers
        private void WritePart(Part part, int offsetToFill, int offsetRelativeTo)
        {
            if(part != null)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - offsetRelativeTo), offsetToFill);

                int partStartOffset = bytes.Count;
                int ColorSelectorsCount = (part.ColorSelectors != null) ? part.ColorSelectors.Count : 0;
                int PhysicsObjectCount = (part.PhysicsParts != null) ? part.PhysicsParts.Count : 0;
                int hideExceptionCount = (part.HideExceptions != null) ? part.HideExceptions.Count : 0;

                //Write bytes
                bytes.AddRange(BitConverter.GetBytes(part.Model));
                bytes.AddRange(BitConverter.GetBytes(part.Model2));
                bytes.AddRange(BitConverter.GetBytes(part.Texture));
                bytes.AddRange(new byte[10]);
                bytes.AddRange(BitConverter.GetBytes(part.Shader));
                bytes.AddRange(BitConverter.GetBytes((short)ColorSelectorsCount));
                bytes.AddRange(new byte[4]);
                bytes.AddRange(BitConverter.GetBytes((int)part.Flags));
                bytes.AddRange(BitConverter.GetBytes((int)part.HideFlags));
                bytes.AddRange(BitConverter.GetBytes((int)part.HideMatFlags));
                bytes.AddRange(BitConverter.GetBytes(part.F_36));
                bytes.AddRange(BitConverter.GetBytes(part.F_40));
                bytes.AddRange(BitConverter.GetBytes(part.I_44));
                bytes.AddRange(BitConverter.GetBytes(part.I_48));

                if (part.CharaCode.Length > 4)
                {
                    throw new InvalidDataException(String.Format("\"{0}\" exceeds the maximum allowed length of 4 for the paramater \"Name\"", part.CharaCode));
                }
                bytes.AddRange(Encoding.ASCII.GetBytes(part.CharaCode));
                bytes.AddRange(new byte[4 - part.CharaCode.Length]);
                stringInfo.Add(new StringWriter.StringInfo()
                {
                    Offset = bytes.Count,
                    RelativeOffset = partStartOffset,
                    StringToWrite = part.EmdPath
                });
                bytes.AddRange(new byte[4]);
                stringInfo.Add(new StringWriter.StringInfo()
                {
                    Offset = bytes.Count,
                    RelativeOffset = partStartOffset,
                    StringToWrite = part.EmmPath
                });
                bytes.AddRange(new byte[4]);
                stringInfo.Add(new StringWriter.StringInfo()
                {
                    Offset = bytes.Count,
                    RelativeOffset = partStartOffset,
                    StringToWrite = part.EmbPath
                });
                bytes.AddRange(new byte[4]);
                stringInfo.Add(new StringWriter.StringInfo()
                {
                    Offset = bytes.Count,
                    RelativeOffset = partStartOffset,
                    StringToWrite = part.EanPath
                });
                bytes.AddRange(new byte[6]);
                bytes.AddRange(BitConverter.GetBytes((short)PhysicsObjectCount));
                bytes.AddRange(new byte[4]);

                if(bcsFile.Version == Version.XV2)
                {
                    bytes.AddRange(new byte[2]);
                    bytes.AddRange(BitConverter.GetBytes((short)hideExceptionCount));
                    bytes.AddRange(new byte[4]);
                }

                //Extended data
                WriteColorSelectors(part.ColorSelectors, partStartOffset + 20, partStartOffset);
                WritePhysicsObjects(part.PhysicsParts, partStartOffset + 76, partStartOffset);

                if(bcsFile.Version == Version.XV2)
                {
                    WriteHideExceptions(part, partStartOffset + 84, partStartOffset);
                }
            }
        }

        private void WriteColorSelectors(AsyncObservableCollection<ColorSelector> colorSelectors, int offsetToFill, int relativeOffset)
        {
            if(colorSelectors?.Count > 0)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - relativeOffset), offsetToFill);

                for(int i = 0; i < colorSelectors.Count; i++)
                {
                    bytes.AddRange(BitConverter.GetBytes(colorSelectors[i].PartColorGroup));
                    bytes.AddRange(BitConverter.GetBytes(colorSelectors[i].ColorIndex));
                }
            }
        }

        private void WritePhysicsObjects(AsyncObservableCollection<PhysicsPart> physicsObjects, int offsetToFill, int relativeOffset)
        {
            if (physicsObjects?.Count > 0)
            {
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - relativeOffset), offsetToFill);

                for (int i = 0; i < physicsObjects.Count; i++)
                {
                    int physicsObjectStartOffset = bytes.Count;

                    //Create values
                    BitArray _i_28 = new BitArray(new bool[] { physicsObjects[i].Hide_FaceBase, physicsObjects[i].Hide_Forehead, physicsObjects[i].Hide_Eye, physicsObjects[i].Hide_Nose, physicsObjects[i].Hide_Ear, physicsObjects[i].Hide_Hair, physicsObjects[i].Hide_Bust, physicsObjects[i].Hide_Pants, physicsObjects[i].Hide_Rist, physicsObjects[i].Hide_Boots, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false }); //32 bools = 32 bits
                    BitArray _i_32 = new BitArray(new bool[] { physicsObjects[i].HideMat_FaceBase, physicsObjects[i].HideMat_Forehead, physicsObjects[i].HideMat_Eye, physicsObjects[i].HideMat_Nose, physicsObjects[i].HideMat_Ear, physicsObjects[i].HideMat_Hair, physicsObjects[i].HideMat_Bust, physicsObjects[i].HideMat_Pants, physicsObjects[i].HideMat_Rist, physicsObjects[i].HideMat_Boots, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false }); //32 bools = 32 bits

                    bytes.AddRange(BitConverter.GetBytes(physicsObjects[i].Model1));
                    bytes.AddRange(BitConverter.GetBytes(physicsObjects[i].Model2));
                    bytes.AddRange(BitConverter.GetBytes(physicsObjects[i].Texture));
                    bytes.AddRange(new byte[18]);
                    bytes.AddRange(BitConverter.GetBytes((int)physicsObjects[i].Flags));
                    bytes.AddRange(Utils.ConvertToByteArray(_i_28, 4));
                    bytes.AddRange(Utils.ConvertToByteArray(_i_32, 4));
                    if (physicsObjects[i].CharaCode.Length > 4)
                    {
                        throw new InvalidDataException(String.Format("BCS: \"{0}\" exceeds the maximum allowed length of 4 for the paramater \"Name\"", physicsObjects[i].CharaCode));
                    }
                    bytes.AddRange(Encoding.ASCII.GetBytes(physicsObjects[i].CharaCode));
                    bytes.AddRange(new byte[4 - physicsObjects[i].CharaCode.Length]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].EmdPath
                    });
                    bytes.AddRange(new byte[4]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].EmmPath
                    });
                    bytes.AddRange(new byte[4]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].EmbPath
                    });
                    bytes.AddRange(new byte[4]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].EanPath
                    });
                    bytes.AddRange(new byte[4]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].BoneToAttach
                    });
                    bytes.AddRange(new byte[4]);
                    stringInfo.Add(new StringWriter.StringInfo()
                    {
                        Offset = bytes.Count,
                        RelativeOffset = physicsObjectStartOffset,
                        StringToWrite = physicsObjects[i].ScdPath
                    });
                    bytes.AddRange(new byte[12]);

                }
            }
        }

        private void WriteHideExceptions(Part part, int offsetToFill, int relativeOffset)
        {
            List<HideException> hideExceptions = part.HideExceptions;
            if (hideExceptions == null) return;

            bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - relativeOffset), offsetToFill);

            //Records come first, then each model list. A list without an explicit offset goes after the previous list.
            if (hideExceptions.Count > ushort.MaxValue)
                throw new InvalidDataException("BCS has too many hide exception records.");
            int tableSize = hideExceptions.Count * 8;
            int nextListPosition = tableSize;
            int[] listPositions = new int[hideExceptions.Count];
            int regionSize = tableSize;

            for (int i = 0; i < hideExceptions.Count; i++)
            {
                HideException hideException = hideExceptions[i];
                int recordPosition = i * 8;

                if (hideException?.Models == null)
                    throw new InvalidDataException("BCS hide exception model list cannot be null.");
                if (hideException.Models.Count > ushort.MaxValue)
                    throw new InvalidDataException("BCS hide exception needs at most 65535 models.");
                if (hideException.ListOffset.HasValue)
                {
                    if (hideException.ListOffset.Value < 0 || hideException.ListOffset.Value > int.MaxValue - recordPosition - hideException.Models.Count * 4)
                        throw new InvalidDataException("BCS hide exception ListOffset is outside the supported range.");
                    listPositions[i] = recordPosition + hideException.ListOffset.Value;
                }
                else
                {
                    listPositions[i] = nextListPosition;
                }

                if (listPositions[i] > int.MaxValue - hideException.Models.Count * 4)
                    throw new InvalidDataException("BCS hide exception model list is too large.");
                if (hideException.Models.Count > 0)
                    nextListPosition = Math.Max(nextListPosition, listPositions[i] + hideException.Models.Count * 4);
                if (hideException.Models.Count > 0 && listPositions[i] % 4 != 0)
                {
                    throw new InvalidDataException(string.Format("HideException {0}: ListOffset must use whole integers.", i));
                }

                if (hideException.Models.Count > 0)
                {
                    regionSize = Math.Max(regionSize, listPositions[i] + hideException.Models.Count * 4);
                }
            }

            if (part.HideExceptionDataSize.HasValue) regionSize = part.HideExceptionDataSize.Value;
            if (regionSize < tableSize || regionSize % 4 != 0)
                throw new InvalidDataException("BCS hide exception data size must fit its records and use whole integers.");
            byte[] region = new byte[regionSize];
            if (part.UnusedHideExceptionValues != null)
            {
                foreach (HideExceptionValue value in part.UnusedHideExceptionValues)
                {
                    if (value.Offset < tableSize || value.Offset > regionSize - 4 || value.Offset % 4 != 0 ||
                        hideExceptions.Where((record, index) => value.Offset >= listPositions[index] && value.Offset < listPositions[index] + record.Models.Count * 4).Any())
                        throw new InvalidDataException("BCS unused hide exception value is outside its data block.");
                    Array.Copy(BitConverter.GetBytes(value.Value), 0, region, value.Offset, 4);
                }
            }

            for (int i = 0; i < hideExceptions.Count; i++)
            {
                HideException hideException = hideExceptions[i];
                int recordPosition = i * 8;

                Array.Copy(BitConverter.GetBytes(hideException.PartType), 0, region, recordPosition, 2);
                Array.Copy(BitConverter.GetBytes((ushort)hideException.Models.Count), 0, region, recordPosition + 2, 2);
                Array.Copy(BitConverter.GetBytes(listPositions[i] - recordPosition), 0, region, recordPosition + 4, 4);
            }

            for (int i = 0; i < hideExceptions.Count; i++)
            {
                for (int j = 0; j < hideExceptions[i].Models.Count; j++)
                {
                    int modelOffset = listPositions[i] + j * 4;
                    int model = hideExceptions[i].Models[j];
                    if (modelOffset >= tableSize && modelOffset <= regionSize - 4)
                        Array.Copy(BitConverter.GetBytes(model), 0, region, modelOffset, 4);
                    if (modelOffset > int.MaxValue - bytes.Count)
                        throw new InvalidDataException("BCS hide exception model offset is too large.");
                    hideReferences.Add(new HideExceptionValue { Offset = bytes.Count + modelOffset, Value = model });
                }
            }

            bytes.AddRange(region);
        }

        //Skeleton Writers
        private void WriteSkeleton(SkeletonData skeleton, int offsetToFill, int headerOffset = -1)
        {
            if(skeleton?.Bones?.Count > 0)
            {
                int relativeTo = bcsFile.Version == Version.XV1 ? 32 : 0;

                int skeletonOffset = headerOffset >= 0 ? headerOffset : bytes.Count;
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(skeletonOffset - relativeTo), offsetToFill);
                int boneCount = (skeleton.Bones != null) ? skeleton.Bones.Count : 0;

                if(bcsFile.Version == Version.XV2)
                {
                    if (headerOffset < 0) bytes.AddRange(new byte[8]);
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(skeleton.I_00), skeletonOffset);
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes((short)boneCount), skeletonOffset + 2);
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - skeletonOffset), skeletonOffset + 4);
                }
                
                for(int i = 0; i < boneCount; i++)
                {
                    bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].I_00));
                    bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].I_04));
                    bytes.AddRange(new byte[4]);

                    if(bcsFile.Version == Version.XV1)
                    {
                        stringInfo.Add(new StringWriter.StringInfo()
                        {
                            Offset = bytes.Count,
                            RelativeOffset = bytes.Count - 12,
                            StringToWrite = skeleton.Bones[i].BoneName
                        });
                        bytes.AddRange(new byte[4]);
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_12));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_16));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_20));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_24));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_28));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_32));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_36));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_40));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_44));
                    }
                    else
                    {
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_12));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_16));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_20));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_24));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_28));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_32));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_36));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_40));
                        bytes.AddRange(BitConverter.GetBytes(skeleton.Bones[i].F_44));
                        stringInfo.Add(new StringWriter.StringInfo()
                        {
                            Offset = bytes.Count,
                            RelativeOffset = bytes.Count - 48,
                            StringToWrite = skeleton.Bones[i].BoneName
                        });
                        bytes.AddRange(new byte[4]);
                    }
                }

            }
        }

    }
}
