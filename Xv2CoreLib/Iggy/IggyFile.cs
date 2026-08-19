using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAXLib;

namespace Xv2CoreLib.Iggy
{
    [YAXSerializeAs("Iggy")]
    public class IggyFile
    {
        public const uint SIGNATURE = 0xED0A6749;
        public const int HEADER_SIZE = 32;
        public const int SUBFILE_DESCRIPTOR_SIZE = 16;
        public const int SUPPORTED_VERSION = 0x900;
        public const int MAIN_SUBFILE_TYPE = 1;
        public const int INDEX_SUBFILE_TYPE = 0;

        [YAXAttributeForClass]
        public int Version { get; set; }

        [YAXAttributeForClass]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ", ")]
        public byte[] Platform { get; set; }

        [YAXAttributeForClass]
        public int I_12 { get; set; }

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "IggySubFile")]
        public List<IggySubFile> SubFiles { get; set; } = new List<IggySubFile>();

        [YAXDontSerialize]
        public byte[] RawBytes { get; private set; }

        public IggySubFile MainSubFile
        {
            get { return SubFiles.FirstOrDefault(x => x.ID == MAIN_SUBFILE_TYPE); }
        }

        [YAXDontSerialize]
        public IggySwf ActionScript
        {
            get { return MainSubFile == null ? null : MainSubFile.ActionScript; }
        }

        public IggySubFile IndexSubFile
        {
            get { return SubFiles.FirstOrDefault(x => x.ID == INDEX_SUBFILE_TYPE && x.IndexStream != null); }
        }

        public IReadOnlyList<IggySubFile> IndexSubFiles
        {
            get { return SubFiles.Where(x => x.ID == INDEX_SUBFILE_TYPE).ToList(); }
        }

        public byte[] GetAbcBlob()
        {
            if (ActionScript == null || !ActionScript.HasAbc)
                return null;

            return (byte[])ActionScript.Abc.Clone();
        }

        public byte[] BuildActionScriptSwf(bool gfx = false)
        {
            if (ActionScript == null || !ActionScript.HasAbc)
                throw new InvalidDataException("IggyFile does not contain an ActionScript 3 blob.");

            return IggySwfWriter.Build(ActionScript.Abc, gfx);
        }

        public void ExtractActionScript(string outputPath)
        {
            if (outputPath == null)
                throw new ArgumentNullException(nameof(outputPath));

            string extension = Path.GetExtension(outputPath);
            if (string.Equals(extension, ".abc", StringComparison.OrdinalIgnoreCase))
            {
                byte[] abc = GetAbcBlob();
                if (abc == null)
                    throw new InvalidDataException("IggyFile does not contain an ActionScript 3 blob.");

                File.WriteAllBytes(outputPath, abc);
                return;
            }

            if (string.Equals(extension, ".swf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".gfx", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(outputPath, BuildActionScriptSwf(string.Equals(extension, ".gfx", StringComparison.OrdinalIgnoreCase)));
                return;
            }

            throw new ArgumentException("The output extension must be .abc, .swf, or .gfx.", nameof(outputPath));
        }

        public static void CreateXml(string path)
        {
            IggyFile file = Load(File.ReadAllBytes(path));

            YAXSerializer serializer = new YAXSerializer(typeof(IggyFile));
            serializer.SerializeToFile(file, path + ".xml");
        }

        public static IggyFile Load(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return Load(File.ReadAllBytes(path));
        }

        public static IggyFile Load(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            RequireRange(bytes, 0, HEADER_SIZE, "Iggy header");

            if (BitConverter.ToUInt32(bytes, 0) != SIGNATURE)
                throw new InvalidDataException("IggyFile.Load: Invalid Iggy signature.");

            IggyFile iggy = new IggyFile
            {
                RawBytes = (byte[])bytes.Clone(),
                Version = BitConverter.ToInt32(bytes, 4),
                Platform = bytes.Skip(8).Take(4).ToArray(),
                I_12 = BitConverter.ToInt32(bytes, 12)
            };

            if (iggy.Version != SUPPORTED_VERSION)
                throw new InvalidDataException($"IggyFile.Load: Unsupported version ({iggy.Version:X}).");

            if (iggy.Platform.Length != 4 || iggy.Platform[1] != 0x40)
                throw new InvalidDataException("IggyFile.Load: Only the 64-bit Iggy platform is supported.");

            int subFilesCount = BitConverter.ToInt32(bytes, 28);
            if (subFilesCount <= 0)
                throw new InvalidDataException("IggyFile.Load: The file has no subfiles.");

            long descriptorBytes = checked((long)HEADER_SIZE + (long)SUBFILE_DESCRIPTOR_SIZE * subFilesCount);
            if (descriptorBytes > bytes.Length)
                throw new InvalidDataException("IggyFile.Load: The subfile table is outside the file.");

            for (int index = 0; index < subFilesCount; index++)
            {
                int offset = HEADER_SIZE + (SUBFILE_DESCRIPTOR_SIZE * index);
                int id = BitConverter.ToInt32(bytes, offset);
                int size = BitConverter.ToInt32(bytes, offset + 4);
                int secondSize = BitConverter.ToInt32(bytes, offset + 8);
                int dataOffset = BitConverter.ToInt32(bytes, offset + 12);

                if (size < 0 || secondSize < 0 || size != secondSize)
                    throw new InvalidDataException($"IggyFile.Load: Subfile {index} has invalid sizes.");
                RequireRange(bytes, dataOffset, size, $"Iggy subfile {index}");

                IggySubFile subFile = new IggySubFile
                {
                    ID = id,
                    Size = size,
                    SecondSize = secondSize,
                    Offset = dataOffset,
                    RawData = CopyRange(bytes, dataOffset, size)
                };

                if (id == MAIN_SUBFILE_TYPE)
                    ReadMainSubFile(subFile);

                iggy.SubFiles.Add(subFile);
            }

            IggySubFile mainSubFile = iggy.MainSubFile;
            if (mainSubFile != null)
            {
                List<IggySubFile> matchingIndexSubFiles = new List<IggySubFile>();
                foreach (IggySubFile indexSubFile in iggy.IndexSubFiles)
                {
                    if (indexSubFile.RawData.Length == 0)
                        continue;
                    try
                    {
                        IggyIndexStream indexStream = IggyIndexStream.Read(indexSubFile.RawData, mainSubFile.RawData.Length);
                        indexSubFile.IndexStream = indexStream;
                        matchingIndexSubFiles.Add(indexSubFile);
                    }
                    catch (InvalidDataException exception)
                    {
                        indexSubFile.IndexStreamError = exception.Message;
                    }
                }

                if (iggy.IndexSubFiles.Count > 0 && matchingIndexSubFiles.Count == 0)
                    throw new InvalidDataException("IggyFile.Load: No index subfile covers the main subfile.");
                if (matchingIndexSubFiles.Count > 1)
                    throw new InvalidDataException("IggyFile.Load: More than one index subfile covers the main subfile.");
            }

            return iggy;
        }

        private static void ReadMainSubFile(IggySubFile subFile)
        {
            byte[] main = subFile.RawData;
            RequireRange(main, 0, 0x44, "Iggy main header");

            long objectTableOffset = BitConverter.ToInt64(main, 0);
            uint objectCount = BitConverter.ToUInt32(main, 0x40);
            if (objectTableOffset < 0 || objectTableOffset > main.Length)
                throw new InvalidDataException("IggyFile.Load: The object table offset is outside the main subfile.");

            long objectTableEnd = checked(objectTableOffset + ((long)objectCount * 8));
            if (objectTableEnd > main.Length)
                throw new InvalidDataException("IggyFile.Load: The object table is outside the main subfile.");

            subFile.ObjectTableOffset = objectTableOffset;
            subFile.ObjectCount = objectCount;
            subFile.StageOffset = ReadResolvedPointer(main, 0x10, "stage");
            subFile.MetadataOffset = ReadResolvedPointer(main, 0x18, "metadata");
            subFile.StageSegments = ReadStageSegments(main, subFile.StageOffset);
            subFile.ActionScript = IggySwf.Read(main);

            List<IggyObject> objects = new List<IggyObject>();
            for (int slot = 0; slot < objectCount; slot++)
            {
                long cellOffset = objectTableOffset + (slot * 8L);
                long relative = BitConverter.ToInt64(main, checked((int)cellOffset));
                if (relative == 1)
                    continue;
                if (relative == 0)
                    throw new InvalidDataException($"IggyFile.Load: Object slot {slot} contains a null pointer.");

                long objectOffset = checked(cellOffset + relative);
                if (objectOffset < 0 || objectOffset + 4 > main.Length)
                    throw new InvalidDataException($"IggyFile.Load: Object slot {slot} points outside the main subfile.");

                ushort type = BitConverter.ToUInt16(main, checked((int)objectOffset));
                ushort id = BitConverter.ToUInt16(main, checked((int)objectOffset + 2));
                objects.Add(new IggyObject
                {
                    Slot = slot,
                    Offset = checked((int)objectOffset),
                    RelativePointer = relative,
                    Type = type,
                    I_01 = (byte)(type & 0xff),
                    ID = id
                });
            }

            List<IggyObject> orderedObjects = objects.OrderBy(x => x.Offset).ToList();
            for (int index = 0; index < orderedObjects.Count; index++)
            {
                IggyObject current = orderedObjects[index];
                int end = index + 1 < orderedObjects.Count
                    ? orderedObjects[index + 1].Offset
                    : (subFile.StageOffset > current.Offset && subFile.StageOffset <= main.Length
                        ? checked((int)subFile.StageOffset)
                        : main.Length);
                if (end <= current.Offset)
                    throw new InvalidDataException($"IggyFile.Load: Object slot {current.Slot} has an invalid span.");
                current.Size = end - current.Offset;
                current.RawData = CopyRange(main, current.Offset, current.Size);

                if (current.Type == 4)
                    current.Type4 = IggyType4.Read(main, current.Offset + 32);
                else if (current.Type == 6)
                    current.Type6 = IggyType6.Read(main, current.Offset + 32);
            }

            subFile.IggyObjects = orderedObjects;
        }

        private static long ReadResolvedPointer(byte[] data, int fieldOffset, string name)
        {
            if (fieldOffset + 8 > data.Length)
                return 0;

            long relative = BitConverter.ToInt64(data, fieldOffset);
            if (relative == 0 || relative == 1)
                return relative;

            long target = checked(fieldOffset + relative);
            if (target < 0 || target >= data.Length)
                throw new InvalidDataException($"IggyFile.Load: The {name} pointer is outside the main subfile.");
            return target;
        }

        private static List<IggyStageSegment> ReadStageSegments(byte[] data, long stageOffset)
        {
            List<IggyStageSegment> segments = new List<IggyStageSegment>();
            if (stageOffset <= 1)
                return segments;

            HashSet<long> visited = new HashSet<long>();
            long currentOffset = stageOffset;
            while (visited.Add(currentOffset))
            {
                if (currentOffset < 0 || currentOffset + 8 > data.Length)
                    throw new InvalidDataException("IggyFile.Load: A stage segment points outside the main subfile.");

                long relative = BitConverter.ToInt64(data, checked((int)currentOffset));
                long nextOffset = relative > 0 ? checked(currentOffset + relative) : 0;
                segments.Add(new IggyStageSegment
                {
                    Offset = checked((int)currentOffset),
                    RelativeNextPointer = relative,
                    NextOffset = nextOffset > 0 && nextOffset < data.Length ? checked((int)nextOffset) : -1
                });

                if (relative <= 0 || nextOffset <= currentOffset || nextOffset >= data.Length)
                    break;
                if (data[checked((int)nextOffset)] == 1)
                    break;
                currentOffset = nextOffset;
            }

            return segments;
        }

        internal static void RequireRange(byte[] data, long offset, long length, string name)
        {
            if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
                throw new InvalidDataException($"{name} is outside the file.");
        }

        internal static byte[] CopyRange(byte[] data, long offset, long length)
        {
            RequireRange(data, offset, length, "Iggy data");
            byte[] result = new byte[checked((int)length)];
            Buffer.BlockCopy(data, checked((int)offset), result, 0, result.Length);
            return result;
        }
    }

    public class IggySubFile
    {
        public const int SIZE = IggyFile.SUBFILE_DESCRIPTOR_SIZE;

        [YAXAttributeForClass]
        public int ID { get; set; }

        [YAXAttributeForClass]
        public int Size { get; set; }

        [YAXAttributeForClass]
        public int SecondSize { get; set; }

        [YAXAttributeForClass]
        public int Offset { get; set; }

        [YAXDontSerialize]
        public byte[] RawData { get; internal set; }

        [YAXDontSerialize]
        public long ObjectTableOffset { get; internal set; }

        [YAXDontSerialize]
        public uint ObjectCount { get; internal set; }

        [YAXDontSerialize]
        public long StageOffset { get; internal set; }

        [YAXDontSerialize]
        public long MetadataOffset { get; internal set; }

        [YAXDontSerialize]
        public List<IggyStageSegment> StageSegments { get; internal set; } = new List<IggyStageSegment>();

        [YAXDontSerialize]
        public IggyIndexStream IndexStream { get; internal set; }

        [YAXDontSerialize]
        public string IndexStreamError { get; internal set; }

        [YAXDontSerialize]
        public IggySwf ActionScript { get; internal set; }

        public List<IggyObject> IggyObjects { get; internal set; } = new List<IggyObject>();
    }

    public class IggyStageSegment
    {
        public int Offset { get; internal set; }
        public long RelativeNextPointer { get; internal set; }
        public int NextOffset { get; internal set; }
    }

    public class IggyObject
    {
        public const int SIZE_32 = 128;
        public const int SIZE_64 = 184;
        public const ushort TYPE_SHAPE = 0xff01;
        public const ushort TYPE_CONTAINER = 0xff03;
        public const ushort TYPE_TEXTURE = 0xff04;
        public const ushort TYPE_SYMBOL = 0xff06;

        [YAXAttributeForClass]
        public ushort Type { get; set; }

        [YAXAttributeForClass]
        public byte I_01 { get; set; }

        [YAXAttributeForClass]
        public int ID { get; set; }

        [YAXAttributeForClass]
        public int Slot { get; internal set; }

        [YAXAttributeForClass]
        public int Offset { get; internal set; }

        [YAXAttributeForClass]
        public int Size { get; internal set; }

        [YAXDontSerialize]
        public long RelativePointer { get; internal set; }

        [YAXDontSerialize]
        public byte[] RawData { get; internal set; }

        [YAXDontSerialize]
        public bool IsTextureReference
        {
            get { return Type == TYPE_TEXTURE; }
        }

        [YAXDontSerializeIfNull]
        public IggyType4 Type4 { get; set; }

        [YAXDontSerializeIfNull]
        public IggyType6 Type6 { get; set; }
    }

    public class IggyType4
    {
        [YAXAttributeFor("I_00")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_00 { get; set; }
        [YAXAttributeFor("I_04")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_04 { get; set; }
        [YAXAttributeFor("I_08")]
        [YAXSerializeAs("value")]
        public int I_08 { get; set; }
        [YAXAttributeFor("I_12")]
        [YAXSerializeAs("value")]
        public int I_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("I_24")]
        [YAXSerializeAs("value")]
        public int I_24 { get; set; }
        [YAXAttributeFor("I_28")]
        [YAXSerializeAs("value")]
        public int I_28 { get; set; }
        [YAXAttributeFor("I_32")]
        [YAXSerializeAs("value")]
        public int I_32 { get; set; }
        [YAXAttributeFor("I_36")]
        [YAXSerializeAs("value")]
        public int I_36 { get; set; }
        [YAXAttributeFor("I_40")]
        [YAXSerializeAs("value")]
        public int I_40 { get; set; }
        [YAXAttributeFor("I_44")]
        [YAXSerializeAs("value")]
        public int I_44 { get; set; }
        [YAXAttributeFor("I_48")]
        [YAXSerializeAs("value")]
        public int I_48 { get; set; }
        [YAXAttributeFor("I_52")]
        [YAXSerializeAs("value")]
        public int I_52 { get; set; }

        public static IggyType4 Read(byte[] bytes, int offset)
        {
            IggyFile.RequireRange(bytes, offset, 56, "Iggy type 4 data");
            IggyType4 type = new IggyType4
            {
                I_00 = BitConverter.ToUInt32(bytes, offset + 0),
                I_04 = BitConverter.ToUInt32(bytes, offset + 4),
                I_08 = BitConverter.ToInt32(bytes, offset + 8),
                I_12 = BitConverter.ToInt32(bytes, offset + 12),
                F_16 = BitConverter.ToSingle(bytes, offset + 16),
                F_20 = BitConverter.ToSingle(bytes, offset + 20),
                I_24 = BitConverter.ToInt32(bytes, offset + 24),
                I_28 = BitConverter.ToInt32(bytes, offset + 28),
                I_32 = BitConverter.ToInt32(bytes, offset + 32),
                I_36 = BitConverter.ToInt32(bytes, offset + 36),
                I_40 = BitConverter.ToInt32(bytes, offset + 40),
                I_44 = BitConverter.ToInt32(bytes, offset + 44),
                I_48 = BitConverter.ToInt32(bytes, offset + 48),
                I_52 = BitConverter.ToInt32(bytes, offset + 52)
            };
            return type;
        }
    }

    public class IggyType6
    {
        [YAXAttributeFor("F_00")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_00 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("F_08")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0#########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("I_16")]
        [YAXSerializeAs("value")]
        public int I_16 { get; set; }
        [YAXAttributeFor("I_20")]
        [YAXSerializeAs("value")]
        public int I_20 { get; set; }
        [YAXAttributeFor("I_32")]
        [YAXSerializeAs("value")]
        public int I_32 { get; set; }
        [YAXAttributeFor("I_36")]
        [YAXSerializeAs("value")]
        public int I_36 { get; set; }
        [YAXAttributeFor("I_40")]
        [YAXSerializeAs("value")]
        public int I_40 { get; set; }
        [YAXAttributeFor("I_44")]
        [YAXSerializeAs("value")]
        public ushort I_44 { get; set; }
        [YAXAttributeFor("I_46")]
        [YAXSerializeAs("value")]
        public ushort I_46 { get; set; }
        [YAXAttributeFor("I_48")]
        [YAXSerializeAs("value")]
        public ushort I_48 { get; set; }
        [YAXAttributeFor("I_50")]
        [YAXSerializeAs("value")]
        public ushort I_50 { get; set; }
        [YAXAttributeFor("I_52")]
        [YAXSerializeAs("value")]
        public ushort I_52 { get; set; }
        [YAXAttributeFor("I_54")]
        [YAXSerializeAs("value")]
        public ushort I_54 { get; set; }
        [YAXAttributeFor("I_56")]
        [YAXSerializeAs("value")]
        public ushort I_56 { get; set; }
        [YAXAttributeFor("I_58")]
        [YAXSerializeAs("value")]
        public ushort I_58 { get; set; }
        [YAXAttributeFor("I_60")]
        [YAXSerializeAs("value")]
        public ushort I_60 { get; set; }
        [YAXAttributeFor("I_62")]
        [YAXSerializeAs("value")]
        public ushort I_62 { get; set; }

        public string EmbeddedElement { get; set; }

        [YAXAttributeForClass]
        public string Font { get; set; }

        public static IggyType6 Read(byte[] bytes, int offset)
        {
            IggyFile.RequireRange(bytes, offset, 68, "Iggy type 6 data");
            IggyType6 type = new IggyType6
            {
                F_00 = BitConverter.ToSingle(bytes, offset + 0),
                F_04 = BitConverter.ToSingle(bytes, offset + 4),
                F_08 = BitConverter.ToSingle(bytes, offset + 8),
                F_12 = BitConverter.ToSingle(bytes, offset + 12),
                I_16 = BitConverter.ToInt32(bytes, offset + 16),
                I_20 = BitConverter.ToInt32(bytes, offset + 20),
                I_32 = BitConverter.ToInt32(bytes, offset + 32),
                I_36 = BitConverter.ToInt32(bytes, offset + 36),
                I_40 = BitConverter.ToInt32(bytes, offset + 40),
                I_44 = BitConverter.ToUInt16(bytes, offset + 44),
                I_46 = BitConverter.ToUInt16(bytes, offset + 46),
                I_48 = BitConverter.ToUInt16(bytes, offset + 48),
                I_50 = BitConverter.ToUInt16(bytes, offset + 50),
                I_52 = BitConverter.ToUInt16(bytes, offset + 52),
                I_54 = BitConverter.ToUInt16(bytes, offset + 54),
                I_56 = BitConverter.ToUInt16(bytes, offset + 56),
                I_58 = BitConverter.ToUInt16(bytes, offset + 58),
                I_60 = BitConverter.ToUInt16(bytes, offset + 60),
                I_62 = BitConverter.ToUInt16(bytes, offset + 62)
            };

            int fontOffset = BitConverter.ToInt32(bytes, offset + 24);
            int xmlOffset = BitConverter.ToInt32(bytes, offset + 64);
            if (fontOffset != 0)
                type.Font = StringEx.GetString(bytes, offset + 24 + fontOffset, false, StringEx.EncodingType.Unicode);
            if (xmlOffset != 0)
                type.EmbeddedElement = StringEx.GetString(bytes, offset + 64 + xmlOffset, false, StringEx.EncodingType.Unicode);
            return type;
        }
    }
}
