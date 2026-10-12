using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YAXLib;

namespace Xv2CoreLib.SCD
{
    public enum SCD_ShapeType
    {
        Box = 1,
        Sphere = 2,
        ConvexHull = 3,
        Capsule = 4
    }

    public enum SCD_ConstraintType
    {
        BallAndSocket = 0,
        StiffSpring = 1,
        Hinge = 2,
        LimitedHinge = 3,
        Ragdoll = 6
    }

    [YAXSerializeAs("SCD")]
    public class SCD_File
    {
        public const string Signature = "#SCD";
        public const string EndTag = "END0";
        internal const int HeaderSize = 0x10;
        internal const int BlockHeaderSize = 8;

        [YAXAttributeForClass]
        [YAXHexValue]
        public ushort I_04 { get; set; }
        /// <summary>
        /// 1 selects the BDY3, BDY4 and BDY5 body layouts. Other values select the BDY0 and BDY1 layout.
        /// </summary>
        [YAXAttributeForClass]
        public byte LayoutVersion { get; set; }
        [YAXAttributeForClass]
        [YAXHexValue]
        public byte I_07 { get; set; }
        /// <summary>
        /// Bit 0 marks a big-endian file. Only little-endian files are supported.
        /// </summary>
        [YAXAttributeForClass]
        [YAXHexValue]
        public uint I_08 { get; set; }
        [YAXAttributeForClass]
        [YAXHexValue]
        public uint I_12 { get; set; }

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Block")]
        public List<SCD_Block> Blocks { get; set; } = new List<SCD_Block>();

        [YAXDontSerialize]
        public bool IsVersion1 => LayoutVersion == 1;

        public static SCD_File Load(string path)
        {
            return new Parser(path, false).GetScdFile();
        }

        public static SCD_File Load(byte[] bytes)
        {
            return new Parser(bytes).GetScdFile();
        }

        public void SaveBinary(string path)
        {
            File.WriteAllBytes(path, Write());
        }

        public byte[] Write()
        {
            return new Deserializer(this).bytes.ToArray();
        }
    }

    /// <summary>
    /// One block of the file. Exactly one content property is set. Tags and layouts with no typed model use <see cref="Raw"/>.
    /// </summary>
    [YAXSerializeAs("Block")]
    public class SCD_Block
    {
        [YAXAttributeForClass]
        public string Tag { get; set; }

        [YAXDontSerializeIfNull]
        public SCD_Config0 Config0 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Config1 Config1 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Body3 Body3 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Body4 Body4 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Body5 Body5 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_BodyV2 BodyV2 { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Constraint Constraint { get; set; }
        [YAXDontSerializeIfNull]
        public SCD_Spring Spring { get; set; }
        /// <summary>
        /// Whole block body after the 8-byte block header, for blocks with no typed model.
        /// </summary>
        [YAXDontSerializeIfNull]
        [YAXCollection(YAXCollectionSerializationTypes.Serially, SeparateBy = ",")]
        public byte[] Raw { get; set; }

        [YAXDontSerialize]
        public SCD_Body Body => (SCD_Body)Body3 ?? (SCD_Body)Body4 ?? (SCD_Body)Body5 ?? BodyV2;
    }

    public class SCD_Vector3
    {
        [YAXDontSerialize]
        public float X { get; set; }
        [YAXDontSerialize]
        public float Y { get; set; }
        [YAXDontSerialize]
        public float Z { get; set; }

        //Text forms keep the sign of negative zero, which float formatting and parsing on .NET Framework drop. Shipped axes and pivots use negative zero.
        [YAXAttributeForClass]
        [YAXSerializeAs("X")]
        public string XText { get => FormatFloat(X); set => X = ParseFloat(value); }
        [YAXAttributeForClass]
        [YAXSerializeAs("Y")]
        public string YText { get => FormatFloat(Y); set => Y = ParseFloat(value); }
        [YAXAttributeForClass]
        [YAXSerializeAs("Z")]
        public string ZText { get => FormatFloat(Z); set => Z = ParseFloat(value); }

        private static string FormatFloat(float value)
        {
            if (value == 0f && BitConverter.GetBytes(value)[3] == 0x80)
                return "-0";

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static float ParseFloat(string text)
        {
            float value = float.Parse(text, CultureInfo.InvariantCulture);
            return value == 0f && text.TrimStart().StartsWith("-") ? -0f : value;
        }
    }

    public class SCD_Config0
    {
        [YAXAttributeFor("MaxSpeed")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float MaxSpeed { get; set; }
    }

    public class SCD_Config1
    {
        [YAXAttributeFor("MaxSpeed")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float MaxSpeed { get; set; }
        [YAXAttributeFor("RootFollow")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float RootFollow { get; set; }
    }

    /// <summary>
    /// Fields shared by every body layout. Offsets differ per layout.
    /// </summary>
    public abstract class SCD_Body
    {
        [YAXAttributeForClass]
        public string Bone { get; set; }

        /// <summary>
        /// Bits 0-1: limit mode (1 limits the twist around <see cref="LimitAxis"/>). Bit 2: keeps the bone at its animated offset plus the pivot.
        /// </summary>
        [YAXElementOrder(-40)]
        [YAXAttributeFor("Flags")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint Flags { get; set; }
        [YAXElementOrder(-39)]
        public SCD_Vector3 LimitAxis { get; set; } = new SCD_Vector3();
        /// <summary>
        /// Twist limit in degrees. The two values can be in either order.
        /// </summary>
        [YAXElementOrder(-38)]
        [YAXAttributeFor("LimitAngles")]
        [YAXSerializeAs("First")]
        [YAXFormat("R")]
        public float LimitAngle1 { get; set; }
        [YAXElementOrder(-37)]
        [YAXAttributeFor("LimitAngles")]
        [YAXSerializeAs("Second")]
        [YAXFormat("R")]
        public float LimitAngle2 { get; set; }
        [YAXElementOrder(-36)]
        [YAXAttributeFor("ShapeType")]
        [YAXSerializeAs("value")]
        public SCD_ShapeType ShapeType { get; set; }
        [YAXElementOrder(-35)]
        public SCD_Vector3 ShapeTranslation { get; set; } = new SCD_Vector3();
        /// <summary>
        /// Shape rotation in degrees.
        /// </summary>
        [YAXElementOrder(-34)]
        public SCD_Vector3 ShapeRotation { get; set; } = new SCD_Vector3();
        [YAXElementOrder(-33)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D0")]
        [YAXFormat("R")]
        public float ShapeDim0 { get; set; }
        [YAXElementOrder(-32)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D1")]
        [YAXFormat("R")]
        public float ShapeDim1 { get; set; }
        [YAXElementOrder(-31)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D2")]
        [YAXFormat("R")]
        public float ShapeDim2 { get; set; }
        [YAXElementOrder(-30)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D3")]
        [YAXFormat("R")]
        public float ShapeDim3 { get; set; }
        [YAXElementOrder(-29)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D4")]
        [YAXFormat("R")]
        public float ShapeDim4 { get; set; }
        [YAXElementOrder(-28)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D5")]
        [YAXFormat("R")]
        public float ShapeDim5 { get; set; }
        [YAXElementOrder(-27)]
        [YAXAttributeFor("ShapeDims")]
        [YAXSerializeAs("D6")]
        [YAXFormat("R")]
        public float ShapeDim6 { get; set; }
        /// <summary>
        /// Layout version 1 uses 4 (keyframed) and 5 (fixed), other values are dynamic. Version 2 uses 0 (fixed), 1 (keyframed) and 2 (dynamic).
        /// </summary>
        [YAXElementOrder(-26)]
        [YAXAttributeFor("MotionType")]
        [YAXSerializeAs("value")]
        public int MotionType { get; set; }
        [YAXElementOrder(-25)]
        [YAXAttributeFor("CollisionFilter")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint CollisionFilter { get; set; }
        [YAXElementOrder(-24)]
        [YAXAttributeFor("Mass")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Mass { get; set; }
        [YAXElementOrder(-23)]
        [YAXAttributeFor("MovementDrag")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float MovementDrag { get; set; }
        [YAXElementOrder(-22)]
        public SCD_Vector3 CenterOfMassOffset { get; set; } = new SCD_Vector3();
        [YAXElementOrder(-21)]
        public SCD_Vector3 InertiaMultipliers { get; set; } = new SCD_Vector3();

        public float[] GetShapeDims()
        {
            return new[] { ShapeDim0, ShapeDim1, ShapeDim2, ShapeDim3, ShapeDim4, ShapeDim5, ShapeDim6 };
        }
    }

    /// <summary>
    /// BDY3 body for layout version 1.
    /// </summary>
    public class SCD_Body3 : SCD_Body
    {
        [YAXAttributeFor("Damping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Damping { get; set; }
        [YAXAttributeFor("F_108")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_108 { get; set; }
        [YAXAttributeFor("F_112")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_112 { get; set; }
        [YAXAttributeFor("Friction")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Friction { get; set; }
        [YAXAttributeFor("F_120")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_120 { get; set; }
        [YAXAttributeFor("F_124")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_124 { get; set; }
    }

    /// <summary>
    /// BDY4 body for layout version 1.
    /// </summary>
    public class SCD_Body4 : SCD_Body
    {
        [YAXAttributeFor("ConvexRadius")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float ConvexRadius { get; set; }
        [YAXAttributeFor("Damping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Damping { get; set; }
        [YAXAttributeFor("F_112")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_112 { get; set; }
        [YAXAttributeFor("F_116")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_116 { get; set; }
        [YAXAttributeFor("Friction")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Friction { get; set; }
        [YAXAttributeFor("F_124")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_124 { get; set; }
        [YAXAttributeFor("F_128")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_128 { get; set; }
        [YAXAttributeFor("IdleDamping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float IdleDamping { get; set; }
    }

    /// <summary>
    /// BDY5 body for layout version 1.
    /// </summary>
    public class SCD_Body5 : SCD_Body
    {
        public SCD_Vector3 Pivot { get; set; } = new SCD_Vector3();
        [YAXAttributeFor("ConvexRadius")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float ConvexRadius { get; set; }
        [YAXAttributeFor("Damping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Damping { get; set; }
        [YAXAttributeFor("F_124")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_124 { get; set; }
        [YAXAttributeFor("F_128")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_128 { get; set; }
        [YAXAttributeFor("Friction")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Friction { get; set; }
        [YAXAttributeFor("F_136")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_136 { get; set; }
        [YAXAttributeFor("F_140")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_140 { get; set; }
        [YAXAttributeFor("IdleDamping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float IdleDamping { get; set; }
    }

    /// <summary>
    /// BDY0 and BDY1 body for layout version 2.
    /// </summary>
    public class SCD_BodyV2 : SCD_Body
    {
        public SCD_Vector3 Pivot { get; set; } = new SCD_Vector3();
        [YAXAttributeFor("ConvexRadius")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float ConvexRadius { get; set; }
        [YAXAttributeFor("I_112")]
        [YAXSerializeAs("value")]
        public int I_112 { get; set; }
        /// <summary>
        /// Stored as a 16-bit half in the body material, next to F_128 and F_132. Meaning not confirmed.
        /// </summary>
        [YAXAttributeFor("F_124")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_124 { get; set; }
        [YAXAttributeFor("F_128")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_128 { get; set; }
        [YAXAttributeFor("F_132")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_132 { get; set; }
        [YAXAttributeFor("LinearDamping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float LinearDamping { get; set; }
        [YAXAttributeFor("AngularDamping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float AngularDamping { get; set; }
        /// <summary>
        /// Multiplies world gravity for this body.
        /// </summary>
        [YAXAttributeFor("GravityFactor")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float GravityFactor { get; set; }
        [YAXAttributeFor("TimeFactor")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float TimeFactor { get; set; }
        [YAXAttributeFor("IdleDamping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float IdleDamping { get; set; }
    }

    /// <summary>
    /// CSR1 and CSR2 constraint between the bodies of two bones. Fields past <see cref="FixedSize"/> are not stored in the block and must be 0.
    /// </summary>
    public class SCD_Constraint
    {
        internal const int MaxFixedSize = 0xA0;

        [YAXAttributeForClass]
        public string BoneA { get; set; }
        [YAXAttributeForClass]
        public string BoneB { get; set; }
        /// <summary>
        /// Byte count from the block start to the first name.
        /// </summary>
        [YAXAttributeForClass]
        public int FixedSize { get; set; }

        [YAXAttributeFor("ConstraintType")]
        [YAXSerializeAs("value")]
        public SCD_ConstraintType ConstraintType { get; set; }
        public SCD_Vector3 PivotA { get; set; } = new SCD_Vector3();
        public SCD_Vector3 PivotB { get; set; } = new SCD_Vector3();
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float F_44 { get; set; }
        [YAXAttributeFor("BreakingThreshold")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float BreakingThreshold { get; set; }
        /// <summary>
        /// For <see cref="SCD_ConstraintType.StiffSpring"/> the X value is the rest length.
        /// </summary>
        public SCD_Vector3 AxisA { get; set; } = new SCD_Vector3();
        public SCD_Vector3 AxisB { get; set; } = new SCD_Vector3();
        public SCD_Vector3 PerpendicularA { get; set; } = new SCD_Vector3();
        public SCD_Vector3 PerpendicularB { get; set; } = new SCD_Vector3();
        /// <summary>
        /// Angle limits in degrees. The two values can be in either order.
        /// </summary>
        [YAXAttributeFor("Angles")]
        [YAXSerializeAs("First")]
        [YAXFormat("R")]
        public float Angle1 { get; set; }
        [YAXAttributeFor("Angles")]
        [YAXSerializeAs("Second")]
        [YAXFormat("R")]
        public float Angle2 { get; set; }
        [YAXAttributeFor("Friction")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Friction { get; set; }
        [YAXAttributeFor("Tau")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Tau { get; set; }
        /// <summary>
        /// Extra ragdoll angles in degrees.
        /// </summary>
        [YAXAttributeFor("RagdollAngles")]
        [YAXSerializeAs("First")]
        [YAXFormat("R")]
        public float RagdollAngle1 { get; set; }
        [YAXAttributeFor("RagdollAngles")]
        [YAXSerializeAs("Second")]
        [YAXFormat("R")]
        public float RagdollAngle2 { get; set; }
        [YAXAttributeFor("RagdollAngles")]
        [YAXSerializeAs("Third")]
        [YAXFormat("R")]
        public float RagdollAngle3 { get; set; }
        [YAXAttributeFor("I_128")]
        [YAXSerializeAs("value")]
        public int I_128 { get; set; }
        [YAXAttributeFor("I_132")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_132 { get; set; }
        [YAXAttributeFor("I_136")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_136 { get; set; }
        [YAXAttributeFor("I_140")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_140 { get; set; }
        [YAXAttributeFor("I_144")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_144 { get; set; }
        [YAXAttributeFor("I_148")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_148 { get; set; }
        [YAXAttributeFor("I_152")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_152 { get; set; }
        [YAXAttributeFor("I_156")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint I_156 { get; set; }

        [YAXDontSerialize]
        public float RestLength => AxisA.X;
    }

    /// <summary>
    /// ACT0 spring between the bodies of two bones.
    /// </summary>
    public class SCD_Spring
    {
        [YAXAttributeForClass]
        public string BoneA { get; set; }
        [YAXAttributeForClass]
        public string BoneB { get; set; }

        /// <summary>
        /// Must be 0.
        /// </summary>
        [YAXAttributeFor("I_16")]
        [YAXSerializeAs("value")]
        public int I_16 { get; set; }
        public SCD_Vector3 PivotA { get; set; } = new SCD_Vector3();
        public SCD_Vector3 PivotB { get; set; } = new SCD_Vector3();
        /// <summary>
        /// A negative value uses the rest distance between the pivots.
        /// </summary>
        [YAXAttributeFor("RestLength")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float RestLength { get; set; }
        [YAXAttributeFor("Strength")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Strength { get; set; }
        [YAXAttributeFor("Damping")]
        [YAXSerializeAs("value")]
        [YAXFormat("R")]
        public float Damping { get; set; }
        [YAXAttributeFor("Flags")]
        [YAXSerializeAs("value")]
        [YAXHexValue]
        public uint Flags { get; set; }
        [YAXAttributeFor("I_60")]
        [YAXSerializeAs("value")]
        public int I_60 { get; set; }
    }
}
