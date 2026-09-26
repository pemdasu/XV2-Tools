using System;
using System.IO;
using Xv2CoreLib.Resource;

namespace Xv2CoreLib.EMB_CLASS
{
    //EMB has been split up into three subclasses:
    //EMB_File             : The most basic EMB object. Just contains data and has no special functionallity
    //EMB_TextureFile      : For EMBs that contain textures -> adds support for texture loading and saving via WriteableBitmap and binding to a user interface
    //EMB_SerializedFile   : For XML serialization -> adds the necessary attributes and methods for XML serialization along with support for binding (Installer)

    [Serializable]
    public class EMB_File : EMB_BaseFile<EmbEntry>
    {
        public EMB_File() { }

        public EMB_File(EMB_BaseFile<EmbEntry> embFile)
        {
            IsEMZ = embFile.IsEMZ;
            Version = embFile.Version;
            I_10 = embFile.I_10;
            UseFileNames = embFile.UseFileNames;
            Entry = embFile.Entry;
        }

        public static EMB_File Load(string path)
        {
            return new EMB_File(LoadInternal(File.ReadAllBytes(path)));
        }

        [FileLoad]
        public static EMB_File Load(byte[] bytes)
        {
            return new EMB_File(LoadInternal(bytes));
        }

        public static EMB_File GetDefault()
        {
            return new EMB_File()
            {
                Version = 37568,
                I_10 = 0,
                UseFileNames = true
            };
        }
    }
}
