using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Core;
using System;
using System.Collections.Generic;

namespace Quantum {
    public unsafe partial struct MarioPlayer {

        public readonly bool IsStarmanInvincible => InvincibilityFrames > 0;
        public readonly bool IsWallsliding => Action is PlayerAction.WallSlide;
        public readonly bool IsCrouchedInShell => Action is PlayerAction.BlueShellCrouch;
        public readonly bool IsDamageable => !IsStarmanInvincible && DamageInvincibilityFrames == 0;
        public readonly bool IsInKnockback => Action is PlayerAction.SoftKnockback or PlayerAction.NormalKnockback or PlayerAction.HardKnockback or PlayerAction.WaterKnockback;
        public readonly bool IsDead => Action is PlayerAction.Death or PlayerAction.LavaDeath or PlayerAction.Respawning;
        public readonly bool CanCollectOwnTeamsObjectiveCoins => !IsInKnockback && DamageInvincibilityFrames == 0;
        public const int DropStarRight = 1 << 8;
        public const int NoStarLoss = -1;

        public readonly byte? GetTeam(Frame f) {
            var data = QuantumUtils.GetPlayerData(f, PlayerRef);
            if (data == null) {
                return null;
            } else {
                return (byte) (data->RealTeam % Constants.MaxPlayers);
            }
        }

        public readonly int GetDeathArgs(Frame f) {
            if (!f.Global->Rules.IsLivesEnabled || Lives > 0) {
                return 0;
            } else {
                return 2;
            }
        }

        public static ActionFlags DefaultActionFlags(PlayerAction action) {
            return action switch {
                PlayerAction.Idle => ActionFlags.AllowBump,
                PlayerAction.Walk => ActionFlags.AllowBump | ActionFlags.AllowHold,
                PlayerAction.Skidding => ActionFlags.AllowBump,
                PlayerAction.Crouch => ActionFlags.AllowBump | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.CrouchAir => ActionFlags.AllowBump | ActionFlags.UsesCrouchHitbox | ActionFlags.AirAction,
                PlayerAction.Sliding => ActionFlags.AllowBump | ActionFlags.Attacking | ActionFlags.IrregularVelocity,
                // PlayerAction.Bounce                 => 0 all this action does is set to another action
                PlayerAction.SingleJump => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.DoubleJump => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.TripleJump => ActionFlags.AllowBump | ActionFlags.AllowHold |ActionFlags.AirAction,
                PlayerAction.Freefall => ActionFlags.AllowBump | ActionFlags.AllowHold | ActionFlags.AirAction,
                PlayerAction.HoldIdle => ActionFlags.AllowBump | ActionFlags.Holding,
                PlayerAction.HoldWalk => ActionFlags.AllowBump | ActionFlags.Holding,
                PlayerAction.HoldJump => ActionFlags.AllowBump | ActionFlags.Holding | ActionFlags.AirAction,
                PlayerAction.HoldFall => ActionFlags.AllowBump | ActionFlags.Holding | ActionFlags.AirAction,
                PlayerAction.WallSlide => ActionFlags.AirAction,
                PlayerAction.Wallkick => ActionFlags.AirAction,
                PlayerAction.GroundPound => ActionFlags.AirAction | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity, // a few flags are handled by the action
                // PlayerAction.MiniGroundPound        => (int) (ActionFlags.AirAction), // has player bounce
                PlayerAction.SoftKnockback => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.NormalKnockback => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.HardKnockback => ActionFlags.Intangible | ActionFlags.DisableTurnaround | ActionFlags.IrregularVelocity,
                PlayerAction.SpinBlockSpin => ActionFlags.AirAction | ActionFlags.CameraChange,
                PlayerAction.SpinBlockDrill => ActionFlags.AirAction | ActionFlags.NoPlayerBounce,
                PlayerAction.BlueShellCrouch => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellCrouchAir => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellSliding => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.Attacking | ActionFlags.AirAction | ActionFlags.NoPlayerBounce | ActionFlags.IrregularVelocity,
                PlayerAction.BlueShellJump => ActionFlags.IsShelled | ActionFlags.DisableTurnaround | ActionFlags.UsesCrouchHitbox | ActionFlags.AirAction | ActionFlags.IrregularVelocity, // the no player bounce based off ActionArg
                // PlayerAction.BlueShellGroundPound   => (int) (ActionFlags.IsShelled | ActionFlags.AirAction | ActionFlags.NoPlayerBounce),
                PlayerAction.PropellerSpin => ActionFlags.AirAction | ActionFlags.CameraChange,
                // PlayerAction.PropellerFall          => (int) (ActionFlags.AirAction | ActionFlags.Takes1Star | ActionFlags.GivesNormalKnockback),
                PlayerAction.PropellerDrill => ActionFlags.AirAction,
                PlayerAction.MegaMushroom => ActionFlags.Cutscene | ActionFlags.OverrideAll,
                PlayerAction.PowerupShoot => ActionFlags.AllowBump,
                PlayerAction.Pushing => ActionFlags.AllowBump,
                PlayerAction.Death => ActionFlags.Cutscene | ActionFlags.Intangible | ActionFlags.OverrideAll,
                PlayerAction.LavaDeath => ActionFlags.Cutscene | ActionFlags.Intangible | ActionFlags.OverrideAll,
                PlayerAction.Respawning => ActionFlags.Cutscene | ActionFlags.Intangible,
                PlayerAction.EnteringPipe => ActionFlags.Cutscene | ActionFlags.Intangible,
                PlayerAction.Swimming => ActionFlags.WaterAction,
                PlayerAction.WaterKnockback => ActionFlags.WaterAction,
                _ => 0 // null
            };
        }

        public void DropItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->Throw(f, entity, true);
            }
        }

        public void ThrowItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->Throw(f, entity, false);
            }
        }

        public void DiscardItem(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* heldItem)) {
                heldItem->DropWithoutThrowing(f, entity);
            }
        }

        public PlayerAction SetPlayerAction(Frame f, PlayerAction playerAction, int arg = 0, bool throwItem = false, bool dropItem = false, bool discardItem = false, QListPtr<EntityRef> entityRefs = default) {
            PrevAction = Action;
            PreActionInput = default;
            if (PlayerRef.IsValid && f.GetPlayerInput(PlayerRef) != null) {
                PreActionInput = *f.GetPlayerInput(PlayerRef);
            }

            if (throwItem) {
                ThrowItem(f, actionObjectB);
            } else if (dropItem) {
                DropItem(f, actionObjectB);
            } else if (discardItem) {
                DiscardItem(f, actionObjectB);
            }

            Action = playerAction;

            ActionTimer = 0;
            ActionState = 0;
            ActionArg = arg;
            ActionEntities = entityRefs;

            ClearStompEvents();
            SetStompLevel();
            SetActionFlags(DefaultActionFlags(playerAction));

            BreakableLevel = CurrentPowerupState;
            int breakableFlags = 0;
            if (HasActionFlags(ActionFlags.AirAction)) {
                // assume they can break ceilings
                breakableFlags |= (int)BreakableFlags.Up;
            }
            if (HasActionFlags(ActionFlags.IsShelled)) {
                breakableFlags |= (int)(BreakableFlags.Left | BreakableFlags.Right);
            }
            CurrBreakableFlags = breakableFlags;

            UnityEngine.Debug.Log($"[Player] Set action to [{Enum.GetName(typeof(PlayerAction), playerAction)}] with Arg [{arg}]");
            return Action;
        }

        public bool SetPlayerActionOnce(Frame f, PlayerAction playerAction, int arg = 0) {
            if (Action == playerAction) {
                return false;
            }
            SetPlayerAction(f, playerAction, arg);
            return true;
        }

        public PlayerAction? SetGroundAction(Frame f, PhysicsObject* physicsObject, PlayerAction? groundAction = null, int actionArg = 0) {
            if (physicsObject->IsTouchingGround) {
                PlayerAction targetAction;
                if (groundAction == null) {
                    if (HasActionFlags(ActionFlags.Holding)) {
                        targetAction = physicsObject->Velocity.X != 0 ? PlayerAction.HoldWalk : PlayerAction.HoldIdle;
                    } else {
                        targetAction = physicsObject->Velocity.X != 0 ? PlayerAction.Walk : PlayerAction.Idle;
                    }
                } else {
                    targetAction = groundAction.GetValueOrDefault();
                }
                return SetPlayerAction(f, targetAction, actionArg);
            }
            return null;
        }

        public PlayerAction? SetAirAction(Frame f, PhysicsObject* physicsObject, PlayerAction? airAction = null, int actionArg = 0, bool ignCoyote = false) {
            if (ignCoyote) {
                CoyoteTimeFrames = 0;
            }

            if (!physicsObject->IsTouchingGround && (CoyoteTimeFrames <= 0)) {
                PlayerAction targetAction;
                if (airAction == null) {
                    if (HasActionFlags(ActionFlags.Holding)) {
                        targetAction = PlayerAction.HoldFall;
                    } else {
                        targetAction = PlayerAction.Freefall;
                    }
                } else {
                    targetAction = airAction.GetValueOrDefault();
                }
                return SetPlayerAction(f, targetAction, actionArg);
            }
            return null;
        }

        public PlayerAction? SetWaterAction(Frame f, PhysicsObject* physicsObject, PlayerAction? waterAction = null, int actionArg = 0) {
            if (physicsObject->IsUnderwater) {
                PlayerAction targetAction;
                if (waterAction == null) {
                    if (HasActionFlags(ActionFlags.Holding)) {
                        targetAction = PlayerAction.WaterHolding;
                    } else {
                        targetAction = PlayerAction.Swimming;
                    }
                } else {
                    targetAction = waterAction.GetValueOrDefault();
                }
                return SetPlayerAction(f, targetAction, actionArg);
            }
            return null;
        }

        public readonly bool HasActionFlags(ActionFlags actionFlags) {
            return (this.CurrActionFlags & (int) actionFlags) != 0;
        }

        public void AddActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags |= (int) actionFlags;
        }

        public void ClearActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags &= ~(int) actionFlags;
        }

        public void ToggleActionFlags(ActionFlags actionFlags, bool add) {
            if (!add) {
                ClearActionFlags(actionFlags);
            } else {
                AddActionFlags(actionFlags);
            }
        }

        public void SetActionFlags(ActionFlags actionFlags) {
            this.CurrActionFlags = (int) actionFlags;
        }

        public readonly bool HasBreakableFlags(BreakableFlags currBreakableFlags) {
            return (this.CurrBreakableFlags & (int) currBreakableFlags) != 0;
        }

        public void AddBreakableFlags(BreakableFlags currBreakableFlags) {
            this.CurrBreakableFlags |= (int) currBreakableFlags;
        }

        public void ClearBreakableFlags(BreakableFlags currBreakableFlags) {
            this.CurrBreakableFlags &= ~(int) currBreakableFlags;
        }

        public void ToggleBreakableFlags(BreakableFlags currBreakableFlags, bool add) {
            if (!add) {
                ClearBreakableFlags(currBreakableFlags);
            } else {
                AddBreakableFlags(currBreakableFlags);
            }
        }

        public void SetBreakableFlags(BreakableFlags currBreakableFlags) {
            this.CurrBreakableFlags = (int) currBreakableFlags;
        }

        public void SetStompEvents(PlayerAction victimAction = PlayerAction.NormalKnockback, int starsToDrop = 1) {
            this.StarStealCount = starsToDrop;
            this.StompAction = victimAction;
        }

        public void ClearStompEvents() {
            this.StarStealCount = NoStarLoss;
            this.StompAction = default;
        }

        public StompLevel SetStompLevel(StompLevel level = StompLevel.Normal, StompLevel miniLevel = StompLevel.NoDamage) {
            StompPowerLevel = CurrentPowerupState == PowerupState.MiniMushroom ? miniLevel : level;
            return level;
        }

        public bool CheckEntityBounce(Frame f, bool checkPlayer = false) {
            // invincible players should never bounce
            if (IsStarmanInvincible) {
                return false;
            }

            if (!checkPlayer) {
                if (HasActionFlags(ActionFlags.NoEnemyBounce)) {
                    return false;
                }
            } else {
                if (HasActionFlags(ActionFlags.NoPlayerBounce)) {
                    return false;
                }
            }
            StompLevel oldStompLevel = StompPowerLevel;
            SetPlayerAction(f, PlayerAction.Bounce);
            //SetStompLevel(oldStompLevel);
            return true;
        }

        public readonly FPVector2 GetHeldItemOffset(Frame f, EntityRef marioEntity) {
            if (!f.Exists(HeldEntity)) {
                return default;
            }

            var holdable = f.Unsafe.GetPointer<Holdable>(HeldEntity);
            var holdableShape = f.Unsafe.GetPointer<PhysicsCollider2D>(HeldEntity)->Shape;

            FP holdableYOffset = (holdableShape.Box.Extents.Y - holdableShape.Centroid.Y);

            if (holdable->HoldAboveHead) {
                var marioShape = f.Unsafe.GetPointer<PhysicsCollider2D>(marioEntity)->Shape;
                FP pickupFrames = 27;
                FP time = FPMath.Clamp01((f.Number - HoldStartFrame) / pickupFrames);
                FP alpha = 1 - QuantumUtils.EaseOut(1 - time);
                return new FPVector2(
                    0,
                    (marioShape.Box.Extents.Y * (2 - FP._0_05) * alpha) + holdableYOffset
                );
            } else {
                var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
                if (marioPhysicsObject->IsUnderwater) {
                    return new FPVector2(
                        (FacingRight ? 1 : -1) * (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_40 : FP._0_33),
                        (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_09 : FP._0_04) + holdableYOffset
                    );
                } else {
                    return new FPVector2(
                        (FacingRight ? 1 : -1) * FP._0_25,
                        (CurrentPowerupState >= PowerupState.Mushroom ? Constants._0_40 : Constants._0_09) + holdableYOffset
                    );
                }
            }
        }

        public readonly bool CanHoldItem(Frame f, EntityRef entity, EntityRef item) {
            Input input = default;
            if (PlayerRef.IsValid) {
                input = *f.GetPlayerInput(PlayerRef);
            }
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            bool forceHold = false;
            bool aboveHead = false;
            if (f.Unsafe.TryGetPointer(item, out Holdable* holdable)) {
                aboveHead = holdable->HoldAboveHead;
                if (aboveHead) {
                    forceHold = (f.Number - HoldStartFrame) < 25;
                }
            }

            return (input.Sprint.IsDown || forceHold || (f.Exists(HeldEntity) && !f.IsPlayerVerifiedOrLocal(PlayerRef)))
                && !freezable->IsFrozen(f) && CurrentPowerupState is not PowerupState.MiniMushroom or PowerupState.MegaMushroom && !IsSkidding 
                && !IsInKnockback && KnockbackGetupFrames == 0 && !IsTurnaround && !IsPropellerFlying && !IsSpinnerFlying && !IsCrouching && !IsDead
                && !IsInShell && !WallslideLeft && !WallslideRight && (f.Exists(item) || physicsObject->IsTouchingGround || JumpState < JumpState.DoubleJump)
                && !IsGroundpounding && !(!f.Exists(item) && physicsObject->IsUnderwater && input.Jump.IsDown)
                && !(aboveHead && physicsObject->IsUnderwater);
        }

        public readonly bool CanPickupItem(Frame f, EntityRef mario, EntityRef item) {
            return !f.Exists(HeldEntity) && CanHoldItem(f, mario, item) && ForceJumpTimer <= 5;
        }

        public readonly bool InstakillsEnemies(PhysicsObject* physicsObject, bool includeSliding) {
            return CurrentPowerupState == PowerupState.MegaMushroom
                || IsStarmanInvincible
                || HasActionFlags(ActionFlags.Attacking) && (includeSliding || Action != PlayerAction.Sliding);
        }

        public readonly int GetSpeedStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP xVel = FPMath.Abs(physicsObject->Velocity.X) - FP._0_01;
            FP[] arr;
            if (physicsObject->IsUnderwater) {
                if (physicsObject->IsTouchingGround) {
                    arr = CurrentPowerupState == PowerupState.BlueShell ? physicsInfo.SwimWalkShellMaxVelocity : physicsInfo.SwimWalkMaxVelocity;
                } else {
                    arr = physicsInfo.SwimMaxVelocity;
                }
            } else if ((IsSpinnerFlying || IsPropellerFlying) && CurrentPowerupState != PowerupState.MegaMushroom) {
                arr = physicsInfo.FlyingMaxVelocity;
            } else {
                arr = physicsInfo.WalkMaxVelocity;
            }

            for (int i = 0; i < arr.Length; i++) {
                if (xVel <= arr[i]) {
                    return i;
                }
            }
            return arr.Length - 1;
        }

        public readonly int GetGravityStage(PhysicsObject* physicsObject, MarioPlayerPhysicsInfo physicsInfo) {
            FP yVel = physicsObject->Velocity.Y;
            FP[] maxArray = physicsObject->IsUnderwater ? physicsInfo.GravitySwimmingVelocity : (CurrentPowerupState == PowerupState.MegaMushroom ? physicsInfo.GravityMegaVelocity : (CurrentPowerupState == PowerupState.MiniMushroom ? physicsInfo.GravityMiniVelocity : physicsInfo.GravityVelocity));
            for (int i = 0; i < maxArray.Length; i++) {
                if (yVel >= maxArray[i]) {
                    return i;
                }
            }
            return maxArray.Length;
        }

        public void SetReserveItem(Frame f, PowerupAsset newItem) {
            var currentItem = f.FindAsset(ReserveItem);

            if (currentItem == null) {
                // We don't have a reserve item, so we can just set it
                ReserveItem = newItem;
                return;
            }

            if (newItem == null) {
                // Not a valid powerup, so just clear our reserve item instead
                ReserveItem = null;
                return;
            }

            sbyte newItemPriority = newItem != null ? newItem.ItemPriority : (sbyte) -1;
            sbyte currentItemPriority = currentItem != null ? currentItem.ItemPriority : (sbyte) -1;

            if (newItemPriority < currentItemPriority) {
                // New item is less important than our current reserve item, so we don't want to replace it
                return;
            }

            // Replace our current reserve item with the new one
            ReserveItem = newItem;
        }

        public void Death(Frame f, EntityRef entity, bool fire, bool dropObjectives, EntityRef attacker) {
            if (IsDead) {
                return;
            }

            IsDead = true;
            FireDeath = fire;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = true;

            PreRespawnFrames = 180;
            RespawnFrames = 78;

            if ((f.Global->Rules.IsLivesEnabled && QuantumUtils.Decrement(ref Lives)) || Disconnected) {
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
                DeathAnimationFrames = (GamemodeData.StarChasers->Stars > 0) ? (byte) 30 : (byte) 36;
            } else {
                if (dropObjectives) {
                    int objectiveCount = 1;
                    /*
                    if (f.Unsafe.TryGetPointer(attacker, out MarioPlayer* attackerMario) && attackerMario->IsGroundpoundActive) {
                        objectiveCount = 3;
                    }
                    */
                    f.Signals.OnMarioPlayerDropObjective(entity, objectiveCount, attacker);
                }
                DeathAnimationFrames = 36;
            }

            // OnSpinner = null;
            CurrentPipe = default;
            IsInShell = false;
            IsPropellerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsDrilling = false;
            IsSliding = false;
            IsCrouching = false;
            IsSkidding = false;
            IsTurnaround = false;
            IsGroundpounding = false;
            CurrentKnockback = KnockbackStrength.None;
            KnockbackGetupFrames = 0;
            WallslideRight = false;
            WallslideLeft = false;
            ForceJumpTimer = 0;

            /*
            IsWaterWalking = false;
            IsFrozen = false;
           
            if (FrozenCube) {
                Runner.Despawn(FrozenCube.Object);
            }
            */

            if (f.Unsafe.TryGetPointer(HeldEntity, out Holdable* holdable)) {
                holdable->DropWithoutThrowing(f, HeldEntity);
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->IsFrozen = true;
            physicsObject->DisableCollision = true;
            physicsObject->CurrentData = default;

            f.Signals.OnMarioPlayerDied(entity);
            f.Events.MarioPlayerDied(entity, fire);
        }

        public bool Powerdown(Frame f, EntityRef entity, bool ignoreInvincible, EntityRef attacker) {
            if (!ignoreInvincible && !IsDamageable) {
                return false;
            }

            QBoolean doDamage = true;
            f.Signals.OnMarioPlayerTakeDamage(entity, ref doDamage);
            if (!doDamage) {
                return false;
            }

            PreviousPowerupState = CurrentPowerupState;

            switch (CurrentPowerupState) {
            case PowerupState.MiniMushroom:
            case PowerupState.NoPowerup: {
                Death(f, entity, false, true, attacker);
                break;
            }
            case PowerupState.Mushroom: {
                CurrentPowerupState = PowerupState.NoPowerup;
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
                break;
            }
            case PowerupState.HammerSuit:
            case PowerupState.FireFlower:
            case PowerupState.IceFlower:
            case PowerupState.PropellerMushroom:
            case PowerupState.BlueShell: {
                CurrentPowerupState = PowerupState.Mushroom;
                f.Signals.OnMarioPlayerDropObjective(entity, 1, attacker);
                break;
            }
            }

            IsDrilling &= !IsPropellerFlying;
            IsPropellerFlying = false;
            IsInShell = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            UsedPropellerThisJump = false;

            if (!IsDead) {
                DamageInvincibilityFrames = 2 * 60;
                f.Events.MarioPlayerTookDamage(entity);
            }
            return true;
        }

        public void SpawnStars(Frame f, EntityRef entity, int amount) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            bool fastStars = amount > 2 && GamemodeData.StarChasers->Stars > 2;
            int starDirection = FacingRight ? 1 : 2;

            if (f.Global->Rules.IsLivesEnabled && Lives == 0) {
                fastStars = true;
                NoLivesStarDirection = (byte) ((NoLivesStarDirection + 1) % 4);
                starDirection = NoLivesStarDirection;

                starDirection = starDirection switch {
                    2 => 1,
                    1 => 2,
                    _ => starDirection
                };
            }

            int droppedStars = 0;
            while (amount > 0) {
                if (GamemodeData.StarChasers->Stars <= 0) {
                    break;
                }

                int actualStarDirection = starDirection % 4;
                if (!fastStars) {
                    actualStarDirection = starDirection switch {
                        0 => 2,
                        3 => 1,
                        _ => starDirection
                    };
                }

                var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
                EntityRef newStarEntity = f.Create(gamemode.BigStarPrototype);
                var newStar = f.Unsafe.GetPointer<BigStar>(newStarEntity);
                var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newStarEntity);
                newStarTransform->Position = transform->Position;
                newStar->InitializeMovingStar(f, stage, newStarEntity, actualStarDirection);

                GamemodeData.StarChasers->Stars--;
                amount--;
                droppedStars++;
                starDirection++;
            }

            if (droppedStars > 0) {
                f.Events.MarioPlayerDroppedStar(entity);
                GameLogicSystem.CheckForGameEnd(f);
            }
        }

        public void PreRespawn(Frame f, EntityRef entity, VersusStageData stage) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            RespawnFrames = 78;

            if ((f.Global->Rules.IsLivesEnabled && Lives == 0) || Disconnected) {
                f.Destroy(entity);
                return;
            }

            FPVector2 spawnpoint = stage.GetWorldSpawnpointForPlayer(SpawnpointIndex, f.Global->TotalMarios);
            transform->Position = spawnpoint;
            f.Unsafe.GetPointer<CameraController>(entity)->Recenter(stage, spawnpoint);
            
            IsDead = true;
            f.Unsafe.GetPointer<Freezable>(entity)->FrozenCubeEntity = EntityRef.None;
            IsRespawning = true;
            FacingRight = true;
            WallslideLeft = false;
            WallslideRight = false;
            WallslideEndFrames = 0;
            WalljumpFrames = 0;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            IsSpinnerFlying = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            JumpState = JumpState.None;
            PreviousPowerupState = CurrentPowerupState = PowerupState.NoPowerup;
            //animationController.DisableAllModels();
            DamageInvincibilityFrames = 0;
            InvincibilityFrames = 0;
            MegaMushroomFrames = 0;
            MegaMushroomStartFrames = 0;
            MegaMushroomEndFrames = 0;
            // f.ResolveHashSet(WaterColliders).Clear();
            IsCrouching = false;
            IsSliding = false;
            IsTurnaround = false;
            CurrentKnockback = KnockbackStrength.None;
            IsGroundpounding = false;
            IsSkidding = false;
            IsInShell = false;
            IsTurnaround = false;
            ForceJumpTimer = 0;

            physicsObject->IsFrozen = true;
            physicsObject->Velocity = FPVector2.Zero;
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;

            f.Events.MarioPlayerPreRespawned(entity, spawnpoint);
        }

        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsDead = false;
            IsRespawning = false;
            DamageInvincibilityFrames = 120;
            CoyoteTimeFrames = 0;
            ForceJumpTimer = 0;

            physicsObject->IsFrozen = false;
            physicsObject->DisableCollision = false;

            f.Events.MarioPlayerRespawned(entity);

            if (Disconnected) {
                // Disconnected while respawning
                Death(f, entity, false, true, EntityRef.None);
            }
        }

        public bool SetKnockbackAction(Frame f, EntityRef entity, bool fromRight, int starsToDrop, KnockbackStrength strength, EntityRef attacker, bool bypassDamageInvincibility = false) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (physicsObject->IsUnderwater) {
                strength = KnockbackStrength.Normal;
            }

            if (IsImmuneFromKnockbackStrength(CurrentKnockback, strength)) {
                return false;
            }

            var freezable = f.Unsafe.GetPointer<Freezable>(entity);
            if ((!bypassDamageInvincibility && DamageInvincibilityFrames > 0) || f.Exists(CurrentPipe) || (freezable->IsFrozen(f) && freezable->FrozenCubeEntity != attacker) || IsDead || MegaMushroomStartFrames > 0 || MegaMushroomEndFrames > 0) {
                return false;
            }

            if (IsInKnockback) {
                ResetKnockback();
            }

            if (CurrentPowerupState == PowerupState.MiniMushroom && strength >= KnockbackStrength.Groundpound) {
                f.Signals.OnMarioPlayerDropObjective(entity, starsToDrop - 1, attacker);
                Powerdown(f, entity, false, attacker);
                return true;
            }

            if (IsInKnockback || IsInWeakKnockback) {
                starsToDrop = Math.Min(1, starsToDrop);
            }

            // Don't go into walls
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            /*
            if (strength > KnockbackStrength.Bump && PhysicsObjectSystem.Raycast((FrameThreadSafe) f, null, transform->Position + collider->Shape.Centroid, fromRight ? FPVector2.Left : FPVector2.Right, FP._0_33, out _)) {
                fromRight = !fromRight;
            }
            */

            var physics = f.FindAsset(PhysicsAsset);
            FPVector2 knockbackVelocity = strength switch {
                KnockbackStrength.Groundpound => new(Constants._8_25 / 2, Constants._3_50),
                KnockbackStrength.FireballBump => new(Constants._3_75 / 2, 0),
                KnockbackStrength.CollisionBump => new(Constants._2_50, Constants._3_50),
                KnockbackStrength.Normal or _ => new(Constants._3_75 / 2, Constants._3_50),
            };
            if (CurrentKnockback == KnockbackStrength.CollisionBump) {
                knockbackVelocity = FPVector2.Zero;
            }
            knockbackVelocity.X *= fromRight ? -1 : 1;
            if (CurrentPowerupState == PowerupState.MiniMushroom) {
                knockbackVelocity.X *= physics.KnockbackMiniMultiplier.X;
                knockbackVelocity.Y *= physics.KnockbackMiniMultiplier.Y;
            }

            KnockbackTick = f.Number;

            bool forceWeak = false;
            if (freezable->IsFrozen(f)) {
                forceWeak = true;
                KnockbackTick -= 25;
            }
            if (strength == KnockbackStrength.FireballBump && !physicsObject->IsTouchingGround) {
                // FacingRight = fromRight;
                knockbackVelocity.X *= FP._0_75;
            }

            CurrentKnockback = strength;
            IsInWeakKnockback = forceWeak || (CurrentPowerupState != PowerupState.MegaMushroom && (strength == KnockbackStrength.CollisionBump || (strength == KnockbackStrength.FireballBump && physicsObject->IsTouchingGround)));

            physicsObject->Velocity = knockbackVelocity;
            physicsObject->IsTouchingGround = false;
            physicsObject->WasTouchingGround = false;
            physicsObject->HoverFrames = 0;

            KnockbackWasOriginallyFacingRight = FacingRight;
            KnockForwards = FacingRight != fromRight;
            
            f.Signals.OnMarioPlayerDropObjective(entity, starsToDrop, attacker);
            return true;
        }

        private static bool IsImmuneFromKnockbackStrength(KnockbackStrength currentStrength, KnockbackStrength newStrength) {
            return currentStrength == newStrength
                || (currentStrength == KnockbackStrength.Groundpound && newStrength == KnockbackStrength.Normal)
                || (currentStrength == KnockbackStrength.Normal && newStrength == KnockbackStrength.Groundpound)
                || (currentStrength == KnockbackStrength.FireballBump && newStrength == KnockbackStrength.CollisionBump);
        }

        public void GetupKnockback(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (IsInWeakKnockback) {
                physicsObject->Velocity.X = 0;
            }
            if (IsInWeakKnockback || DoEntityBounce || physicsObject->IsUnderwater) {
                // No getup frames
                ResetKnockback();
            } else {
                KnockbackGetupFrames = 25;
            }
        }

        public void ResetKnockback() {
            KnockbackGetupFrames = 0;
            DamageInvincibilityFrames = 90;
            CurrentKnockback = KnockbackStrength.None;
            IsInWeakKnockback = false;
            FacingRight = KnockbackWasOriginallyFacingRight;
        }

        public void EnterPipe(Frame f, EntityRef mario, EntityRef pipe) {
            if (f.Exists(CurrentPipe)
                || PipeCooldownFrames > 0) {
                return;
            }

            var physics = f.FindAsset(f.Unsafe.GetPointer<MarioPlayer>(mario)->PhysicsAsset);
            PipeFrames = physics.PipeEnterDuration;

            CurrentPipe = pipe;

            var pipeComponent = f.Unsafe.GetPointer<EnterablePipe>(pipe);
            PipeDirection = pipeComponent->IsCeilingPipe ? FPVector2.Up : FPVector2.Down;

            var pipeTransform = f.Unsafe.GetPointer<Transform2D>(pipe);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(mario);
            marioTransform->Position.X = pipeTransform->Position.X;

            IsCrouching = false;
            IsSliding = false;
            IsPropellerFlying = false;
            UsedPropellerThisJump = false;
            PropellerLaunchFrames = 0;
            PropellerSpinFrames = 0;
            IsSpinnerFlying = false;
            IsInShell = false;
            PipeEntering = true;

            if (InvincibilityFrames > 0) {
                InvincibilityFrames += (ushort) (PipeFrames * 2);
            }

            f.Events.MarioPlayerEnteredPipe(mario, CurrentPipe);
        }
    }
}