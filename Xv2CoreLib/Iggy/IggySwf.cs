using System;
using System.IO;

namespace Xv2CoreLib.Iggy
{
    public sealed class IggySwf
    {
        public const int FlashHeaderSize64 = 0xB8;

        public int MainSectionOffset { get; private set; }
        public int As3NamesSectionOffset { get; private set; }
        public int As3CodeSectionOffset { get; private set; }
        public int NamesSectionOffset { get; private set; }
        public int LastSectionOffset { get; private set; }

        public byte[] MainSection { get; private set; }
        public byte[] As3NamesSection { get; private set; }
        public byte[] As3CodeSection { get; private set; }
        public byte[] NamesSection { get; private set; }
        public byte[] LastSection { get; private set; }
        public byte[] Abc { get; private set; }

        public bool HasAbc
        {
            get { return Abc != null; }
        }

        internal static IggySwf Read(byte[] data)
        {
            IggyFile.RequireRange(data, 0, FlashHeaderSize64, "Iggy Flash header");

            ulong mainOffset = BitConverter.ToUInt64(data, 0);
            ulong as3SectionOffset = BitConverter.ToUInt64(data, 8);
            ulong namesOffset = BitConverter.ToUInt64(data, 0x70);
            ulong lastSectionOffset = BitConverter.ToUInt64(data, 0x88);
            ulong as3CodeOffset = BitConverter.ToUInt64(data, 0x98);

            if (mainOffset < FlashHeaderSize64 || mainOffset > (ulong)data.Length)
                throw new InvalidDataException("Iggy Flash header has an invalid main offset.");

            int mainEnd = ToOffset(as3SectionOffset, data.Length, "AS3 section");
            if (mainEnd < FlashHeaderSize64)
                throw new InvalidDataException("Iggy Flash header has an invalid AS3 section offset.");

            int as3CodeEnd = as3CodeOffset == 1
                ? -1
                : ToRelativeOffset(as3CodeOffset, 0x98, data.Length, "AS3 code");
            int namesEnd = namesOffset == 1
                ? -1
                : ToRelativeOffset(namesOffset, 0x70, data.Length, "names");
            int lastEnd = lastSectionOffset == 1
                ? -1
                : ToRelativeOffset(lastSectionOffset, 0x88, data.Length, "last section");

            int current = 0;
            IggySwf result = new IggySwf
            {
                MainSectionOffset = mainEnd,
                As3CodeSectionOffset = -1,
                NamesSectionOffset = -1,
                LastSectionOffset = -1
            };

            result.MainSection = IggyFile.CopyRange(data, current, mainEnd - current);
            current = mainEnd;

            int next;
            if (as3CodeOffset != 1)
                next = as3CodeEnd;
            else if (namesOffset != 1)
                next = namesEnd;
            else if (lastSectionOffset != 1)
                next = lastEnd;
            else
                next = data.Length;
            ValidateSectionBounds(current, next, data.Length, "AS3 names");
            result.As3NamesSectionOffset = current;
            result.As3NamesSection = IggyFile.CopyRange(data, current, next - current);
            current = next;

            if (as3CodeOffset != 1)
            {
                if (namesOffset != 1)
                    next = namesEnd;
                else if (lastSectionOffset != 1)
                    next = lastEnd;
                else
                    next = data.Length;
                ValidateSectionBounds(current, next, data.Length, "AS3 code");
                result.As3CodeSectionOffset = current;
                result.As3CodeSection = IggyFile.CopyRange(data, current, next - current);
                result.Abc = ReadAbc(result.As3CodeSection);
                current = next;
            }
            else
            {
                result.As3CodeSection = new byte[0];
            }

            if (namesOffset != 1)
            {
                next = lastSectionOffset != 1 ? lastEnd : data.Length;
                ValidateSectionBounds(current, next, data.Length, "names");
                result.NamesSectionOffset = current;
                result.NamesSection = IggyFile.CopyRange(data, current, next - current);
                current = next;
            }
            else
            {
                result.NamesSection = new byte[0];
            }

            if (lastSectionOffset != 1)
            {
                next = data.Length;
                ValidateSectionBounds(current, next, data.Length, "last section");
                result.LastSectionOffset = current;
                result.LastSection = IggyFile.CopyRange(data, current, next - current);
                current = next;
            }
            else
            {
                result.LastSection = new byte[0];
            }

            if (current != data.Length)
                throw new InvalidDataException("Iggy Flash sections do not cover the main subfile.");

            return result;
        }

        private static byte[] ReadAbc(byte[] section)
        {
            if (section.Length == 0)
                return null;
            if (section.Length < 0x10)
                throw new InvalidDataException("Iggy AS3 code section is too small.");

            uint abcSize = BitConverter.ToUInt32(section, 8);
            if (abcSize > section.Length - 0x0C)
                throw new InvalidDataException("Iggy AS3 code section has an invalid ABC size.");

            return IggyFile.CopyRange(section, 0x0C, abcSize);
        }

        private static int ToOffset(ulong value, int length, string name)
        {
            if (value > int.MaxValue || value > (ulong)length)
                throw new InvalidDataException($"Iggy {name} offset is outside the main subfile.");
            return (int)value;
        }

        private static int ToRelativeOffset(ulong value, int fieldOffset, int length, string name)
        {
            if (value > int.MaxValue || value > (ulong)(length - fieldOffset))
                throw new InvalidDataException($"Iggy {name} offset is outside the main subfile.");
            return checked(fieldOffset + (int)value);
        }

        private static void ValidateSectionBounds(int start, int end, int length, string name)
        {
            if (start < 0 || end < start || end > length)
                throw new InvalidDataException($"Iggy {name} section has invalid bounds.");
        }
    }

    internal static class IggySwfWriter
    {
        private const byte SwfVersion = 9;
        private const ushort FileAttributesTag = 69;
        private const ushort DoAbcTag = 82;

        public static byte[] Build(byte[] abc, bool gfx)
        {
            if (abc == null)
                throw new ArgumentNullException(nameof(abc));

            long abcTagLength = checked(5L + abc.Length);
            if (abcTagLength > uint.MaxValue)
                throw new InvalidDataException("The ActionScript blob is too large for a SWF tag.");

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(gfx ? (byte)'G' : (byte)'F');
                writer.Write(gfx ? (byte)'F' : (byte)'W');
                writer.Write(gfx ? (byte)'X' : (byte)'S');
                writer.Write(SwfVersion);
                writer.Write(0U);

                WriteRect(writer);
                writer.Write((ushort)(30 << 8));
                writer.Write((ushort)0);

                WriteTag(writer, FileAttributesTag, new byte[] { 0x08, 0x00, 0x00, 0x00 });

                using (MemoryStream abcPayload = new MemoryStream())
                using (BinaryWriter abcWriter = new BinaryWriter(abcPayload))
                {
                    abcWriter.Write(1U);
                    abcWriter.Write((byte)0);
                    abcWriter.Write(abc);
                    WriteTag(writer, DoAbcTag, abcPayload.ToArray());
                }

                WriteTag(writer, 0, new byte[0]);

                if (stream.Length > uint.MaxValue)
                    throw new InvalidDataException("The generated SWF is too large.");

                writer.Flush();
                stream.Position = 4;
                writer.Write((uint)stream.Length);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteRect(BinaryWriter writer)
        {
            BitWriter bits = new BitWriter();
            bits.WriteUnsigned(16, 5);
            bits.WriteSigned(0, 16);
            bits.WriteSigned(1280 * 20, 16);
            bits.WriteSigned(0, 16);
            bits.WriteSigned(720 * 20, 16);
            writer.Write(bits.ToArray());
        }

        private static void WriteTag(BinaryWriter writer, ushort code, byte[] payload)
        {
            if (payload.Length >= 0x3F)
            {
                writer.Write((ushort)((code << 6) | 0x3F));
                writer.Write((uint)payload.Length);
            }
            else
            {
                writer.Write((ushort)((code << 6) | payload.Length));
            }

            writer.Write(payload);
        }

        private sealed class BitWriter
        {
            private readonly MemoryStream stream = new MemoryStream();
            private int bitCount;

            public void WriteUnsigned(long value, int count)
            {
                for (int bit = count - 1; bit >= 0; bit--)
                {
                    int bitValue = (int)((value >> bit) & 1L);
                    if ((bitCount & 7) == 0)
                        stream.WriteByte(0);

                    if (bitValue != 0)
                    {
                        int position = checked((int)stream.Position - 1);
                        byte current = stream.GetBuffer()[position];
                        stream.GetBuffer()[position] = (byte)(current | (1 << (7 - (bitCount & 7))));
                    }

                    bitCount++;
                }
            }

            public void WriteSigned(int value, int count)
            {
                long mask = (1L << count) - 1L;
                WriteUnsigned(value & mask, count);
            }

            public byte[] ToArray()
            {
                return stream.ToArray();
            }
        }
    }
}
