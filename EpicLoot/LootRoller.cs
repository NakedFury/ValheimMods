using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using ExtendedItemDataFramework;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace EpicLoot
{
    public static class LootRoller
    {
        public static LootConfig Config;
        public static readonly Dictionary<string, LootItemSet> ItemSets = new Dictionary<string, LootItemSet>();
        public static readonly Dictionary<string, List<LootTable>> LootTables = new Dictionary<string, List<LootTable>>();

        public static event Action<ExtendedItemData, MagicItem> MagicItemGenerated;

        private static WeightedRandomCollection<int[]> _weightedDropCountTable;
        private static WeightedRandomCollection<LootDrop> _weightedLootTable;
        private static WeightedRandomCollection<MagicItemEffectDefinition> _weightedEffectTable;
        private static WeightedRandomCollection<KeyValuePair<int, int>> _weightedEffectCountTable;
        private static WeightedRandomCollection<KeyValuePair<ItemRarity, int>> _weightedRarityTable;

        public static void Initialize(LootConfig lootConfig)
        {
            Config = lootConfig;

            var random = new System.Random();
            _weightedDropCountTable = new WeightedRandomCollection<int[]>(random);
            _weightedLootTable = new WeightedRandomCollection<LootDrop>(random);
            _weightedEffectTable = new WeightedRandomCollection<MagicItemEffectDefinition>(random);
            _weightedEffectCountTable = new WeightedRandomCollection<KeyValuePair<int, int>>(random);
            _weightedRarityTable = new WeightedRandomCollection<KeyValuePair<ItemRarity, int>>(random);

            LootTables.Clear();
            AddItemSets(lootConfig.ItemSets);
            AddLootTables(lootConfig.LootTables);
        }

        private static void AddItemSets([NotNull] IEnumerable<LootItemSet> itemSets)
        {
            foreach (var itemSet in itemSets)
            {
                if (string.IsNullOrEmpty(itemSet.Name))
                {
                    Debug.LogError($"Tried to add ItemSet with no name!");
                    continue;
                }

                if (!ItemSets.ContainsKey(itemSet.Name))
                {
                    Debug.Log($"Added ItemSet: {itemSet.Name}");
                    ItemSets.Add(itemSet.Name, itemSet);
                }
                else
                {
                    Debug.LogError($"Tried to add ItemSet {itemSet.Name}, but it already exists!");
                }
            }
        }

        public static void AddLootTables([NotNull] IEnumerable<LootTable> lootTables)
        {
            foreach (var lootTable in lootTables)
            {
                AddLootTable(lootTable);
            }
        }

        public static void AddLootTable([NotNull] LootTable lootTable)
        {
            var key = lootTable.Object;
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("Loot table missing Object name!");
                return;
            }

            Debug.Log($"Added LootTable: {key}");
            if (!LootTables.ContainsKey(key))
            {
                LootTables.Add(key, new List<LootTable>());
            }

            LootTables[key].Add(lootTable);
        }

        public static List<GameObject> RollLootTableAndSpawnObjects(List<LootTable> lootTables, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTableInternal(lootTables, level, objectName, dropPoint, true);
        }

        public static List<GameObject> RollLootTableAndSpawnObjects(LootTable lootTable, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTableInternal(lootTable, level, objectName, dropPoint, true);
        }

        public static List<ItemDrop.ItemData> RollLootTable(List<LootTable> lootTables, int level, string objectName, Vector3 dropPoint)
        {
            var results = new List<ItemDrop.ItemData>();
            var gameObjects = RollLootTableInternal(lootTables, level, objectName, dropPoint, false);
            foreach (var itemObject in gameObjects)
            {
                results.Add(itemObject.GetComponent<ItemDrop>().m_itemData.Clone());
                Object.Destroy(itemObject);
            }

            return results;
        }

        public static List<ItemDrop.ItemData> RollLootTable(LootTable lootTable, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTable(new List<LootTable> { lootTable }, level, objectName, dropPoint);
        }

        private static List<GameObject> RollLootTableInternal(IEnumerable<LootTable> lootTables, int level, string objectName, Vector3 dropPoint, bool initializeObject)
        {
            var results = new List<GameObject>();
            foreach (var lootTable in lootTables)
            {
                results.AddRange(RollLootTableInternal(lootTable, level, objectName, dropPoint, initializeObject));
            }
            return results;
        }

        private static List<GameObject> RollLootTableInternal(LootTable lootTable, int level, string objectName, Vector3 dropPoint, bool initializeObject)
        {
            var results = new List<GameObject>();
            if (lootTable == null || level <= 0 || string.IsNullOrEmpty(objectName))
            {
                return results;
            }

            var drops = GetDropsForLevel(lootTable, level);
            if (ArrayUtils.IsNullOrEmpty(drops))
            {
                return results;
            }

            _weightedDropCountTable.Setup(drops, dropPair => dropPair.Length == 2 ? dropPair[1] : 1);
            var dropCountRollResult = _weightedDropCountTable.Roll();
            var dropCount = dropCountRollResult != null && dropCountRollResult.Length >= 1 ? dropCountRollResult[0] : 0;
            if (dropCount == 0)
            {
                return results;
            }

            var loot = GetLootForLevel(lootTable, level);
            _weightedLootTable.Setup(loot, x => x.Weight);
            var selectedDrops = _weightedLootTable.Roll(dropCount);

            foreach (var ld in selectedDrops)
            {
                var lootDrop = ResolveLootDrop(ld, ld.Rarity);

                var itemPrefab = ObjectDB.instance.GetItemPrefab(lootDrop.Item);
                if (itemPrefab == null)
                {
                    Debug.LogError($"Tried to spawn loot ({lootDrop.Item}) for ({objectName}), but the item prefab was not found!");
                    continue;
                }

                var randomRotation = Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f);
                ZNetView.m_forceDisableInit = !initializeObject;
                var item = Object.Instantiate(itemPrefab, dropPoint, randomRotation);
                ZNetView.m_forceDisableInit = false;
                var itemDrop = item.GetComponent<ItemDrop>();
                if (EpicLoot.CanBeMagicItem(itemDrop.m_itemData) && !ArrayUtils.IsNullOrEmpty(lootDrop.Rarity))
                {
                    var itemData = new ExtendedItemData(itemDrop.m_itemData);
                    var magicItemComponent = itemData.AddComponent<MagicItemComponent>();
                    var magicItem = RollMagicItem(lootDrop, itemData);
                    magicItemComponent.SetMagicItem(magicItem);

                    itemDrop.m_itemData = itemData;
                    InitializeMagicItem(itemData);
                    MagicItemGenerated?.Invoke(itemData, magicItem);
                }

                results.Add(item);
            }

            return results;
        }

        private static LootDrop ResolveLootDrop(LootDrop lootDrop, int[] rarityOverride)
        {
            var result = new LootDrop { Item = lootDrop.Item, Rarity = lootDrop.Rarity, Weight = lootDrop.Weight };
            var needsResolve = true;
            while (needsResolve)
            {
                if (ItemSets.TryGetValue(result.Item, out LootItemSet itemSet))
                {
                    if (itemSet.Loot.Length == 0)
                    {
                        Debug.LogError($"Tried to roll using ItemSet ({itemSet.Name}) but its loot list was empty!");
                    }
                    _weightedLootTable.Setup(itemSet.Loot, x => x.Weight);
                    result = _weightedLootTable.Roll();
                    if (!ArrayUtils.IsNullOrEmpty(rarityOverride))
                    {
                        result.Rarity = rarityOverride;
                    }
                }
                else if (IsLootTableRefence(result.Item, out LootDrop[] lootList))
                {
                    if (lootList.Length == 0)
                    {
                        Debug.LogError($"Tried to roll using loot table reference ({result.Item}) but its loot list was empty!");
                    }
                    _weightedLootTable.Setup(lootList, x => x.Weight);
                    result = _weightedLootTable.Roll();
                    if (ArrayUtils.IsNullOrEmpty(rarityOverride))
                    {
                        result.Rarity = rarityOverride;
                    }
                }
                else
                {
                    needsResolve = false;
                }
            }

            return result;
        }

        private static bool IsLootTableRefence(string lootDropItem, out LootDrop[] lootList)
        {
            lootList = null;
            var parts = lootDropItem.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            var objectName = parts[0];
            var levelText = parts[1];
            if (!int.TryParse(levelText, out var level))
            {
                Debug.LogError($"Tried to get a loot table reference from '{lootDropItem}' but could not parse the level value ({levelText})!");
                return false;
            }

            if (LootTables.ContainsKey(objectName))
            {
                var lootTable = LootTables[objectName].FirstOrDefault();
                if (lootTable != null)
                {
                    lootList = GetLootForLevel(lootTable, level);
                    return true;
                }

                Debug.LogError($"UNLIKELY: LootTables contains entry for {objectName} but no valid loot tables! Weird!");
            }

            return false;
        }

        public static MagicItem RollMagicItem(LootDrop lootDrop, ExtendedItemData baseItem)
        {
            var rarity = RollItemRarity(lootDrop);
            return RollMagicItem(rarity, baseItem);
        }

        public static MagicItem RollMagicItem(ItemRarity rarity, ExtendedItemData baseItem)
        {
            var magicItem = new MagicItem { Rarity = rarity };

            var effectCount = RollEffectCountPerRarity(magicItem.Rarity);
            for (int i = 0; i < effectCount; i++)
            {
                var availableEffects = MagicItemEffectDefinitions.GetAvailableEffects(baseItem, magicItem);
                if (availableEffects.Count == 0)
                {
                    Debug.LogWarning($"Tried to add more effects to magic item ({baseItem.m_shared.m_name}) but there were no more available effects. " +
                                     $"Current Effects: {(string.Join(", ", magicItem.Effects.Select(x => x.EffectType.ToString())))}");
                    break;
                }

                _weightedEffectTable.Setup(availableEffects, x => x.SelectionWeight);
                var effectDef = _weightedEffectTable.Roll();

                var effect = RollEffect(effectDef, magicItem.Rarity);
                magicItem.Effects.Add(effect);
            }

            return magicItem;
        }

        private static void InitializeMagicItem(ExtendedItemData baseItem)
        {
            if (baseItem.m_shared.m_useDurability)
            {
                baseItem.m_durability = Random.Range(0.2f, 1.0f) * baseItem.GetMaxDurability();
            }
        }

        public static int RollEffectCountPerRarity(ItemRarity rarity)
        {
            var countPercents = GetEffectCountsPerRarity(rarity);
            _weightedEffectCountTable.Setup(countPercents, x => x.Value);
            return _weightedEffectCountTable.Roll().Key;
        }

        public static List<KeyValuePair<int, int>> GetEffectCountsPerRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Fine:
                    return Config.MagicEffectsCount.Fine.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
				case ItemRarity.Masterwork:
                    return Config.MagicEffectsCount.Masterwork.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Rare:
                    return Config.MagicEffectsCount.Rare.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Exotic:
                    return Config.MagicEffectsCount.Exotic.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Legendary:
                    return Config.MagicEffectsCount.Legendary.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
				case ItemRarity.Ascended:
                    return Config.AscendedEffectsCount.Ascended.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                default:
                    throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null);
            }
        }

        public static MagicItemEffect RollEffect(MagicItemEffectDefinition effectDef, ItemRarity itemRarity)
        {
            float value = 0;
            var valuesDef = effectDef.GetValuesForRarity(itemRarity);
            if (valuesDef != null)
            {
                value = valuesDef.MinValue;
                if (valuesDef.Increment != 0)
                {
                    var incrementCount = (int)((valuesDef.MaxValue - valuesDef.MinValue) / valuesDef.Increment);
                    value = valuesDef.MinValue + (Random.Range(0, incrementCount + 1) * valuesDef.Increment);
                }
            }

            return new MagicItemEffect()
            {
                EffectType = effectDef.Type,
                EffectValue = value
            };
        }

        public static ItemRarity RollItemRarity(LootDrop lootDrop)
        {
            if (lootDrop.Rarity == null || lootDrop.Rarity.Length == 0)
            {
                return ItemRarity.Magic;
            }

            Dictionary<ItemRarity, int> rarityWeights = new Dictionary<ItemRarity, int>()
            {
                { ItemRarity.Fine, lootDrop.Rarity.Length >= 1 ? lootDrop.Rarity[0] : 0 },
				{ ItemRarity.Masterwork, lootDrop.Rarity.Length >= 1 ? lootDrop.Rarity[1] : 0 },
                { ItemRarity.Rare, lootDrop.Rarity.Length >= 2 ? lootDrop.Rarity[1] : 0 },
                { ItemRarity.Exotic, lootDrop.Rarity.Length >= 3 ? lootDrop.Rarity[2] : 0 },
                { ItemRarity.Legendary, lootDrop.Rarity.Length >= 4 ? lootDrop.Rarity[3] : 0 },
				{ ItemRarity.Ascended, lootDrop.Rarity.Length >= 4 ? lootDrop.Rarity[3] : 0 }
            };

            _weightedRarityTable.Setup(rarityWeights, x => x.Value);
            return _weightedRarityTable.Roll().Key;
        }

        public static List<LootTable> GetLootTable(string objectName)
        {
            var results = new List<LootTable>();
            if (LootTables.TryGetValue(objectName, out List<LootTable> lootTables))
            {
                foreach (var lootTable in lootTables)
                {
                    results.Add(lootTable);
                }
            }
            return results;
        }

        public static int[][] GetDropsForLevel([NotNull] LootTable lootTable, int level, bool useNextHighestIfNotPresent = true)
        {
            if (level == 1 && !ArrayUtils.IsNullOrEmpty(lootTable.Drops))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops'");
                }
                return lootTable.Drops;
            }

            if (level == 2 && !ArrayUtils.IsNullOrEmpty(lootTable.Drops2))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops{level}'");
                }
                return lootTable.Drops2;
            }

            if (level == 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Drops3))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops{level}'");
                }
                return lootTable.Drops3;
            }

            for (var lvl = level; lvl >= 1; --lvl)
            {
                var found = lootTable.LeveledLoot.Find(x => x.Level == lvl);
                if (found != null && !ArrayUtils.IsNullOrEmpty(found.Drops))
                {
                    return found.Drops;
                }

                if (!useNextHighestIfNotPresent)
                {
                    return null;
                }
            }

            Debug.LogError($"Could not find any leveled drops for ({lootTable.Object} lvl {level}), but a loot table exists for this object!");
            return null;
        }

        public static LootDrop[] GetLootForLevel([NotNull] LootTable lootTable, int level, bool useNextHighestIfNotPresent = true)
        {
            if (level == 1 && !ArrayUtils.IsNullOrEmpty(lootTable.Loot))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot'");
                }
                return lootTable.Loot.ToArray();
            }

            if (level == 2 && !ArrayUtils.IsNullOrEmpty(lootTable.Loot2))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot{level}'");
                }
                return lootTable.Loot2.ToArray();
            }

            if (level == 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Loot3))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    Debug.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot{level}'");
                }
                return lootTable.Loot3.ToArray();
            }

            for (var lvl = level; lvl >= 1; --lvl)
            {
                var found = lootTable.LeveledLoot.Find(x => x.Level == lvl);
                if (found != null && !ArrayUtils.IsNullOrEmpty(found.Loot))
                {
                    return found.Loot;
                }

                if (!useNextHighestIfNotPresent)
                {
                    return null;
                }
            }

            Debug.LogError($"Could not find any leveled loot for ({lootTable.Object} lvl {level}), but a loot table exists for this object!");
            return null;
        }
    }
}
