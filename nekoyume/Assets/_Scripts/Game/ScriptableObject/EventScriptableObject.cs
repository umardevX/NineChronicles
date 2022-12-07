﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nekoyume
{
    [CreateAssetMenu(
        fileName = "EventData",
        menuName = "Scriptable Object/Event Data",
        order = int.MaxValue)]
    public class EventScriptableObject : ScriptableObjectIncludeEnum<EnumType.EventType>
    {
        public EventInfo defaultSettings;
        public List<TimeBasedEventInfo> timeBasedEvents;
        public List<BlockIndexBasedEventInfo> blockIndexBasedEvents;
        public List<EventDungeonIdBasedEventInfo> eventDungeonIdBasedEvents;
    }

    [Serializable]
    public class EventInfo
    {
        [field: SerializeField]
        [Tooltip("The type that describes the event")]
        public EnumType.EventType EventType { get; private set; }

        [field: SerializeField]
        [Tooltip("The sprite used by UI_IntroScreen.prefab")]
        public Sprite Intro { get; private set; }

        [field: SerializeField]
        [Tooltip("The sprite used by WorldMapStage.prefab")]
        public Sprite StageIcon { get; private set; }

        [field: SerializeField]
        [Tooltip("Value to modify step icon coordinates")]
        public Vector2 StageIconOffset { get; private set; }

        [field: SerializeField]
        [Tooltip(
            "Main lobby bgm. Reference only name of audio clip. Audio is managed by AudioController")]
        public AudioClip MainBGM { get; private set; }
    }

    [Serializable]
    public class TimeBasedEventInfo : EventInfo
    {
        [field: SerializeField]
        [Tooltip("DateTimeFormat(UTC):MM/dd/ HH:mm:ss (E.g: 05/10 10:20:30)")]
        public string BeginDateTime { get; private set; }

        [field: SerializeField]
        [Tooltip("DateTimeFormat(UTC):MM/dd/ HH:mm:ss (E.g: 05/10 11:22:33)")]
        public string EndDateTime { get; private set; }
    }

    [Serializable]
    public class BlockIndexBasedEventInfo : EventInfo
    {
        [field: SerializeField]
        [Tooltip("Beginning block index")]
        public long BeginBlockIndex { get; private set; }

        [field: SerializeField]
        [Tooltip("End block index")]
        public long EndBlockIndex { get; private set; }
    }

    [Serializable]
    public class EventDungeonIdBasedEventInfo : EventInfo
    {
        [field: SerializeField]
        [Tooltip("ID list of `EventDungeonSheet`")]
        public int[] TargetDungeonIds { get; private set; }

        [field: SerializeField]
        [Tooltip("The Key used by WorldMapPage in WorldMapWorld.prefab")]
        public string EventDungeonKey { get; private set; }

        [field: SerializeField]
        [Tooltip("The sprite used by GuidedQuestCell.prefab as event dungeon icon")]
        public Sprite EventDungeonGuidedQuestIcon { get; private set; }

        [field: SerializeField]
        [Tooltip("The sprite used by GuideQuestCell.prefab as event recipe icon")]
        public Sprite EventRecipeGuidedQuestIcon { get; private set; }

        [field: SerializeField]
        [Tooltip("The sprite used by UI_BattlePreparation.prefab")]
        public Sprite EventDungeonBattlePreparationBg { get; private set; }
    }
}
