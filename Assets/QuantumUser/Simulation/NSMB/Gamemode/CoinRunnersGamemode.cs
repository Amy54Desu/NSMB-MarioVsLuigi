using Photon.Deterministic;
using System;
using UnityEngine;

namespace Quantum {
    public unsafe class CoinRunnersGamemode : GamemodeAsset {

        public AssetRef<EntityPrototype> ObjectiveCoinPrototype, StarCoinPrototype;

        public override void EnableGamemode(Frame f) {
            f.SystemEnable<ObjectiveCoinSystem>();
            f.SystemEnable<GoldBlockSystem>();
        }

        public override void DisableGamemode(Frame f) {
            f.SystemDisable<ObjectiveCoinSystem>();
            f.SystemDisable<GoldBlockSystem>();
       }

        public override void CheckForGameEnd(Frame f) {
            // End Condition: only one team alive
            Span<int> objectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, objectiveCounts);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < objectiveCounts.Length; i++) {
                if (objectiveCounts[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            if (aliveTeamCount <= 1) {
                if (aliveTeam == -1) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                } else if (f.Global->RealPlayers > 1) {
                    // <team> wins, assuming more than 1 player
                    // so the player doesn't insta-win in a solo game.
                    GameLogicSystem.EndGame(f, false, aliveTeam);
                    return;
                }
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                if (f.Global->Rules.DrawOnTimeUp) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                }

                // Check if one team is winning
                int? winningTeam = GetWinningTeam(f, out _);
                if (winningTeam != null) {
                    // <team> wins
                    GameLogicSystem.EndGame(f, false, winningTeam.Value);
                    return;
                }
            }
        }

        public override int GetObjectiveCount(Frame f, PlayerRef player) {
            var marioFilter = f.Filter<MarioPlayer>();
            marioFilter.UseCulling = false;

            while (marioFilter.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (player != mario->PlayerRef) {
                    continue;
                }

                return GetObjectiveCount(f, mario);
            }

            return -1;
        }

        public override int GetObjectiveCount(Frame f, MarioPlayer* mario) {
            if (mario == null || mario->Disconnected || (mario->Lives == 0 && f.Global->Rules.IsLivesEnabled)) {
                return -1;
            }

            // Make a copy to not modify the `type` variable
            // Which can cause desyncs.
            GamemodeSpecificData gamemodeDataCopy = mario->GamemodeData;
            return gamemodeDataCopy.CoinRunners->ObjectiveCoins;
        }

        public override FP GetItemSpawnWeight(Frame f, CoinItemAsset coinItem, int leaderCoins, int ourCoins) {
            FP coinDifference = leaderCoins - ourCoins;
            FP percentageTimeRemaining = f.Global->Timer / (f.Global->Rules.TimerMinutes * 60);
            FP bonus = coinItem.LosingSpawnBonus * FPMath.Log((coinDifference / 40) + 1, FP.E) * 1 - (percentageTimeRemaining * percentageTimeRemaining);
            return FPMath.Max(0, coinItem.SpawnChance + bonus);
        }
        public override Color GetPlayerColor(Frame f, PlayerRef player, float s = 1, float v = 1, bool considerDisqualifications = true) {
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

        public override Color GetTeamColor(Frame f, int team, float s = 1, float v = 1) {
            var spectatorColor = GamemodeAsset.spectatorColor;
            var teams = f.SimulationConfig.Teams;
            if (team < 0 || team >= teams.Length) {
                return spectatorColor;
            }

            Color color = f.FindAsset(teams[team]).color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            return Color.HSVToRGB(hue, saturation * s, value * v);
        }
    }
}