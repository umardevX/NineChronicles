using System.Collections.Generic;
using System.Linq;
using mixpanel;
using Nekoyume.L10n;
using Nekoyume.UI.Module;
using UnityEngine;

namespace Nekoyume.UI
{
    public class TutorialController
    {
        private readonly Dictionary<TutorialTargetType, RectTransform> _targets =
            new Dictionary<TutorialTargetType, RectTransform>(new TutorialTargetTypeComparer());

        private readonly Dictionary<TutorialActionType, TutorialAction> _actions =
            new Dictionary<TutorialActionType, TutorialAction>(new TutorialActionTypeComparer());

        private readonly List<Preset> _preset = new List<Preset>();
        private readonly List<Scenario> _scenario = new List<Scenario>();

        private readonly Tutorial _tutorial;
        private readonly RectTransform _buttonRectTransform;

        private const string ScenarioPath = "Tutorial/Data/TutorialScenario";
        private const string PresetPath = "Tutorial/Data/TutorialPreset";
        private static string CheckPointKey =>
            $"Tutorial_Check_Point_{Game.Game.instance.States.CurrentAvatarKey}";

        private readonly List<int> _mixpanelTargets = new List<int>() { 1, 2, 6, 11, 49 };

        public bool IsPlaying => _tutorial.IsActive();

        public TutorialController(IEnumerable<Widget> widgets)
        {
            foreach (var widget in widgets)
            {
                if (widget is Tutorial tutorial)
                {
                    _tutorial = tutorial;
                    _buttonRectTransform = tutorial.NextButton.GetComponent<RectTransform>();
                    continue;
                }

                foreach (var target in widget.tutorialTargets.Where(target => target != null))
                {
                    if (_targets.ContainsKey(target.type))
                    {
                        Debug.LogError($"Duplication Tutorial Targets AlreadyRegisterd : {_targets[target.type].gameObject.name}  TryRegisterd : {target.rectTransform.gameObject.name}");
                        continue;
                    }
                    _targets.Add(target.type, target.rectTransform);
                }

                foreach (var action in widget.tutorialActions)
                {
                    var type = widget.GetType();
                    var methodInfo = type.GetMethod(action.ToString());
                    if (methodInfo != null)
                    {
                        if (_actions.ContainsKey(action))
                        {
                            Debug.LogError($"Duplication Tutorial {action} Action AlreadyRegisterd : {_actions[action].ActionWidget.name}  TryRegisterd : {widget.name}");
                            continue;
                        }
                        _actions.Add(action, new TutorialAction(widget, methodInfo));
                    }
                }
            }

            _scenario.AddRange(GetData<TutorialScenario>(ScenarioPath).scenario);
            _preset.AddRange(GetData<TutorialPreset>(PresetPath).preset);
        }

        public void Run(int clearedStageId)
        {
            if (clearedStageId < GameConfig.RequireClearedStageLevel.CombinationEquipmentAction)
            {
                Play(1);
            }
            else
            {
                var checkPoint = GetCheckPoint(clearedStageId);
                if (checkPoint > 0)
                {
                    Play(checkPoint);
                }
            }
        }

        public void Play(int id)
        {
            if (!_tutorial.isActiveAndEnabled)
            {
                _tutorial.Show(true);
                WidgetHandler.Instance.IsActiveTutorialMaskWidget = true;
            }

            var scenario = _scenario.FirstOrDefault(x => x.id == id);
            if (scenario != null)
            {
                SendMixPanel(id);
                SetCheckPoint(scenario.checkPointId);
                var viewData = GetTutorialData(scenario.data);
                _tutorial.Play(viewData, scenario.data.presetId, () =>
                {
                    PlayAction(scenario.data.actionType);
                    Play(scenario.nextId);
                });
            }
            else
            {
                _tutorial.Stop(() =>
                {
                    _tutorial.gameObject.SetActive(false);
                    WidgetHandler.Instance.IsActiveTutorialMaskWidget = false;
                });
            }
        }

        private void PlayAction(TutorialActionType actionType)
        {
            if (_actions.ContainsKey(actionType))
            {
                _actions[actionType].ActionMethodInfo
                    ?.Invoke(_actions[actionType].ActionWidget, null);
            }
        }

        private List<ITutorialData> GetTutorialData(ScenarioData data)
        {
            var preset = _preset.First(x => x.id == data.presetId);
            var target = _targets.ContainsKey(data.targetType) ? _targets[data.targetType] : null;
            var scriptKey = data.scriptKey;
            var script = L10nManager.Localize(scriptKey);

            return new List<ITutorialData>()
            {
                new GuideBackgroundData(
                    preset.isExistFadeInBackground,
                    preset.isEnableMask,
                    target,
                    _buttonRectTransform,
                    data.fullScreenButton,
                    data.buttonRaycastPadding,
                    data.targetPositionOffset,
                    data.targetSizeOffset),
                new GuideArrowData(
                    data.noArrow ? GuideType.Stop : data.guideType,
                    target,
                    data.targetPositionOffset,
                    data.targetSizeOffset,
                    data.arrowPositionOffset,
                    data.arrowAdditionalDelay,
                    preset.isSkipArrowAnimation),
                new GuideDialogData(
                    data.emojiType,
                    (DialogCommaType) preset.commaId,
                    script,
                    target)
            };
        }

        private static T GetData<T>(string path) where T : new()
        {
            var json = Resources.Load<TextAsset>(path).ToString();
            var data = JsonUtility.FromJson<T>(json);
            return data;
        }

        private static int GetCheckPoint(int clearedStageId)
        {
            /*
             * check point = 0 : not playing tutorial
             * check point > 0 : already playing tutorial
             * check point < 0 : ended tutorial per stage, not playing tutorial
             *
             * when playing tutorial, check point = tutorial id
             * when ended tutorial, check point = stage id * -1 (in TutorialScenario, "checkPointId")
             */

            var checkPoint = PlayerPrefs.GetInt(CheckPointKey, 0);
            if (checkPoint > 0)
            {
                return checkPoint;
            }

            // If PlayerPrefs doesn't exist
            if (clearedStageId < GameConfig.RequireClearedStageLevel.CombinationEquipmentAction)
            {
                checkPoint = 1;
            }
            else if (clearedStageId == GameConfig.RequireClearedStageLevel.CombinationEquipmentAction &&
                     checkPoint != GameConfig.RequireClearedStageLevel.CombinationEquipmentAction * -1)
            {
                checkPoint = 2;
            }
            // playing tutorial id = clearedStageId * 100000
            else if (clearedStageId == 5 && checkPoint != -5)
            {
                checkPoint = 50000;
            }
            else if (clearedStageId == 10 && checkPoint != -10)
            {
                var summonRow = Game.Game.instance.TableSheets.SummonSheet.First;
                if (summonRow is not null && SimpleCostButton.CheckCostOfType(
                        (CostType)summonRow.CostMaterial, summonRow.CostMaterialCount))
                {
                    checkPoint = 100000;
                }
            }

            // format example
            void Check(int stageIdForTutorial)  // ex) 5, 10
            {
                // clearedStageId == stageIdForTutorial => 튜토리얼이 실행되어야 하는 스테이지
                // 튜토리얼이 종료된 후 checkPoint = -stageIdForTutorial 연산을 함
                // checkPoint != -stageIdForTutorial => 해당 스테이지의 튜토리얼이 종료된적 없음
                if (clearedStageId == stageIdForTutorial && checkPoint != -stageIdForTutorial)
                {
                    checkPoint = stageIdForTutorial * 10000;
                }
            }

            return checkPoint;
        }

        private static void SetCheckPoint(int id)
        {
            PlayerPrefs.SetInt(CheckPointKey, id);
        }

        private void SendMixPanel(int id)
        {
            if (!_mixpanelTargets.Exists(x => x == id))
            {
                return;
            }

            var props = new Dictionary<string, Value>()
            {
                ["Id"] = id,
            };
            Analyzer.Instance.Track("Unity/Tutorial progress", props);
        }
    }
}
