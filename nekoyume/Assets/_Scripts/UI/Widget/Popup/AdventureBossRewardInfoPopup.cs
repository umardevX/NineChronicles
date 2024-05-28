using Cysharp.Threading.Tasks;
using Nekoyume.UI;
using Nekoyume.UI.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nekoyume.UI
{
    using Nekoyume.Action.AdventureBoss;
    using Nekoyume.Helper;
    using Nekoyume.L10n;
    using Nekoyume.Model.AdventureBoss;
    using Nekoyume.TableData;
    using System.Linq;
    using TMPro;
    using UniRx;
    using UnityEngine.UI;
    using static Nekoyume.Model.State.RedeemCodeState;

    public class AdventureBossRewardInfoPopup : PopupWidget
    {
        [SerializeField] private UnityEngine.UI.ToggleGroup toggleGroup;
        [SerializeField] private Toggle toggleScore;
        [SerializeField] private Toggle toggleFloor;
        [SerializeField] private Toggle toggleOperational;
        [SerializeField] private GameObject contentsScore;
        [SerializeField] private GameObject contentsFloor;
        [SerializeField] private GameObject contentsOperational;
        [SerializeField] private TextMeshProUGUI remainingBlockTime;

        [Header("Score Contents")]
        [SerializeField] private TextMeshProUGUI totalScore;
        [SerializeField] private TextMeshProUGUI myScore;
        [SerializeField] private BaseItemView[] baseItemViews;

        [Header("Floor Contents")]
        [SerializeField] private FloorRewardCell[] floorRewardCells;

        [Header("Operational Contents")]
        [SerializeField] private Image currentSeasonBossImg;
        [SerializeField] private TextMeshProUGUI currentSeasonBossName;
        [SerializeField] private BaseItemView[] currentSeasonBossRewardViews;
        [SerializeField] private BossRewardCell[] bossRewardCells;

        private readonly List<System.IDisposable> _disposablesByEnable = new();
        private long _seasonEndBlock;

        override protected void Awake()
        {
            toggleScore.onValueChanged.AddListener((isOn) =>
            {
                var adventureBossData = Game.Game.instance.AdventureBossData;
                totalScore.text = adventureBossData.ExploreBoard.Value.TotalPoint.ToString("#,0");
                var contribution = (long)adventureBossData.ExploreInfo.Value.Score / adventureBossData.ExploreBoard.Value.TotalPoint;
                myScore.text = $"{adventureBossData.ExploreInfo.Value.Score.ToString("#,0")} ({contribution.ToString("F2")}%)";
                var myReward = adventureBossData.GetCurrentTotalRewards();
                int i = 0;
                foreach (var item in myReward.ItemReward)
                {
                    baseItemViews[i].ItemViewSetItemData(item.Key, item.Value);
                    i++;
                }
                foreach(var fav in myReward.FavReward)
                {
                    if (baseItemViews[i].ItemViewSetCurrencyData(fav.Key, fav.Value))
                    {
                        i++;
                    }
                }
                for (; i < baseItemViews.Length; i++)
                {
                    baseItemViews[i].gameObject.SetActive(false);
                }

                contentsScore.SetActive(isOn);
            });
            toggleFloor.onValueChanged.AddListener((isOn) =>
            {
                contentsFloor.SetActive(isOn);
            });
            toggleOperational.onValueChanged.AddListener((isOn) =>
            {
                contentsOperational.SetActive(isOn);
                if(Game.Game.instance.AdventureBossData.SeasonInfo.Value != null)
                {
                    var bossId = Game.Game.instance.AdventureBossData.SeasonInfo.Value.BossId;
                    currentSeasonBossImg.sprite = SpriteHelper.GetBigCharacterIcon(bossId);
                    currentSeasonBossImg.SetNativeSize();
                    currentSeasonBossName.text = L10nManager.LocalizeCharacterName(bossId);
                }
                if(Game.Game.instance.AdventureBossData.BountyBoard.Value != null)
                {
                    var bountyBoard = Game.Game.instance.AdventureBossData.BountyBoard.Value;
                    var currentInvestorInfo = Game.Game.instance.AdventureBossData.GetCurrentInvestorInfo();

                    if(currentInvestorInfo != null)
                    {
                        var wantedReward = Game.Game.instance.AdventureBossData.GetCurrentBountyRewards();
                        int itemIndex = 0;
                        foreach (var item in wantedReward.ItemReward)
                        {
                            currentSeasonBossRewardViews[itemIndex].ItemViewSetItemData(item.Key, item.Value);
                            itemIndex++;
                            if (itemIndex >= currentSeasonBossRewardViews.Length)
                            {
                                NcDebug.LogError("currentSeasonBossRewardViews is not enough");
                                break;
                            }
                        }
                        foreach (var fav in wantedReward.FavReward)
                        {
                            if (baseItemViews[itemIndex].ItemViewSetCurrencyData(fav.Key, fav.Value))
                            {       
                                itemIndex++;
                            }
                            if(itemIndex >= currentSeasonBossRewardViews.Length)
                            {
                                NcDebug.LogError("currentSeasonBossRewardViews is not enough");
                                break;
                            }
                        }
                    }
                    else
                    {
                        if(bountyBoard.FixedRewardItemId != null)
                        {
                            currentSeasonBossRewardViews[0].ItemViewSetItemData(bountyBoard.FixedRewardItemId.Value, 0);
                        }
                        if(bountyBoard.FixedRewardFavId != null)
                        {
                            currentSeasonBossRewardViews[0].ItemViewSetCurrencyData(bountyBoard.FixedRewardFavId.Value, 0);
                        }

                        if(bountyBoard.RandomRewardItemId != null)
                        {
                            currentSeasonBossRewardViews[1].ItemViewSetItemData(bountyBoard.RandomRewardItemId.Value, 0);
                        }
                        if(bountyBoard.RandomRewardFavId != null)
                        {
                            currentSeasonBossRewardViews[1].ItemViewSetCurrencyData(bountyBoard.RandomRewardFavId.Value, 0);
                        }
                    }

                    for (int i = 0; i < bossRewardCells.Length; i++)
                    {
                        if(i < Game.Game.instance.AdventureBossData.WantedRewardList.Count())
                        {
                            bossRewardCells[i].SetData(Game.Game.instance.AdventureBossData.WantedRewardList[i]);
                        }
                        else
                        {
                            bossRewardCells[i].gameObject.SetActive(false);
                        }
                    }
                }
            }); 
        }

        public override void Show(bool ignoreShowAnimation = false)
        {
            Game.Game.instance.AdventureBossData.SeasonInfo.
                Subscribe(RefreshSeasonInfo).
                AddTo(_disposablesByEnable);
            Game.Game.instance.Agent.BlockIndexSubject
                .Subscribe(UpdateViewAsync)
                .AddTo(_disposablesByEnable);
            base.Show(ignoreShowAnimation);
        }

        private void RefreshSeasonInfo(SeasonInfo seasonInfo)
        {
            if (seasonInfo == null)
            {
                return;
            }
            _seasonEndBlock = seasonInfo.EndBlockIndex;
        }

        private void UpdateViewAsync(long blockIndex)
        {
            var remainingBlockIndex = _seasonEndBlock - blockIndex;
            if (remainingBlockIndex < 0)
            {
                Close();
                return;
            }
            remainingBlockTime.text = $"{remainingBlockIndex:#,0}({remainingBlockIndex.BlockRangeToTimeSpanString()})";
        }
    }
}
