using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Cysharp.Threading.Tasks;
using Nekoyume.Helper;
using Nekoyume.Model.Item;
using UnityEngine.UI;
using Nekoyume.Game.Controller;
using System.Numerics;
using TMPro;
using System.Linq;
using Nekoyume.ApiClient;
using Nekoyume.State;
using Nekoyume.Model.Mail;
using Nekoyume.L10n;
using Nekoyume.UI.Scroller;

namespace Nekoyume.UI
{
    public class SeasonPassPremiumPopup : PopupWidget
    {
        [SerializeField]
        private GameObject[] isPremiumObj;

        [SerializeField]
        private GameObject[] notPremiumObj;

        [SerializeField]
        private GameObject[] isPremiumPlusObj;

        [SerializeField]
        private GameObject[] notPremiumPlusObj;

        [SerializeField]
        private GameObject premiumPurchaseButtonDisabledObj;

        [SerializeField]
        private GameObject premiumPurchaseButtonPriceObj;

        [SerializeField]
        private GameObject premiumPurchaseButtonLoadingObj;

        [SerializeField]
        private GameObject premiumPlusPurchaseButtonDisabledObj;

        [SerializeField]
        private GameObject premiumPlusPurchaseButtonPriceObj;

        [SerializeField]
        private GameObject premiumPlusPurchaseButtonLoadingObj;

        [SerializeField]
        private BaseItemView[] premiumRewards;

        [SerializeField]
        private BaseItemView[] premiumPlusRewards;

        [SerializeField]
        private TextMeshProUGUI[] premiumPrices;

        [SerializeField]
        private TextMeshProUGUI[] premiumPlusPrices;

        [SerializeField]
        private GameObject[] premiumInfoList;

        [SerializeField]
        private GameObject[] premiumPlusInfoList;

        protected override void Awake()
        {
            base.Awake();
            var seasonPassManager = ApiClients.Instance.SeasonPassServiceManager;
            seasonPassManager.AvatarInfo.Subscribe((seasonPassInfo) => { RefreshIcons(seasonPassInfo); }).AddTo(gameObject);

            var infoKeyIndex = 1;
            foreach (var item in premiumInfoList)
            {
                if (L10nManager.ContainsKey($"SEASONPASS_PREMIUM_INFO_{infoKeyIndex}"))
                {
                    item.SetActive(true);
                    item.GetComponentInChildren<TextMeshProUGUI>().text = L10nManager.Localize($"SEASONPASS_PREMIUM_INFO_{infoKeyIndex}");
                }
                else
                {
                    item.SetActive(false);
                }

                infoKeyIndex++;
            }

            infoKeyIndex = 1;
            foreach (var item in premiumPlusInfoList)
            {
                if (L10nManager.ContainsKey($"SEASONPASS_PREMIUM_PLUS_INFO_{infoKeyIndex}"))
                {
                    item.SetActive(true);
                    item.GetComponentInChildren<TextMeshProUGUI>().text = L10nManager.Localize($"SEASONPASS_PREMIUM_PLUS_INFO_{infoKeyIndex}");
                }
                else
                {
                    item.SetActive(false);
                }

                infoKeyIndex++;
            }
        }

        public override void Show(bool ignoreShowAnimation = false)
        {
            base.Show(ignoreShowAnimation);

            foreach (var item in premiumRewards)
            {
                item.gameObject.SetActive(false);
            }

            foreach (var item in premiumPlusRewards)
            {
                item.gameObject.SetActive(false);
            }

            var seasonPassManager = ApiClients.Instance.SeasonPassServiceManager;
            var iapStoreManager = Game.Game.instance.IAPStoreManager;

            var premiumProductKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}Premium";
            if (iapStoreManager.SeasonPassProduct.TryGetValue(premiumProductKey, out var premiumProduct))
            {
                var index = 0;
                for (var i = 0; i < premiumProduct.FavList.Count && index < premiumRewards.Length; i++, index++)
                {
                    premiumRewards[index].ItemViewSetCurrencyData(premiumProduct.FavList[i].Ticker, premiumProduct.FavList[i].Amount);
                }

                for (var i = 0; i < premiumProduct.FungibleItemList.Count && index < premiumRewards.Length; i++, index++)
                {
                    premiumRewards[index].ItemViewSetItemData(premiumProduct.FungibleItemList[i].SheetItemId, premiumProduct.FungibleItemList[i].Amount);
                    AddToolTip(premiumRewards[index], premiumProduct.FungibleItemList[i].SheetItemId);
                }

                var _puchasingData = iapStoreManager.IAPProducts.First(p => p.definition.id == premiumProduct.Sku());
                if (_puchasingData != null)
                {
                    foreach (var item in premiumPrices)
                    {
                        item.text = MobileShop.GetPrice(_puchasingData.metadata.isoCurrencyCode, _puchasingData.metadata.localizedPrice);
                    }
                }
            }

            var premiumPlusProductKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}PremiumAll";
            if (ApiClients.Instance.SeasonPassServiceManager.AvatarInfo.Value.IsPremium)
            {
                premiumPlusProductKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}Premiumplus";
            }

            if (iapStoreManager.SeasonPassProduct.TryGetValue(premiumPlusProductKey, out var premiumPlusProduct))
            {
                var index = 0;
                for (var i = 0; i < premiumPlusProduct.FavList.Count && index < premiumPlusRewards.Length; i++, index++)
                {
                    premiumPlusRewards[index].ItemViewSetCurrencyData(premiumPlusProduct.FavList[i].Ticker, premiumPlusProduct.FavList[i].Amount);
                }

                for (var i = 0; i < premiumPlusProduct.FungibleItemList.Count && index < premiumPlusRewards.Length; i++, index++)
                {
                    premiumPlusRewards[index].ItemViewSetItemData(premiumPlusProduct.FungibleItemList[i].SheetItemId, premiumPlusProduct.FungibleItemList[i].Amount);
                    AddToolTip(premiumPlusRewards[index], premiumPlusProduct.FungibleItemList[i].SheetItemId);
                }

                var _puchasingData = iapStoreManager.IAPProducts.First(p => p.definition.id == premiumPlusProduct.Sku());
                if (_puchasingData != null)
                {
                    foreach (var item in premiumPlusPrices)
                    {
                        item.text = MobileShop.GetPrice(_puchasingData.metadata.isoCurrencyCode, _puchasingData.metadata.localizedPrice);
                    }
                }
            }
        }

        private void AddToolTip(BaseItemView itemView, int itemId)
        {
            if (itemView.TryGetComponent<SeasonPassPremiumItemView>(out var seasonPassPremiumItemView))
            {
                var itemSheetData = Game.Game.instance.TableSheets.ItemSheet[itemId];
                if (seasonPassPremiumItemView.TooltipButton.onClick.GetPersistentEventCount() < 1)
                {
                    var dummyItem = ItemFactory.CreateItem(itemSheetData, new Cheat.DebugRandom());
                    seasonPassPremiumItemView.TooltipButton.onClick.AddListener(() =>
                    {
                        if (dummyItem == null)
                        {
                            return;
                        }

                        AudioController.PlayClick();
                        var tooltip = ItemTooltip.Find(dummyItem.ItemType);
                        tooltip.Show(dummyItem, string.Empty, false, null);
                    });
                }
            }
        }

        private void RefreshIcons(SeasonPassServiceClient.UserSeasonPassSchema seasonPassInfo)
        {
            if (seasonPassInfo == null)
            {
                return;
            }

            foreach (var item in isPremiumObj)
            {
                item.SetActive(seasonPassInfo.IsPremium);
            }

            foreach (var item in notPremiumObj)
            {
                item.SetActive(!seasonPassInfo.IsPremium);
            }

            premiumPurchaseButtonPriceObj.SetActive(seasonPassInfo.IsPremium);

            foreach (var item in isPremiumPlusObj)
            {
                item.SetActive(seasonPassInfo.IsPremiumPlus);
            }

            foreach (var item in notPremiumPlusObj)
            {
                item.SetActive(!seasonPassInfo.IsPremiumPlus);
            }

            premiumPlusPurchaseButtonPriceObj.SetActive(seasonPassInfo.IsPremiumPlus);
        }

        private void OnPurchase(string productKey)
        {
            ApiClients.Instance.IAPServiceManager.CheckProductAvailable(productKey, States.Instance.AgentState.address, Game.Game.instance.CurrentPlanetId.ToString(),
                //success
                () => { Game.Game.instance.IAPStoreManager.OnPurchaseClicked(productKey); },
                //failed
                () =>
                {
                    PurchaseButtonLoadingEnd();
                    OneLineSystem.Push(MailType.System,
                        L10nManager.Localize("ERROR_CODE_SHOPITEM_EXPIRED"),
                        NotificationCell.NotificationType.Alert);
                }).AsUniTask().Forget();
        }

        public void PurchaseSeasonPassPremiumButton()
        {
            var seasonPassManager = ApiClients.Instance.SeasonPassServiceManager;
            if (seasonPassManager.AvatarInfo.Value.IsPremium)
            {
                return;
            }

            var productKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}Premium";

            if (Game.Game.instance.IAPStoreManager.SeasonPassProduct.TryGetValue(productKey, out var product))
            {
                premiumPurchaseButtonDisabledObj.SetActive(true);
                premiumPurchaseButtonPriceObj.SetActive(false);
                premiumPurchaseButtonLoadingObj.SetActive(true);
                OnPurchase(product.Sku());
            }
        }

        public void PurchaseSeasonPassPremiumPlusButton()
        {
            var seasonPassManager = ApiClients.Instance.SeasonPassServiceManager;
            if (seasonPassManager.AvatarInfo.Value.IsPremiumPlus)
            {
                return;
            }

            string productKey;

            if (seasonPassManager.AvatarInfo.Value.IsPremium)
            {
                productKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}Premiumplus";
            }
            else
            {
                productKey = $"SeasonPass{seasonPassManager.CurrentSeasonPassData.Id}PremiumAll";
            }

            if (Game.Game.instance.IAPStoreManager.SeasonPassProduct.TryGetValue(productKey, out var product))
            {
                premiumPlusPurchaseButtonDisabledObj.SetActive(true);
                premiumPlusPurchaseButtonPriceObj.SetActive(false);
                premiumPlusPurchaseButtonLoadingObj.SetActive(true);
                OnPurchase(product.Sku());
            }
        }

        public void PurchaseButtonLoadingEnd()
        {
            ApiClients.Instance.SeasonPassServiceManager.AvatarStateRefreshAsync().AsUniTask().Forget();
            premiumPurchaseButtonLoadingObj.SetActive(false);
            premiumPlusPurchaseButtonLoadingObj.SetActive(false);
            RefreshIcons(ApiClients.Instance.SeasonPassServiceManager.AvatarInfo.Value);
        }
    }
}
