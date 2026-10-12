using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YAXLib;

namespace Xv2CoreLib.SCD
{
    public class Parser
    {
        private readonly byte[] rawBytes;
        private readonly SCD_File scdFile = new SCD_File();

        //Fixed bytes of the block being read, counted from the block start.
        private byte[] block;

        public Parser(string location, bool writeXml)
        {
            rawBytes = File.ReadAllBytes(location);
            ParseScd();

            if (writeXml)
            {
                YAXSerializer serializer = new YAXSerializer(typeof(SCD_File), YAXSerializationOptions.DontSerializeNullObjects);
                serializer.SerializeToFile(scdFile, location + ".xml");
            }
        }

        public Parser(byte[] bytes)
        {
            rawBytes = bytes;
            ParseScd();
        }

        public SCD_File GetScdFile()
        {
            return scdFile;
        }

        private void ParseScd()
        {
            if (rawBytes.Length < SCD_File.HeaderSize || Encoding.ASCII.GetString(rawBytes, 0, 4) != SCD_File.Signature)
                throw new InvalidDataException("SCD: file does not start with the \"#SCD\" signature.");

            scdFile.I_04 = BitConverter.ToUInt16(rawBytes, 4);
            scdFile.LayoutVersion = rawBytes[6];
            scdFile.I_07 = rawBytes[7];
            scdFile.I_08 = BitConverter.ToUInt32(rawBytes, 8);
            scdFile.I_12 = BitConverter.ToUInt32(rawBytes, 12);

            if ((scdFile.I_08 & 1) != 0)
                throw new InvalidDataException("SCD: big-endian SCD files are not supported.");

            int position = SCD_File.HeaderSize;

            while (true)
            {
                if (position + SCD_File.BlockHeaderSize > rawBytes.Length)
                    throw new InvalidDataException($"SCD: block header at 0x{position:X} goes past the end of the file. The file has no END0 block.");

                string tag = ReadTag(position);
                int size = BitConverter.ToInt32(rawBytes, position + 4);

                if (size < SCD_File.BlockHeaderSize || size > rawBytes.Length - position)
                    throw new InvalidDataException($"SCD: block {tag} at 0x{position:X} has size {size}, which is not between {SCD_File.BlockHeaderSize} and the bytes left in the file.");

                if (tag == SCD_File.EndTag)
                {
                    if (size != SCD_File.BlockHeaderSize || position + size != rawBytes.Length)
                        throw new InvalidDataException($"SCD: END0 block at 0x{position:X} must have size {SCD_File.BlockHeaderSize} and end the file.");

                    return;
                }

                scdFile.Blocks.Add(ParseBlock(position, tag, size));
                position += size;
            }
        }

        private SCD_Block ParseBlock(int blockStart, string tag, int size)
        {
            SCD_Block scdBlock = new SCD_Block() { Tag = tag };
            if (!HasTypedLayout(tag, scdFile.IsVersion1))
            {
                scdBlock.Raw = rawBytes.GetRange(blockStart + SCD_File.BlockHeaderSize, size - SCD_File.BlockHeaderSize);
                return scdBlock;
            }

            List<string> names = ReadNames(blockStart, tag, size, GetNameCount(tag), out int fixedSize);

            switch (tag)
            {
                case "CFG0":
                    LoadFixed(blockStart, tag, fixedSize, 0x0C);
                    scdBlock.Config0 = new SCD_Config0() { MaxSpeed = F(0x08) };
                    break;
                case "CFG1":
                    LoadFixed(blockStart, tag, fixedSize, 0x10);
                    scdBlock.Config1 = new SCD_Config1() { MaxSpeed = F(0x08), RootFollow = F(0x0C) };
                    break;
                case "BDY3":
                    LoadFixed(blockStart, tag, fixedSize, 0x9C);
                    scdBlock.Body3 = ParseBody3();
                    break;
                case "BDY4":
                    LoadFixed(blockStart, tag, fixedSize, 0xA4);
                    scdBlock.Body4 = ParseBody4();
                    break;
                case "BDY5":
                    LoadFixed(blockStart, tag, fixedSize, 0xB0);
                    scdBlock.Body5 = ParseBody5();
                    break;
                case "BDY0":
                case "BDY1":
                    LoadFixed(blockStart, tag, fixedSize, 0xB8);
                    scdBlock.BodyV2 = ParseBodyV2();
                    break;
                case "CSR1":
                case "CSR2":
                    if (fixedSize > SCD_Constraint.MaxFixedSize)
                        throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} has {fixedSize} fixed bytes. At most {SCD_Constraint.MaxFixedSize} are supported.");

                    block = new byte[SCD_Constraint.MaxFixedSize];
                    Array.Copy(rawBytes, blockStart, block, 0, fixedSize);
                    scdBlock.Constraint = ParseConstraint();
                    scdBlock.Constraint.BoneA = names[0];
                    scdBlock.Constraint.BoneB = names[1];
                    scdBlock.Constraint.FixedSize = fixedSize;
                    break;
                case "ACT0":
                    LoadFixed(blockStart, tag, fixedSize, 0x40);
                    scdBlock.Spring = ParseSpring();
                    scdBlock.Spring.BoneA = names[0];
                    scdBlock.Spring.BoneB = names[1];
                    break;
            }

            if (scdBlock.Body != null)
                scdBlock.Body.Bone = names[0];

            return scdBlock;
        }

        /// <summary>
        /// True for tags with a typed model in this layout version. Other blocks are kept as raw bytes.
        /// </summary>
        internal static bool HasTypedLayout(string tag, bool isVersion1)
        {
            switch (tag)
            {
                case "CFG0":
                case "CFG1":
                case "CSR1":
                case "CSR2":
                case "ACT0":
                    return true;
                case "BDY3":
                case "BDY4":
                case "BDY5":
                    return isVersion1;
                case "BDY0":
                case "BDY1":
                    return !isVersion1;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Number of name offset fields at block offset 0x08 in a typed block.
        /// </summary>
        internal static int GetNameCount(string tag)
        {
            switch (tag)
            {
                case "CSR1":
                case "CSR2":
                case "ACT0":
                    return 2;
                case "CFG0":
                case "CFG1":
                    return 0;
                default:
                    return 1;
            }
        }

        private void LoadFixed(int blockStart, string tag, int fixedSize, int expectedSize)
        {
            if (fixedSize != expectedSize)
                throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} has {fixedSize} fixed bytes, expected {expectedSize}.");

            block = rawBytes.GetRange(blockStart, fixedSize);
        }

        private SCD_Body3 ParseBody3()
        {
            SCD_Body3 body = new SCD_Body3()
            {
                Damping = F(0x68),
                F_108 = F(0x6C),
                F_112 = F(0x70),
                Friction = F(0x74),
                F_120 = F(0x78),
                F_124 = F(0x7C)
            };

            ParseBody(body, 0x10, 0x24, 0x28, 0x60, 0x80, 0x84);
            return body;
        }

        private SCD_Body4 ParseBody4()
        {
            SCD_Body4 body = new SCD_Body4()
            {
                ConvexRadius = F(0x28),
                Damping = F(0x6C),
                F_112 = F(0x70),
                F_116 = F(0x74),
                Friction = F(0x78),
                F_124 = F(0x7C),
                F_128 = F(0x80),
                IdleDamping = F(0x88)
            };

            ParseBody(body, 0x10, 0x24, 0x2C, 0x64, 0x84, 0x8C);
            return body;
        }

        private SCD_Body5 ParseBody5()
        {
            SCD_Body5 body = new SCD_Body5()
            {
                Pivot = V(0x10),
                ConvexRadius = F(0x34),
                Damping = F(0x78),
                F_124 = F(0x7C),
                F_128 = F(0x80),
                Friction = F(0x84),
                F_136 = F(0x88),
                F_140 = F(0x8C),
                IdleDamping = F(0x94)
            };

            ParseBody(body, 0x1C, 0x30, 0x38, 0x70, 0x90, 0x98);
            return body;
        }

        private SCD_BodyV2 ParseBodyV2()
        {
            SCD_BodyV2 body = new SCD_BodyV2()
            {
                Pivot = V(0x10),
                ConvexRadius = F(0x34),
                I_112 = I(0x70),
                F_124 = F(0x7C),
                F_128 = F(0x80),
                F_132 = F(0x84),
                LinearDamping = F(0x88),
                AngularDamping = F(0x8C),
                GravityFactor = F(0x90),
                TimeFactor = F(0x94),
                IdleDamping = F(0x9C)
            };

            ParseBody(body, 0x1C, 0x30, 0x38, 0x74, 0x98, 0xA0);
            return body;
        }

        //Rotation, dims and motion type follow the translation, the angles follow the limit axis, mass follows the collision filter and the inertia multipliers follow the center of mass offset in every layout.
        private void ParseBody(SCD_Body body, int limitAxis, int shapeType, int translation, int collisionFilter, int movementDrag, int centerOfMass)
        {
            body.Flags = U(0x0C);
            body.LimitAxis = V(limitAxis);
            body.LimitAngle1 = F(limitAxis + 12);
            body.LimitAngle2 = F(limitAxis + 16);
            body.ShapeType = (SCD_ShapeType)I(shapeType);
            body.ShapeTranslation = V(translation);
            body.ShapeRotation = V(translation + 12);
            body.ShapeDim0 = F(translation + 24);
            body.ShapeDim1 = F(translation + 28);
            body.ShapeDim2 = F(translation + 32);
            body.ShapeDim3 = F(translation + 36);
            body.ShapeDim4 = F(translation + 40);
            body.ShapeDim5 = F(translation + 44);
            body.ShapeDim6 = F(translation + 48);
            body.MotionType = I(translation + 52);
            body.CollisionFilter = U(collisionFilter);
            body.Mass = F(collisionFilter + 4);
            body.MovementDrag = F(movementDrag);
            body.CenterOfMassOffset = V(centerOfMass);
            body.InertiaMultipliers = V(centerOfMass + 12);
        }

        private SCD_Constraint ParseConstraint()
        {
            return new SCD_Constraint()
            {
                ConstraintType = (SCD_ConstraintType)I(0x10),
                PivotA = V(0x14),
                PivotB = V(0x20),
                F_44 = F(0x2C),
                BreakingThreshold = F(0x30),
                AxisA = V(0x34),
                AxisB = V(0x40),
                PerpendicularA = V(0x4C),
                PerpendicularB = V(0x58),
                Angle1 = F(0x64),
                Angle2 = F(0x68),
                Friction = F(0x6C),
                Tau = F(0x70),
                RagdollAngle1 = F(0x74),
                RagdollAngle2 = F(0x78),
                RagdollAngle3 = F(0x7C),
                I_128 = I(0x80),
                I_132 = U(0x84),
                I_136 = U(0x88),
                I_140 = U(0x8C),
                I_144 = U(0x90),
                I_148 = U(0x94),
                I_152 = U(0x98),
                I_156 = U(0x9C)
            };
        }

        private SCD_Spring ParseSpring()
        {
            return new SCD_Spring()
            {
                I_16 = I(0x10),
                PivotA = V(0x14),
                PivotB = V(0x20),
                RestLength = F(0x2C),
                Strength = F(0x30),
                Damping = F(0x34),
                Flags = U(0x38),
                I_60 = I(0x3C)
            };
        }

        private float F(int offset) => BitConverter.ToSingle(block, offset);

        private int I(int offset) => BitConverter.ToInt32(block, offset);

        private uint U(int offset) => BitConverter.ToUInt32(block, offset);

        private SCD_Vector3 V(int offset) => new SCD_Vector3() { X = F(offset), Y = F(offset + 4), Z = F(offset + 8) };

        private string ReadTag(int position)
        {
            for (int index = 0; index < 4; index++)
            {
                byte value = rawBytes[position + index];

                if (value < 0x21 || value > 0x7E)
                    throw new InvalidDataException($"SCD: block at 0x{position:X} does not start with a 4-character ASCII tag.");
            }

            return Encoding.ASCII.GetString(rawBytes, position, 4);
        }

        /// <summary>
        /// Reads the name offsets at block offset 0x08 and the names they point to. The fixed size is the offset of the first name.
        /// </summary>
        private List<string> ReadNames(int blockStart, string tag, int size, int nameCount, out int fixedSize)
        {
            List<string> names = new List<string>();
            int namesStart = SCD_File.BlockHeaderSize + 4 * nameCount;
            fixedSize = size;

            if (nameCount == 0)
                return names;

            if (size < namesStart)
                throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} is too small for its {nameCount} name offset(s).");

            int[] nameOffsets = new int[nameCount];

            for (int index = 0; index < nameCount; index++)
            {
                int nameOffset = BitConverter.ToInt32(rawBytes, blockStart + SCD_File.BlockHeaderSize + 4 * index);

                if (nameOffset < namesStart || nameOffset >= size)
                    throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} has name offset 0x{nameOffset:X} outside the block.");

                int nameEnd = Array.IndexOf(rawBytes, (byte)0, blockStart + nameOffset, size - nameOffset);

                if (nameEnd < 0)
                    throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} has a name at 0x{nameOffset:X} with no null terminator inside the block.");

                nameOffsets[index] = nameOffset;
                names.Add(Encoding.ASCII.GetString(rawBytes, blockStart + nameOffset, nameEnd - blockStart - nameOffset));
            }

            fixedSize = nameOffsets[0];

            //The writer rebuilds the names as one null-terminated list padded to 4 bytes. Reject any other layout so a write cannot silently change the file.
            byte[] nameTail = Deserializer.BuildNameTail(tag, names);

            if (fixedSize + nameTail.Length != size || !new ArraySegment<byte>(rawBytes, blockStart + fixedSize, nameTail.Length).SequenceEqual(nameTail) || !nameOffsets.SequenceEqual(Deserializer.GetNameOffsets(names, fixedSize)))
                throw new InvalidDataException($"SCD: block {tag} at 0x{blockStart:X} has names that are not a packed list at the end of the block.");

            return names;
        }
    }
}
