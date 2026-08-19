using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using CSharpImageLibrary;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.HslColor;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.Image;
using YAXLib;

namespace Xv2CoreLib.IggyTexture
{
    [Serializable]
    [YAXSerializeAs("IggyTexture")]
    public class IggyTextureFile
    {
        private const int DATA_ALIGNMENT = 128;
        private const int HEADER_SIZE = 16;
        private const int ENTRY_SIZE = 16;
        private const uint SIGNATURE_0 = 0x7967676F;
        private const uint SIGNATURE_1 = 0x00786574;

        public const string IGGYTEX_EXTENSION = ".iggytex";
        public const string IGGYTED_EXTENSION = ".iggyted";

        [YAXAttributeForClass]
        public uint TableOffset { get; set; } = HEADER_SIZE;

        [YAXDontSerialize]
        public uint EntryCount
        {
            get { return Entry == null ? 0u : checked((uint)Entry.Count); }
        }

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "IggyTextureEntry")]
        public AsyncObservableCollection<IggyTextureEntry> Entry { get; set; } = new AsyncObservableCollection<IggyTextureEntry>();

        [YAXDontSerialize]
        public IggyTextureFileKind Kind { get; private set; }

        [YAXDontSerialize]
        public string FileExtension { get; private set; }

        [YAXDontSerialize]
        public byte[] RawBytes { get; private set; }

        public bool IsIggyTed
        {
            get { return Kind == IggyTextureFileKind.IggyTed; }
        }

        public static IggyTextureFile Load(string path)
        {
            return LoadIggyTexture(path);
        }

        public static IggyTextureFile LoadIggyTexture(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                string sourceExtension = Path.GetExtension(Path.GetFileNameWithoutExtension(path));
                if (!IsTextureExtension(sourceExtension))
                    throw new ArgumentException("The XML file must be named after an .iggytex or .iggyted file.", nameof(path));

                YAXSerializer serializer = new YAXSerializer(typeof(IggyTextureFile), YAXSerializationOptions.DontSerializeNullObjects);
                IggyTextureFile xmlFile = (IggyTextureFile)serializer.DeserializeFromFile(path);
                xmlFile.SetFileKind(sourceExtension);
                return xmlFile;
            }

            if (!IsTextureExtension(extension))
                throw new ArgumentException("The texture file extension must be .iggytex or .iggyted.", nameof(path));

            IggyTextureFile file = LoadIggyTexture(File.ReadAllBytes(path));
            file.SetFileKind(extension);
            return file;
        }

        public static IggyTextureFile Load(byte[] bytes)
        {
            return LoadIggyTexture(bytes);
        }

        public static IggyTextureFile LoadIggyTexture(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            RequireRange(bytes, 0, HEADER_SIZE, "Iggy texture header");
            if (BitConverter.ToUInt32(bytes, 0) != SIGNATURE_0 || BitConverter.ToUInt32(bytes, 4) != SIGNATURE_1)
                throw new InvalidDataException("IggyTextureFile.Load: Invalid oggytex signature.");

            uint entryCount = BitConverter.ToUInt32(bytes, 8);
            uint tableOffset = BitConverter.ToUInt32(bytes, 12);
            long tableSize = checked((long)entryCount * ENTRY_SIZE);
            RequireRange(bytes, tableOffset, tableSize, "Iggy texture table");

            if (entryCount > int.MaxValue)
                throw new InvalidDataException("Iggy texture entry count is too large.");

            IggyTextureFile file = new IggyTextureFile
            {
                TableOffset = tableOffset,
                RawBytes = (byte[])bytes.Clone(),
                Entry = new AsyncObservableCollection<IggyTextureEntry>()
            };

            for (int index = 0; index < entryCount; index++)
            {
                int offset = checked((int)tableOffset + (index * ENTRY_SIZE));
                uint textureId = BitConverter.ToUInt32(bytes, offset);
                uint payloadSize = BitConverter.ToUInt32(bytes, offset + 4);
                uint payloadOffset = BitConverter.ToUInt32(bytes, offset + 8);
                uint unknown = BitConverter.ToUInt32(bytes, offset + 12);

                RequireRange(bytes, payloadOffset, payloadSize, $"Iggy texture payload {textureId:X8}");
                file.Entry.Add(new IggyTextureEntry
                {
                    Index = index,
                    TextureId = textureId,
                    PayloadOffset = payloadOffset,
                    Unknown = unknown,
                    Data = CopyRange(bytes, payloadOffset, payloadSize)
                });
            }

            return file;
        }

        public static IggyTextureFile LoadIggyTexture(byte[] bytes, string extension)
        {
            IggyTextureFile file = LoadIggyTexture(bytes);
            if (!IsTextureExtension(extension))
                throw new ArgumentException("The texture file extension must be .iggytex or .iggyted.", nameof(extension));

            file.SetFileKind(extension);
            return file;
        }

        public byte[] SaveToBytes()
        {
            if (Entry == null)
                Entry = new AsyncObservableCollection<IggyTextureEntry>();
            if (TableOffset < HEADER_SIZE)
                throw new InvalidDataException("Iggy texture table offset is before the header.");

            byte[] originalLayoutBytes;
            if (TryPatchOriginalLayout(out originalLayoutBytes))
                return originalLayoutBytes;

            int tableOffset = checked((int)TableOffset);
            int tableEnd = checked(tableOffset + (Entry.Count * ENTRY_SIZE));
            List<byte> bytes = new List<byte>(tableEnd);
            GrowTo(bytes, tableEnd);

            for (int index = 0; index < Entry.Count; index++)
            {
                IggyTextureEntry entry = Entry[index];
                if (entry == null)
                    throw new InvalidDataException($"Iggy texture entry {index} is null.");

                entry.Index = index;
                byte[] data = entry.Data ?? new byte[0];
                uint payloadOffset = 0;
                if (data.Length > 0)
                {
                    int alignedOffset = bytes.Count + Utils.CalculatePadding(bytes.Count, DATA_ALIGNMENT);
                    GrowTo(bytes, alignedOffset);
                    payloadOffset = checked((uint)alignedOffset);
                    bytes.AddRange(data);
                }

                entry.PayloadOffset = payloadOffset;
                int entryOffset = checked(tableOffset + (index * ENTRY_SIZE));
                WriteUInt32(bytes, entryOffset, entry.TextureId);
                WriteUInt32(bytes, entryOffset + 4, checked((uint)data.Length));
                WriteUInt32(bytes, entryOffset + 8, payloadOffset);
                WriteUInt32(bytes, entryOffset + 12, entry.Unknown);
            }

            WriteUInt32(bytes, 0, SIGNATURE_0);
            WriteUInt32(bytes, 4, SIGNATURE_1);
            WriteUInt32(bytes, 8, checked((uint)Entry.Count));
            WriteUInt32(bytes, 12, TableOffset);

            byte[] result = bytes.ToArray();
            RawBytes = (byte[])result.Clone();
            return result;
        }

        public void Save(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            File.WriteAllBytes(path, SaveToBytes());
        }

        public void SaveBinaryIggyTexture(string path)
        {
            Save(path);
        }

        public void SaveXmlIggyTexture(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            YAXSerializer serializer = new YAXSerializer(typeof(IggyTextureFile));
            serializer.SerializeToFile(this, path);
        }

        public static void CreateXml(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            string xmlPath = path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? path : path + ".xml";
            LoadIggyTexture(path).SaveXmlIggyTexture(xmlPath);
        }

        public void LoadTextures(bool reload = true)
        {
            if (Entry == null)
                return;

            foreach (IggyTextureEntry entry in Entry)
            {
                if (!entry.IsReserved && (reload || !entry.LoadedTexture))
                    entry.LoadDds();
            }
        }

        public void SaveTextures(bool onlySaveIfEdited = true)
        {
            if (Entry == null)
                return;

            foreach (IggyTextureEntry entry in Entry)
                entry.SaveDds(onlySaveIfEdited);
        }

        public void LoadDdsImages(bool reload = true)
        {
            LoadTextures(reload);
        }

        public void SaveDdsImages()
        {
            SaveTextures(true);
        }

        public List<RgbColor> GetUsedColors()
        {
            List<RgbColor> colors = new List<RgbColor>();
            if (Entry == null)
                return colors;

            foreach (IggyTextureEntry entry in Entry)
            {
                if (!entry.IsReserved)
                    colors.Add(entry.GetDdsColor());
            }

            return colors;
        }

        public IggyTextureEntry GetEntry(int index)
        {
            if (Entry == null || index < 0 || index >= Entry.Count)
                return null;

            return Entry[index];
        }

        public IggyTextureEntry FindById(uint textureId)
        {
            return Entry == null ? null : Entry.FirstOrDefault(x => x.TextureId == textureId);
        }

        private void SetFileKind(string extension)
        {
            FileExtension = extension.ToLowerInvariant();
            Kind = string.Equals(FileExtension, IGGYTED_EXTENSION, StringComparison.Ordinal)
                ? IggyTextureFileKind.IggyTed
                : IggyTextureFileKind.IggyTex;
        }

        private static bool IsTextureExtension(string extension)
        {
            return string.Equals(extension, IGGYTEX_EXTENSION, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, IGGYTED_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        private static void GrowTo(List<byte> bytes, int length)
        {
            if (length > bytes.Count)
                bytes.AddRange(new byte[length - bytes.Count]);
        }

        private static void WriteUInt32(List<byte> bytes, int offset, uint value)
        {
            GrowTo(bytes, checked(offset + 4));
            byte[] raw = BitConverter.GetBytes(value);
            for (int index = 0; index < raw.Length; index++)
                bytes[offset + index] = raw[index];
        }

        private static byte[] CopyRange(byte[] bytes, uint offset, uint length)
        {
            RequireRange(bytes, offset, length, "Iggy texture payload");
            return Utils.GetRangeFromByteArray(bytes, checked((int)offset), checked((int)length));
        }

        private static void RequireRange(byte[] bytes, long offset, long length, string name)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
                throw new InvalidDataException($"{name} is outside the file.");
        }

        private bool TryPatchOriginalLayout(out byte[] result)
        {
            result = null;
            if (RawBytes == null || Entry == null || TableOffset < HEADER_SIZE)
                return false;

            int tableOffset = checked((int)TableOffset);
            long tableEnd = checked((long)tableOffset + (Entry.Count * ENTRY_SIZE));
            if (tableEnd > RawBytes.Length || BitConverter.ToUInt32(RawBytes, 12) != TableOffset ||
                BitConverter.ToUInt32(RawBytes, 8) != Entry.Count)
                return false;

            byte[] patched = (byte[])RawBytes.Clone();
            for (int index = 0; index < Entry.Count; index++)
            {
                IggyTextureEntry entry = Entry[index];
                if (entry == null)
                    return false;

                entry.Index = index;
                int entryOffset = checked(tableOffset + (index * ENTRY_SIZE));
                uint originalSize = BitConverter.ToUInt32(RawBytes, entryOffset + 4);
                uint originalOffset = BitConverter.ToUInt32(RawBytes, entryOffset + 8);
                if (entry.Data == null || entry.Data.Length != originalSize || entry.PayloadOffset != originalOffset)
                    return false;

                WriteUInt32(patched, entryOffset, entry.TextureId);
                WriteUInt32(patched, entryOffset + 4, originalSize);
                WriteUInt32(patched, entryOffset + 8, originalOffset);
                WriteUInt32(patched, entryOffset + 12, entry.Unknown);

                if (originalSize > 0)
                {
                    RequireRange(patched, originalOffset, originalSize, $"Iggy texture payload {entry.TextureId:X8}");
                    Buffer.BlockCopy(entry.Data, 0, patched, checked((int)originalOffset), checked((int)originalSize));
                }
            }

            RawBytes = patched;
            result = (byte[])patched.Clone();
            return true;
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            Buffer.BlockCopy(raw, 0, bytes, offset, raw.Length);
        }
    }

    public enum IggyTextureFileKind
    {
        Unknown,
        IggyTex,
        IggyTed
    }

    [Serializable]
    public class IggyTextureEntry : INotifyPropertyChanged
    {
        public const int DDS_SIGNATURE = 542327876;

        private byte[] data = new byte[0];
        private WriteableBitmap texture;
        private bool ddsIsLoading;
        private bool loadDdsFail;
        private bool loadDds;
        private bool loadDdsLock;

        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        [YAXAttributeForClass]
        public int Index { get; set; }

        [YAXAttributeForClass]
        public uint TextureId { get; set; }

        [YAXAttributeForClass]
        public uint Unknown { get; set; }

        [YAXAttributeFor("Data")]
        [YAXSerializeAs("bytes")]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ",")]
        public byte[] Data
        {
            get { return data; }
            set
            {
                data = value ?? new byte[0];
                loadDdsFail = false;
                if (loadDds && !loadDdsLock)
                    LoadDds();
                NotifyPropertyChanged(nameof(Data));
            }
        }

        [YAXDontSerialize]
        public uint PayloadSize
        {
            get { return checked((uint)(Data == null ? 0 : Data.Length)); }
        }

        [YAXDontSerialize]
        public uint PayloadOffset { get; internal set; }

        [YAXDontSerialize]
        public bool IsReserved
        {
            get { return PayloadSize == 0 && PayloadOffset == 0; }
        }

        [YAXDontSerialize]
        public int SignedTextureId
        {
            get { return unchecked((int)TextureId); }
        }

        [YAXDontSerialize]
        public bool LoadedTexture
        {
            get { return loadDds; }
        }

        [YAXDontSerialize]
        public bool LoadTextureFailed
        {
            get { return loadDdsFail; }
        }

        [YAXDontSerialize]
        public WriteableBitmap Texture
        {
            get
            {
                if (texture == null && !loadDdsFail && !ddsIsLoading)
                    LoadDds();
                return texture;
            }
            set
            {
                if (texture != value)
                {
                    texture = value;
                    NotifyPropertyChanged(nameof(Texture));
                }
            }
        }

        [YAXDontSerialize]
        public ImageEngineFormat ImageFormat { get; private set; } = ImageEngineFormat.DDS_DXT5;

        [YAXDontSerialize]
        public int Width
        {
            get
            {
                if (IsNull())
                    return 0;
                return Data.Length >= 20 && BitConverter.ToInt32(Data, 0) == DDS_SIGNATURE
                    ? BitConverter.ToInt32(Data, 12)
                    : (Texture == null ? 0 : (int)Texture.Width);
            }
        }

        [YAXDontSerialize]
        public int Height
        {
            get
            {
                if (IsNull())
                    return 0;
                return Data.Length >= 20 && BitConverter.ToInt32(Data, 0) == DDS_SIGNATURE
                    ? BitConverter.ToInt32(Data, 16)
                    : (Texture == null ? 0 : (int)Texture.Height);
            }
        }

        [YAXDontSerialize]
        public string ImageFormatString
        {
            get
            {
                switch (ImageFormat)
                {
                    case ImageEngineFormat.DDS_DXT1: return "DDS BC1";
                    case ImageEngineFormat.DDS_DXT3: return "DDS BC2";
                    case ImageEngineFormat.DDS_DXT5: return "DDS BC3";
                    case ImageEngineFormat.DDS_ATI1: return "DDS BC4";
                    case ImageEngineFormat.DDS_ATI2_3Dc: return "DDS BC5";
                    default: return ImageFormat.ToString();
                }
            }
        }

        [YAXDontSerialize]
        public string FilesizeString
        {
            get
            {
                if (Data == null)
                    return "Unknown";
                if (Data.Length < 1000)
                    return String.Format("{0} bytes", Data.Length);
                if (Data.Length < 1000000)
                    return String.Format("{0} KB", Utils.BytesToKilobytes(Data.Length));
                return String.Format("{0} MB", Utils.BytesToMegabytes(Data.Length));
            }
        }

        [YAXDontSerialize]
        public string TextureToolTip
        {
            get
            {
                if (Texture == null)
                    return null;
                return String.Format("Type: {0}\nDimensions: {1}x{2}\nSize: {3}", ImageFormatString, Height, Width, FilesizeString);
            }
        }

        public bool IsNull()
        {
            return Data == null || Data.Length == 0;
        }

        public void LoadDds()
        {
            if (!EepkToolInterlop.LoadTextures)
                return;

            try
            {
                ddsIsLoading = true;
                ImageEngineFormat format;
                Texture = TextureHelper.GetWpfBitmap(Data, out format);
                ImageFormat = format;
            }
            catch
            {
                loadDdsFail = true;
            }
            finally
            {
                loadDds = true;
                ddsIsLoading = false;
            }
        }

        public void SaveDds(bool onlySaveIfEdited = true)
        {
            if (Texture == null || (onlySaveIfEdited && !wasEdited))
                return;

            try
            {
                loadDdsLock = true;
                Data = TextureHelper.SaveToBytes(Texture, ImageFormat);
                wasEdited = false;
            }
            finally
            {
                loadDdsLock = false;
            }
        }

        [YAXDontSerialize]
        public bool wasEdited { get; set; }

        public RgbColor GetDdsColor()
        {
            if (Texture == null)
                throw new InvalidOperationException("GetDdsColor: DdsImage was null.");

            List<RgbColor> colors = new List<RgbColor>();
            for (int x = 0; x < Texture.Width; x += 15)
            {
                for (int y = 0; y < Texture.Height; y += 15)
                {
                    System.Windows.Media.Color pixel = Texture.GetPixel(x, y);
                    RgbColor color = new RgbColor(pixel.R, pixel.G, pixel.B);
                    if (!color.IsWhiteOrBlack)
                        colors.Add(color);
                }
            }

            return colors.Count == 0 ? new RgbColor(255, 255, 255) : ColorEx.GetAverageColor(colors);
        }

        public IggyTextureEntry Clone()
        {
            return new IggyTextureEntry
            {
                Index = Index,
                TextureId = TextureId,
                Unknown = Unknown,
                Data = Data == null ? new byte[0] : (byte[])Data.Clone()
            };
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
