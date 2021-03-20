using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtendedItemDataFramework;
using fastJSON;
using UnityEngine;

namespace EpicLoot
{
    public class MagicItemEffectRequirements
    {
        private static StringBuilder _sb = new StringBuilder();
        private static List<string> _flags = new List<string>();

        public bool NoRoll;
        public bool ExclusiveSelf = true;
        public List<string> ExclusiveEffectTypes = new List<string>();
        public List<ItemDrop.ItemData.ItemType> AllowedItemTypes = new List<ItemDrop.ItemData.ItemType>();
        public List<ItemRarity> AllowedRarities = new List<ItemRarity>();
        public List<string> AllowedItemNames = new List<string>();
        public bool ItemHasPhysicalDamage;
        public bool ItemHasElementalDamage;
        public bool ItemUsesDurability;
        public bool ItemHasNegativeMovementSpeedModifier;
        public bool ItemHasBlockPower;
        public bool ItemHasParryPower;
        public bool ItemHasArmor;
        public bool ItemHasBackstabBonus;
        public bool ItemUsesStaminaOnAttack;

        public List<string> CustomFlags;

        public override string ToString()
        {
            _sb.Clear();
            _flags.Clear();

            if (NoRoll) _flags.Add(nameof(NoRoll));
            if (ExclusiveSelf) _flags.Add(nameof(ExclusiveSelf));
            if (ItemHasPhysicalDamage) _flags.Add(nameof(ItemHasPhysicalDamage));
            if (ItemHasElementalDamage) _flags.Add(nameof(ItemHasElementalDamage));
            if (ItemUsesDurability) _flags.Add(nameof(ItemUsesDurability));
            if (ItemHasNegativeMovementSpeedModifier) _flags.Add(nameof(ItemHasNegativeMovementSpeedModifier));
            if (ItemHasBlockPower) _flags.Add(nameof(ItemHasBlockPower));
            if (ItemHasParryPower) _flags.Add(nameof(ItemHasParryPower));
            if (ItemHasArmor) _flags.Add(nameof(ItemHasArmor));
            if (ItemHasBackstabBonus) _flags.Add(nameof(ItemHasBackstabBonus));
            if (ItemUsesStaminaOnAttack) _flags.Add(nameof(ItemUsesStaminaOnAttack));

            if (_flags.Count > 0)
            {
                _sb.AppendLine($"> > **Flags:** `{string.Join(", ", _flags)}`");
            }

            if (ExclusiveEffectTypes != null && ExclusiveEffectTypes.Count > 0)
            {
                _sb.AppendLine($"> > **ExclusiveEffectTypes:** `{string.Join(", ", ExclusiveEffectTypes)}`");
            }

            if (AllowedItemTypes != null && AllowedItemTypes.Count > 0)
            {
                _sb.AppendLine($"> > **AllowedItemTypes:** `{string.Join(", ", AllowedItemTypes)}`");
            }

            if (AllowedRarities != null && AllowedRarities.Count > 0)
            {
                _sb.AppendLine($"> > **AllowedRarities:** `{string.Join(", ", AllowedRarities)}`");
            }

            if (AllowedItemNames != null && AllowedItemNames.Count > 0)
            {
                _sb.AppendLine($"> > **AllowedItemNames:** `{string.Join(", ", AllowedItemNames)}`");
            }

            if (CustomFlags != null && CustomFlags.Count > 0)
            {
                _sb.AppendLine($"> > **CustomFlags:** `{string.Join(", ", CustomFlags)}`");
            }

            return _sb.ToString();
        }
    }

    public class MagicItemEffectDefinition
    {
        public class ValueDef
        {
            public float MinValue;
            public float MaxValue;
            public float Increment;
        }

        public class ValuesPerRarityDef
        {
            public ValueDef Magic;
            public ValueDef Rare;
            public ValueDef Epic;
            public ValueDef Legendary;
        }

        public string Type { get; set; }

        public string DisplayText = "";
        public MagicItemEffectRequirements Requirements = new MagicItemEffectRequirements();
        public ValuesPerRarityDef ValuesPerRarity = new ValuesPerRarityDef();
        public float SelectionWeight = 1;
        public string Comment;

        public List<ItemDrop.ItemData.ItemType> GetAllowedItemTypes()
        {
            return Requirements?.AllowedItemTypes ?? new List<ItemDrop.ItemData.ItemType>();
        }

        public bool CheckRequirements(ExtendedItemData itemData, MagicItem magicItem)
        {
            if (Requirements == null)
            {
                return true;
            }

            if (Requirements.NoRoll)
            {
                return false;
            }

            if (Requirements.ExclusiveSelf && magicItem.HasEffect(Type))
            {
                return false;
            }

            if (Requirements.ExclusiveEffectTypes?.Count > 0 && magicItem.HasAnyEffect(Requirements.ExclusiveEffectTypes))
            {
                return false;
            }

            if (Requirements.AllowedItemTypes?.Count > 0 && !Requirements.AllowedItemTypes.Contains(itemData.m_shared.m_itemType))
            {
                return false;
            }

            if (Requirements.AllowedRarities?.Count > 0 && !Requirements.AllowedRarities.Contains(magicItem.Rarity))
            {
                return false;
            }

            if (Requirements.AllowedItemNames?.Count > 0 && !Requirements.AllowedItemNames.Contains(itemData.m_shared.m_name))
            {
                return false;
            }

            if (Requirements.ItemHasPhysicalDamage && itemData.m_shared.m_damages.GetTotalPhysicalDamage() <= 0)
            {
                return false;
            }

            if (Requirements.ItemHasElementalDamage && itemData.m_shared.m_damages.GetTotalElementalDamage() <= 0)
            {
                return false;
            }

            if (Requirements.ItemUsesDurability && !itemData.m_shared.m_useDurability)
            {
                return false;
            }

            if (Requirements.ItemHasNegativeMovementSpeedModifier && itemData.m_shared.m_movementModifier >= 0)
            {
                return false;
            }

            if (Requirements.ItemHasBlockPower && itemData.m_shared.m_blockPower <= 0)
            {
                return false;
            }

            if (Requirements.ItemHasParryPower && itemData.m_shared.m_deflectionForce <= 0)
            {
                return false;
            }

            if (Requirements.ItemHasArmor && itemData.m_shared.m_armor <= 0)
            {
                return false;
            }

            if (Requirements.ItemHasBackstabBonus && itemData.m_shared.m_backstabBonus <= 0)
            {
                return false;
            }

            if (Requirements.ItemUsesStaminaOnAttack && itemData.m_shared.m_attack.m_attackStamina <= 0 && itemData.m_shared.m_secondaryAttack.m_attackStamina <= 0)
            {
                return false;
            }

            return true;
        }

        public bool HasRarityValues()
        {
            return ValuesPerRarity.Fine != null && ValuesPerRarity.Masterwork != null && ValuesPerRarity.Rare != null && ValuesPerRarity.Exotic != null && ValuesPerRarity.Legendary != null && ValuesPerRarity.Ascended != null;
        }

        public ValueDef GetValuesForRarity(ItemRarity itemRarity)
        {
            switch (itemRarity)
            {
                case ItemRarity.Fine: return ValuesPerRarity.Fine;
				case ItemRarity.Masterwork: return ValuesPerRarity.Masterwork;
                case ItemRarity.Rare: return ValuesPerRarity.Rare;
                case ItemRarity.Exotic: return ValuesPerRarity.Exotic;
                case ItemRarity.Legendary: return ValuesPerRarity.Legendary;
				case ItemRarity.Ascended: return ValuesPerRarity.Ascended;
                default:
                    throw new ArgumentOutOfRangeException(nameof(itemRarity), itemRarity, null);
            }
        }
    }

    public class MagicItemEffectsList
    {
        public List<MagicItemEffectDefinition> MagicItemEffects = new List<MagicItemEffectDefinition>();
    }

    public static partial class MagicItemEffectDefinitions
    {
        private static readonly List<ItemDrop.ItemData.ItemType> Weapons = new List<ItemDrop.ItemData.ItemType>()
        {
            ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeapon, ItemDrop.ItemData.ItemType.Bow, ItemDrop.ItemData.ItemType.Torch
        };
        private static readonly List<ItemDrop.ItemData.ItemType> Shields = new List<ItemDrop.ItemData.ItemType>()
        {
            ItemDrop.ItemData.ItemType.Shield
        };
        private static readonly List<ItemDrop.ItemData.ItemType> Armor = new List<ItemDrop.ItemData.ItemType>()
        {
            ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs, ItemDrop.ItemData.ItemType.Shoulder, ItemDrop.ItemData.ItemType.Utility
        };
        private static readonly List<ItemDrop.ItemData.ItemType> Tools = new List<ItemDrop.ItemData.ItemType>()
        {
            ItemDrop.ItemData.ItemType.Tool
        };

        public static readonly Dictionary<string, MagicItemEffectDefinition> AllDefinitions = new Dictionary<string, MagicItemEffectDefinition>();
        public static event Action OnSetupMagicItemEffectDefinitions;

        public static void Initialize(MagicItemEffectsList config)
        {
            foreach (var magicItemEffectDefinition in config.MagicItemEffects)
            {
                Add(magicItemEffectDefinition);
            }
            OnSetupMagicItemEffectDefinitions?.Invoke();
        }

        public static void Add(MagicItemEffectDefinition effectDef)
        {
            if (AllDefinitions.ContainsKey(effectDef.Type))
            {
                Debug.LogWarning($"Removed previously existing magic effect type: {effectDef.Type}");
                AllDefinitions.Remove(effectDef.Type);
            }

            Debug.Log($"Added MagicItemEffect: {effectDef.Type}");
            AllDefinitions.Add(effectDef.Type, effectDef);
        }

        public static MagicItemEffectDefinition Get(string type)
        {
            AllDefinitions.TryGetValue(type, out MagicItemEffectDefinition effectDef);
            return effectDef;
        }

        public static List<MagicItemEffectDefinition> GetAvailableEffects(ExtendedItemData itemData, MagicItem magicItem)
        {
            return AllDefinitions.Values.Where(x => x.CheckRequirements(itemData, magicItem)).ToList();
        }
    }
}
