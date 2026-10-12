using System.Collections.Generic;
using Xv2CoreLib.BAC;

namespace Xv2CoreLib.ValuesDictionary
{
    public static partial class BAC
    {
        /// <summary>
        /// Search the file for any unknown values and add them to the dictionaries.
        /// </summary>
        public static void AddMissing(BAC_File bacFile)
        {
            if (bacFile == null) return;

            foreach(var bacEntry in bacFile?.BacEntries)
            {
                if (bacEntry.IBacTypes == null) return;

                foreach(var type in bacEntry.IBacTypes)
                {
                    if (type is BAC_Type1 type1)
                    {
                        if (!BdmType.ContainsKey((byte)type1.bdmFile))
                            BdmType.Add((byte)type1.bdmFile, $"Unknown ({type1.bdmFile})");
                    }
                    else if (type is BAC_Type3 type3)
                    {
                        if (!InvulnerabilityTypes.ContainsKey(type3.InvulnerabilityType))
                            InvulnerabilityTypes.Add(type3.InvulnerabilityType, $"Unknown ({type3.InvulnerabilityType})");
                    }
                    else if (type is BAC_Type8 type8)
                    {
                        if (!EepkType.ContainsKey((ushort)type8.EepkType))
                            EepkType.Add((ushort)type8.EepkType, $"Unknown ({type8.EepkType})");
                    }
                    else if (type is BAC_Type9 type9)
                    {
                        if (!BsaType.ContainsKey((byte)type9.BsaType))
                            BsaType.Add((byte)type9.BsaType, $"Unknown ({type9.BsaType})");

                        if (!BsaSpawnSource.ContainsKey(type9.SpawnSource))
                            BsaSpawnSource.Add(type9.SpawnSource, $"Unknown ({type9.SpawnSource})");

                        if (!BsaSpawnOrientation.ContainsKey(type9.SpawnOrientation))
                            BsaSpawnOrientation.Add(type9.SpawnOrientation, $"Unknown ({type9.SpawnOrientation})");

                    }
                    else if(type is BAC_Type11 sound)
                    {
                        if(!AcbType.ContainsKey((ushort)sound.AcbType))
                            AcbType.Add((ushort)sound.AcbType, $"Unknown ({sound.AcbType})");
                    }
                    else if (type is BAC_Type18 physics)
                    {
                        if (!PhysicsObjectControlType.ContainsKey((ushort)physics.Function))
                            PhysicsObjectControlType.Add((ushort)physics.Function, $"Unknown ({(ushort)physics.Function})");
                    }
                    else if (type is BAC_Type31 callback)
                    {
                        if (!ProjectileCallbackFilterMode.ContainsKey(callback.FilterMode))
                            ProjectileCallbackFilterMode.Add(callback.FilterMode, $"Unknown ({callback.FilterMode})");
                    }
                    else if (type is BAC_Type20 homing)
                    {
                        if (!HomingType.ContainsKey((ushort)homing.HomingMovementType))
                            HomingType.Add((ushort)homing.HomingMovementType, $"Unknown ({(ushort)homing.HomingMovementType})");
                    }

                    if(type is IBacBone bacBone)
                    {
                        if(!BoneNames.ContainsKey(bacBone.BoneLink))
                            BoneNames.Add(bacBone.BoneLink, $"Unknown Bone ({bacBone.BoneLink})");
                    }
                }
            }
        }

        public static void AddDefaultMovesetNamesToBac(BAC_File bacFile)
        {
            if (bacFile == null) return;

            foreach(BAC_Entry bacEntry in bacFile.BacEntries)
            {
                if(MovesetBacEntry.TryGetValue(bacEntry.SortID, out string name))
                    bacEntry.SetDefaultName(name);
            }
        }

        //Animation Ean Type
        public static Dictionary<ushort, string> AnimationEanType { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0 , "Common" },
            { 5 , "Character" },
            { 65534 , "Skill" },
            { 9 , "Common (tail)" },
            { 2 , "Common MCM.DBA" },
            { 21, "Common MCM.DBA (left hand)" },
            { 22, "Common MCM.DBA (both hands)" },
            { 40, "Common CMN_TTL" },
            { 41, "Common MCM.XV1" },
            { 42, "Common MCM.TTL" },
            { 43, "Common MCM.TU6" },
            { 94, "Common MCM.TU13.5" },
            { 95, "Common CM2.xvs" },
            { 96, "Common MCM.CMN.FULL" },
            { 97, "Common CM2.XAC.frs" },
            { 98, "Common CM2.XAN.frs" },
            { 10 , "Face Base" },
            { 11 , "Face Forehead" }
        };

        public static Dictionary<byte, string> BoundingBoxType { get; } = new Dictionary<byte, string>()
        {
            { 0, "Sphere" },
            { 1, "Capsule" },
            { 2, "Box" },
            { 3, "Sphere (Fixed Size)" },
            { 4, "Capsule (Fixed Size)" },
            { 5, "Box (Fixed Size)" }
        };

        //BdmType
        public static Dictionary<byte, string> BdmType { get; private set; } = new Dictionary<byte, string>()
        {
            { 0 , "Common" },
            { 1 , "Character" },
            { 2 , "Skill" }
        };

        //BDM HitboxFlags (DamageProperties): These are flags but are better represented as a dropdown menu. In all BAC files none of these are ever used in combination so it is safe to do. (exception is the first 4 bits, which will be seperated into bools)
        public static Dictionary<ushort, string> BdmImpactType { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0x0000 , "Continuous" },
            { 0x4000 , "Continuous (Prioritize Block)" },
            { 0x2000 , "Single Impact" },
            { 0x1000 , "Cancel Projectiles" },
            { 0x8000 , "Same Actor Only" }
        };

        public static Dictionary<ushort, string> BdmDamageType { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0x0200 , "Restore Health" },
            { 0x0000 , "Health" },
            { 0x0400 , "Ki" },
            { 0x0100 , "Block Projectiles" },
            { 0x0800 , "Reflect Projectiles" }
        };

        public static Dictionary<ushort, string> BdmSpawnSource { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0x0000 , "User" },
            { 0x0040 , "Target Hit Actor" },
            { 0x0080 , "Target" },
            { 0x0010 , "Absorb Projectiles" },
            { 0x0020 , "Deflect Projectiles" }
        };

        //Invulnerability
        public static Dictionary<ushort, string> InvulnerabilityTypes { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0xffff , "None" },
            { 0x0 , "All Attacks Miss" },
            { 0x1 , "Unknown" },
            { 0x2 , "Stun Proof" },
            { 0x3 , "No Damage, Stun Proof" },
            { 0x4 , "No Damage, Stun Proof" },
            { 0x5 , "No Damage, Consume Stamina" },
            { 0x6 , "Attacks Absorbed" },
            { 0x7 , "Attacks Absorbed" },
            { 0x8 , "No Damage, Stun Proof" },
            { 0x9 , "First Hit Damages, Stun Proof" },
            { 0xa , "First Hit Damages, Stun Proof" },
            { 0xb , "First Hit Damages, Stun Proof" },
            { 0xc , "First Hit Damages, Stun Proof" },
            { 0xd , "Automatic Guard" },
            { 0xe , "Ki Blasts Fly Through" }
        };


        //Camera Ean Type
        public static Dictionary<ushort, string> CameraEanType { get; private set; } = new Dictionary<ushort, string>()
        {
            //Primary EANs
            { 3 , "Common" },
            { 4 , "Character" },
            { 5 , "Skill" },
            { 0x19 , "Activate Extended Camera" },
            { 0x1a , "Disable Extended Camera (2)" },
            { 16 , "Common CMN_TTL" },

            //Everything else
            { 9 , "MCM" },
            { 0 , "Rumble (0x0)" },
            { 1 , "Heavy Rumble (0x1)" },
            { 2 , "Extreme Rumble (0x2)" },
            { 6 , "Orbit Arc (0x6)" },
            { 7 , "Static (0x7)" },
            { 8 , "Victim Focus (0x8)" },
            { 0xa , "Wide Orbit Arc (0xa)" },
            { 0xb , "Disable Extended Camera" },
            { 0xc , "Over the Shoulder (0xc)" },
            { 0xd , "Victim Focus, No Offset (0xd)" },
            { 0xe , "Light Rumble (0xe)" },
            { 0xf , "Violent Rumble (0xf)" },
            { 0x11 , "Front Close-up (0x11)" },
            { 0x12 , "Behind Actor (0x12)" },
            { 0x13 , "Behind Actor, Look Between (0x13)" },
            { 0x14 , "Two-Shot, Look at Object (0x14)" },
            { 0x15 , "Smooth Rumble (0x15)" },
            { 0x16 , "Smooth Heavy Rumble (0x16)" },
            { 0x17 , "Smooth Extreme Rumble (0x17)" },
            { 0x18 , "Side Two-Shot (0x18)" },
        };

        //"Flags"
        public static Dictionary<ushort, string> Flags { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0x0 , "Both" },
            { 0x1 , "CaC" },
            { 0x2 , "Roster" },
            { 0x5 , "Unknown (5)" },
        };

        public static string GetEepkTypeName(ushort type)
        {
            return EepkType.TryGetValue(type, out string name) ? name : type.ToString();
        }

        public static string GetAnimationEanTypeName(ushort type)
        {
            return AnimationEanType.TryGetValue(type, out string name) ? name : $"Unknown ({type})";
        }

        public static string GetCameraEanTypeName(ushort type)
        {
            return CameraEanType.TryGetValue(type, out string name) ? name : $"Unknown ({type})";
        }

        public static string GetAcbTypeName(ushort type)
        {
            return AcbType.TryGetValue(type, out string name) ? name : type.ToString();
        }

        //AcbType
        public static Dictionary<ushort, string> AcbType { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0 , "Common SE" },
            { 2 , "Character SE" },
            { 3 , "Character VOX" },
            { 4 , "Footsteps" },
            { 5, "Mission SE" },
            { 6, "Quest Common SE" },
            { 7, "Quest SE" },
            { 8, "Title SE" },
            { 10 , "Skill SE" },
            { 11 , "Skill VOX" },
            { 12 , "Ultimate VOX" }
        };

        //EepkType
        public static Dictionary<ushort, string> EepkType { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0, "Common" },
            { 2, "Character" },
            { 3, "Any Skill" },
            { 5, "Super Skill" },
            { 6, "Ultimate Skill" },
            { 12, "Awoken Skill" },
            { 7, "Evasive Skill" },
            { 9, "Ki Blast Skill" },
            { 1, "Stage BG" },
            { 11, "Stage" }
        };

        //EepkGlobalSkillIds
        public static Dictionary<ushort, string> EepkGlobalSkillId { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0 , "BTL_CMN" },
            { 1 , "BTL_AURA" },
            { 2 , "BTL_KDN" },
            { 6 , "BTL_CMN2" },
            { 3 , "lby_cmn/LBY_CMN" },
            { 4 , "TTL/TTL" },
            { 5 , "ttl_lby/TTL_LBY" }
        };

        //BsaType
        public static Dictionary<byte, string> BsaType { get; private set; } = new Dictionary<byte, string>()
        {
            { 0, "Common" },
            { 3, "Any Skill" },
            { 5, "Super Skill" },
            { 6, "Ultimate Skill" },
            { 12, "Awoken Skill" },
            { 7, "Evasive Skill" },
            { 8, "Unknown (8)" },
            { 9, "Ki Blast Skill" },
        };

        //BsaSpawnSource
        public static Dictionary<byte, string> BsaSpawnSource { get; private set; } = new Dictionary<byte, string>()
        {
            { 0 , "User (0)" },
            { 1 , "Target (1)" },
            { 2 , "Unknown (2)" },
            { 3 , "Unknown (3)" },
            { 4 , "Unknown (4)" },
            { 5,  "Target (5)" },
            { 6 , "Unknown (6)" },
            { 7 , "Map (7)" },
        };

        //BsaSpawnOrientation
        public static Dictionary<byte, string> BsaSpawnOrientation { get; private set; } = new Dictionary<byte, string>()
        {
            { 0 , "Default" },
            { 1 , "User Direction (1)" },
            { 3 , "User Direction (3)" }
        };

        //BCS Part ID
        public static Dictionary<ushort, string> BcsPartId { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0 , "FaceBase" },
            { 1 , "FaceForehead" },
            { 2 , "FaceEye" },
            { 3 , "FaceNose" },
            { 4 , "FaceEar" },
            { 5 , "Hair" },
            { 6 , "Bust" },
            { 7 , "Pants" },
            { 8 , "Rists" },
            { 9 , "Boots" },
        };

        //Used on Cameras, Effects, Projectiles, ScreenEffects, ThrowHandlers...
        public static Dictionary<BoneLinks, string> BoneNames = new Dictionary<BoneLinks, string>()
        {
            //All values tested manually ingame.
            { BoneLinks.b_C_Base , "b_C_Base" },
            { BoneLinks.b_C_Chest , "b_C_Chest" },
            { BoneLinks.b_C_Head , "b_C_Head" },
            { BoneLinks.b_C_Neck1 , "b_C_Neck1" },
            { BoneLinks.b_C_Pelvis , "b_C_Pelvis" },
            { BoneLinks.b_C_Spine1 , "b_C_Spine1" },
            { BoneLinks.b_C_Spine2 , "b_C_Spine2" },
            { BoneLinks.b_R_Hand , "b_R_Hand" },
            { BoneLinks.b_L_Hand , "b_L_Hand" },
            { BoneLinks.b_R_Elbow , "b_R_Elbow" },
            { BoneLinks.b_L_Elbow , "b_L_Elbow" },
            { BoneLinks.b_R_Shoulder , "b_R_Shoulder" },
            { BoneLinks.b_L_Shoulder , "b_L_Shoulder" },
            { BoneLinks.b_R_Foot , "b_R_Foot" },
            { BoneLinks.b_L_Foot , "b_L_Foot" },
            { BoneLinks.b_R_Leg1 , "b_R_Leg1" },
            { BoneLinks.b_L_Leg1 , "b_L_Leg1" },
            { BoneLinks.g_C_Head , "g_C_Head" },
            { BoneLinks.g_C_Pelvis , "g_C_Pelvis" },
            { BoneLinks.g_L_Foot , "g_L_Foot" },
            { BoneLinks.g_L_Hand , "g_L_Hand" },
            { BoneLinks.g_R_Foot , "g_R_Foot" },
            { BoneLinks.g_R_Hand , "g_R_Hand" },
            { BoneLinks.g_x_CAM, "g_x_CAM" },
            { BoneLinks.g_x_LND , "g_x_LND" },

            //Finger tips, added in 1.20
            { BoneLinks.h_L_Thumb3 , "h_L_Thumb3" },
            { BoneLinks.h_L_Index3 , "h_L_Index3" },
            { BoneLinks.h_L_Middle3 , "h_L_Middle3" },
            { BoneLinks.h_L_Ring3 , "h_L_Ring3" },
            { BoneLinks.h_L_Pinky3 , "h_L_Pinky3" },
            { BoneLinks.h_R_Thumb3 , "h_R_Thumb3" },
            { BoneLinks.h_R_Index3 , "h_R_Index3" },
            { BoneLinks.h_R_Middle3 , "h_R_Middle3" },
            { BoneLinks.h_R_Ring3 , "h_R_Ring3" },
            { BoneLinks.h_R_Pinky3 , "h_R_Pinky3" },
        };



    }
}
