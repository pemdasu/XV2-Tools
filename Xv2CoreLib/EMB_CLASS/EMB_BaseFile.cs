using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xv2CoreLib.EMZ;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.UndoRedo;
using YAXLib;

namespace Xv2CoreLib.EMB_CLASS
{
    [Serializable]
    public class EMB_BaseFile<T> where T : EmbEntry, new()
    {
        internal const int SIGNATURE = 1112360227;
        private const int EMB_HEADER_SIZE = 32;
        private const int EMB_DATA_TABLE_SIZE = 8;
        private const int DATA_BYTE_ALIGNMENT = 64;
        public const int MAX_EFFECT_TEXTURES = 128;

        public virtual bool IsEMZ { get; set; }
        public virtual ushort Version { get; set; }
        public virtual ushort I_10 { get; set; }
        public virtual bool UseFileNames { get; set; }

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "EmbEntry")]
        public AsyncObservableCollection<T> Entry { get; set; } = new();

        #region LoadSave
        protected static EMB_BaseFile<T> LoadInternal(byte[] rawBytes)
        {
            EMB_BaseFile<T> embFile = new EMB_BaseFile<T>();

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

            if (BitConverter.ToInt32(rawBytes, 0) != SIGNATURE)
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
            for (int i = 0; i < count; i++)
            {
                int dataTableOffset = offset + (i * EMB_DATA_TABLE_SIZE);

                int dataOffset = BitConverter.ToInt32(rawBytes, dataTableOffset);
                int dataSize = BitConverter.ToInt32(rawBytes, dataTableOffset + 4);

                if (dataOffset > 0 && dataSize > 0)
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

                    embFile.Entry.Add(new T()
                    {
                        ID = i,
                        Name = name,
                        Data = data
                    });
                }
            }

            return embFile;
        }

        [FileSave]
        public byte[] SaveToBytes()
        {
            //Validate indexing; all EMB entries must have a unique ID. Throw if duplicates exist
            HashSet<int> entryHashList = new HashSet<int>(Entry.Count);

            for (int i = 0; i < Entry.Count; i++)
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
                T entry = GetEntry(i);
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
                return emz.SaveToBytes();
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

            for (int i = 0; i < Entry.Count; i++)
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
            File.WriteAllBytes(path, SaveToBytes());
        }

        #endregion

        #region Helper
        public T GetEntry(int id)
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry[i].ID == id)
                    return Entry[i];
            }

            return null;
        }

        public T GetEntry(string name)
        {
            foreach (var entry in Entry)
            {
                if (entry.Name == name) return entry;
            }

            return null;
        }

        public T Compare(T EmbEntry, bool ignoreName = false)
        {
            foreach (var entry in Entry)
            {
                if (entry == EmbEntry) return entry;

                if (entry.Compare(EmbEntry, ignoreName))
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

        protected int IndexOf(int id)
        {
            for (int i = 0; i < Entry.Count; i++)
            {
                if (Entry[i].ID == id) return i;
            }
            return -1;
        }

        protected int IndexOf(string name)
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

        public void MergeEmbFile(EMB_BaseFile<T> embFile)
        {
            if (embFile == null) return;

            foreach (var entry in embFile.Entry)
            {
                string name = embFile.UseFileNames ? GetUnusedName(entry.Name) : $"DATA{Entry.Count}.dds";
                T newEntry = (T)entry.Clone();
                newEntry.Name = name;

                Entry.Add(newEntry);
            }
        }

        public void SetIndexAsID()
        {
            for (int i = 0; i < Entry.Count; i++)
                Entry[i].ID = i;
        }
        #endregion

        #region Add
        public int AddEntry(byte[] data, string name = null)
        {
            name = string.IsNullOrWhiteSpace(name) ? GetUnusedName("DATA.dds") : name;
            int id = GetNewID();

            Entry.Add(new T()
            {
                Name = name,
                Data = data,
                ID = id
            });

            return id;
        }

        #endregion
    }

    [Serializable]
    public class EmbEntry
    {
        [YAXDontSerialize]
        public virtual int ID { get; set; }
        [YAXDontSerialize]
        public virtual string Name { get; set; } = string.Empty;
        [YAXDontSerialize]
        public virtual byte[] Data { get; set; } = Array.Empty<byte>();


        public bool Compare(EmbEntry EmbEntry, bool ignoreName = false)
        {
            //Name and bytes must be the same to return true
            if (EmbEntry.Name == Name || ignoreName)
            {
                return Data.SequenceEqual(EmbEntry.Data);
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

        public virtual EmbEntry Clone()
        {
            return new EmbEntry
            {
                Name = Name,
                Data = Data
            };
        }
    }
}
