using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// A shop where affordability drives whether a buy button is interactable.
    /// </summary>
    /// <remarks>
    /// The pattern that makes the kit worth having in a real game loop: game state
    /// (<see cref="_coins"/>) sets <c>interactable</c>, and the button's visual state, its sound and
    /// its haptic all follow from that one assignment. Nothing here talks to the animation system.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoShop : MonoBehaviour
    {
        [Serializable]
        private struct Offer
        {
            public EnhancedButton button;
            public Text priceLabel;
            public int price;
        }

        [SerializeField] private List<Offer> m_Offers = new List<Offer>();
        [SerializeField] private Text m_CoinLabel;
        [SerializeField] private Text m_Readout;
        [SerializeField] private EnhancedButton m_AddCoins;
        [SerializeField] private int m_StartingCoins = 150;

        private int _coins;

        private void Start()
        {
            _coins = m_StartingCoins;

            for (int i = 0; i < m_Offers.Count; i++)
            {
                Offer offer = m_Offers[i];
                if (offer.button == null) continue;

                if (offer.priceLabel != null) offer.priceLabel.text = $"{offer.price} coins";

                int index = i;
                offer.button.onClick.AddListener(() => Buy(index));

                // Rejected fires when the button refuses the click, which is where the "can't
                // afford" message belongs — onClick never runs for a non-interactable button.
                offer.button.Rejected += _ =>
                    Report($"Not enough coins — item {index + 1} costs {m_Offers[index].price}.");
            }

            if (m_AddCoins != null) m_AddCoins.onClick.AddListener(AddCoins);

            Refresh();
            Report("Spend down past 100 and watch the buy buttons disable themselves.");
        }

        private void Buy(int index)
        {
            Offer offer = m_Offers[index];
            if (offer.button == null || !offer.button.interactable) return;

            _coins -= offer.price;
            Refresh();
            Report($"Bought item {index + 1} for {offer.price}.");
        }

        private void AddCoins()
        {
            _coins += 100;
            Refresh();
            Report("Added 100 coins. Anything newly affordable animated back to Normal.");
        }

        /// <summary>One assignment per offer; every visual and audible consequence follows from it.</summary>
        private void Refresh()
        {
            if (m_CoinLabel != null) m_CoinLabel.text = $"{_coins} coins";

            foreach (Offer offer in m_Offers)
                if (offer.button != null)
                    offer.button.interactable = _coins >= offer.price;
        }

        private void Report(string message)
        {
            if (m_Readout != null) m_Readout.text = message;
        }
    }
}
