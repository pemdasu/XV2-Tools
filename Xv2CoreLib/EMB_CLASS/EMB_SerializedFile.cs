using System.IO;
using YAXLib;

namespace Xv2CoreLib.EMB_CLASS
{
    public enum InstallMode
    {
        MatchName,
        MatchIndex
    }
    //For XML / Installer use

    [YAXComment("InstallMode values (used by LB Mod Installer):" +
        "\nMatchIndex: install entry into this index (can use AutoID)" +
        "\nMatchName: if entry with same name exists, overwrite it, else add as new (can rename Index to Alias and type an alias in, without the binding brackets)")]
    [YAXSerializeAs("EMB_File")]
    public class EMB_SerializedFile : EMB_BaseFile<EMB_SerializedEntry>
    {
        [YAXAttributeForClass]
        [YAXErrorIfMissed(YAXExceptionTypes.Ignore)]
        public override bool IsEMZ { get => base.IsEMZ; set => base.IsEMZ = value; }

        [YAXAttributeForClass]
        [YAXSerializeAs("I_08")]
        public override ushort Version { get => base.Version; set => base.Version = value; }
        [YAXAttributeForClass]
        [YAXSerializeAs("I_10")]
        public override ushort I_10 { get => base.I_10; set => base.I_10 = value; }
        [YAXAttributeForClass]
        public override bool UseFileNames { get => base.UseFileNames; set => base.UseFileNames = value; }
        [YAXAttributeForClass]
        [YAXSerializeAs("InstallMode")]
        [YAXErrorIfMissed(YAXExceptionTypes.Ignore)]
        public InstallMode InstallMode { get; set; } = InstallMode.MatchName;

        public EMB_SerializedFile() { }

        public EMB_SerializedFile(EMB_BaseFile<EMB_SerializedEntry> embFile)
        {
            IsEMZ = embFile.IsEMZ;
            Version = embFile.Version;
            I_10 = embFile.I_10;
            UseFileNames = embFile.UseFileNames;
            Entry = embFile.Entry;
        }

        public EMB_SerializedFile(EMB_File embFile)
        {
            IsEMZ = embFile.IsEMZ;
            Version = embFile.Version;
            I_10 = embFile.I_10;
            UseFileNames = embFile.UseFileNames;

            foreach (var entry in embFile.Entry)
                Entry.Add(new EMB_SerializedEntry(entry));
        }

        #region LoadSave
        public static EMB_SerializedFile Load(string path)
        {
            return new EMB_SerializedFile(LoadInternal(File.ReadAllBytes(path)));
        }

        [FileLoad]
        public static EMB_SerializedFile Load(byte[] bytes)
        {
            return new EMB_SerializedFile(LoadInternal(bytes));
        }

        public static void CreateXml(string path)
        {
            EMB_SerializedFile file = Load(File.ReadAllBytes(path));
            file.SaveAsXml(path + ".xml");
        }

        public static void SaveXml(string xmlPath)
        {
            string path = string.Format("{0}/{1}", Path.GetDirectoryName(xmlPath), Path.GetFileNameWithoutExtension(xmlPath));
            YAXSerializer serializer = new YAXSerializer(typeof(EMB_SerializedFile), YAXSerializationOptions.DontSerializeNullObjects);
            EMB_SerializedFile embFile = (EMB_SerializedFile)serializer.DeserializeFromFile(xmlPath);
            embFile.Save(path);
        }

        public void SaveAsXml(string xmlPath)
        {
            YAXSerializer serializer = new YAXSerializer(typeof(EMB_SerializedFile));
            serializer.SerializeToFile(this, xmlPath);
        }

        #endregion

        public int InstallEntry(EmbEntry embEntry, InstallMode installMode)
        {
            if (installMode == InstallMode.MatchIndex)
            {
                int idx = IndexOf(embEntry.ID);

                if (idx != -1)
                {
                    Entry[idx] = new EMB_SerializedEntry(embEntry);
                }
                else
                {
                    Entry.Add(new EMB_SerializedEntry(embEntry));
                }

                return embEntry.ID;
            }
            else if (installMode == InstallMode.MatchName)
            {
                int idx = IndexOf(embEntry.Name);

                if (idx != -1)
                {
                    embEntry.ID = Entry[idx].ID;
                    Entry[idx] = new EMB_SerializedEntry(embEntry);
                }
                else
                {
                    embEntry.ID = GetNewID();
                    Entry.Add(new EMB_SerializedEntry(embEntry));
                }

                return embEntry.ID;
            }

            return -1;
        }

        public void UninstallEntry(string index, EmbEntry original = null)
        {
            int id = int.Parse(index);
            int idx = IndexOf(id);

            if (idx != -1)
            {
                if (original != null)
                {
                    original.ID = id;
                    Entry[idx] = new EMB_SerializedEntry(original);
                }
                else
                {
                    Entry.RemoveAt(idx);
                }
            }
        }

    }

    [YAXSerializeAs("EmbEntry")]
    public class EMB_SerializedEntry : EmbEntry, IInstallable
    {
        [YAXDontSerialize]
        public int SortID
        {
            get => ID;
            set => ID = value;
        }
        [YAXDontSerialize]
        public override int ID { get => Utils.TryParseInt(Index); set => Index = value.ToString(); }

        [YAXAttributeForClass]
        [YAXSerializeAs("Name")]
        public string SerializedName { get => base.Name; set => base.Name = value; }
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
        public byte[] SerializedData { get => base.Data; set => base.Data = value; }

        public EMB_SerializedEntry() { }

        public EMB_SerializedEntry(EmbEntry entry)
        {
            ID = entry.ID;
            Name = entry.Name;
            Data = entry.Data;
        }
    }

}
