﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using Cysharp.Threading.Tasks;
using Libplanet;
using Nekoyume.Action;
using Nekoyume.Game;
using Nekoyume.Model.GrandFinale;
using Nekoyume.Model.State;
using Nekoyume.TableData.GrandFinale;
using UnityEngine;
using static Lib9c.SerializeKeys;

namespace Nekoyume.State
{
    public class GrandFinaleStates
    {
        public GrandFinaleParticipant[] GrandFinaleParticipants { get; private set; } =
            Array.Empty<GrandFinaleParticipant>();

        public PlayerGrandFinaleParticipant GrandFinalePlayer { get; private set; }

        public bool IsUpdating { get; private set; } = false;

        private long _participantsUpdatedBlockIndex;

        public class GrandFinaleParticipant
        {
            public readonly Address AvatarAddr;
            public readonly int Score;
            public readonly int Rank;
            public readonly AvatarState AvatarState;
            public readonly int CP;

            public GrandFinaleParticipant(
                Address avatarAddr,
                int score,
                int rank,
                AvatarState avatarState)
            {
                AvatarAddr = avatarAddr;
                Score = score;
                Rank = rank;
                AvatarState = avatarState;

                CP = AvatarState?.GetCP() ?? 0;
            }

            public GrandFinaleParticipant(GrandFinaleParticipant value)
            {
                AvatarAddr = value.AvatarAddr;
                Score = value.Score;
                Rank = value.Rank;
                AvatarState = value.AvatarState;

                CP = AvatarState?.GetCP() ?? 0;
            }
        }

        public class PlayerGrandFinaleParticipant : GrandFinaleParticipant
        {
            public GrandFinaleInformation CurrentInfo;

            public PlayerGrandFinaleParticipant(
                Address avatarAddr,
                int score,
                int rank,
                AvatarState avatarState,
                GrandFinaleInformation currentInfo)
                : base(
                    avatarAddr,
                    score,
                    rank,
                    avatarState)
            {
                CurrentInfo = currentInfo;
            }

            public PlayerGrandFinaleParticipant(
                GrandFinaleParticipant grandFinaleParticipant,
                GrandFinaleInformation currentInfo)
                : base(grandFinaleParticipant)
            {
                CurrentInfo = currentInfo;
            }
        }

        public async UniTask<GrandFinaleParticipant[]>
            UpdateGrandFinaleParticipantsOrderedWithScoreAsync()
        {
            var states = States.Instance;
            var agent = Game.Game.instance.Agent;
            var tableSheets = TableSheets.Instance;
            var avatarAddress = states.CurrentAvatarState?.address;
            if (!avatarAddress.HasValue)
            {
                GrandFinaleParticipants = Array.Empty<GrandFinaleParticipant>();
                return GrandFinaleParticipants;
            }

            if (_participantsUpdatedBlockIndex == agent.BlockIndex)
            {
                return GrandFinaleParticipants;
            }

            _participantsUpdatedBlockIndex = agent.BlockIndex;

            var currentAvatar = states.CurrentAvatarState;
            var currentAvatarAddr = currentAvatar.address;
            var currentGrandFinaleData = tableSheets.GrandFinaleScheduleSheet.GetRowByBlockIndex(agent.BlockIndex);
            if (currentGrandFinaleData is null)
            {
                return Array.Empty<GrandFinaleParticipant>();
            }

            if (!tableSheets.GrandFinaleParticipantsSheet.TryGetValue(currentGrandFinaleData.Id, out var row)
                || !row.Participants.Any())
            {
                Debug.Log(
                    $"Failed to get {nameof(GrandFinaleParticipantsSheet)} with {currentGrandFinaleData.Id}");
                GrandFinalePlayer = null;
                return Array.Empty<GrandFinaleParticipant>();
            }

            IsUpdating = true;
            var avatarAddrList = row.Participants;
            var isGrandFinaleParticipant = avatarAddrList.Contains(currentAvatarAddr);
            var scoreDeriveString = string.Format(
                CultureInfo.InvariantCulture,
                BattleGrandFinale.ScoreDeriveKey,
                row.GrandFinaleId);
            var avatarAndScoreAddrList = avatarAddrList
                .Select(avatarAddr => (avatarAddr,
                    avatarAddr.Derive(scoreDeriveString))
                ).ToArray();
            // NOTE: If addresses is too large, and split and get separately.
            var scores = await agent.GetStateBulk(
                avatarAndScoreAddrList.Select(tuple => tuple.Item2));
            var avatarAddrAndScores = avatarAndScoreAddrList
                .Select(tuple =>
                {
                    var (avatarAddr, scoreAddr) = tuple;
                    return (
                        avatarAddr,
                        scores[scoreAddr] is Integer score
                            ? (int)score
                            : BattleGrandFinale.DefaultScore
                    );
                })
                .ToArray();
            var avatarAddrAndScoresWithRank =
                AddRank(avatarAddrAndScores, currentAvatarAddr);
            PlayerGrandFinaleParticipant playerGrandFinaleParticipant = null;
            var playerScore = 0;
            if (isGrandFinaleParticipant)
            {
                try
                {
                    var playerTuple = avatarAddrAndScoresWithRank.First(tuple =>
                        tuple.avatarAddr.Equals(currentAvatarAddr));
                    playerScore = playerTuple.score;
                }
                catch
                {
                    // Chain has not Score and GrandFinaleInfo about current avatar.
                    var arenaAvatarState = currentAvatar.ToArenaAvatarState();
                    var clonedCurrentAvatar = currentAvatar.CloneAndApplyToInventory(arenaAvatarState);
                    playerGrandFinaleParticipant = new PlayerGrandFinaleParticipant(
                        currentAvatarAddr,
                        BattleGrandFinale.DefaultScore,
                        0,
                        clonedCurrentAvatar,
                        default);
                    playerScore = playerGrandFinaleParticipant.Score;
                }
            }

            var addrBulk = avatarAddrAndScoresWithRank
                .SelectMany(tuple => new[]
                {
                    tuple.avatarAddr,
                    tuple.avatarAddr.Derive(LegacyInventoryKey),
                    ArenaAvatarState.DeriveAddress(tuple.avatarAddr),
                })
                .ToList();
            var playerGrandFinaleInfoAddr = GrandFinaleInformation.DeriveAddress(
                currentAvatarAddr,
                row.GrandFinaleId);
            if (isGrandFinaleParticipant)
            {
                addrBulk.Add(playerGrandFinaleInfoAddr);
            }

            // NOTE: If the [`addrBulk`] is too large, and split and get separately.
            var stateBulk = await agent.GetStateBulk(addrBulk);
            var result = avatarAddrAndScoresWithRank.Select(tuple =>
            {
                var (avatarAddr, score, rank) = tuple;
                var avatar = stateBulk[avatarAddr] is Dictionary avatarDict
                    ? new AvatarState(avatarDict)
                    : null;
                var inventory =
                    stateBulk[avatarAddr.Derive(LegacyInventoryKey)] is List inventoryList
                        ? new Model.Item.Inventory(inventoryList)
                        : null;
                if (avatar is { })
                {
                    avatar.inventory = inventory;
                }

                var arenaAvatar =
                    stateBulk[ArenaAvatarState.DeriveAddress(avatarAddr)] is List arenaAvatarList
                        ? new ArenaAvatarState(arenaAvatarList)
                        : null;
                avatar = avatar.ApplyToInventory(arenaAvatar);
                return new GrandFinaleParticipant(
                    avatarAddr,
                    avatarAddr.Equals(currentAvatarAddr)
                        ? playerScore
                        : score,
                    rank,
                    avatar
                );
            }).ToArray();

            if (isGrandFinaleParticipant)
            {
                var playerGrandFinaleInfo = stateBulk[playerGrandFinaleInfoAddr] is List serialized
                    ? new GrandFinaleInformation(serialized)
                    : new GrandFinaleInformation(currentAvatarAddr, 1);
                if (playerGrandFinaleParticipant is null)
                {
                    var participant = result.FirstOrDefault(e =>
                        e.AvatarAddr.Equals(currentAvatarAddr));
                    playerGrandFinaleParticipant = new PlayerGrandFinaleParticipant(participant, playerGrandFinaleInfo);
                }
                else
                {
                    playerGrandFinaleParticipant.CurrentInfo = playerGrandFinaleInfo;
                }
            }

            GrandFinalePlayer = playerGrandFinaleParticipant;
            GrandFinaleParticipants = result;
            IsUpdating = false;
            return GrandFinaleParticipants;
        }

        private static (Address avatarAddr, int score, int rank)[] AddRank(
            (Address avatarAddr, int score)[] tuples, Address? currentAvatarAddr = null)
        {
            if (tuples.Length == 0)
            {
                return default;
            }

            var orderedTuples = tuples
                .OrderByDescending(tuple => tuple.score)
                .ThenByDescending(tuple => tuple.avatarAddr == currentAvatarAddr)
                .ThenBy(tuple => tuple.avatarAddr)
                .Select(tuple => (tuple.avatarAddr, tuple.score, 0))
                .ToArray();

            var result = new List<(Address avatarAddr, int score, int rank)>();
            var trunk = new List<(Address avatarAddr, int score, int rank)>();
            int? currentScore = null;
            var currentRank = 1;
            for (var i = 0; i < orderedTuples.Length; i++)
            {
                var tuple = orderedTuples[i];
                if (!currentScore.HasValue)
                {
                    currentScore = tuple.score;
                    trunk.Add(tuple);
                    continue;
                }

                if (currentScore.Value == tuple.score)
                {
                    trunk.Add(tuple);
                    currentRank++;
                    if (i < orderedTuples.Length - 1)
                    {
                        continue;
                    }

                    foreach (var tupleInTrunk in trunk)
                    {
                        result.Add((
                            tupleInTrunk.avatarAddr,
                            tupleInTrunk.score,
                            currentRank));
                    }

                    trunk.Clear();

                    continue;
                }

                foreach (var tupleInTrunk in trunk)
                {
                    result.Add((
                        tupleInTrunk.avatarAddr,
                        tupleInTrunk.score,
                        currentRank));
                }

                trunk.Clear();
                if (i < orderedTuples.Length - 1)
                {
                    trunk.Add(tuple);
                    currentScore = tuple.score;
                    currentRank++;
                    continue;
                }

                result.Add((
                    tuple.avatarAddr,
                    tuple.score,
                    currentRank + 1));
            }

            return result.ToArray();
        }
    }
}
