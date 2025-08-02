using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using UnityEngine;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject {

        public string NamePrefix, TranslationKey, DescriptionTranslationKey, DiscordRpcKey;
        public string ObjectiveSymbolPrefix;
        public AssetRef<CoinItemAsset>[] AllCoinItems;
        public AssetRef<CoinItemAsset> FallbackCoinItem;
        public AssetRef<EntityPrototype> LooseCoinPrototype;
        public bool DisableSpawningInvinciblity;

        public GameRulesPrototype DefaultRules;

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public abstract int GetObjectiveCount(Frame f, PlayerRef player);

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public virtual Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1, bool considerDisqualifications = true) {
            var spectatorColor = GamemodeAsset.spectatorColor;
            if (f == null || player == PlayerRef.None) {
                return spectatorColor;
            }

            // Prioritize spectator status
            if (!f.TryResolveDictionary(f.Global->PlayerDatas, out var playerDataDict)
                || !playerDataDict.TryGetValue(player, out EntityRef playerDataEntity)
                || !f.Unsafe.TryGetPointer(playerDataEntity, out PlayerData* playerData)
                || playerData->IsSpectator) {

                return spectatorColor;
            }

            // Or dead marios
            if (f.Global->GameState > GameState.WaitingForPlayers && considerDisqualifications) {
                var marioFilter = f.Filter<MarioPlayer>();
                marioFilter.UseCulling = false;
                MarioPlayer* existingMario = null;
                while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                    if (mario->PlayerRef == player) {
                        existingMario = mario;
                        break;
                    }
                }

                if (existingMario == null
                    || (f.Global->GameState >= GameState.Playing && f.Global->Rules.IsLivesEnabled && existingMario->Lives <= 0)) {
                    return spectatorColor;
                }
            }

            // Then team
            if (f.Global->Rules.TeamsEnabled) {
                return GetTeamColor(f, f.Global->GameState == GameState.PreGameRoom ? playerData->RequestedTeam : playerData->RealTeam, s, v);
            }

            // Then id based color
            int ourIndex = 0;
            int totalPlayers = 0;
            if (f.Global->GameState == GameState.PreGameRoom) {
                // use PlayerData here
                PlayerData* ourPlayerData = QuantumUtils.GetPlayerData(f, player);

                var playerFilter = f.Filter<PlayerData>();
                playerFilter.UseCulling = false;
                while (playerFilter.NextUnsafe(out _, out PlayerData* otherPlayerData)) {
                    if (otherPlayerData->IsSpectator) {
                        continue;
                    }

                    totalPlayers++;
                    if (otherPlayerData->JoinTick < ourPlayerData->JoinTick) {
                        ourIndex++;
                    }
                }
            } else {
                // use PlayerInformation here
                ourIndex = -1;
                totalPlayers = f.Global->RealPlayers;
                var playerInfos = f.Global->PlayerInfo;
                for (int i = 0; i < totalPlayers; i++) {
                    if (playerInfos[i].PlayerRef == player) {
                        ourIndex = i;
                        break;
                    }
                }

                if (ourIndex == -1) {
                    // Spectator
                    return spectatorColor;
                }
            }

            return Color.HSVToRGB(ourIndex / (totalPlayers + 1f), s, v);
        }

        public virtual Color GetTeamColor(Frame f, int team, float s = 1, float v = 1) {
            var spectatorColor = GamemodeAsset.spectatorColor;
            var teams = f.SimulationConfig.Teams;
            if (team < 0 || team >= teams.Length) {
                return spectatorColor;
            }

            Color color = f.FindAsset(teams[team]).color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation * s, value * v);
        }

        public virtual bool OverridePowerdownSystem(Frame f, EntityRef entity, EntityRef attacker) { return true; }
        public virtual bool OverridePowerupSystem(Frame f, EntityRef powerupEntity, EntityRef marioEntity) { return true; }
        public virtual bool OnProjectileMarioInteraction(Frame f, EntityRef marioEntity, EntityRef projectileEntity) { return true; }
        public static readonly Color spectatorColor = new(0.8f, 0.8f, 0.8f, 0.7f);

        public virtual CoinItemAsset GetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // "Losing" variable based on ln(x+1)

            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;
            int leaderObjectiveCount = GetFirstPlaceObjectiveCount(f);

            var rules = f.Global->Rules;
            bool custom = rules.CustomPowerupsEnabled;
            bool lives = rules.IsLivesEnabled;
            bool big = stage.SpawnBigPowerups;
            bool vertical = stage.SpawnVerticalPowerups;

            bool canSpawnMega = true;

            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;
            while (allPlayers.NextUnsafe(out _, out MarioPlayer* otherPlayer)) {
                // Check if another player is actively mega (not growing or shrinking)
                if (otherPlayer->CurrentPowerupState == PowerupState.MegaMushroom
                    && otherPlayer->MegaMushroomStartFrames == 0) {
                    canSpawnMega = false;
                    break;
                }
            }

            bool onlyOneAlreadyExists = false;
            var allCoinItems = f.Filter<CoinItem>();
            while (allCoinItems.NextUnsafe(out _, out var coinItem)) {
                if (f.FindAsset(coinItem->Scriptable).OnlyOneCanExist) {
                    onlyOneAlreadyExists = true;
                    break;
                }
            }

            FP totalChance = 0;
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if ((coinItem is PowerupAsset powerup) && powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                    continue;
                }

                if ((coinItem.BigPowerup && !big)
                    || (coinItem.VerticalPowerup && !vertical)
                    || (coinItem.CustomPowerup && !custom)
                    || (coinItem.LivesOnlyPowerup && !lives)
                    || (!coinItem.CanSpawnFromBlock && fromBlock)
                    || (coinItem.OnlyOneCanExist && onlyOneAlreadyExists)) {
                    continue;
                }

                totalChance += GetItemSpawnWeight(f, coinItem, leaderObjectiveCount, ourObjectiveCount);
            }

            FP rand = mario->RNG.Next(0, totalChance);
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if ((coinItem is PowerupAsset powerup) && powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                    continue;
                }

                if ((coinItem.BigPowerup && !big)
                    || (coinItem.VerticalPowerup && !vertical)
                    || (coinItem.CustomPowerup && !custom)
                    || (coinItem.LivesOnlyPowerup && !lives)
                    || (!coinItem.CanSpawnFromBlock && fromBlock)
                    || (coinItem.OnlyOneCanExist && onlyOneAlreadyExists)) {
                    continue;
                }

                FP chance = GetItemSpawnWeight(f, coinItem, leaderObjectiveCount, ourObjectiveCount);

                if (rand < chance) {
                    return coinItem;
                }

                rand -= chance;
            }

            return f.FindAsset(FallbackCoinItem);
        }

        public abstract FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int leaderObjectiveCount, int ourObjectiveCount);

        public virtual int? GetWinningTeam(Frame f, out int winningObjectiveCount) {
            winningObjectiveCount = 0;
            int? winningTeam = null;
            bool tie = false;
            
            Span<int> teamObjectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);

            for (int i = 0; i < Constants.MaxPlayers; i++) {
                int objectiveCount = teamObjectiveCounts[i];
                if (objectiveCount < 0) {
                    continue;
                } else if (winningTeam == null) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount > winningObjectiveCount) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount == winningObjectiveCount) {
                    tie = true;
                }
            }

            return tie ? null : winningTeam;
        }

        public virtual void GetAllTeamsObjectiveCounts(Frame f, Span<int> teamObjectiveCounts) {
            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;

            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                teamObjectiveCounts[i] = -1;
            }

            while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (mario->Disconnected || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)) {
                    continue;
                }
                if (mario->GetTeam(f) is not byte team) {
                    continue;
                }

                if (teamObjectiveCounts[team] == -1) {
                    teamObjectiveCounts[team] = 0;
                }

                if (team < teamObjectiveCounts.Length) {
                    teamObjectiveCounts[team] += GetObjectiveCount(f, mario);
                }
            }
        }

        public virtual int? GetTeamObjectiveCount(Frame f, byte? nullableTeam) {
            if (nullableTeam is not byte team) {
                return null;
            }
            return GetTeamObjectiveCount(f, team);
        }

        public virtual int GetTeamObjectiveCount(Frame f, byte team) {
            int sum = 0;
            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;
            while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (mario->GetTeam(f) != team
                    || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)
                    || mario->Disconnected) {
                    continue;
                }

                sum += GetObjectiveCount(f, mario);
            }

            return sum;
        }

        public virtual int GetFirstPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int max = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > max) {
                    max = objectiveCount;
                }
            }

            return max;
        }

        public virtual EntityRef SpawnLooseCoin(Frame f, FPVector2 position) {
            EntityRef newCoinEntity = f.Create(LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(newCoinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoinEntity);
            coinTransform->Position = position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            return newCoinEntity;
        }
    }
}