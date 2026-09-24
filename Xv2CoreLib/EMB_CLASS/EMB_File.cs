using CSharpImageLibrary;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.EMZ;
using Xv2CoreLib.HslColor;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.Image;
using Xv2CoreLib.Resource.UndoRedo;
using YAXLib;

namespace Xv2CoreLib.EMB_CLASS
{
    public enum InstallMode
    {
        MatchName,
        MatchIndex
    }

    [YAXComment("InstallMode values (used by LB Mod Installer):" +
        "\nMatchIndex: install entry into this index (can use AutoID)" +
        "\nMatchName: if entry with same name exists, overwrite it, else add as new (can rename Index to Alias and type an alias in, without the binding brackets)")]
    [Serializable]
    public class EMB_File
    {
        internal const int SIGNATURE = 1112360227;
        private const int EMB_HEADER_SIZE = 32;
        private const int EMB_DATA_TABLE_SIZE = 8;
        private const int DATA_BYTE_ALIGNMENT = 64;
        public const int MAX_EFFECT_TEXTURES = 128;

        public event EventHandler TexturesChanged;

        public void TriggerTexturesChanged()
        {
            TexturesChanged?.Invoke(this, EventArgs.Empty);
        }

        [YAXAttributeForClass]
        [YAXErrorIfMissed(YAXExceptionTypes.Ignore)]
        public bool IsEMZ { get; set; }

        [YAXAttributeForClass]
        [YAXSerializeAs("I_08")]
        public ushort Version { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("I_10")]
        public ushort I_10 { get; set; }
        [YAXAttributeForClass]
        public bool UseFileNames { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("InstallMode")]
        public InstallMode InstallMode { get; set; } = InstallMode.MatchName;

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "EmbEntry")]
        public AsyncObservableCollection<EmbEntry> Entry { get; set; } = new();

        #region LoadSave
        public static EMB_File Load(string path)
        {
            return Load(File.ReadAllBytes(path));
        }

        public static EMB_File Load(byte[] rawBytes)
        {
            EMB_File embFile = new EMB_File();

            if (rawBytes.Length < EMB_HEADER_SIZE)
                return embFile;

            //Check if input bytes are for a EMZ file -> if it is, load the EMZ as a EMB
            if (BitConverter.ToInt32(rawBytes, 0) == EMZ_File.SIGNATURE)
            {
                EMZ_File emz = EMZ_File.Load(rawBytes);
                rawBytes = emz.Data;
                embFile.IsEMZ = true;

                if (rawBytes.Length < EMB_HEADER_SIZE)
                    return embFile;
            }

            if(BitConverter.ToInt32(rawBytes, 0) != SIGNATURE)
            {
                throw new InvalidDataException("EMB_File.Load: EMB magic bytes not found. This is not a EMB file.");
            }

            //Read header
            embFile.Version = BitConverter.ToUInt16(rawBytes, 8);
            embFile.I_10 = BitConverter.ToUInt16(rawBytes, 10);
            int count = BitConverter.ToInt32(rawBytes, 12);
            int offset = BitConverter.ToInt32(rawBytes, 24);
            int namesTableOffset = BitConverter.ToInt32(rawBytes, 28);
            embFile.UseFileNames = namesTableOffset != 0;

            //Parse entries
            for(int i = 0; i < count; i++)
            {
                int dataTableOffset = offset + (i * EMB_DATA_TABLE_SIZE);

                int dataOffset = BitConverter.ToInt32(rawBytes, dataTableOffset);
                int dataSize = BitConverter.ToInt32(rawBytes, dataTableOffset + 4);

                if(dataOffset > 0 && dataSize > 0)
                {
                    string name = null;

                    if (embFile.UseFileNames)
                    {
                        int nameOffset = BitConverter.ToInt32(rawBytes, namesTableOffset + (i * 4));
                        name = nameOffset > 0 ? rawBytes.ReadStringASCII(nameOffset) : null;
                    }
                    else
                    {
                        name = $"DATA{i:####000}";
                    }


                    byte[] data = new byte[dataSize];

                    if (dataOffset > 0 && dataSize > 0)
                        Buffer.BlockCopy(rawBytes, dataOffset + dataTableOffset, data, 0, dataSize);

                    embFile.Entry.Add(new EmbEntry()
                    {
                        ID = i,
                        Name = name,
                        Data = data
                    });
                }
            }

            return embFile;
        }

        public byte[] Write()
        {
            //Validate indexing; all EMB entries must have a unique ID. Throw if duplicates exist
            HashSet<int> entryHashList = new HashSet<int>(Entry.Count);

            for(int i = 0; i < Entry.Count; i++)
            {
                if (!entryHashList.Add(Entry[i].ID))
                {
                    throw new InvalidOperationException($"This EMB file contains duplicate IDs");
                }
            }

            //Calculate file size, offsets and create the buffer to write into
            CalculateFileSize(out int fileSize, out int nameOffsetTableStart, out int dataStart, out int namesStart, out int realCount);
            byte[] buffer = new byte[fileSize];
            Span<byte> span = buffer;

            //Header
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), SIGNATURE);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4, 2), 65534);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(6, 2), EMB_HEADER_SIZE);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(8, 2), Version);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(10, 2), I_10);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(12, 2), (ushort)realCount);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(24, 4), Entry.Count > 0 ? EMB_HEADER_SIZE : 0); //DataOffsetTable
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(28, 4), UseFileNames ? nameOffsetTableStart : 0);

            //Write data to buffer
            int dataBufferPosition = dataStart;
            int nameBufferPosition = namesStart;

            for (int i = 0; i < realCount; i++)
            {
                EmbEntry entry = GetEntry(i);
                byte[] data;
                bool isNull = entry == null || entry.IsNull();

                if (!isNull)
                {
                    data = entry.Data;
                }
                else
                {
                    data = null;
                }

                int dataOffsetTable = EMB_HEADER_SIZE + (EMB_DATA_TABLE_SIZE * i);
                dataBufferPosition += Utils.CalculatePadding(dataBufferPosition, DATA_BYTE_ALIGNMENT);

                //Write to data table
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(dataOffsetTable, 4), dataBufferPosition - dataOffsetTable);
                BinaryPrimitives.WriteInt32LittleEndian(span.Slice(dataOffsetTable + 4, 4), data?.Length ?? 0);

                //Copy Data into buffer
                if (data?.Length > 0)
                {
                    Buffer.BlockCopy(entry.Data, 0, buffer, dataBufferPosition, entry.Data.Length);
                    dataBufferPosition += entry.Data.Length;
                }

                //Write names
                if (UseFileNames)
                {
                    int nameTableOffset = nameOffsetTableStart + (4 * i);
                    string name = !isNull ? entry.Name : $"dummy_{i:00000}";

                    //Default name fallback to ensure that each entry has a name
                    if (string.IsNullOrWhiteSpace(name))
                        name = $"entry_{i:00000}";

                    BinaryPrimitives.WriteInt32LittleEndian(span.Slice(nameTableOffset, 4), nameBufferPosition);

                    if (name != null)
                    {
                        nameBufferPosition += buffer.WriteStringASCII(nameBufferPosition, name);
                    }
                    else
                    {
                        nameBufferPosition++;
                    }
                }
            }

            if (IsEMZ)
            {
                EMZ_File emz = new EMZ_File(buffer);
                return emz.Write();
            }
            else
            {
                return buffer;
            }
        }

        private void CalculateFileSize(out int fileSize, out int nameOffsetTableStart, out int dataStart, out int namesStart, out int realCount)
        {
            realCount = Entry.Max(x => x.ID) + 1;

            //Calculate file size and section starts
            fileSize = EMB_HEADER_SIZE;
            fileSize += EMB_DATA_TABLE_SIZE * realCount;
            nameOffsetTableStart = fileSize;

            if (UseFileNames)
            {
                fileSize += 4 * realCount;
            }

            fileSize += Utils.CalculatePadding(fileSize, DATA_BYTE_ALIGNMENT);
            dataStart = fileSize;

            for(int i = 0; i < Entry.Count; i++)
            {
                fileSize += Entry[i].Data.Length;

                if (UseFileNames || i < Entry.Count - 1)
                    fileSize += Utils.CalculatePadding(fileSize, DATA_BYTE_ALIGNMENT);
            }

            namesStart = fileSize;
            if (UseFileNames)
            {
                for (int i = 0; i < Entry.Count; i++)
                {
                    if (!Entry[i].IsNull())
                    {
                        //If no name is supplied, one will automatically be generated as follows: entry_XXXXX, where x is the ID (11 characters, so 11 bytes each + 1 null byte)
                        fileSize += !string.IsNullOrWhiteSpace(Entry[i].Name) ? Entry[i].Name.Length + 1 : 12;
                    }
                    else
                    {
                        //This is a dummy / null entry, so use the standard dummy name length (any name it has assigned to it will be ignored)
                        fileSize += 12;
                    }
                }

                //Space for the "dummy" entries. The names for these entries are always formated to have 11 characters (dummy_00000) so it is easy to calculate how much space is required. Like the default name, it is 12 bytes
                fileSize += 12 * (realCount - Entry.Count);
            }
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, Write());
        }

        //XML
        public static void CreateXml(string path)
        {
            EMB_File file = Load(File.ReadAllBytes(path));
            file.SaveAsXml(path + ".xml");
        }

        public static void SaveXml(string xmlPath)
        {
            string path = string.Format("{0}/{1}", Path.GetDirectoryName(xmlPath), Path.GetFileNameWithoutExtension(xmlPath));
            YAXSerializer serializer = new YAXSerializer(typeof(EMB_File), YAXSerializationOptions.DontSerializeNullObjects);
            EMB_File embFile = (EMB_File)serializer.DeserializeFromFile(xmlPath);
            embFile.Save(path);
        }

        public void SaveAsXml(string xmlPath)
        {
            YAXSerializer serializer = new YAXSerializer(typeof(EMB_File));
            serializer.SerializeToFile(this, xmlPath);
        }

        #endregion

        #region Entry Add / Remove
        public void AddEntry(byte[] data)
        {
            string name = GetUnusedName("DATA.dds");
            Entry.Add(new EmbEntry()
            {
                Name = name,
                Data = data,
                Index = Entry.Count.ToString()
            });
        }

        /// <summary>
        /// Add embEntry if new. If a similar one already exists then that will be returned.
        /// </summary>
        /// <returns></returns>
        public EmbEntry Add(EmbEntry embEntry, List<IUndoRedo> undos = null)
        {
            foreach (var entry in Entry)
            {
                if (entry == embEntry) return entry;

                if (entry.Compare(embEntry))
                {
                    return entry;
                }
            }

            //Check entry size
            if (Entry.Count >= MAX_EFFECT_TEXTURES)
            {
                throw new Exception(String.Format("EMB_File.Add: Texture limit has been reached. Cannot add any more."));
            }

            if (undos != null)
                undos.Add(new UndoableListAdd<EmbEntry>(Entry, embEntry));

            Entry.Add(embEntry);

            return embEntry;
        }

        public int AddEntry(EmbEntry embEntry, InstallMode installMode)
        {
            if (installMode == InstallMode.MatchIndex)
            {
                int idx = IndexOf(embEntry.ID);

                if(idx != -1)
                {
                    Entry[idx] = embEntry;
                }
                else
                {
                    Entry.Add(embEntry);
                }

                return embEntry.ID;
            }
            else if (installMode == InstallMode.MatchName)
            {
                int idx = IndexOf(embEntry.Name);

                if(idx != -1)
                {
                    embEntry.ID = Entry[idx].ID;
                    Entry[idx] = embEntry;
                }
                else
                {
                    embEntry.ID = GetNewID();
                    Entry.Add(embEntry);
                }

                return embEntry.ID;
            }

            return -1;
        }

        public void RemoveEntry(string index, EmbEntry original = null)
        {
            int id = int.Parse(index);
            int idx = IndexOf(id);

            if(idx != -1)
            {
                if(original != null)
                {
                    original.ID = id;
                    Entry[idx] = original;
                }
                else
                {
                    Entry.RemoveAt(idx);
                }
            }
        }

        #endregion

        #region Helper
        public EmbEntry GetEntry(int id)
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry[i].ID == id)
                    return Entry[i];
            }

            return null;
        }

        public EmbEntry GetEntry(string name)
        {
            foreach (var entry in Entry)
            {
                if (entry.Name == name) return entry;
            }

            return null;
        }

        public EmbEntry Compare(EmbEntry embEntry2, bool ignoreName = false)
        {
            foreach (var entry in Entry)
            {
                if (entry == embEntry2) return entry;

                if (entry.Compare(embEntry2, ignoreName))
                {
                    return entry;
                }
            }

            return null;
        }

        public int GetNewID()
        {
            int id = 0;

            while (Entry.Any(x => x.ID == id))
                id++;

            return id;
        }

        public string GetUnusedName(string name)
        {
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            string newName = name;
            int num = 1;

            while (NameUsed(newName))
            {
                newName = string.Format("{0}_{1}{2}", nameWithoutExtension, num, extension);
                num++;
            }

            return newName;
        }

        public bool NameUsed(string name)
        {
            foreach (var entry in Entry)
            {
                if (entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;

            }

            return false;
        }

        public static EMB_File DefaultEmbFile(bool textureEmb)
        {
            if (textureEmb == true)
            {
                return new EMB_File()
                {
                    Version = 1,
                    I_10 = 1,
                    UseFileNames = true
                };
            }
            else
            {
                return new EMB_File()
                {
                    Version = 37568,
                    I_10 = 0,
                    UseFileNames = true
                };
            }
        }

        private int IndexOf(int id)
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry[i].ID == id) return i;
            }
            return -1;
        }

        private int IndexOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;

            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry[i].Name == name) return i;
            }
            return -1;
        }

        public void ValidateNames()
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry.Any(x => x != Entry[i] && x.Name == Entry[i].Name))
                {
                    Entry[i].Name = GetUnusedName(Entry[i].Name);
                }
            }
        }

        public void MergeEmbFile(EMB_File embFile)
        {
            if (embFile == null) return;

            foreach (var entry in embFile.Entry)
            {
                string name = embFile.UseFileNames ? GetUnusedName(entry.Name) : $"DATA{Entry.Count}.dds";
                EmbEntry newEntry = entry.Clone();
                newEntry.Name = name;

                Entry.Add(newEntry);
            }
        }

        #endregion

        #region Texture

        /// <summary>
        /// Attemps to load all EMB entries as a DDS image file and saves them to a ImageSource object.
        /// </summary>
        public void LoadDdsImages(bool reload = true)
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (!Entry[i].loadDds || reload)
                {
                    Entry[i].LoadDds();
                }
            }
        }

        /// <summary>
        /// Saves all loaded DdsImages into Data.
        /// </summary>
        public void SaveDdsImages()
        {
            string name = null;
            try
            {
                foreach (var entry in Entry)
                {
                    name = entry.Name;
                    if (entry.loadDds)
                    {
                        entry.SaveDds();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("SaveDdsImages: Failed on entry with name = {0}.", name), ex);
            }
        }

        public List<RgbColor> GetUsedColors()
        {
            List<RgbColor> colors = new List<RgbColor>();

            foreach (var entry in Entry)
            {
                colors.Add(entry.GetDdsColor());
            }

            return colors;
        }

        public List<EmbEntry> GetAllEmbEntriesByBitmap(List<WriteableBitmap> bitmaps)
        {
            List<EmbEntry> entries = new List<EmbEntry>();

            foreach (var bitmap in bitmaps)
            {
                entries.Add(Entry.FirstOrDefault(x => x.Texture == bitmap));
            }

            return entries;
        }

        public async void ChangeHue(double hue, double saturation, double lightness, List<IUndoRedo> undos = null, bool hueSet = false, int variance = 0)
        {
            if (Entry == null) return;

            foreach (var entry in Entry)
            {
                await entry.ChangeHue(hue, saturation, lightness, undos, hueSet, variance);
                entry.SaveDds(true, undos);
            }
        }
        #endregion

    }

    [Serializable]
    public class EmbEntry : IInstallable, INotifyPropertyChanged
    {
        #region NotPropChanged
        [field: NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
        public const int DDS_SIGNATURE = 542327876;

        private string _name = null;
        private byte[] _data = Array.Empty<byte>();

        #region ID
        [YAXDontSerialize]
        public int SortID
        {
            get => ID;
            set => ID = value;
        }
        [YAXDontSerialize]
        public int ID
        {
            get => Utils.TryParseInt(Index);
            set
            {
                Index = value.ToString();
                NotifyPropertyChanged(nameof(ID));
                NotifyPropertyChanged(nameof(Index));
            }
        }
        #endregion

        [YAXAttributeForClass]
        [YAXSerializeAs("Name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    NotifyPropertyChanged(nameof(Name));
                }
            }
        }
        [YAXAttributeForClass]
        [YAXSerializeAs("Index")]
        [YAXDontSerializeIfNull]
        [BindingAutoId]
        public string Index { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Alias")]
        [YAXDontSerializeIfNull]
        public string InstallerAlias { get; set; } //Alias to use when installing with NameMatch mode. The ID of the name matched entry, or a newly added entry (if no entry with that name was found) will be assigned to this alias.
        [YAXAttributeFor("Data")]
        [YAXSerializeAs("bytes")]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ",")]
        public byte[] Data
        {
            get
            {
                return this._data;
            }
            set
            {
                if (value != _data)
                {
                    _data = value;
                    loadDdsFail = false;

                    //Reload DdsImage IF it has been loaded already (loadDds == true) AND it is not currently being saved (loadDdsLock == false)
                    if (loadDds && !_loadDdsLock)
                    {
                        //Reload the dds with the new image data
                        LoadDds();
                        wasEdited = false;
                    }

                    NotifyPropertyChanged(nameof(Data));
                }
            }
        }

        #region Texture
        //Texture loading & handling
        public ImageEngineFormat ImageFormat = ImageEngineFormat.DDS_DXT5;

        [NonSerialized]
        public bool wasEdited = false; //If false, we won't save the DDS file.
        [NonSerialized]
        public bool wasReloaded = false; //Was SaveDds() ever called on this object?
        [NonSerialized]
        private bool _loadDdsLock = false;
        [NonSerialized]
        public bool loadDds = false;
        [NonSerialized]
        private bool ddsIsLoading = false;
        [NonSerialized]
        public bool loadDdsFail = false;
        [NonSerialized]
        private WriteableBitmap _texture = null;
        [YAXDontSerialize]
        public WriteableBitmap Texture
        {
            get
            {
                if (_texture == null && !loadDdsFail && !ddsIsLoading)
                {
                    LoadDds();
                }
                return _texture;
            }
            set
            {
                if (value != this._texture)
                {
                    _texture = value;
                    NotifyPropertyChanged(nameof(Texture));
                }
            }
        }


        //Texture Details (readonly)
        [YAXDontSerialize]
        public int Height
        {
            get
            {
                //It is possible for the texture to not be DDS (and loads perfectly fine ingame), so we must check.
                if (IsNull() || Texture == null) return 0;
                return (BitConverter.ToInt32(Data, 0) == DDS_SIGNATURE) ? BitConverter.ToInt32(Data, 16) : (int)Texture.Height;
            }
        }
        [YAXDontSerialize]
        public int Width
        {
            get
            {
                if (IsNull() || Texture == null) return 0;
                return (BitConverter.ToInt32(Data, 0) == DDS_SIGNATURE) ? BitConverter.ToInt32(Data, 12) : (int)Texture.Width;
            }
        }
        [YAXDontSerialize]
        public string FilesizeString
        {
            get
            {
                if (Data != null)
                {
                    if (Data.Length < 1000)
                    {
                        //Is less than a kilobyte
                        return String.Format("{0} bytes", Data.Length);
                    }
                    else if (Data.Length < 1000000)
                    {
                        //Is atleast a kilobyte and less than a megabyte
                        return String.Format("{0} KB", Utils.BytesToKilobytes(Data.Length));
                    }
                    else
                    {
                        //Is a megabyte or more
                        return String.Format("{0} MB", Utils.BytesToMegabytes(Data.Length));
                    }
                }
                else
                {
                    return "Unknown";
                }
            }
        }
        [YAXDontSerialize]
        public string ImageFormatString
        {
            get
            {
                switch (ImageFormat)
                {
                    case ImageEngineFormat.DDS_DXT1:
                        return "DDS BC1";
                    case ImageEngineFormat.DDS_DXT3:
                        return "DDS BC2";
                    case ImageEngineFormat.DDS_DXT5:
                        return "DDS BC3";
                    case ImageEngineFormat.DDS_ATI1:
                        return "DDS BC4";
                    case ImageEngineFormat.DDS_ATI2_3Dc:
                        return "DDS BC5";
                    default:
                        return ImageFormat.ToString();
                }
            }
        }
        [YAXDontSerialize]
        public string TextureToolTip
        {
            get
            {
                if (Texture != null)
                {
                    return $"Type: {ImageFormatString}\n" +
                           $"Dimensions: {Height}x{Width}\n" +
                           $"Size: {FilesizeString}";
                }
                return null;
            }
        }
        #endregion

        public bool Compare(EmbEntry embEntry2, bool ignoreName = false)
        {
            //Name and bytes must be the same to return true
            if (embEntry2.Name == Name || ignoreName)
            {
                return Data.SequenceEqual(embEntry2.Data);
            }
            else
            {
                return false;
            }
        }

        public bool IsNull()
        {
            return Data == null || Data.Length == 0;
        }

        public EmbEntry Clone()
        {
            EmbEntry newEntry = new EmbEntry();
            newEntry.Name = Name;
            newEntry.Data = Data;
            newEntry.Texture = Texture;
            return newEntry;
        }

        public static EmbEntry Empty(int idx = 0)
        {
            return new EmbEntry()
            {
                Name = "dummy_" + idx.ToString(),
                Data = new byte[0],
                Index = idx.ToString()
            };
        }

        #region Texture

        /// <summary>
        /// Loads DdsImage from Data.
        /// </summary>
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

        /// <summary>
        /// Saves DdsImage into Data.
        /// </summary>
        public void SaveDds(bool onlySaveIfEdited = true, List<IUndoRedo> undos = null)
        {
            if (Texture == null || (!wasEdited && onlySaveIfEdited))
                return;

            try
            {
                _loadDdsLock = true;
                byte[] data = TextureHelper.SaveToBytes(Texture, ImageFormat);

                if (undos != null)
                    undos.Add(new UndoablePropertyGeneric(nameof(Data), this, Data, data));

                Data = data;
            }
            finally
            {
                wasReloaded = true;
                _loadDdsLock = false;
            }

        }

        public async Task ChangeHue(double hue, double _saturation, double lightness, List<IUndoRedo> undos = null, bool hueSet = false, int variance = 0)
        {
            if (Texture == null)
                return;

            WriteableBitmapEditOperation editOperation = new WriteableBitmapEditOperation(Texture);

            if (hueSet)
            {
                if (variance != 0)
                    hue += Random.Range(-variance, variance);

                await editOperation.AsyncApplyHueSet((int)hue);
            }
            else
            {
                float brightness = (float)lightness / 5f;
                float saturation = (float)_saturation;
                await editOperation.AsyncApplyHueAdjust((int)hue, saturation, brightness);
            }

            wasEdited = true;
            Texture = editOperation.OutputBitmap;

            if (undos != null)
                undos.Add(new UndoableProperty<EmbEntry>(nameof(Texture), this, editOperation.SourceBitmap, editOperation.OutputBitmap));
        }

        public RgbColor GetDdsColor()
        {
            if (Texture == null) throw new InvalidOperationException("GetDdsColor: DdsImage was null.");
            List<RgbColor> colors = new List<RgbColor>();

            //Lazy code. Checking every single pixel would be WAY too slow, so we just skim through them instead.
            for (int i = 0; i < Texture.Width; i += 15)
            {
                if (i > Texture.Width) break;

                for (int a = 0; a < Texture.Height; a += 15)
                {
                    if (a > Texture.Height) break;

                    var pixel = Texture.GetPixel(i, a);
                    RgbColor rgbColor = new RgbColor(pixel.R, pixel.G, pixel.B);

                    if (!rgbColor.IsWhiteOrBlack)
                    {
                        colors.Add(rgbColor);
                    }
                }
            }

            if (colors.Count == 0)
            {
                return new RgbColor(255, 255, 255);
            }

            return ColorEx.GetAverageColor(colors);
        }

        public BitmapSource GetBitmap()
        {
            return Texture;
        }

        #endregion

        #region Texture Mering (Supertexture)
        public static List<WriteableBitmap> GetBitmaps(IList<EmbEntry> entries)
        {
            List<WriteableBitmap> bitmaps = new List<WriteableBitmap>();

            foreach (var entry in entries)
                bitmaps.Add(entry.Texture);

            return bitmaps;
        }

        public static double SelectTextureSize(double maxDimension, int textureCount)
        {
            double size = Math.Sqrt(textureCount) * maxDimension;
            double textureSize = 64;

            while (textureSize < size)
            {
                if (textureSize >= 2048)
                    return -1; //2k max texture size

                textureSize *= 2;
            }

            return textureSize;
        }

        public static double HighestDimension(List<WriteableBitmap> bitmaps)
        {
            double dimension = 0;

            foreach (var bitmap in bitmaps)
            {
                if (bitmap.Width > dimension) dimension = bitmap.Width;
                if (bitmap.Height > dimension) dimension = bitmap.Height;
            }

            return dimension;
        }

        #endregion

    }
}
