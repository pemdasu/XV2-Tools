using System;
using System.Collections.Generic;
using System.IO;

namespace Xv2CoreLib.Iggy
{
    public sealed class IggyIndexStream
    {
        public byte[] RawData { get; private set; }
        public IReadOnlyList<IggyIndexTable> Tables { get; private set; }
        public IReadOnlyList<IggyIndexField> Fields { get; private set; }
        public int LogicalSize { get; private set; }

        public static IggyIndexStream Read(byte[] bytes, int mainSize)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (mainSize < 0) throw new ArgumentOutOfRangeException(nameof(mainSize));
            if (bytes.Length == 0) throw new InvalidDataException("Iggy index stream is empty.");

            int cursor = 0;
            int tableCount = bytes[cursor++];
            List<IggyIndexTable> tables = new List<IggyIndexTable>(tableCount);
            for (int tableIndex = 0; tableIndex < tableCount; tableIndex++)
            {
                Require(bytes, cursor, 2, "Iggy index table header");
                int stride = bytes[cursor++];
                int fieldCount = bytes[cursor++];
                Require(bytes, cursor, checked(fieldCount * 2), "Iggy index table fields");

                List<IggyIndexFieldDefinition> definitions = new List<IggyIndexFieldDefinition>(fieldCount);
                for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                {
                    definitions.Add(new IggyIndexFieldDefinition
                    {
                        Offset = bytes[cursor++],
                        Kind = bytes[cursor++]
                    });
                }

                tables.Add(new IggyIndexTable
                {
                    Index = tableIndex,
                    Stride = stride,
                    Fields = definitions
                });
            }

            List<IggyIndexField> fields = new List<IggyIndexField>();
            int logical = 0;
            while (cursor < bytes.Length)
            {
                int opcodeOffset = cursor;
                byte opcode = bytes[cursor++];
                if (opcode < 0x80)
                {
                    IggyIndexTable table = GetTable(tables, opcode, opcodeOffset);
                    AddTableFields(fields, table, logical);
                    logical = checked(logical + table.Stride);
                }
                else if (opcode < 0xc0)
                {
                    Require(bytes, cursor, 1, "Iggy index repeat");
                    int tableIndex = bytes[cursor++];
                    IggyIndexTable table = GetTable(tables, tableIndex, opcodeOffset);
                    int repeatCount = opcode - 0x7f;
                    for (int repeat = 0; repeat < repeatCount; repeat++)
                    {
                        AddTableFields(fields, table, logical);
                        logical = checked(logical + table.Stride);
                    }
                }
                else if (opcode < 0xd0)
                {
                    logical = checked(logical + (opcode * 2) - 0x17e);
                }
                else if (opcode < 0xe0)
                {
                    Require(bytes, cursor, 1, "Iggy typed skip");
                    int encodedCount = bytes[cursor++];
                    int typeCode = opcode & 0x0f;
                    int unit = GetTypedSkipUnit(typeCode);
                    logical = checked(logical + (unit * (encodedCount + 1)));
                }
                else if (opcode == 0xfc)
                {
                    Require(bytes, cursor, 1, "Iggy 0xfc instruction");
                    cursor++;
                }
                else if (opcode == 0xfd)
                {
                    Require(bytes, cursor, 2, "Iggy field instruction");
                    int amount = bytes[cursor++];
                    int fieldCount = bytes[cursor++];
                    Require(bytes, cursor, checked(fieldCount * 2), "Iggy field instruction fields");
                    for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++)
                    {
                        fields.Add(new IggyIndexField
                        {
                            Offset = checked(logical + bytes[cursor++]),
                            Kind = bytes[cursor++],
                            OpcodeOffset = opcodeOffset
                        });
                    }
                    logical = checked(logical + amount);
                }
                else if (opcode == 0xfe)
                {
                    Require(bytes, cursor, 1, "Iggy short skip");
                    logical = checked(logical + bytes[cursor++] + 1);
                }
                else if (opcode == 0xff)
                {
                    Require(bytes, cursor, 4, "Iggy long skip");
                    uint skip = BitConverter.ToUInt32(bytes, cursor);
                    logical = checked(logical + checked((int)skip));
                    cursor += 4;
                }
                else
                {
                    throw new InvalidDataException($"Unknown Iggy index opcode 0x{opcode:X2} at 0x{opcodeOffset:X}.");
                }

                if (logical < 0 || logical > mainSize)
                    throw new InvalidDataException($"Iggy index stream advances to 0x{logical:X}, outside the main subfile.");
            }

            if (logical != mainSize)
                throw new InvalidDataException($"Iggy index stream ends at 0x{logical:X}, expected 0x{mainSize:X}.");

            List<IggyIndexField> uniqueFields = new List<IggyIndexField>();
            HashSet<string> seen = new HashSet<string>();
            foreach (IggyIndexField field in fields)
            {
                string key = field.Offset + ":" + field.Kind;
                if (seen.Add(key))
                    uniqueFields.Add(field);
            }

            return new IggyIndexStream
            {
                RawData = (byte[])bytes.Clone(),
                Tables = tables,
                Fields = uniqueFields,
                LogicalSize = logical
            };
        }

        private static void AddTableFields(List<IggyIndexField> fields, IggyIndexTable table, int logical)
        {
            foreach (IggyIndexFieldDefinition definition in table.Fields)
            {
                fields.Add(new IggyIndexField
                {
                    Offset = checked(logical + definition.Offset),
                    Kind = definition.Kind,
                    TableIndex = table.Index
                });
            }
        }

        private static IggyIndexTable GetTable(List<IggyIndexTable> tables, int tableIndex, int opcodeOffset)
        {
            if (tableIndex < 0 || tableIndex >= tables.Count)
                throw new InvalidDataException($"Iggy index opcode at 0x{opcodeOffset:X} references table {tableIndex}, but only {tables.Count} tables exist.");
            return tables[tableIndex];
        }

        private static int GetTypedSkipUnit(int typeCode)
        {
            if (typeCode <= 2) return 8;
            if (typeCode <= 4) return 2;
            if (typeCode == 5) return 4;
            return 8;
        }

        private static void Require(byte[] bytes, int offset, int length, string name)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
                throw new InvalidDataException($"{name} is truncated.");
        }
    }

    public sealed class IggyIndexTable
    {
        public int Index { get; internal set; }
        public int Stride { get; internal set; }
        public IReadOnlyList<IggyIndexFieldDefinition> Fields { get; internal set; }
    }

    public sealed class IggyIndexFieldDefinition
    {
        public int Offset { get; internal set; }
        public byte Kind { get; internal set; }
    }

    public sealed class IggyIndexField
    {
        public int Offset { get; internal set; }
        public byte Kind { get; internal set; }
        public int TableIndex { get; internal set; } = -1;
        public int OpcodeOffset { get; internal set; } = -1;

        public bool IsSelfRelativePointer
        {
            get { return Kind == 2; }
        }
    }
}
