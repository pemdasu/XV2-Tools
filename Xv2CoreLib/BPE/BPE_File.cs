using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xv2CoreLib.Color;
using Xv2CoreLib.Resource.UndoRedo;
using YAXLib;

namespace Xv2CoreLib.BPE
{
    /// <summary>
    /// The 0 to 1 color of the keyframe types that have one.
    /// </summary>
    public static class BpeColors
    {
        /// <summary>
        /// The red, green and blue field names of a keyframe, or null when its type has no color.
        /// </summary>
        public static string[] GetColorFields(object keyframe)
        {
            switch (keyframe)
            {
                case BPE_Type2 _: return new[] { nameof(BPE_Type2.F_28), nameof(BPE_Type2.F_32), nameof(BPE_Type2.F_36) };
                case BPE_Type6 _: return new[] { nameof(BPE_Type6.F_08), nameof(BPE_Type6.F_12), nameof(BPE_Type6.F_16) };
                case BPE_Type8 _: return new[] { nameof(BPE_Type8.F_12), nameof(BPE_Type8.F_16), nameof(BPE_Type8.F_20) };
                case BPE_Type9 _: return new[] { nameof(BPE_Type9.F_20), nameof(BPE_Type9.F_24), nameof(BPE_Type9.F_28) };
                default: return null;
            }
        }

        public static Xv2Color GetColor(object keyframe)
        {
            float[] values = GetColorFields(keyframe).Select(field => (float)keyframe.GetType().GetProperty(field).GetValue(keyframe)).ToArray();
            return new Xv2Color(values[0], values[1], values[2]);
        }

        public static void AccumulateUsedColors(object keyframe, Xv2ColorAccumulator accumulator)
        {
            Xv2Color color = GetColor(keyframe);

            if (!color.IsWhiteOrBlack)
                accumulator.AddColor(color);
        }

        /// <summary>
        /// Shifts the hue, saturation and lightness of the keyframe color, or sets its hue for a hue set. White and black stay the same.
        /// </summary>
        public static Task ChangeHue(object keyframe, HueAdjustmentInput input)
        {
            Xv2Color color = GetColor(keyframe);
            if (color.IsWhiteOrBlack) return Task.CompletedTask;

            Xv2Color newColor = input.HueSet ? color.ApplyHueSet((int)input.Hue, input.Variance) : color.ApplyHueAdjustment((int)input.Hue, (float)input.Saturation, (float)input.Lightness);
            string[] fields = GetColorFields(keyframe);
            float[] newValues = { newColor.R, newColor.G, newColor.B };

            for (int i = 0; i < fields.Length; i++)
            {
                var property = keyframe.GetType().GetProperty(fields[i]);
                float oldValue = (float)property.GetValue(keyframe);
                float newValue = newValues[i];
                property.SetValue(keyframe, newValue);
                input.Undos.Add(new UndoablePropertyGeneric(fields[i], keyframe, oldValue, newValue));
            }

            return Task.CompletedTask;
        }
    }

    
    public enum BpeType
    {
        Blur = 0,
        WhiteShine = 1,
        ColorFilter = 2,
        Disorientation = 4,
        Zoom = 6,
        Hue = 8,
        BodyOutline = 9,
        Fog = 10
    }

    [YAXSerializeAs("BPE")]
    public class BPE_File : ISorting
    {
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_Entry")]
        public List<BPE_Entry> Entries { get; set; } = new List<BPE_Entry>();

        public byte[] SaveToBytes()
        {
            return new Deserializer(this).bytes.ToArray();
        }

        public static BPE_File Load(byte[] bytes)
        {
            return new Parser(bytes).GetBpeFile();
        }

        public void SaveBinary(string path)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            new Deserializer(this, path);
        }

        public void SortEntries()
        {
            Entries.Sort((x, y) => x.SortID - y.SortID);
        }

    }
    
    public class BPE_Entry : IInstallable
    {
        [YAXDontSerialize]
        public int SortID { get { return int.Parse(Index); } }

        [YAXAttributeForClass]
        [BindingAutoId]
        public string Index { get; set; } //int32
        [YAXAttributeForClass]
        public int I_00 { get; set; }
        [YAXAttributeForClass]
        [YAXSerializeAs("Duration")]
        public ushort I_04 { get; set; }
        [YAXAttributeForClass]
        public ushort I_06 { get; set; }
        [YAXAttributeForClass]
        public ushort I_08 { get; set; }

        
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_SubEntry")]
        public List<BPE_SubEntry> SubEntries { get; set; } = new List<BPE_SubEntry>();

        

    }

    [YAXSerializeAs("BPE_SubEntry")]
    public class BPE_SubEntry
    {
        [YAXAttributeForClass]
        [YAXSerializeAs("Type")]
        public BpeType BpeType { get; set; } //Int32
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public ushort I_04 { get; set; }
        [YAXAttributeFor("End_Time")]
        [YAXSerializeAs("value")]
        public ushort I_06 { get; set; }
        [YAXAttributeFor("I_08")]
        [YAXSerializeAs("value")]
        public ushort I_08 { get; set; }
        [YAXAttributeFor("I_10")]
        [YAXSerializeAs("value")]
        public ushort I_10 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("F_24")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("F_28")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("F_32")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0##########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("I_36")]
        [YAXSerializeAs("value")]
        public int I_36 { get; set; }
        [YAXAttributeFor("I_40")]
        [YAXSerializeAs("value")]
        public ushort I_40 { get; set; }

        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Blur")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type0> Type0 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "WhiteShine")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type1> Type1 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_Type2")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type2> Type2 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_Type3")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type3> Type3 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Disorientation")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type4> Type4 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_Type5")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type5> Type5 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Zoom")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type6> Type6 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BPE_Type7")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type7> Type7 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "Hue")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type8> Type8 { get; set; }
        [YAXCollection(YAXCollectionSerializationTypes.RecursiveWithNoContainingElement, EachElementName = "BodyOutline")]
        [YAXDontSerializeIfNull]
        public List<BPE_Type9> Type9 { get; set; }
        public List<BPE_Type10> Type10 { get; set; }


    }

    [YAXSerializeAs("Blur")]
    public class BPE_Type0
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("Amount")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
    }

    [YAXSerializeAs("WhiteShine")]
    public class BPE_Type1
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("Intensity")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
    }

    [YAXSerializeAs("BPE_Type2")]
    public class BPE_Type2 : IHueAdjustment
    {
        public void AccumulateUsedColors(Xv2ColorAccumulator accumulator) => BpeColors.AccumulateUsedColors(this, accumulator);

        public Task ApplyHueAdjustment(HueAdjustmentInput input) => BpeColors.ChangeHue(this, input);

        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("I_04")]
        [YAXSerializeAs("value")]
        public int I_04 { get; set; }
        [YAXAttributeFor("F_08")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("F_24")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("R")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("G")]
        [YAXFormat("0.0###########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("B")]
        [YAXFormat("0.0###########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("F_40")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_40 { get; set; }
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_44 { get; set; }
        [YAXAttributeFor("F_48")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_48 { get; set; }
    }
    
    [YAXSerializeAs("BPE_Type3")]
    public class BPE_Type3
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("I_04")]
        [YAXSerializeAs("value")]
        public int I_04 { get; set; }
        [YAXAttributeFor("F_08")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
    }

    [YAXSerializeAs("Disorientation")]
    public class BPE_Type4
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("I_04")]
        [YAXSerializeAs("value")]
        public int I_04 { get; set; }
    }

    [YAXSerializeAs("BPE_Type5")]
    public class BPE_Type5
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("F_08")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("F_20")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("F_24")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("F_28")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("F_32")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("F_36")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("F_40")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_40 { get; set; }
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_44 { get; set; }
    }

    [YAXSerializeAs("Zoom")]
    public class BPE_Type6 : IHueAdjustment
    {
        public void AccumulateUsedColors(Xv2ColorAccumulator accumulator) => BpeColors.AccumulateUsedColors(this, accumulator);

        public Task ApplyHueAdjustment(HueAdjustmentInput input) => BpeColors.ChangeHue(this, input);

        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("EffectArea")]
        [YAXSerializeAs("Size")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("R")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("G")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("B")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("A")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("F_24")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("F_28")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("F_32")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("F_36")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_36 { get; set; }
        [YAXAttributeFor("ZoomLevel")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_40 { get; set; }
        [YAXAttributeFor("F_44")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_44 { get; set; }
    }

    [YAXSerializeAs("BPE_Type7")]
    public class BPE_Type7
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
    }

    [YAXSerializeAs("Hue")]
    public class BPE_Type8 : IHueAdjustment
    {
        public void AccumulateUsedColors(Xv2ColorAccumulator accumulator) => BpeColors.AccumulateUsedColors(this, accumulator);

        public Task ApplyHueAdjustment(HueAdjustmentInput input) => BpeColors.ChangeHue(this, input);

        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("I_04")]
        [YAXSerializeAs("value")]
        public int I_04 { get; set; }
        [YAXAttributeFor("BlendMode")]
        [YAXSerializeAs("value")]
        public int I_08 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("R")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("G")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("B")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("A")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
    }

    [YAXSerializeAs("BodyOutline")]
    public class BPE_Type9 : IHueAdjustment
    {
        public void AccumulateUsedColors(Xv2ColorAccumulator accumulator) => BpeColors.AccumulateUsedColors(this, accumulator);

        public Task ApplyHueAdjustment(HueAdjustmentInput input) => BpeColors.ChangeHue(this, input);

        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("NearFadeDistance")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("FarFadeDistance")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("Transparency")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("R")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("G")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("Color")]
        [YAXSerializeAs("B")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("F_32")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_32 { get; set; }
    }

    /// <summary>
    /// Depth fog over the whole screen. Colors are ARGB.
    /// </summary>
    [YAXSerializeAs("Fog")]
    public class BPE_Type10
    {
        [YAXAttributeFor("Start_Time")]
        [YAXSerializeAs("value")]
        public int I_00 { get; set; }
        [YAXAttributeFor("F_04")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_04 { get; set; }
        [YAXAttributeFor("F_08")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_08 { get; set; }
        [YAXAttributeFor("F_12")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_12 { get; set; }
        [YAXAttributeFor("F_16")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_16 { get; set; }
        [YAXAttributeFor("FogDistance")]
        [YAXSerializeAs("Near")]
        [YAXFormat("0.0###########")]
        public float F_20 { get; set; }
        [YAXAttributeFor("FogDistance")]
        [YAXSerializeAs("Far")]
        [YAXFormat("0.0###########")]
        public float F_24 { get; set; }
        [YAXAttributeFor("F_28")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_28 { get; set; }
        [YAXAttributeFor("FogCurve")]
        [YAXSerializeAs("value")]
        [YAXFormat("0.0###########")]
        public float F_32 { get; set; }
        [YAXAttributeFor("I_36")]
        [YAXSerializeAs("value")]
        public uint I_36 { get; set; }
        [YAXAttributeFor("I_40")]
        [YAXSerializeAs("value")]
        public uint I_40 { get; set; }
        [YAXAttributeFor("FogColor")]
        [YAXSerializeAs("ARGB")]
        public uint I_44 { get; set; }
        [YAXAttributeFor("AddColor")]
        [YAXSerializeAs("ARGB")]
        public uint I_48 { get; set; }
    }

}
