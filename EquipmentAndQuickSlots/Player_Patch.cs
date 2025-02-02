﻿using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EquipmentAndQuickSlots
{
    [HarmonyPatch(typeof(Player), "Save")]
    public static class Player_Save_Patch
    {
        public static bool Prefix(Player __instance)
        {
            __instance.BeforeSave();
            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "Load")]
    public static class Player_Load_Patch
    {
        public static void Postfix(Player __instance)
        {
            __instance.AfterLoad();
        }
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    public static class Player_Awake_Patch
    {
        public static void Postfix(Player __instance)
        {
            var inv = __instance.m_inventory;
            inv.m_onChanged = null;
            __instance.m_inventory = new ExtendedInventory(__instance, inv.m_name, inv.m_bkg, inv.m_width, inv.m_height);
            __instance.m_inventory.m_onChanged += __instance.OnInventoryChanged;
            __instance.m_inventory.Extended().OverrideAwake();
        }
    }

    //public void CreateTombStone()
    [HarmonyPatch(typeof(Player), "CreateTombStone")]
    public static class Player_CreateTombStone_Patch
    {
        public static void Prefix(Player __instance)
        {
            if (__instance.m_inventory.NrOfItems() == 0)
            {
                return;
            }

            EquipmentSlotHelper.AllowMove = false;
            __instance.UnequipAllItems();

            var allInventories = __instance.GetAllInventories();

            // Modify tombstone prefab
            var totalPossibleSlots = allInventories.Sum(x => x.m_width * x.m_height);
            var width = __instance.m_inventory.m_width;
            var height = (totalPossibleSlots / width) + 1;
            __instance.m_tombstone.GetComponent<Container>().m_width = width;
            __instance.m_tombstone.GetComponent<Container>().m_height = height;

            var gameObject = Object.Instantiate(__instance.m_tombstone, __instance.GetCenterPoint(), __instance.transform.rotation);
            var tombStone = gameObject.GetComponent<TombStone>();
            var playerProfile = Game.instance.GetPlayerProfile();
            var name = playerProfile.GetName();
            var playerId = playerProfile.GetPlayerID();
            tombStone.Setup(name, playerId);

            var container = gameObject.GetComponent<Container>();

            EquipmentAndQuickSlots.LogWarning("== PLAYER DIED ==");
            var containerInventory = container.GetInventory();
            foreach (var inventory in allInventories)
            {
                foreach (var item in inventory.m_inventory)
                {
                    if (containerInventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y) != null)
                    {
                        var newSlot = containerInventory.FindEmptySlot(true);
                        item.m_gridPos = newSlot;
                        EquipmentAndQuickSlots.LogWarning($"Adding item to tombstone [newslot]: {item.m_shared.m_name} ({newSlot})");
                        containerInventory.m_inventory.Add(item);
                    }
                    else
                    {
                        containerInventory.m_inventory.Add(item);
                        EquipmentAndQuickSlots.LogWarning($"Adding item to tombstone [manual ]: {item.m_shared.m_name}");
                    }
                }

                inventory.m_inventory.RemoveAll(item => !item.m_shared.m_questItem && !item.m_equiped);
                inventory.Changed();
                containerInventory.Changed();
            }
            
            // I don't know why I need this here, vanilla doesn't have it
            container.Save();

            EquipmentAndQuickSlots.LogWarning($"Creating tombstone for ({name}) with w:{width} h:{height} (total:{totalPossibleSlots})");
            EquipmentAndQuickSlots.LogWarning($"== Container Inventory ({containerInventory.NrOfItems()}):");
            foreach (var item in containerInventory.m_inventory)
            {
                EquipmentAndQuickSlots.LogWarning($"  - {item.m_shared.m_name} {item.m_stack}");
            }

            EquipmentSlotHelper.AllowMove = true;
        }
    }

    //public bool Interact(Humanoid character, bool hold)
    /*{
        if (hold || this.m_container.GetInventory().NrOfItems() == 0)
        return false;
        if (!this.IsOwner() || !this.EasyFitInInventory(character as Player))
        return this.m_container.Interact(character, false);
        ZLog.Log((object) "Grave should fit in inventory, loot all");
        this.m_container.TakeAll(character);
        return true;
    }*/
    /*[HarmonyPatch(typeof(TombStone), "Interact")]
    public static class Tombstone_Interact_Patch
    {
        public static bool Prefix(TombStone __instance, ref bool __result, Humanoid character, bool hold)
        {
            var containerInventory = __instance.m_container.GetInventory();
            if (hold || containerInventory.NrOfItems() == 0)
            {
                return false;
            }

            EquipmentAndQuickSlots.LogWarning($"Interacting with tombstone for ({character.m_name})");
            EquipmentAndQuickSlots.LogWarning($"== Container Inventory ({containerInventory.NrOfItems()}):");
            foreach (var item in containerInventory.m_inventory)
            {
                EquipmentAndQuickSlots.LogWarning($"  - {item.m_shared.m_name} {item.m_stack}");
            }

            __result = __instance.m_container.Interact(character, false);
            return false;
        }
    }*/
}
