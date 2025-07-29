using NSMB.Quantum;
using NSMB.Replay;
using NSMB.UI.Game;
using NSMB.UI.Loading;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Sound {
    public unsafe class MusicManager : QuantumSceneViewComponent<StageContext> {
        private enum MusicType {
            Normal,
            Mega,
            Invincibility,
            Silence
        }


        //---Serialized Variables
        [SerializeField] private LoopingMusicPlayer musicPlayer;
        [SerializeField] private Songinator midiMusicPlayerNormal;
        [SerializeField] private Songinator midiMusicPlayerMega;
        [SerializeField] private Songinator midiMusicPlayerStarman;

        //---Private variables
        private MusicType currentMusicType = MusicType.Silence;
        private bool usesMidi = false;

        public void OnValidate() {
            usesMidi = musicPlayer == null;
            this.SetIfNull(ref musicPlayer);
            this.SetIfNull(ref midiMusicPlayerNormal);
            this.SetIfNull(ref midiMusicPlayerMega);
            this.SetIfNull(ref midiMusicPlayerStarman);
        }

        public unsafe void Start() {
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);

            ActiveReplayManager.OnReplayFastForwardEnded += OnReplayFastForwardEnded;
            LoadingCanvas.OnLoadingEnded += OnLoadingEnded;

            var game = QuantumRunner.DefaultGame;
            Frame f;
            if (game != null && (f = game.Frames.Predicted) != null) {
                GameState state = f.Global->GameState;
                if (state == GameState.Starting || state == GameState.Playing) {
                    // Already in a game
                    HandleMusic(game, true);
                }
            }
        }

        public void OnDestroy() {
            ActiveReplayManager.OnReplayFastForwardEnded -= OnReplayFastForwardEnded;
            LoadingCanvas.OnLoadingEnded -= OnLoadingEnded;
        }

        public void OnUpdateView(CallbackUpdateView e) {
            if (e.Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(e.Game, false);
            }
        }

        private void HandleMusicStream(QuantumGame game, bool force) {
            Frame f = game.Frames.Predicted;
            var rules = f.Global->Rules;

            if (!force && !musicPlayer.IsPlaying) {
                return;
            }

            bool invincible = false;
            bool mega = false;
            bool speedup = false;

            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;

            int playersWithOneLife = 0;
            while (allPlayers.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                if (rules.IsLivesEnabled && mario->Lives == 0) {
                    continue;
                }
                if (rules.IsLivesEnabled && mario->Lives == 1) {
                    playersWithOneLife++;
                }

                bool isSpectateTarget = false;
                foreach (var playerElement in PlayerElements.AllPlayerElements) {
                    if (playerElement.Entity == entity) {
                        isSpectateTarget = true;
                        break;
                    }
                }

                if (!game.PlayerIsLocal(mario->PlayerRef) && !isSpectateTarget) {
                    continue;
                }

                speedup |= rules.IsLivesEnabled && mario->Lives == 1;
                mega |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.MegaMushroom) && mario->MegaMushroomFrames > 0;
                invincible |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.Starman) && mario->IsStarmanInvincible;
            }

            speedup |= rules.IsTimerEnabled && f.Global->Timer <= 60;

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            if (gamemode is StarChasersGamemode) {
                speedup |= gamemode.GetFirstPlaceObjectiveCount(f) >= rules.StarsToWin - 1;
            } else if (gamemode is IceRunGamemode) {
                speedup |= gamemode.GetFirstPlaceObjectiveCount(f) >= rules.ScoresToWin - 10;
            }

            if (!speedup && rules.IsLivesEnabled) {
                // Also speed up the music if:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life
                speedup |= (f.Global->RealPlayers <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= f.Global->RealPlayers);
            }

            VersusStageData stage = ViewContext.Stage;
            if (mega) {
                musicPlayer.Play(f.FindAsset(stage.MegaMushroomMusic));
            } else if (invincible) {
                musicPlayer.Play(f.FindAsset(stage.InvincibleMusic));
            } else {
                musicPlayer.Play(f.FindAsset(stage.GetCurrentMusic(f)));
            }

            musicPlayer.FastMusic = speedup;
        }

        private bool AnyMusicPlaying() {
            return midiMusicPlayerNormal.state == Songinator.PlaybackState.PLAYING ||
                   midiMusicPlayerMega.state == Songinator.PlaybackState.PLAYING ||
                   midiMusicPlayerStarman.state == Songinator.PlaybackState.PLAYING;
        }

        private void HandleMusicMidi(QuantumGame game, bool force) {
            Frame f = game.Frames.Predicted;
            var rules = f.Global->Rules;

            if (!force && !AnyMusicPlaying()) {
                return;
            }

            bool invincible = false;
            bool mega = false;
            bool speedup = false;

            var allPlayers = f.Filter<MarioPlayer>();
            allPlayers.UseCulling = false;

            int playersWithOneLife = 0;
            while (allPlayers.NextUnsafe(out EntityRef entity, out MarioPlayer* mario)) {
                if (rules.IsLivesEnabled && mario->Lives == 0) {
                    continue;
                }
                if (rules.IsLivesEnabled && mario->Lives == 1) {
                    playersWithOneLife++;
                }

                bool isSpectateTarget = false;
                foreach (var playerElement in PlayerElements.AllPlayerElements) {
                    if (playerElement.Entity == entity) {
                        isSpectateTarget = true;
                        break;
                    }
                }

                if (!game.PlayerIsLocal(mario->PlayerRef) && !isSpectateTarget) {
                    continue;
                }

                mega |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.MegaMushroom) && mario->MegaMushroomFrames > 0;
                invincible |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.Starman) && mario->IsStarmanInvincible;
            }

            speedup |= rules.IsTimerEnabled && f.Global->Timer <= 60;
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            if (gamemode is StarChasersGamemode) {
                speedup |= gamemode.GetFirstPlaceObjectiveCount(f) >= rules.StarsToWin - 1;
            } else if (gamemode is IceRunGamemode) {
                speedup |= gamemode.GetFirstPlaceObjectiveCount(f) >= rules.ScoresToWin - 10;
            }

            if (!speedup && rules.IsLivesEnabled) {
                // Also speed up the music if:
                // A: two players left, at least one has one life
                // B: three+ players left, all have one life
                speedup |= (f.Global->RealPlayers <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= f.Global->RealPlayers);
            }

            if (mega) {
                // musicPlayer.Play(stage.MegaMushroomMusic);
                SetMusicType(MusicType.Mega);
            } else if (invincible) {
                // musicPlayer.Play(stage.InvincibleMusic);
                SetMusicType(MusicType.Invincibility);
            } else {
                // musicPlayer.Play(stage.MainMusic[f.Global->TotalGamesPlayed % stage.MainMusic.Length]);
                SetMusicType(MusicType.Normal);
            }

            // musicPlayer.FastMusic = speedup;
        }

        private void SetMusicType(MusicType type) {
            if (currentMusicType == type) return;

            switch (type) {
            case MusicType.Normal:
                midiMusicPlayerMega.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerStarman.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerNormal.SetPlaybackState(Songinator.PlaybackState.PLAYING);
                break;
            case MusicType.Mega:
                midiMusicPlayerNormal.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerStarman.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerMega.SetPlaybackState(Songinator.PlaybackState.PLAYING);
                break;
            case MusicType.Invincibility:
                midiMusicPlayerNormal.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerMega.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerStarman.SetPlaybackState(Songinator.PlaybackState.PLAYING);
                break;
            case MusicType.Silence:
            default:
                midiMusicPlayerNormal.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerMega.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                midiMusicPlayerStarman.SetPlaybackState(Songinator.PlaybackState.STOPPED);
                break;
            }

            currentMusicType = type;
        }

        public void HandleMusic(QuantumGame game, bool force) {
            if (usesMidi) {
                HandleMusicStream(game, force);
            } else {
                HandleMusicMidi(game, force);
            }
        }

        private void OnGameEnded(EventGameEnded e) {
            musicPlayer.Stop();
        }

        private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (IsMarioLocal(e.Entity) && !musicPlayer.IsPlaying) {
                HandleMusic(e.Game, true);
            }
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (IsMarioLocal(e.Entity) && Settings.Instance.audioRestartMusicOnDeath) {
                musicPlayer.Stop();
            }
        }

        private void OnGameResynced(CallbackGameResynced e) {
            if (e.Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(e.Game, true);
            }
        }

        private void OnReplayFastForwardEnded(ActiveReplayManager arm) {
            if (Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(Game, true);
            }
        }

        private void OnLoadingEnded(bool longIntro) {
            if (!longIntro && Game != null) {
                HandleMusic(Game, true);
            }
        }
    }
}