using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NocnaStraz
{
    /// <summary>
    /// Pomocnik do restartu runu bez ryzyka, że coroutine zginie wraz z niszczonym managerem.
    /// </summary>
    public sealed class RestartHelper : MonoBehaviour
    {
        public void Begin(NightGameManager oldManager)
        {
            StartCoroutine(Co(oldManager));
        }

        private IEnumerator Co(NightGameManager oldManager)
        {
            // Poczekaj do końca klatki, żeby UI zdążyło zamknąć kliknięcie przycisku.
            yield return null;

            if (oldManager != null)
                Destroy(oldManager.gameObject);

            // Poczekaj, aż Unity faktycznie usunie obiekty.
            yield return null;

            SceneManager.LoadScene(0);

            // Usuwamy runner (niepotrzebny po restarcie)
            Destroy(gameObject);
        }
    }
}
