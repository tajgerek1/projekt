using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightWatch.Items
{
    [DisallowMultipleComponent]
    public sealed class InventoryController : MonoBehaviour
    {
        [Serializable]
        public sealed class InventoryEntry
        {
            public ItemDefinition item;
            public int amount;
        }

        [SerializeField] private List<InventoryEntry> items = new List<InventoryEntry>();

        public IReadOnlyList<InventoryEntry> Items => items;

        public void AddItem(ItemDefinition itemDefinition, int amount = 1)
        {
            if (itemDefinition == null)
            {
                Debug.LogWarning("[InventoryController] Cannot add null item.", this);
                return;
            }

            int finalAmount = Mathf.Max(1, amount);

            InventoryEntry existing = null;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].item == itemDefinition)
                {
                    existing = items[i];
                    break;
                }
            }

            if (existing == null)
            {
                existing = new InventoryEntry
                {
                    item = itemDefinition,
                    amount = 0
                };
                items.Add(existing);
            }

            existing.amount += finalAmount;
            Debug.Log($"[InventoryController] Added '{itemDefinition.ItemName}' x{finalAmount}. Total={existing.amount}.", this);
        }

        public bool HasItem(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                InventoryEntry entry = items[i];
                if (entry != null && entry.item == itemDefinition && entry.amount > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
