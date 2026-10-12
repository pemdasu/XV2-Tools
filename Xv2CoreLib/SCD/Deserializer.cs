using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using YAXLib;

namespace Xv2CoreLib.SCD
{
    public class Deserializer
    {
        private readonly SCD_File scdFile;
        public List<byte> bytes = new List<byte>();

        //Fixed bytes of the block being written, counted from the block start.
        private byte[] block;

        public Deserializer(string location)
        {
            string saveLocation = string.Format("{0}/{1}", Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location));
            YAXSerializer serializer = new YAXSerializer(typeof(SCD_File), YAXSerializationOptions.DontSerializeNullObjects);
            scdFile = (SCD_File)serializer.Deserialize(XDocument.Load(location).Root);
            Write();
            File.WriteAllBytes(saveLocation, bytes.ToArray());
        }

        public Deserializer(SCD_File scdFile)
        {
            this.scdFile = scdFile;
            Write();
        }

        private void Write()
        {
            if ((scdFile.I_08 & 1) != 0)
                throw new InvalidDataException("SCD: big-endian SCD files are not supported.");

            bytes.AddRange(Encoding.ASCII.GetBytes(SCD_File.Signature));
            bytes.AddRange(BitConverter.GetBytes(scdFile.I_04));
            bytes.Add(scdFile.LayoutVersion);
            bytes.Add(scdFile.I_07);
            bytes.AddRange(BitConverter.GetBytes(scdFile.I_08));
            bytes.AddRange(BitConverter.GetBytes(scdFile.I_12));

            foreach (SCD_Block scdBlock in scdFile.Blocks)
            {
                WriteBlock(scdBlock);
            }

            bytes.AddRange(Encoding.ASCII.GetBytes(SCD_File.EndTag));
            bytes.AddRange(BitConverter.GetBytes(SCD_File.BlockHeaderSize));
        }

        private void WriteBlock(SCD_Block scdBlock)
        {
            string tag = scdBlock.Tag;

            if (tag == null || tag.Length != 4)
                throw new InvalidDataException($"SCD: block tag \"{tag}\" must be 4 characters.");

            CheckContent(scdBlock);

            if (scdBlock.Raw != null)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes(tag));
                bytes.AddRange(BitConverter.GetBytes(SCD_File.BlockHeaderSize + scdBlock.Raw.Length));
                bytes.AddRange(scdBlock.Raw);
                return;
            }

            List<string> names = new List<string>();

            switch (tag)
            {
                case "CFG0":
                    block = new byte[0x0C];
                    SetFloat(0x08, scdBlock.Config0.MaxSpeed);
                    break;
                case "CFG1":
                    block = new byte[0x10];
                    SetFloat(0x08, scdBlock.Config1.MaxSpeed);
                    SetFloat(0x0C, scdBlock.Config1.RootFollow);
                    break;
                case "BDY3":
                    block = new byte[0x9C];
                    WriteBody3(scdBlock.Body3);
                    names.Add(scdBlock.Body3.Bone);
                    break;
                case "BDY4":
                    block = new byte[0xA4];
                    WriteBody4(scdBlock.Body4);
                    names.Add(scdBlock.Body4.Bone);
                    break;
                case "BDY5":
                    block = new byte[0xB0];
                    WriteBody5(scdBlock.Body5);
                    names.Add(scdBlock.Body5.Bone);
                    break;
                case "BDY0":
                case "BDY1":
                    block = new byte[0xB8];
                    WriteBodyV2(scdBlock.BodyV2);
                    names.Add(scdBlock.BodyV2.Bone);
                    break;
                case "CSR1":
                case "CSR2":
                    block = new byte[SCD_Constraint.MaxFixedSize];
                    WriteConstraint(scdBlock.Constraint);
                    block = TrimConstraint(tag, scdBlock.Constraint.FixedSize);
                    names.Add(scdBlock.Constraint.BoneA);
                    names.Add(scdBlock.Constraint.BoneB);
                    break;
                case "ACT0":
                    block = new byte[0x40];
                    WriteSpring(scdBlock.Spring);
                    names.Add(scdBlock.Spring.BoneA);
                    names.Add(scdBlock.Spring.BoneB);
                    break;
            }

            byte[] nameTail = BuildNameTail(tag, names);
            int[] nameOffsets = GetNameOffsets(names, block.Length);

            Array.Copy(Encoding.ASCII.GetBytes(tag), 0, block, 0, 4);
            SetInt(0x04, block.Length + nameTail.Length);

            for (int index = 0; index < nameOffsets.Length; index++)
                SetInt(0x08 + 4 * index, nameOffsets[index]);

            bytes.AddRange(block);
            bytes.AddRange(nameTail);
        }

        /// <summary>
        /// Checks that the block has exactly the content its tag and the layout version need.
        /// </summary>
        private void CheckContent(SCD_Block scdBlock)
        {
            object[] contents = { scdBlock.Config0, scdBlock.Config1, scdBlock.Body3, scdBlock.Body4, scdBlock.Body5, scdBlock.BodyV2, scdBlock.Constraint, scdBlock.Spring, scdBlock.Raw };
            int setCount = 0;

            foreach (object content in contents)
            {
                if (content != null) setCount++;
            }

            if (setCount != 1)
                throw new InvalidDataException($"SCD: block {scdBlock.Tag} must have exactly one content element, but has {setCount}.");

            bool isTyped = Parser.HasTypedLayout(scdBlock.Tag, scdFile.IsVersion1);

            if (scdBlock.Raw != null)
            {
                if (isTyped)
                    throw new InvalidDataException($"SCD: block {scdBlock.Tag} must use its typed element, not Raw.");

                return;
            }

            object expected = null;

            switch (scdBlock.Tag)
            {
                case "CFG0": expected = scdBlock.Config0; break;
                case "CFG1": expected = scdBlock.Config1; break;
                case "BDY3": expected = scdBlock.Body3; break;
                case "BDY4": expected = scdBlock.Body4; break;
                case "BDY5": expected = scdBlock.Body5; break;
                case "BDY0":
                case "BDY1": expected = scdBlock.BodyV2; break;
                case "CSR1":
                case "CSR2": expected = scdBlock.Constraint; break;
                case "ACT0": expected = scdBlock.Spring; break;
            }

            if (!isTyped || expected == null)
                throw new InvalidDataException($"SCD: block {scdBlock.Tag} has content that does not match its tag in layout version {scdFile.LayoutVersion}. Body3, Body4 and Body5 need layout version 1, BodyV2 needs BDY0 or BDY1 with another layout version, and other tags need Raw.");
        }

        private byte[] TrimConstraint(string tag, int fixedSize)
        {
            int minimumSize = SCD_File.BlockHeaderSize + 8;

            if (fixedSize < minimumSize || fixedSize > SCD_Constraint.MaxFixedSize || fixedSize % 4 != 0)
                throw new InvalidDataException($"SCD: {tag} FixedSize {fixedSize} must be a multiple of 4 between {minimumSize} and {SCD_Constraint.MaxFixedSize}.");

            for (int index = fixedSize; index < block.Length; index++)
            {
                if (block[index] != 0)
                    throw new InvalidDataException($"SCD: {tag} has a nonzero value at offset 0x{index & ~3:X}, past its FixedSize {fixedSize}. Raise FixedSize to keep it.");
            }

            return block.GetRange(0, fixedSize);
        }

        private void WriteBody3(SCD_Body3 body)
        {
            WriteBody(body, 0x10, 0x24, 0x28, 0x60, 0x80, 0x84);
            SetFloat(0x68, body.Damping);
            SetFloat(0x6C, body.F_108);
            SetFloat(0x70, body.F_112);
            SetFloat(0x74, body.Friction);
            SetFloat(0x78, body.F_120);
            SetFloat(0x7C, body.F_124);
        }

        private void WriteBody4(SCD_Body4 body)
        {
            WriteBody(body, 0x10, 0x24, 0x2C, 0x64, 0x84, 0x8C);
            SetFloat(0x28, body.ConvexRadius);
            SetFloat(0x6C, body.Damping);
            SetFloat(0x70, body.F_112);
            SetFloat(0x74, body.F_116);
            SetFloat(0x78, body.Friction);
            SetFloat(0x7C, body.F_124);
            SetFloat(0x80, body.F_128);
            SetFloat(0x88, body.IdleDamping);
        }

        private void WriteBody5(SCD_Body5 body)
        {
            WriteBody(body, 0x1C, 0x30, 0x38, 0x70, 0x90, 0x98);
            SetVector(0x10, body.Pivot);
            SetFloat(0x34, body.ConvexRadius);
            SetFloat(0x78, body.Damping);
            SetFloat(0x7C, body.F_124);
            SetFloat(0x80, body.F_128);
            SetFloat(0x84, body.Friction);
            SetFloat(0x88, body.F_136);
            SetFloat(0x8C, body.F_140);
            SetFloat(0x94, body.IdleDamping);
        }

        private void WriteBodyV2(SCD_BodyV2 body)
        {
            WriteBody(body, 0x1C, 0x30, 0x38, 0x74, 0x98, 0xA0);
            SetVector(0x10, body.Pivot);
            SetFloat(0x34, body.ConvexRadius);
            SetInt(0x70, body.I_112);
            SetFloat(0x7C, body.F_124);
            SetFloat(0x80, body.F_128);
            SetFloat(0x84, body.F_132);
            SetFloat(0x88, body.LinearDamping);
            SetFloat(0x8C, body.AngularDamping);
            SetFloat(0x90, body.GravityFactor);
            SetFloat(0x94, body.TimeFactor);
            SetFloat(0x9C, body.IdleDamping);
        }

        //Same offset rules as Parser.ParseBody.
        private void WriteBody(SCD_Body body, int limitAxis, int shapeType, int translation, int collisionFilter, int movementDrag, int centerOfMass)
        {
            SetUInt(0x0C, body.Flags);
            SetVector(limitAxis, body.LimitAxis);
            SetFloat(limitAxis + 12, body.LimitAngle1);
            SetFloat(limitAxis + 16, body.LimitAngle2);
            SetInt(shapeType, (int)body.ShapeType);
            SetVector(translation, body.ShapeTranslation);
            SetVector(translation + 12, body.ShapeRotation);
            SetFloat(translation + 24, body.ShapeDim0);
            SetFloat(translation + 28, body.ShapeDim1);
            SetFloat(translation + 32, body.ShapeDim2);
            SetFloat(translation + 36, body.ShapeDim3);
            SetFloat(translation + 40, body.ShapeDim4);
            SetFloat(translation + 44, body.ShapeDim5);
            SetFloat(translation + 48, body.ShapeDim6);
            SetInt(translation + 52, body.MotionType);
            SetUInt(collisionFilter, body.CollisionFilter);
            SetFloat(collisionFilter + 4, body.Mass);
            SetFloat(movementDrag, body.MovementDrag);
            SetVector(centerOfMass, body.CenterOfMassOffset);
            SetVector(centerOfMass + 12, body.InertiaMultipliers);
        }

        private void WriteConstraint(SCD_Constraint constraint)
        {
            SetInt(0x10, (int)constraint.ConstraintType);
            SetVector(0x14, constraint.PivotA);
            SetVector(0x20, constraint.PivotB);
            SetFloat(0x2C, constraint.F_44);
            SetFloat(0x30, constraint.BreakingThreshold);
            SetVector(0x34, constraint.AxisA);
            SetVector(0x40, constraint.AxisB);
            SetVector(0x4C, constraint.PerpendicularA);
            SetVector(0x58, constraint.PerpendicularB);
            SetFloat(0x64, constraint.Angle1);
            SetFloat(0x68, constraint.Angle2);
            SetFloat(0x6C, constraint.Friction);
            SetFloat(0x70, constraint.Tau);
            SetFloat(0x74, constraint.RagdollAngle1);
            SetFloat(0x78, constraint.RagdollAngle2);
            SetFloat(0x7C, constraint.RagdollAngle3);
            SetInt(0x80, constraint.I_128);
            SetUInt(0x84, constraint.I_132);
            SetUInt(0x88, constraint.I_136);
            SetUInt(0x8C, constraint.I_140);
            SetUInt(0x90, constraint.I_144);
            SetUInt(0x94, constraint.I_148);
            SetUInt(0x98, constraint.I_152);
            SetUInt(0x9C, constraint.I_156);
        }

        private void WriteSpring(SCD_Spring spring)
        {
            SetInt(0x10, spring.I_16);
            SetVector(0x14, spring.PivotA);
            SetVector(0x20, spring.PivotB);
            SetFloat(0x2C, spring.RestLength);
            SetFloat(0x30, spring.Strength);
            SetFloat(0x34, spring.Damping);
            SetUInt(0x38, spring.Flags);
            SetInt(0x3C, spring.I_60);
        }

        private void SetFloat(int offset, float value) => Array.Copy(BitConverter.GetBytes(value), 0, block, offset, 4);

        private void SetInt(int offset, int value) => Array.Copy(BitConverter.GetBytes(value), 0, block, offset, 4);

        private void SetUInt(int offset, uint value) => Array.Copy(BitConverter.GetBytes(value), 0, block, offset, 4);

        private void SetVector(int offset, SCD_Vector3 value)
        {
            SetFloat(offset, value.X);
            SetFloat(offset + 4, value.Y);
            SetFloat(offset + 8, value.Z);
        }

        /// <summary>
        /// Names as one null-terminated ASCII list, padded to 4 bytes.
        /// </summary>
        internal static byte[] BuildNameTail(string tag, List<string> names)
        {
            List<byte> tail = new List<byte>();

            foreach (string name in names)
            {
                if (name == null)
                    throw new InvalidDataException($"SCD: block {tag} is missing a name.");

                foreach (char character in name)
                {
                    if (character == 0 || character > 0x7F)
                        throw new InvalidDataException($"SCD: block {tag} name \"{name}\" must be ASCII with no null characters.");
                }

                tail.AddRange(Encoding.ASCII.GetBytes(name));
                tail.Add(0);
            }

            tail.AddRange(new byte[Utils.CalculatePadding(tail.Count, 4)]);
            return tail.ToArray();
        }

        internal static int[] GetNameOffsets(List<string> names, int nameStart)
        {
            int[] offsets = new int[names.Count];
            int offset = nameStart;

            for (int index = 0; index < names.Count; index++)
            {
                offsets[index] = offset;
                offset += names[index].Length + 1;
            }

            return offsets;
        }
    }
}
