using UnityEngine;

namespace NightWatch.Items
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "NightWatch/Items/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Basic")]
        [SerializeField] private string itemName = "New Item";
        [SerializeField] private ItemType itemType = ItemType.Tool;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject handPrefab;

        [Header("Values")]
        [SerializeField] [Min(1)] private int amountValue = 1;

        public string ItemName => string.IsNullOrWhiteSpace(itemName) ? name : itemName;
        public ItemType ItemType => itemType;
        public Sprite Icon => icon;
        public GameObject HandPrefab => handPrefab;
        public int AmountValue => Mathf.Max(1, amountValue);

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = name;
            }

            if (amountValue < 1)
            {
                amountValue = 1;
            }
        }
    }
}
