using Nekoyume.UI.Module;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Nekoyume.UI
{
    using Cysharp.Threading.Tasks;
    using Nekoyume.Blockchain;
    using Nekoyume.L10n;
    using UniRx;
    public class AdventureBossBattlePopup : PopupWidget
    {
        [SerializeField] private GameObject[] challengeFloors;
        [SerializeField] private GameObject[] breakThroughFloors;
        [SerializeField] private TextMeshProUGUI challengeFloorText;
        [SerializeField] private TextMeshProUGUI breakThroughFloorText;
        [SerializeField] private ConditionalButton challengeButton;
        [SerializeField] private ConditionalButton breakThroughButton;
        [SerializeField] private BaseItemView[] firstClearItems;
        [SerializeField] private BaseItemView[] challengeRandomItems;
        [SerializeField] private BaseItemView[] breakThroughRandomItems;
        [SerializeField] private TextMeshProUGUI challengeApCostText;
        [SerializeField] private TextMeshProUGUI breakThroughApCostText;
        [SerializeField] private GameObject challengeLockObj;
        [SerializeField] private GameObject challengeContents;
        [SerializeField] private Button gotoUnlock;
        [SerializeField] private GameObject breakThroughContent;
        [SerializeField] private GameObject breakThroughNoContent;

        protected override void Awake()
        {
            base.Awake();
            challengeButton.OnClickSubject.Subscribe(_ => {
                Find<AdventureBossPreparation>().Show(L10nManager.Localize("UI_ADVENTURE_BOSS_CHALLENGE"));
                Close();
            }).AddTo(gameObject);
            breakThroughButton.OnClickSubject.Subscribe(_ => {
                Find<AdventureBossPreparation>().Show(L10nManager.Localize("UI_ADVENTURE_BOSS_BREAKTHROUGH"));
                Close();
            }).AddTo(gameObject);
            gotoUnlock.onClick.AddListener(() =>
            {
                Close();
                var floor = Find<AdventureBoss>().CurrentUnlockFloor;
                if (floor != null)
                {
                    floor.OnClickUnlockAction();
                }
            });
        }

        public override void Show(bool ignoreShowAnimation = false)
        {
            var adventurebossData = Game.Game.instance.AdventureBossData;
            var currentFloor = 0;
            var maxFloor = 5;

            if(adventurebossData.ExploreInfo.Value != null)
            {
                currentFloor = adventurebossData.ExploreInfo.Value.Floor;
                maxFloor = adventurebossData.ExploreInfo.Value.MaxFloor;
            }

            if(currentFloor >= maxFloor)
            {
                challengeLockObj.SetActive(true);
                challengeContents.SetActive(false);
                challengeFloorText.text = "";
            }
            else
            {
                challengeLockObj.SetActive(false);
                challengeContents.SetActive(true);
                challengeFloorText.text = $"{currentFloor+1}F ~ {maxFloor}F";
            }

            if(currentFloor == 0)
            {
                breakThroughContent.SetActive(false);
                breakThroughNoContent.SetActive(true);
                breakThroughFloorText.text = "";
            }
            else
            {
                breakThroughContent.SetActive(true);
                breakThroughNoContent.SetActive(false);
                breakThroughFloorText.text = $"{1}F ~ {currentFloor}F";
            }

            for (int i = 0; i < challengeFloors.Length; i++)
            {
                breakThroughFloors[i].SetActive(i < currentFloor);
                challengeFloors[i].SetActive(i >= currentFloor && i < maxFloor);
            }

            breakThroughApCostText.text = L10nManager.Localize("UI_ADVENTURE_BOSS_BATTLEPOPUP_AP_DESC", currentFloor);
            challengeApCostText.text = L10nManager.Localize("UI_ADVENTURE_BOSS_BATTLEPOPUP_AP_DESC", maxFloor-currentFloor);

            base.Show(ignoreShowAnimation);
        }
    }
}
