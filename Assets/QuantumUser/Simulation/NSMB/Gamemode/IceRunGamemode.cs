using Photon.Deterministic;
using System;
using System.Collections;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.Diagnostics;

namespace Quantum
{
    public unsafe class IceRunGamemode : GamemodeAsset
    {
        public const int DeathPenalty = -10;
        public const int DamagePenalty = -4;
        public const int MiniInc = 1;
        public const int MushroomInc = 3;
        public const int FireFlowerInc = 5; // used for any powerup above the mushroom

        public static readonly FP TimeMulti = 60;
        private static readonly WaitForSeconds TimeChangeInterval = new(1f/60f);

        public override void EnableGamemode(Frame f) {

        }

        public override void DisableGamemode(Frame f) {
            
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

            // End Condition: team has had propeller for that long
            int? winningTeam = GetWinningTeam(f, out int time);
            if (winningTeam != null && time >= f.Global->Rules.ScoresToWin) {
                // <team> wins
                GameLogicSystem.EndGame(f, false, winningTeam.Value);
                return;
            }

            // End Condition: timer expires
            if (f.Global->Rules.IsTimerEnabled && f.Global->Timer <= 0) {
                if (f.Global->Rules.DrawOnTimeUp) {
                    // It's a draw
                    GameLogicSystem.EndGame(f, false, null);
                    return;
                }

                // Check if one team is winning
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
            return (int)Math.Floor((float)gamemodeDataCopy.IceRun->PropellerTime / 60f);
        }

        public override FP GetItemSpawnWeight(Frame f, CoinItemAsset item, int leaderTime, int ourTime) {
            var allPlayers = f.Filter<MarioPlayer>();
            int timeToWin = f.Global->Rules.StarsToWin;
            int timeDiff = leaderTime - ourTime;
            FP bonus = 0;
            while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
                if (mario->CurrentPowerupState == PowerupState.PropellerMushroom) {
                    bonus = 0;
                } else {
                    bonus = item.LosingSpawnBonus * FPMath.Log(timeDiff + 1, FP.E) * (FP._1 - ((FP) (timeToWin - leaderTime) / timeToWin));
                }
            }
            return FPMath.Max(0, item.SpawnChance + bonus);
        }

        public void SetPlayerAsPropeller(Frame f, EntityRef marioEntity, bool wasRandom = false) {
            f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario);
            f.Global->PropellerPlayer = marioEntity;
            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = PowerupState.PropellerMushroom;
            if (++f.Global->PropellerSwaps % (f.Global->RealPlayers * 2) == 0) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                stage.ResetStage(f, false);
            }
            f.Events.MarioPlayerChangedPropeller(f.Global->PropellerPlayer, wasRandom);
        }

        public void SelectRandomPlayer(Frame f, bool ignoreDead) {
            var allPlayers = f.Filter<MarioPlayer>();
            EntityRef[] marios = new EntityRef[Constants.MaxPlayers];
            f.Global->OldPropellerPlayer = f.Global->PropellerPlayer;
            // count all the marios that are valid
            int validPlayers = 0;
            while (allPlayers.NextUnsafe(out EntityRef entity, out MarioPlayer* currMario)) {
                if ((currMario->IsDead &! ignoreDead) || currMario->Disconnected || entity == f.Global->OldPropellerPlayer) {
                    continue;
                }

                // only run this if we already have a Propeller Player!
                if (f.Unsafe.TryGetPointer(f.Global->OldPropellerPlayer, out MarioPlayer* oldMario)) {
                    if (currMario->GetTeam(f) == oldMario->GetTeam(f)) {
                        continue;
                    }
                }

                marios[validPlayers++] = entity;
            }

            // pick a random player from the list
            if (validPlayers > 0) {
                int rng = f.RNG->Next(0, validPlayers);
                SetPlayerAsPropeller(f, marios[rng], true);
            } else {
                // reset player state
                f.Unsafe.TryGetPointer(f.Global->PropellerPlayer, out MarioPlayer* mario);
                mario->PreviousPowerupState = mario->CurrentPowerupState;
                mario->CurrentPowerupState = PowerupState.PropellerMushroom;
            }
        }

        public bool IsPlayerPropeller(Frame f, EntityRef entity) {
            return entity == f.Global->PropellerPlayer;
        }

        public bool IsPlayerPropeller(Frame f, MarioPlayer* mario) {
            f.Unsafe.TryGetPointer(f.Global->PropellerPlayer, out MarioPlayer* propMario);
            return mario == propMario;
        }

        public bool IsPlayerPropeller(Frame f, MarioPlayer mario) {
            f.Unsafe.TryGetPointer(f.Global->PropellerPlayer, out MarioPlayer* propMario);
            return &mario == propMario;
        }

        public FP SubtractOrAddScore(Frame f, EntityRef entity, FP num) {
            f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario);
            return _SubtractOrAddScore(f, mario, num);
        }

        public FP SubtractOrAddScore(Frame f, MarioPlayer mario, FP num) {
            return _SubtractOrAddScore(f, &mario, num);
        }

        private FP _SubtractOrAddScore(Frame f, MarioPlayer* marioPtr, FP num) {
            var icerun = marioPtr->GamemodeData.IceRun;
            icerun->PropellerTime += num*TimeMulti;

            f.Events.MarioPlayerSetPropellerTime(marioPtr->PlayerRef, num*TimeMulti);
            if (num < 0) {
                return icerun->PropellerTime = FPMath.Max(icerun->PropellerTime, 0);
            } else {
                return icerun->PropellerTime = FPMath.Min(icerun->PropellerTime / TimeMulti, f.Global->Rules.ScoresToWin) * TimeMulti;
            }
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

                return Color.HSVToRGB(ourIndex / (totalPlayers + 1f), s, v);
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

                var playerFilter = f.Filter<PlayerData>();
                playerFilter.UseCulling = false;
                f.Unsafe.TryGetPointer(f.Global->PropellerPlayer, out MarioPlayer* propMario);
                if (propMario != null) {
                    // now check player infos
                    for (int i = 0; i < totalPlayers; i++) {
                        var pRef = playerInfos[i].PlayerRef;
                        if (pRef == player && pRef == propMario->PlayerRef) {
                            return Color.cyan;
                        }
                    }
                }
                return Color.red;
            }
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

        public override bool OverridePowerdownSystem(Frame f, EntityRef entity, EntityRef attacker) {
            f.Unsafe.TryGetPointer(entity, out MarioPlayer* marioPtr);
            bool isIceRunner = false;
            if (isIceRunner = IsPlayerPropeller(f, entity)) {
                SubtractOrAddScore(f, entity, DamagePenalty);
            } else {
                marioPtr->DoKnockback(f, entity, marioPtr->FacingRight, 0, KnockbackStrength.CollisionBump, attacker);
                return false;
            }

            QBoolean doDamage = true;
            f.Signals.OnMarioPlayerTakeDamage(entity, ref doDamage);
            if (!doDamage) {
                return false;
            }

            marioPtr->DamageInvincibilityFrames = 2 * 60;
            f.Events.MarioPlayerTookDamage(entity);
            return false;
        }

        public override bool OverridePowerupSystem(Frame f, EntityRef powerupEntity, EntityRef marioEntity) {
            var powerup = f.Unsafe.GetPointer<Powerup>(powerupEntity);
            if (powerup->IgnorePlayerFrames > 0) {
                return false;
            }

            var coinItem = f.Unsafe.GetPointer<CoinItem>(powerupEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            var newPowerup = (PowerupAsset) f.FindAsset(coinItem->Scriptable);

            if (newPowerup.Type == PowerupType.Starman) {
                mario->InvincibilityFrames = 200;
                f.Signals.OnMarioPlayerBecameInvincible(marioEntity);
            } else {
                switch (newPowerup.State) {
                case PowerupState.MiniMushroom:
                    SubtractOrAddScore(f, marioEntity, MiniInc);
                    break;
                case PowerupState.Mushroom:
                    SubtractOrAddScore(f, marioEntity, MushroomInc);
                    break;
                default:
                    SubtractOrAddScore(f, marioEntity, FireFlowerInc);
                    break;
                }
            }

            f.Signals.OnMarioPlayerCollectedPowerup(marioEntity, powerupEntity);
            f.Events.MarioPlayerCollectedPowerup(marioEntity, PowerupReserveResult.NoneButPlaySound, newPowerup);
            f.Events.CollectableDespawned(powerupEntity, f.Unsafe.GetPointer<Transform2D>(powerupEntity)->Position, true);
            f.Destroy(powerupEntity);
            return false;
        }
    }
}
