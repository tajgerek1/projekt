using TMPro;
using UnityEngine;

namespace NightWatch.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private CanvasGroup promptGroup;

        private void Awake()
        {
            HidePrompt();
        }

        public void ShowPrompt(string text)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = text;
            SetVisible(true);
        }

        public void HidePrompt()
        {
            if (promptText != null)
            {
                promptText.text = string.Empty;
            }

            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (promptGroup != null)
            {
                promptGroup.alpha = visible ? 1f : 0f;
                promptGroup.interactable = false;
                promptGroup.blocksRaycasts = false;
                return;
            }

            if (promptText != null)
            {
                promptText.gameObject.SetActive(visible);
            }
        }
    }
}
