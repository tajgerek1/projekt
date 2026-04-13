using UnityEngine;

namespace NightWatch.Items
{
    [DisallowMultipleComponent]
    public sealed class EquippedToolController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private Transform handAnchor;
        [SerializeField] private GameObject currentToolVisual;
        [SerializeField] private ItemDefinition currentEquippedItem;

        public ItemDefinition CurrentEquippedItem => currentEquippedItem;
        public GameObject CurrentToolVisual => currentToolVisual;

        public void Equip(ItemDefinition itemDefinition)
        {
            ClearCurrentToolVisual();
            currentEquippedItem = itemDefinition;

            if (itemDefinition == null)
            {
                return;
            }

            if (itemDefinition.ItemType != ItemType.Tool)
            {
                Debug.LogWarning($"[EquippedToolController] Cannot equip non-tool item '{itemDefinition.ItemName}'.", this);
                return;
            }

            if (handAnchor == null)
            {
                Debug.LogWarning("[EquippedToolController] Missing Hand Anchor reference.", this);
                return;
            }

            ValidateAnchorHierarchy();

            if (itemDefinition.HandPrefab == null)
            {
                Debug.LogWarning($"[EquippedToolController] Tool '{itemDefinition.ItemName}' has no handPrefab.", this);
                return;
            }

            currentToolVisual = Instantiate(itemDefinition.HandPrefab);

            Transform visualTransform = currentToolVisual.transform;
            visualTransform.SetParent(handAnchor, false);
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }

        public void Unequip()
        {
            ClearCurrentToolVisual();
            currentEquippedItem = null;
        }

        private void ClearCurrentToolVisual()
        {
            if (currentToolVisual == null)
            {
                return;
            }

            Destroy(currentToolVisual);
            currentToolVisual = null;
        }

        private void ValidateAnchorHierarchy()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[EquippedToolController] MainCamera not found. Make sure your camera is tagged as MainCamera.", this);
                return;
            }

            if (!handAnchor.IsChildOf(mainCamera.transform))
            {
                Debug.LogWarning("[EquippedToolController] Hand Anchor is not a child of MainCamera. Tool can look unstable on screen.", this);
            }
        }
    }
}
