﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YAXLib;

namespace Xv2CoreLib.EMD
{
    public class Deserializer
    {
        private string saveLocation { get; set; }
        private EMD_File emdFile { get; set; }
        public List<byte> bytes { get; private set; } = new List<byte>();

        public Deserializer(EMD_File _emdFile)
        {
            emdFile = _emdFile;
            WriteEmd();
        }

        public Deserializer(EMD_File _emdFile, string savePath)
        {
            saveLocation = savePath;
            emdFile = _emdFile;
            WriteEmd();
            File.WriteAllBytes(saveLocation, bytes.ToArray());
        }

        public Deserializer(string location)
        {
            saveLocation = String.Format("{0}/{1}", Path.GetDirectoryName(location), Path.GetFileNameWithoutExtension(location));
            YAXSerializer serializer = new YAXSerializer(typeof(EMD_File), YAXSerializationOptions.DontSerializeNullObjects);
            emdFile = (EMD_File)serializer.DeserializeFromFile(location);
            WriteEmd();
            File.WriteAllBytes(saveLocation, bytes.ToArray());
        }

        private void WriteEmd()
        {
            List<StringWriter.StringInfo> StrWriter = new List<StringWriter.StringInfo>();
            int modelCount = (emdFile.Models != null) ? emdFile.Models.Count : 0;

            //Header
            bytes.AddRange(BitConverter.GetBytes(EMD_File.EMD_SIGNATURE));
            bytes.AddRange(BitConverter.GetBytes((ushort)65534));
            bytes.AddRange(BitConverter.GetBytes((ushort)28));
            bytes.AddRange(BitConverter.GetBytes(emdFile.Version));
            bytes.AddRange(new byte[6]);
            bytes.AddRange(BitConverter.GetBytes((ushort)modelCount));
            bytes.AddRange(new byte[8]); //Offsets, fill in later

            //Model pointer table
            bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), 20);
            List<PtrWriter.Ptr> ModelPtrs = new List<PtrWriter.Ptr>();

            for (int i = 0; i < modelCount; i++)
            {
                ModelPtrs.Add(new PtrWriter.Ptr()
                {
                    Offset = bytes.Count
                });
                bytes.AddRange(new byte[4]);
            }

            PadFile(16); //At the end of the model pointer list, we need to pad the file to 16-byte alignment

            //Models
            for (int i = 0; i < modelCount; i++)
            {
                PadFile(16);

                //Fill in pointer
                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - ModelPtrs[i].RelativeTo), ModelPtrs[i].Offset);

                List<PtrWriter.Ptr> meshPtrs = new List<PtrWriter.Ptr>();
                ushort meshCount = (ushort)((emdFile.Models[i].Meshes != null) ? emdFile.Models[i].Meshes.Count : 0);
                int meshRelativeOfs = bytes.Count;

                bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].I_00));
                bytes.AddRange(BitConverter.GetBytes(meshCount));
                bytes.AddRange(BitConverter.GetBytes(8));

                //Mesh pointers
                for (int a = 0; a < meshCount; a++)
                {
                    meshPtrs.Add(new PtrWriter.Ptr() { Offset = bytes.Count, RelativeTo = meshRelativeOfs });
                    bytes.AddRange(new byte[4]);
                }

                //Pad file
                PadFile(16);

                //Mesh
                for (int a = 0; a < meshCount; a++)
                {
                    PadFile(16);
                    //Fill in pointer
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - meshPtrs[a].RelativeTo), meshPtrs[a].Offset);

                    ushort submeshCount = (ushort)((emdFile.Models[i].Meshes[a].Submeshes != null) ? emdFile.Models[i].Meshes[a].Submeshes.Count : 0);
                    int meshStart = bytes.Count;

                    bytes.AddRange(emdFile.Models[i].Meshes[a].AABB.Write());
                    bytes.AddRange(new byte[4]); // Name offset
                    bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].I_52));
                    bytes.AddRange(BitConverter.GetBytes(submeshCount));
                    bytes.AddRange(new byte[4]); //Pointer table offset

                    //Write name
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - meshStart), meshStart + 48);
                    bytes.AddRange(Encoding.ASCII.GetBytes(emdFile.Models[i].Meshes[a].Name));
                    bytes.Add(0);

                    //Pad file to be in 32-bit alignment (4 bytes)
                    PadFile(4);

                    //Fill submesh pointer list offset
                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - meshStart), meshStart + 56);

                    List<PtrWriter.Ptr> submeshPtrs = new List<PtrWriter.Ptr>();
                    //Submesh pointer table
                    for (int s = 0; s < submeshCount; s++)
                    {
                        submeshPtrs.Add(new PtrWriter.Ptr() { Offset = bytes.Count, RelativeTo = meshStart });
                        bytes.AddRange(new byte[4]);
                    }

                    //Pad file
                    PadFile(16);

                    //Submeshes
                    for (int s = 0; s < submeshCount; s++)
                    {
                        //Pad file to 16 byte alignment
                        PadFile(16);

                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshPtrs[s].RelativeTo), submeshPtrs[s].Offset);

                        int submeshStart = bytes.Count;

                        bytes.AddRange(emdFile.Models[i].Meshes[a].Submeshes[s].AABB.Write());
                        bytes.AddRange(BitConverter.GetBytes((uint)emdFile.Models[i].Meshes[a].Submeshes[s].VertexFlags));
                        bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].VertexSize));
                        bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].VertexCount));
                        bytes.AddRange(new byte[4]); //Vertex pointer
                        bytes.AddRange(new byte[4]); //Submesh name pointer
                        bytes.Add(0); //Unknown, probably padding
                        bytes.Add((byte)emdFile.Models[i].Meshes[a].Submeshes[s].TextureDefinitionCount);
                        bytes.AddRange(BitConverter.GetBytes((ushort)emdFile.Models[i].Meshes[a].Submeshes[s].TriangleListCount));
                        bytes.AddRange(new byte[4]); //Texture definitions pointer
                        bytes.AddRange(new byte[4]); //Triangles pointer

                        //Write name
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshStart), submeshStart + 64);
                        bytes.AddRange(Encoding.ASCII.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].Name));
                        bytes.Add(0);

                        //Pad file to be in 32-bit alignment (4 bytes)
                        PadFile(4);

                        //Texture definitions
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshStart), submeshStart + 72);
                        bytes.AddRange(EMD_TextureSamplerDef.Write(emdFile.Models[i].Meshes[a].Submeshes[s].TextureSamplerDefs));

                        //Triangles pointer list
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshStart), submeshStart + 76);
                        int triangleTableStart = bytes.Count;

                        for (int t = 0; t < emdFile.Models[i].Meshes[a].Submeshes[s].TriangleListCount; t++)
                        {
                            bytes.AddRange(new byte[4]);
                        }


                        //Triangles
                        for (int t = 0; t < emdFile.Models[i].Meshes[a].Submeshes[s].TriangleListCount; t++)
                        {
                            bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshStart), triangleTableStart);

                            int triangleStart = bytes.Count;

                            bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].FaceCount));
                            bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].BonesCount));
                            bytes.AddRange(BitConverter.GetBytes(16));
                            bytes.AddRange(new byte[4]);
                            bool is32Bit = emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].FaceCount > ushort.MaxValue;

                            //Faces
                            for (int f = 0; f < emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].FaceCount; f++)
                            {
                                if(is32Bit)
                                {
                                    bytes.AddRange(BitConverter.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].Faces[f]));
                                }
                                else
                                {
                                    bytes.AddRange(BitConverter.GetBytes((ushort)emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].Faces[f]));
                                }
                            }

                            //The cause of all that "emd corruption" 
                            PadFile(4);

                            //Bones ptr list
                            int boneTablePos = bytes.Count;
                            if(emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].BonesCount > 0)
                                bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - triangleStart), triangleStart + 12);

                            for (int b = 0; b < emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].BonesCount; b++)
                            {
                                bytes.AddRange(new byte[4]);
                            }

                            //Write strings
                            for (int b = 0; b < emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].BonesCount; b++)
                            {
                                if (emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].Bones[b] != "NULL")
                                {
                                    bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - triangleStart), boneTablePos);
                                    bytes.AddRange(Encoding.ASCII.GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].Triangles[t].Bones[b]));
                                    bytes.Add(0);
                                }

                                boneTablePos += 4;
                            }

                            PadFile(4);

                            triangleTableStart += 4;
                        }

                        //Vertex
                        bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count - submeshStart), submeshStart + 60);

                        for (int v = 0; v < emdFile.Models[i].Meshes[a].Submeshes[s].VertexCount; v++)
                        {
                            bytes.AddRange(emdFile.Models[i].Meshes[a].Submeshes[s].Vertexes[v].GetBytes(emdFile.Models[i].Meshes[a].Submeshes[s].VertexFlags));
                        }

                    }
                }
            }

            //Model names
            PadFile(4);
            bytes = Utils.ReplaceRange(bytes, BitConverter.GetBytes(bytes.Count), 24);

            for (int i = 0; i < modelCount; i++)
            {
                StrWriter.Add(new StringWriter.StringInfo()
                {
                    Offset = bytes.Count,
                    StringToWrite = emdFile.Models[i].Name
                });
                bytes.AddRange(new byte[4]);
            }

            bytes = StringWriter.WritePointerStrings(StrWriter, bytes);
        }

        private void PadFile(int alignment)
        {
            int padding = Utils.CalculatePadding(bytes.Count, alignment);
            bytes.AddRange(new byte[padding]);
        }
    }


}
