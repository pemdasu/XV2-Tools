using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using YAXLib;

namespace Xv2CoreLib.BCM
{
    public class Deserializer
    {
        BCM_File bcmFile { get; set; }
        public List<byte> bytes = new List<byte>();

        private readonly List<WriteEntry> entries = new List<WriteEntry>();
        private readonly Dictionary<BCM_Entry, WriteEntry> writeEntries = new Dictionary<BCM_Entry, WriteEntry>();
        private readonly Dictionary<string, int> slotsByIndex = new Dictionary<string, int>();
        private readonly HashSet<string> duplicateIndexes = new HashSet<string>();

        public Deserializer (string location)
        {
            YAXSerializer serializer = new YAXSerializer(typeof(BCM_File), YAXSerializationOptions.DontSerializeNullObjects);
            bcmFile = (BCM_File)serializer.Deserialize(XDocument.Load(location).Root);
            WriteBinaryFile();
            File.WriteAllBytes(String.Format("{0}/{1}", Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location)), bytes.ToArray());
        }

        public Deserializer(BCM_File _bcmFile, string _saveLocation)
        {
            bcmFile = _bcmFile;
            WriteBinaryFile();
            File.WriteAllBytes(_saveLocation, bytes.ToArray());
        }

        public Deserializer(BCM_File _bcmFile)
        {
            bcmFile = _bcmFile;
            WriteBinaryFile();
        }

        private void WriteBinaryFile()
        {
            byte[] header = new byte[BCM_File.HeaderSize] { 35, 66, 67, 77, 254, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            if (bcmFile.BCMEntries == null || bcmFile.BCMEntries.Count == 0)
            {
                bytes = header.ToList();
                return;
            }

            if (bcmFile.BCMEntries.Count != 1)
                throw new InvalidDataException("Incorrect BCM hierarchy. There must be exactly 1 root BCM entry.");

            if (bcmFile.BCMEntries[0].LoopAsChild != null)
                throw new InvalidDataException("Child_GoTo_Idx is not valid for the root BCM entry.");

            if (bcmFile.BCMEntries[0].LoopAsSibling != null)
                throw new InvalidDataException("Sibling_GoTo_Idx is not valid for the root BCM entry.");

            AddEntries(bcmFile.BCMEntries, null, null);
            SetSlots();

            byte[] file = new byte[BCM_File.HeaderSize + entries.Count * BCM_File.EntrySize];
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream(file)))
            {
                writer.Write(header, 0, 8);
                writer.Write(entries.Count);
                writer.Write(GetOffset(entries[0]));

                foreach (WriteEntry entry in entries)
                {
                    writer.BaseStream.Position = GetOffset(entry);
                    WriteEntryBytes(writer, entry);
                }
            }

            bytes = file.ToList();
        }

        private void AddEntries(List<BCM_Entry> list, WriteEntry parent, WriteEntry topLevel)
        {
            for (int i = 0; i < list.Count; i++)
            {
                BCM_Entry entry = list[i];

                if (entry.LoopAsSibling != null && i < list.Count - 1)
                    throw new InvalidDataException($"Invalid Sibling_GoTo_Idx on BCM entry {entry.Index}. It has a following sibling, so the GoTo ({entry.LoopAsSibling}) cannot be used.");

                if (writeEntries.ContainsKey(entry))
                    throw new InvalidDataException($"BCM entry {entry.Index} is in the tree more than once. Use a GoTo link to reuse it.");

                WriteEntry writeEntry = new WriteEntry()
                {
                    Entry = entry,
                    Parent = parent,
                    NextSibling = i < list.Count - 1 ? list[i + 1] : null,
                    TreePosition = entries.Count
                };
                writeEntry.TopLevel = parent == null ? null : topLevel ?? writeEntry;

                entries.Add(writeEntry);
                writeEntries.Add(entry, writeEntry);

                if (entry.BCMEntries?.Count > 0)
                    AddEntries(entry.BCMEntries, writeEntry, writeEntry.TopLevel);
            }
        }

        //Entries with a numeric Index go first, in Index order, which keeps the layout of a parsed file.
        //Other entries (such as ones with installer suffixes) follow in tree order.
        private void SetSlots()
        {
            List<WriteEntry> layout = entries
                .OrderBy(x => int.TryParse(x.Entry.Index, out int index) ? index : int.MaxValue)
                .ThenBy(x => x.TreePosition)
                .ToList();

            for (int slot = 0; slot < layout.Count; slot++)
            {
                WriteEntry entry = layout[slot];
                entry.Slot = slot;

                if (entry.Entry.Index == null) continue;

                if (slotsByIndex.ContainsKey(entry.Entry.Index))
                    duplicateIndexes.Add(entry.Entry.Index);
                else
                    slotsByIndex.Add(entry.Entry.Index, slot);
            }
        }

        private void WriteEntryBytes(BinaryWriter writer, WriteEntry writeEntry)
        {
            BCM_Entry bcmEntry = writeEntry.Entry;

            int siblingOffset = bcmEntry.LoopAsSibling != null ? GetGoToOffset(bcmEntry, bcmEntry.LoopAsSibling)
                : writeEntry.NextSibling != null ? GetOffset(writeEntries[writeEntry.NextSibling]) : 0;

            int childOffset = bcmEntry.LoopAsChild != null ? GetGoToOffset(bcmEntry, bcmEntry.LoopAsChild)
                : bcmEntry.BCMEntries?.Count > 0 ? GetOffset(writeEntries[bcmEntry.BCMEntries[0]]) : 0;

            //Children of the root entry store 0 as parent. Every entry below the root stores its top-level ancestor.
            int parentOffset = writeEntry.Parent?.Parent == null ? 0 : GetOffset(writeEntry.Parent);
            int rootOffset = writeEntry.TopLevel == null ? 0 : GetOffset(writeEntry.TopLevel);

            writer.Write(bcmEntry.I_00);
            writer.Write((uint)bcmEntry.DirectionalInput);
            writer.Write((uint)bcmEntry.ButtonInput);
            writer.Write(bcmEntry.HoldDownConditions);
            writer.Write(bcmEntry.PriorityLayer);
            writer.Write(bcmEntry.I_18);
            writer.Write(bcmEntry.I_19);
            writer.Write(bcmEntry.MinimumLoopDuration);
            writer.Write(bcmEntry.MaximumLoopDuration);
            writer.Write((uint)bcmEntry.PrimaryActivatorConditions);
            writer.Write((uint)bcmEntry.ActivatorState);
            writer.Write(bcmEntry.BacEntryPrimary);
            writer.Write(bcmEntry.BacEntryCharge);
            writer.Write(bcmEntry.I_36);
            writer.Write(bcmEntry.BacEntryUserConnect);
            writer.Write(bcmEntry.BacEntryVictimConnect);
            writer.Write(bcmEntry.BacEntryAirborne);
            writer.Write(bcmEntry.BacEntryTargetingOverride);
            writer.Write(bcmEntry.RandomFlag);
            writer.Write(siblingOffset);
            writer.Write(childOffset);
            writer.Write(parentOffset);
            writer.Write(rootOffset);
            writer.Write(bcmEntry.I_64);
            writer.Write(bcmEntry.I_68);
            writer.Write(bcmEntry.I_72);
            writer.Write(bcmEntry.MoveCategory);
            writer.Write(bcmEntry.I_80);
            writer.Write(bcmEntry.StaminaCost);
            writer.Write(bcmEntry.I_88);
            writer.Write(bcmEntry.KiRequired);
            writer.Write(bcmEntry.HealthRequired);
            writer.Write(bcmEntry.TransStage);
            writer.Write(bcmEntry.CusAura);
            writer.Write(bcmEntry.I_104);
            writer.Write(bcmEntry.I_108);
        }

        private int GetOffset(WriteEntry entry)
        {
            return GetSlotOffset(entry.Slot);
        }

        private int GetSlotOffset(int slot)
        {
            return BCM_File.HeaderSize + slot * BCM_File.EntrySize;
        }

        private int GetGoToOffset(BCM_Entry entry, string targetIndex)
        {
            if (duplicateIndexes.Contains(targetIndex))
                throw new InvalidDataException($"Cannot complete GoTo on BCM entry {entry.Index}. More than one entry has Idx {targetIndex}.");

            if (!slotsByIndex.TryGetValue(targetIndex, out int slot))
                throw new InvalidDataException($"Cannot complete GoTo on BCM entry {entry.Index}. Idx {targetIndex} does not exist in the BCM file.");

            return GetSlotOffset(slot);
        }

        private class WriteEntry
        {
            public BCM_Entry Entry;
            public WriteEntry Parent;
            public WriteEntry TopLevel;
            public BCM_Entry NextSibling;
            public int TreePosition;
            public int Slot;
        }
    }
}
