using UnityEngine;

namespace NightWatch.Items
{
    [DisallowMultipleComponent]
    public sealed class Wallet : MonoBehaviour
    {
        [SerializeField] [Min(0)] private int money;

        public int Money => money;

        public void AddMoney(int amount)
        {
            int finalAmount = Mathf.Max(0, amount);
            if (finalAmount <= 0)
            {
                return;
            }

            money += finalAmount;
            Debug.Log($"[Wallet] Added +{finalAmount}. Current money={money}.", this);
        }
    }
}
