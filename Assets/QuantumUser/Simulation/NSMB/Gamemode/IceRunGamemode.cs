using Photon.Deterministic;
using System;

namespace Quantum
{
    public unsafe class IceRunGamemode : GamemodeAsset
    {
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

            // End Condition: team gets to enough stars
            int? winningTeam = GetWinningTeam(f, out int time);
            if (winningTeam != null && time >= f.Global->Rules.StarsToWin) {
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

        public void SetPlayerAsPropeller(Frame f, EntityRef marioEntity) {
            f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario);
            f.Global->PropellerPlayer = marioEntity;
            mario->PreviousPowerupState = mario->CurrentPowerupState;
            mario->CurrentPowerupState = PowerupState.PropellerMushroom;
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
                UnityEngine.Debug.Log("[IceRun] - Added player " + marios[validPlayers-1]);
            }

            // pick a random player from the list
           if (validPlayers > 0) {
                int rng = f.RNG->Next(0, validPlayers);
                SetPlayerAsPropeller(f, marios[rng]);
                UnityEngine.Debug.Log("[IceRun] - Selected Player " + marios[rng]);
           }
        }

        public bool IsPlayerPropeller(Frame f, EntityRef entity) {
            return entity == f.Global->PropellerPlayer;
        }

        public bool IsPlayerPropeller(Frame f, MarioPlayer* mario) {
            f.Unsafe.TryGetPointer(f.Global->PropellerPlayer, out MarioPlayer* propMario);
            return mario == propMario;
        }
    }
}
