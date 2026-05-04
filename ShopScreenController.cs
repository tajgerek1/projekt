using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NightWatch.Foundation;

namespace NightWatch.Ui
{
    [DisallowMultipleComponent]
    public sealed class ShopScreenController : MonoBehaviour
    {
        private static ShopScreenController activeInstance;
        public static ShopScreenController ActiveInstance => activeInstance;

        [Serializable]
        private sealed class ShopItemEntry
        {
            [Header("Data")]
            public string itemId = "item_1";
            public string displayName = "ITEM";
            [TextArea(2, 4)] public string description = "Opis przedmiotu";
            [Min(0)] public int price = 100;
            public Sprite icon;

            [Header("UI References")]
            public Image iconImage;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI descriptionText;
            public TextMeshProUGUI priceText;
            public TextMeshProUGUI stateText;
            public Button buyButton;

            [Header("Optional Gameplay Hook")]
            public bool selectToolAfterPurchase;
            public ToolType toolToSelect = ToolType.Flashlight;
        }

        [Header("References")]
        [SerializeField] private GameObject shopRoot;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private WalletManager walletManager;
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private InteractionPromptUI interactionPromptUI;
        [SerializeField] private ToolSelectionController toolSelectionController;
        [SerializeField] private PlayerInteractor playerInteractor;

        [Header("Shop Items")]
        [SerializeField] private ShopItemEntry[] shopItems = Array.Empty<ShopItemEntry>();

        [Header("Shop Labels")]
        [SerializeField] private string moneyPrefix = "$";
        [SerializeField] private string buyStateText = "KUP";
        [SerializeField] private string boughtStateText = "KUPIONE";
        [SerializeField] private string noMoneyStateText = "BRAK PIENIEDZY";
        [SerializeField] private TextMeshProUGUI purchaseFeedbackText;
        [SerializeField] private string notEnoughMoneyFeedbackText = "Za malo pieniedzy.";
        [SerializeField] private string purchasedFeedbackPrefix = "Kupiono: ";

        [Header("Persistence")]
        [SerializeField] private bool persistPurchaseState = true;
        [SerializeField] private bool forcePersistPurchaseState = true;
        [SerializeField] private string purchaseKeyPrefix = "shop_item_";

        [Header("Timed Task Markers")]
        [SerializeField] private string taskMarkerItemId = "item_1";
        [SerializeField] [Min(1f)] private float taskMarkerDurationMinutes = 120f;
        [SerializeField] private string timedItemActiveStateText = "AKTYWNE";

        [Header("Player Lock While Shop Is Open")]
        [SerializeField] private MonoBehaviour[] componentsToDisableWhileOpen = new MonoBehaviour[0];

        [Header("Input")]
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private bool forceCursorUnlockedWhileOpen = true;
        [SerializeField] private bool forceHideInteractionPromptWhileOpen = true;

        private readonly HashSet<string> purchasedItemKeys = new HashSet<string>();

        public bool IsOpen { get; private set; }

        private bool hasInitialized;
        private float taskMarkerExpiresAtShiftMinute = -1f;
        private bool taskMarkerEffectWasActive;

        private void Awake()
        {
            if (!enabled)
            {
                return;
            }

            InitializeController();
        }

        private void InitializeController()
        {
            if (hasInitialized)
            {
                return;
            }

            if (activeInstance != null && activeInstance != this)
            {
                if (activeInstance.isActiveAndEnabled)
                {
                    Debug.LogWarning("[ShopScreenController] Multiple ShopScreenController instances detected. Disabling duplicate instance.", this);
                    enabled = false;
                    return;
                }

                activeInstance = null;
            }

            activeInstance = this;
            hasInitialized = true;

            if (playerInteractor == null)
            {
                playerInteractor = FindFirstObjectByType<PlayerInteractor>();
            }

            if (timeManager == null)
            {
                timeManager = FindFirstObjectByType<TimeManager>();
            }

            if (interactionPromptUI == null)
            {
                interactionPromptUI = FindFirstObjectByType<InteractionPromptUI>();
            }

            if (shopRoot != null)
            {
                shopRoot.SetActive(false);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(CloseShop);
            }

            BindItemButtons();
            LoadPurchasedItems();
            RefreshAllItemVisuals();
            SetFeedback(string.Empty);
        }

        private void OnEnable()
        {
            InitializeController();
            if (!hasInitialized || !enabled)
            {
                return;
            }

            if (walletManager != null)
            {
                walletManager.OnWalletChanged += HandleWalletChanged;
                HandleWalletChanged(walletManager.CurrentBalance);
            }
        }

        private void OnDisable()
        {
            if (walletManager != null)
            {
                walletManager.OnWalletChanged -= HandleWalletChanged;
            }

            if (activeInstance == this)
            {
                activeInstance = null;
            }

            if (IsOpen)
            {
                PlayerInteractor.GlobalInteractionBlocked = false;
            }
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(CloseShop);
            }
        }

        private void Update()
        {
            RefreshTimedItemStateIfNeeded();

            if (!IsOpen)
            {
                return;
            }

            if (forceHideInteractionPromptWhileOpen && interactionPromptUI != null)
            {
                interactionPromptUI.Hide();
            }

            if (forceCursorUnlockedWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Input.GetKeyDown(closeKey))
            {
                CloseShop();
            }
        }

        public void OpenShop()
        {
            if (!isActiveAndEnabled && activeInstance != null && activeInstance != this)
            {
                activeInstance.OpenShop();
                return;
            }

            InitializeController();

            if (IsOpen)
            {
                return;
            }

            if (shopRoot == null)
            {
                Debug.LogError("[ShopScreenController] Missing Shop Root reference.", this);
                return;
            }

            IsOpen = true;
            PlayerInteractor.GlobalInteractionBlocked = true;
            EnsureEventSystemExists();

            if (interactionPromptUI != null)
            {
                interactionPromptUI.Hide();
            }
            HideAllInteractionPrompts();

            if (playerInteractor != null)
            {
                playerInteractor.enabled = false;
            }

            shopRoot.SetActive(true);
            shopRoot.transform.SetAsLastSibling();
            SetShopCanvasInteraction(true);
            SetPlayerGameplayEnabled(false);
            RefreshMoneyLabel();
            RefreshAllItemVisuals();
            SetFeedback(string.Empty);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (backButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
            }
        }

        public void CloseShop()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            PlayerInteractor.GlobalInteractionBlocked = false;

            if (shopRoot != null)
            {
                shopRoot.SetActive(false);
            }

            if (playerInteractor != null)
            {
                playerInteractor.enabled = true;
            }

            SetPlayerGameplayEnabled(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetFeedback(string.Empty);
        }

        private void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem (Auto)");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
            Debug.LogWarning("[ShopScreenController] Missing EventSystem in scene. Created one automatically.", this);
        }

        private static void HideAllInteractionPrompts()
        {
#if UNITY_2023_1_OR_NEWER
            InteractionPromptUI[] prompts = FindObjectsByType<InteractionPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            InteractionPromptUI[] prompts = FindObjectsOfType<InteractionPromptUI>(true);
#endif
            for (int i = 0; i < prompts.Length; i++)
            {
                if (prompts[i] != null)
                {
                    prompts[i].Hide();
                }
            }
        }

        private void TryBuyItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= shopItems.Length)
            {
                return;
            }

            ShopItemEntry entry = shopItems[itemIndex];
            if (entry == null)
            {
                return;
            }

            string key = GetPurchaseKey(itemIndex, entry);
            bool isTimedTaskMarker = IsTaskMarkerItem(itemIndex, entry);

            if (isTimedTaskMarker && IsTaskMarkerEffectActive())
            {
                SetFeedback($"{entry.displayName} jest juz aktywne.");
                return;
            }

            if (!isTimedTaskMarker && purchasedItemKeys.Contains(key))
            {
                SetFeedback($"{entry.displayName} jest juz kupione.");
                return;
            }

            if (walletManager == null)
            {
                Debug.LogError("[ShopScreenController] Cannot buy item: missing WalletManager.", this);
                return;
            }

            int price = Mathf.Max(0, entry.price);
            if (!walletManager.TrySpendMoney(price))
            {
                RefreshItemVisual(itemIndex);
                SetFeedback(notEnoughMoneyFeedbackText);
                Debug.LogWarning($"[ShopScreenController] Not enough money to buy '{entry.displayName}'. Price: {price}, Balance: {walletManager.CurrentBalance}", this);
                return;
            }

            if (isTimedTaskMarker)
            {
                ActivateTaskMarkerEffect();
                ClearPersistedPurchaseKey(key);
            }
            else
            {
                purchasedItemKeys.Add(key);
                if (ShouldPersistPurchaseState)
                {
                    PlayerPrefs.SetInt(key, 1);
                    PlayerPrefs.Save();
                }
            }

            if (entry.selectToolAfterPurchase && toolSelectionController != null)
            {
                toolSelectionController.SetCurrentTool(entry.toolToSelect);
            }

            Debug.Log($"[ShopScreenController] Purchased item '{entry.displayName}' for ${price}.", this);
            SetFeedback($"{purchasedFeedbackPrefix}{entry.displayName}");
            RefreshAllItemVisuals();
        }

        private void BindItemButtons()
        {
            for (int i = 0; i < shopItems.Length; i++)
            {
                ShopItemEntry entry = shopItems[i];
                if (entry == null || entry.buyButton == null)
                {
                    continue;
                }

                int capturedIndex = i;
                entry.buyButton.onClick.RemoveAllListeners();
                entry.buyButton.onClick.AddListener(() => TryBuyItem(capturedIndex));
            }
        }

        private void LoadPurchasedItems()
        {
            purchasedItemKeys.Clear();

            for (int i = 0; i < shopItems.Length; i++)
            {
                ShopItemEntry entry = shopItems[i];
                if (entry == null)
                {
                    continue;
                }

                string key = GetPurchaseKey(i, entry);
                if (IsTaskMarkerItem(i, entry))
                {
                    ClearPersistedPurchaseKey(key);
                    continue;
                }

                bool purchased = ShouldPersistPurchaseState && PlayerPrefs.GetInt(key, 0) == 1;
                if (purchased)
                {
                    purchasedItemKeys.Add(key);
                }
            }
        }

        private string GetPurchaseKey(int index, ShopItemEntry entry)
        {
            string rawId = entry != null ? entry.itemId : string.Empty;
            string safeId = string.IsNullOrWhiteSpace(rawId) ? $"item_{index + 1}" : rawId.Trim();
            safeId = NormalizeItemId(safeId);
            return $"{purchaseKeyPrefix}{safeId}";
        }

        private void SetPlayerGameplayEnabled(bool enabled)
        {
            for (int i = 0; i < componentsToDisableWhileOpen.Length; i++)
            {
                MonoBehaviour component = componentsToDisableWhileOpen[i];
                if (component != null)
                {
                    if (component == this)
                    {
                        Debug.LogWarning("[ShopScreenController] 'componentsToDisableWhileOpen' contains ShopScreenController itself. Skipping this entry.", this);
                        continue;
                    }

                    component.enabled = enabled;
                }
            }
        }

        private void HandleWalletChanged(int value)
        {
            if (!IsOpen)
            {
                return;
            }

            SetMoneyText(value);
            RefreshAllItemVisuals();
        }

        private void RefreshMoneyLabel()
        {
            int money = walletManager != null ? walletManager.CurrentBalance : 0;
            SetMoneyText(money);
        }

        private void SetMoneyText(int value)
        {
            if (moneyText == null)
            {
                return;
            }

            moneyText.text = $"{moneyPrefix}{value}";
        }

        private void SetShopCanvasInteraction(bool enabled)
        {
            if (shopRoot == null)
            {
                return;
            }

            CanvasGroup[] groups = shopRoot.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null)
                {
                    continue;
                }

                group.alpha = enabled ? 1f : 0f;
                group.interactable = enabled;
                group.blocksRaycasts = enabled;
            }
        }

        private void SetFeedback(string message)
        {
            if (purchaseFeedbackText == null)
            {
                return;
            }

            purchaseFeedbackText.text = message;
            purchaseFeedbackText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void RefreshAllItemVisuals()
        {
            for (int i = 0; i < shopItems.Length; i++)
            {
                RefreshItemVisual(i);
            }
        }

        private void RefreshItemVisual(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= shopItems.Length)
            {
                return;
            }

            ShopItemEntry entry = shopItems[itemIndex];
            if (entry == null)
            {
                return;
            }

            if (entry.iconImage != null)
            {
                entry.iconImage.sprite = entry.icon;
                entry.iconImage.enabled = entry.icon != null;
            }

            if (entry.nameText != null)
            {
                entry.nameText.text = entry.displayName;
            }

            if (entry.descriptionText != null)
            {
                entry.descriptionText.text = entry.description;
            }

            int price = Mathf.Max(0, entry.price);
            if (entry.priceText != null)
            {
                entry.priceText.text = $"{moneyPrefix}{price}";
            }

            bool isTimedTaskMarker = IsTaskMarkerItem(itemIndex, entry);
            bool purchased = isTimedTaskMarker ? IsTaskMarkerEffectActive() : purchasedItemKeys.Contains(GetPurchaseKey(itemIndex, entry));
            bool canAfford = walletManager != null && walletManager.CanAfford(price);

            if (entry.stateText != null)
            {
                entry.stateText.text = purchased
                    ? GetPurchasedStateText(isTimedTaskMarker)
                    : (canAfford ? buyStateText : noMoneyStateText);
            }

            if (entry.buyButton != null)
            {
                entry.buyButton.interactable = !purchased;
            }
        }

        public bool IsPurchased(string itemId)
        {
            string safeId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            if (string.IsNullOrEmpty(safeId))
            {
                return false;
            }

            if (IsTaskMarkerItemId(safeId))
            {
                return IsTaskMarkerEffectActive();
            }

            string purchaseKey = $"{purchaseKeyPrefix}{NormalizeItemId(safeId)}";
            return purchasedItemKeys.Contains(purchaseKey) ||
                   (ShouldPersistPurchaseState && PlayerPrefs.GetInt(purchaseKey, 0) == 1);
        }

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || shopItems == null)
            {
                return false;
            }

            string normalizedItemId = NormalizeItemId(itemId);
            for (int i = 0; i < shopItems.Length; i++)
            {
                ShopItemEntry entry = shopItems[i];
                if (entry == null)
                {
                    continue;
                }

                string entryId = string.IsNullOrWhiteSpace(entry.itemId) ? $"item_{i + 1}" : entry.itemId;
                if (NormalizeItemId(entryId) == normalizedItemId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldPersistPurchaseState => persistPurchaseState || forcePersistPurchaseState;

        private void ActivateTaskMarkerEffect()
        {
            float currentShiftMinutes = GetCurrentShiftMinutes();
            taskMarkerExpiresAtShiftMinute = currentShiftMinutes + Mathf.Max(1f, taskMarkerDurationMinutes);
            taskMarkerEffectWasActive = true;
        }

        private bool IsTaskMarkerEffectActive()
        {
            if (taskMarkerExpiresAtShiftMinute <= 0f)
            {
                return false;
            }

            if (timeManager == null)
            {
                timeManager = FindFirstObjectByType<TimeManager>();
            }

            if (timeManager != null && !timeManager.IsShiftRunning)
            {
                return false;
            }

            return GetCurrentShiftMinutes() < taskMarkerExpiresAtShiftMinute;
        }

        private float GetCurrentShiftMinutes()
        {
            if (timeManager == null)
            {
                timeManager = FindFirstObjectByType<TimeManager>();
            }

            return timeManager != null ? timeManager.ElapsedShiftMinutes : 0f;
        }

        private float GetTaskMarkerRemainingMinutes()
        {
            if (!IsTaskMarkerEffectActive())
            {
                return 0f;
            }

            return Mathf.Max(0f, taskMarkerExpiresAtShiftMinute - GetCurrentShiftMinutes());
        }

        private void RefreshTimedItemStateIfNeeded()
        {
            bool isActive = IsTaskMarkerEffectActive();
            if (taskMarkerEffectWasActive == isActive)
            {
                return;
            }

            taskMarkerEffectWasActive = isActive;
            RefreshAllItemVisuals();
        }

        private string GetPurchasedStateText(bool isTimedTaskMarker)
        {
            if (!isTimedTaskMarker)
            {
                return boughtStateText;
            }

            int remainingMinutes = Mathf.CeilToInt(GetTaskMarkerRemainingMinutes());
            return remainingMinutes > 0
                ? $"{timedItemActiveStateText} {remainingMinutes} MIN"
                : buyStateText;
        }

        private bool IsTaskMarkerItem(int itemIndex, ShopItemEntry entry)
        {
            string rawId = entry != null && !string.IsNullOrWhiteSpace(entry.itemId)
                ? entry.itemId
                : $"item_{itemIndex + 1}";

            return IsTaskMarkerItemId(rawId);
        }

        private bool IsTaskMarkerItemId(string itemId)
        {
            return !string.IsNullOrWhiteSpace(taskMarkerItemId) &&
                   NormalizeItemId(itemId) == NormalizeItemId(taskMarkerItemId);
        }

        private static void ClearPersistedPurchaseKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            {
                return;
            }

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private static string NormalizeItemId(string rawId)
        {
            return string.IsNullOrWhiteSpace(rawId) ? string.Empty : rawId.Trim().ToLowerInvariant();
        }
    }
}
