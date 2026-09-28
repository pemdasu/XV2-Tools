using System.Collections.Generic;
using Xv2CoreLib.BSA;
using Xv2CoreLib.CUS;

namespace Xv2CoreLib.ValuesDictionary
{
    // BSA numbers EepkType and AcbType differently to BAC, so these cannot be shared with ValuesDictionary.BAC.
    // BSA has no AwokenSkill = 12, and BSA AcbType 3 is Skill_SE while BAC AcbType 3 is Character_VOX.
    // EepkType 3 is named AwokenSkill for serialization only. It means the skill that is currently loaded,
    // not an awoken skill, so it is shown as Any Skill. The real awoken skill is BAC EepkType 12.
    public static class BSA
    {
        public static void AddMissing(BSA_File bsaFile)
        {
            if (bsaFile?.BSA_Entries == null) return;

            foreach (BSA_Entry entry in bsaFile.BSA_Entries)
            {
                if (entry.IBsaTypes != null)
                {
                    foreach (IBsaType type in entry.IBsaTypes)
                    {
                        switch (type)
                        {
                            case BSA_Type0 passing:
                                AddUnknown(EntryPassingCondition, passing.I_00);
                                break;
                            case BSA_Type6 effect:
                                AddUnknown(EepkType, effect.EepkType);
                                AddUnknown(ProjectileEffectAttachment, effect.I_06);
                                AddUnknown(Switch, effect.I_08);
                                break;
                            case BSA_Type7 sound:
                                AddUnknown(AcbType, sound.AcbType);
                                break;
                            case BSA_Type8 postEffect:
                                AddUnknown(BAC.ScreenEffectIds, postEffect.I_00);
                                AddUnknown(ProjectileEffectAttachment, postEffect.I_02);
                                break;
                            case BSA_Type10 upgrade:
                                AddUnknown(CusSkillType, upgrade.SkillType);
                                AddUnknown(SkillUpgradeOperation, upgrade.UpgradeOperation);
                                break;
                            case BSA_Type11 propertyControl:
                                AddUnknown(BAC.EepkType, propertyControl.SkillType);
                                break;
                            case BSA_Type12 signal:
                                AddUnknown(EepkType, signal.EepkType);
                                AddUnknown(SignalDeliveryMode, signal.I_12);
                                break;
                            case BSA_Type13 protection:
                                AddUnknown(ProjectileProtectionState, protection.I_00);
                                AddUnknown(AdditionalSelectorCoverage, protection.I_12);
                                break;
                            case BSA_Type14 placement:
                                AddUnknown(EffectPlacementMode, placement.I_00);
                                AddUnknown(EepkType, placement.EepkType);
                                if (placement.EepkType == Xv2CoreLib.BSA.EepkType.Common)
                                    AddUnknown(CommonEepkType, placement.F_52);
                                break;
                        }
                    }
                }

                if (entry.SubEntries?.CollisionEntries != null)
                    foreach (BSA_Collision collision in entry.SubEntries.CollisionEntries)
                        AddUnknown(EepkType, collision.EepkType);

                if (entry.SubEntries?.ExpirationEntries != null)
                    foreach (BSA_Expiration sound in entry.SubEntries.ExpirationEntries)
                        AddUnknown(AcbType, sound.I_00);
            }
        }

        private static void AddUnknown<TKey>(Dictionary<TKey, string> choices, TKey value)
        {
            if (!choices.ContainsKey(value))
                choices.Add(value, $"Unknown ({value})");
        }

        public static Dictionary<short, string> EntryPassingCondition { get; private set; } = new Dictionary<short, string>()
        {
            { 0, "Always" },
            { 1, "Enemy impact (1)" },
            { 2, "Enemy impact (2)" },
            { 3, "World ray hit" },
            { 4, "Target within range" },
            { 5, "Signal / BAC condition" },
            { 6, "No enemy impact" },
            { 7, "Set pending entry" },
            { 8, "Actor state checks" },
            { 9, "Related object within range" },
            { 10, "Event flag 25" },
            { 11, "Event flag 26" }
        };

        public static Dictionary<byte, string> ImpactA { get; } = new Dictionary<byte, string>()
        {
            { 0, "None" },
            { 1, "Loop" },
            { 2, "Enemy" },
            { 3, "Loop, enemy" },
            { 4, "Projectile" },
            { 6, "Enemy or projectile" },
            { 7, "Loop, enemy or projectile" },
            { 8, "Ground" },
            { 10, "Enemy or ground" },
            { 12, "Projectile or ground" },
            { 14, "Enemy, projectile, or ground" },
            { 15, "Loop, enemy, projectile, or ground" }
        };

        public static Dictionary<byte, string> ImpactB { get; } = new Dictionary<byte, string>()
        {
            { 0, "None" },
            { 1, "Target reaction check" },
            { 2, "Linked actor state change" }
        };

        public static Dictionary<int, string> MovementOperation { get; private set; } = new Dictionary<int, string>()
        {
            { 0, "Set velocity" },
            { 1, "Add velocity" },
            { 2, "Set acceleration" },
            { 3, "Add acceleration" },
            { 4, "Stop movement" }
        };

        public static Dictionary<short, string> CusSkillType { get; private set; } = new Dictionary<short, string>()
        {
            { (short)CUS_File.SkillType.Super, "Super" },
            { (short)CUS_File.SkillType.Ultimate, "Ultimate" },
            { (short)CUS_File.SkillType.Evasive, "Evasive" },
            { (short)CUS_File.SkillType.Blast, "Blast" },
            { (short)CUS_File.SkillType.Awoken, "Awoken" }
        };

        public static Dictionary<byte, string> SkillUpgradeOperation { get; private set; } = new Dictionary<byte, string>()
        {
            { 0, "Add to level" },
            { 1, "Reset level to zero" }
        };

        public static Dictionary<EepkType, string> EepkType { get; private set; } = new Dictionary<EepkType, string>()
        {
            { Xv2CoreLib.BSA.EepkType.Common, "Common" },
            { Xv2CoreLib.BSA.EepkType.StageBG, "Stage BG" },
            { Xv2CoreLib.BSA.EepkType.Character, "Character" },
            { Xv2CoreLib.BSA.EepkType.AwokenSkill, "Any Skill" },
            { Xv2CoreLib.BSA.EepkType.SuperSkill, "Super Skill" },
            { Xv2CoreLib.BSA.EepkType.UltimateSkill, "Ultimate Skill" },
            { Xv2CoreLib.BSA.EepkType.EvasiveSkill, "Evasive Skill" },
            { Xv2CoreLib.BSA.EepkType.KiBlastSkill, "Ki Blast Skill" },
            { Xv2CoreLib.BSA.EepkType.Stage, "Stage" }
        };

        public static Dictionary<uint, string> CommonEepkType { get; private set; } = new Dictionary<uint, string>()
        {
            { 0, "BTL_CMN" },
            { 1, "BTL_AURA" },
            { 2, "BTL_KDN" },
            { 6, "BTL_CMN2" },
            { 3, "lby_cmn/LBY_CMN" },
            { 4, "TTL/TTL" },
            { 5, "ttl_lby/TTL_LBY" }
        };

        public static Dictionary<AcbType, string> AcbType { get; private set; } = new Dictionary<AcbType, string>()
        {
            { Xv2CoreLib.BSA.AcbType.Common_SE, "Common SE" },
            { Xv2CoreLib.BSA.AcbType.Chara_SE, "Character SE" },
            { Xv2CoreLib.BSA.AcbType.Skill_SE, "Skill SE" }
        };

        public static Dictionary<ushort, string> ProjectileEffectAttachment { get; private set; } = new Dictionary<ushort, string>()
        {
            { 0, "ROOT" },
            { 1, "TRS" },
            { 2, "NULL_0" },
            { 3, "NULL_1" },
            { 4, "NULL_2" },
            { 5, "NULL_3" },
            { ushort.MaxValue, "No attachment" }
        };

        public static string GetEepkTypeName(Xv2CoreLib.BSA.EepkType type)
        {
            return EepkType.TryGetValue(type, out string name) ? name : type.ToString();
        }

        public static string GetAcbTypeName(Xv2CoreLib.BSA.AcbType type)
        {
            return AcbType.TryGetValue(type, out string name) ? name : type.ToString();
        }

        public static Dictionary<Switch, string> Switch { get; private set; } = new Dictionary<Switch, string>()
        {
            { Xv2CoreLib.BSA.Switch.On, "On" },
            { Xv2CoreLib.BSA.Switch.Off, "Off" }
        };

        public static Dictionary<int, string> SignalDeliveryMode { get; private set; } = new Dictionary<int, string>()
        {
            { 0, "Broadcast" },
            { 1, "Same-Context Highest Priority" }
        };

        public static Dictionary<ProjectileProtectionOperation, string> ProjectileProtectionState { get; private set; } = new Dictionary<ProjectileProtectionOperation, string>()
        {
            { Xv2CoreLib.BSA.ProjectileProtectionOperation.EnableOrReplace, "On" },
            { Xv2CoreLib.BSA.ProjectileProtectionOperation.DisableWithoutSignal, "Off" }
        };

        public static Dictionary<SpatialEffectGeometryMode, string> EffectPlacementMode { get; private set; } = new Dictionary<SpatialEffectGeometryMode, string>()
        {
            { Xv2CoreLib.BSA.SpatialEffectGeometryMode.Default, "Default Placement" },
            { Xv2CoreLib.BSA.SpatialEffectGeometryMode.DistanceRelative, "Distance-Based Placement" },
            { Xv2CoreLib.BSA.SpatialEffectGeometryMode.FullVector, "Explicit Vector Placement" }
        };

        public static Dictionary<float, string> AdditionalSelectorCoverage { get; private set; } = new Dictionary<float, string>()
        {
            { 0f, "None" },
            { 1f, "Groups 4 and 5" },
            { 2f, "Group 6" },
            { 3f, "Groups 4, 5, and 6" }
        };
    }
}
