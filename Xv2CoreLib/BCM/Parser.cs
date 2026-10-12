using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using YAXLib;

namespace Xv2CoreLib.BCM
{
    public class Parser
    {
        public BCM_File bcmFile { get; private set; } = new BCM_File();
        byte[] rawBytes { get; set; }
        int count;

        public Parser(byte[] _rawBytes)
        {
            rawBytes = _rawBytes;

            ParseBcm();
        }

        public Parser (string location, bool writeXml)
        {
            rawBytes = File.ReadAllBytes(location);

            ParseBcm();

            if (writeXml)
            {
                YAXSerializer serializer = new YAXSerializer(typeof(BCM_File));
                serializer.SerializeToFile(bcmFile, location + ".xml");
            }
        }

        public BCM_File GetBcmFile()
        {
            return bcmFile;
        }

        private void ParseBcm()
        {
            count = BitConverter.ToInt32(rawBytes, 8);
            int rootOffset = BitConverter.ToInt32(rawBytes, 12);

            if (rootOffset == 0)
                return;

            if (count <= 0 || rawBytes.Length != BCM_File.HeaderSize + count * BCM_File.EntrySize)
                throw new InvalidDataException($"BCM header declares {count} entries, but the file size is {rawBytes.Length} bytes.");

            BCM_Entry[] entries = new BCM_Entry[count];
            for (int i = 0; i < count; i++)
                entries[i] = ParseBcmEntry(BCM_File.HeaderSize + i * BCM_File.EntrySize, i);

            int rootIndex = GetEntryIndex(rootOffset, "header", "root");

            //The tree comes from each entry's parent offset. Sibling and child links that do not follow
            //that tree stay as GoTo links, so every entry keeps its place in the file.
            int[] parents = new int[count];
            List<int>[] children = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                if (i == rootIndex) continue;

                int parentOffset = ReadLink(i, 56);
                parents[i] = parentOffset == 0 ? rootIndex : GetEntryIndex(parentOffset, entries[i].Index, "parent");

                if (children[parents[i]] == null)
                    children[parents[i]] = new List<int>();

                children[parents[i]].Add(i);
            }

            bool[] placed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                List<int> ordered = children[i] == null ? new List<int>() : OrderChildren(i, children[i], parents, placed);

                if (ordered.Count > 0)
                    entries[i].BCMEntries = ordered.Select(x => entries[x]).ToList();

                entries[i].LoopAsChild = GetGoTo(entries[i], ReadLink(i, 52), ordered.Count > 0 ? ordered[0] : -1, "child");

                for (int k = 0; k < ordered.Count; k++)
                    entries[ordered[k]].LoopAsSibling = GetGoTo(entries[ordered[k]], ReadLink(ordered[k], 48), k < ordered.Count - 1 ? ordered[k + 1] : -1, "sibling");
            }

            entries[rootIndex].LoopAsSibling = GetGoTo(entries[rootIndex], ReadLink(rootIndex, 48), -1, "sibling");
            bcmFile.BCMEntries = new List<BCM_Entry>() { entries[rootIndex] };

            HashSet<BCM_Entry> inTree = new HashSet<BCM_Entry>(BCM_File.FlattenEntries(bcmFile.BCMEntries));
            BCM_Entry detached = entries.FirstOrDefault(x => !inTree.Contains(x));
            if (detached != null)
                throw new InvalidDataException($"BCM entry {detached.Index} has a parent offset loop and does not connect to the root entry.");
        }

        //Follow the stored sibling chain first, then add the children it does not reach in file order.
        private List<int> OrderChildren(int parent, List<int> children, int[] parents, bool[] placed)
        {
            List<int> ordered = new List<int>();
            int offset = ReadLink(parent, 52);

            while (IsEntryOffset(offset))
            {
                int index = GetBcmIndex(offset);
                if (parents[index] != parent || placed[index]) break;

                ordered.Add(index);
                placed[index] = true;
                offset = ReadLink(index, 48);
            }

            foreach (int index in children)
            {
                if (placed[index]) continue;

                ordered.Add(index);
                placed[index] = true;
            }

            return ordered;
        }

        private string GetGoTo(BCM_Entry entry, int linkOffset, int treeTarget, string linkName)
        {
            if (linkOffset == 0)
            {
                if (treeTarget != -1)
                    throw new InvalidDataException($"BCM entry {entry.Index} has no {linkName} link, but the parent offsets place entry {treeTarget} there. This layout cannot be represented.");

                return null;
            }

            int target = GetEntryIndex(linkOffset, entry.Index, linkName);
            return target == treeTarget ? null : target.ToString();
        }

        private int ReadLink(int index, int fieldOffset)
        {
            return BitConverter.ToInt32(rawBytes, BCM_File.HeaderSize + index * BCM_File.EntrySize + fieldOffset);
        }

        private bool IsEntryOffset(int offset)
        {
            return offset >= BCM_File.HeaderSize && (offset - BCM_File.HeaderSize) % BCM_File.EntrySize == 0 && GetBcmIndex(offset) < count;
        }

        private int GetEntryIndex(int offset, string entryIndex, string linkName)
        {
            if (!IsEntryOffset(offset))
                throw new InvalidDataException($"BCM entry {entryIndex} has an invalid {linkName} offset 0x{offset:X}.");

            return GetBcmIndex(offset);
        }

        private int GetBcmIndex(int offset)
        {
            return (offset - BCM_File.HeaderSize) / BCM_File.EntrySize;
        }

        private BCM_Entry ParseBcmEntry(int offset, int index)
        {
            return new BCM_Entry()
            {
                Index = index.ToString(),
                I_00 = BitConverter.ToUInt32(rawBytes, offset + 0),
                DirectionalInput = (DirectionalInput)BitConverter.ToUInt32(rawBytes, offset + 4),
                ButtonInput = (ButtonInput)BitConverter.ToUInt32(rawBytes, offset + 8),
                HoldDownConditions = BitConverter.ToUInt32(rawBytes, offset + 12),
                PriorityLayer = BitConverter.ToUInt16(rawBytes, offset + 16),
                I_18 = rawBytes[offset + 18],
                I_19 = rawBytes[offset + 19],
                MinimumLoopDuration = BitConverter.ToUInt16(rawBytes, offset + 20),
                MaximumLoopDuration = BitConverter.ToUInt16(rawBytes, offset + 22),
                PrimaryActivatorConditions = (PrimaryActivatorConditions)BitConverter.ToUInt32(rawBytes, offset + 24),
                ActivatorState = (ActivatorState)BitConverter.ToUInt32(rawBytes, offset + 28),
                BacEntryPrimary = BitConverter.ToInt16(rawBytes, offset + 32),
                BacEntryCharge = BitConverter.ToInt16(rawBytes, offset + 34),
                I_36 = BitConverter.ToInt16(rawBytes, offset + 36),
                BacEntryUserConnect = BitConverter.ToInt16(rawBytes, offset + 38),
                BacEntryVictimConnect = BitConverter.ToInt16(rawBytes, offset + 40),
                BacEntryAirborne = BitConverter.ToInt16(rawBytes, offset + 42),
                BacEntryTargetingOverride = BitConverter.ToUInt16(rawBytes, offset + 44),
                RandomFlag = BitConverter.ToUInt16(rawBytes, offset + 46),
                I_64 = BitConverter.ToUInt32(rawBytes, offset + 64),
                I_68 = BitConverter.ToUInt32(rawBytes, offset + 68),
                I_72 = BitConverter.ToUInt32(rawBytes, offset + 72),
                MoveCategory = BitConverter.ToUInt32(rawBytes, offset + 76),
                I_80 = BitConverter.ToUInt32(rawBytes, offset + 80),
                StaminaCost = BitConverter.ToUInt32(rawBytes, offset + 84),
                I_88 = BitConverter.ToUInt32(rawBytes, offset + 88),
                KiRequired = BitConverter.ToUInt32(rawBytes, offset + 92),
                HealthRequired = BitConverter.ToSingle(rawBytes, offset + 96),
                TransStage = BitConverter.ToInt16(rawBytes, offset + 100),
                CusAura = BitConverter.ToInt16(rawBytes, offset + 102),
                I_104 = BitConverter.ToUInt32(rawBytes, offset + 104),
                I_108 = BitConverter.ToUInt32(rawBytes, offset + 108)
            };
        }
    }
}
