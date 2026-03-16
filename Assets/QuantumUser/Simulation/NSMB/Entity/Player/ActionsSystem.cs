using Photon.Deterministic;
using Quantum.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;
using Filter = Quantum.MarioPlayerSystem.Filter;

namespace Quantum
{
    [Flags]
    public enum ActionFlags {
        None = 0,
        Airborne = 1 << 1,
        AllowWallkick = 1 << 2,
        AllowGrab = 1 << 3,
        StarSpinning = 1 << 4,
        InstaKillEnemies = 1 << 5,
        AllowProjectileShoot = 1 << 6,
        Custscene = 1 << 7,
        HoldingEntity = 1 << 8,
        AllowHold = 1 << 9,
        Cutscene = 1 << 10,
        SpecialEnemyKill = 1 << 11,
        DisablePlayerInteraction = 1 << 12,
        DisableEnemyInteraction = 1 << 13,
        DisablePlayerBounce = 1 << 14,
        DisableEnemyBounce = 1 << 15,
        UseCrouchHitbox = 1 << 16,
        UseSmallHitbox = 1 << 17,
        Swimming = 1 << 18,
    }

    public enum PlayerAction {
        Stationary,
        Freefall,
        Walking,
        Jump,
        DoubleJump,
        TripleJump,
        Wallslide,
        Wallkick,
        Crouch,
        CrouchJump,
        CrouchAir,
        Sliding,
        Groundpound,
        GroundpoundGround,
        NormalKnockback,
        HardKnockback,
        BumpKnockback,
        NormalKnockbackAir,
        HardKnockbackAir,
        BumpKnockbackAir,
        KnockbackEnd,
        BlueShellCrouch,
        BlueShellSpinning,
        BlueShellJump,
        BlueShellAir,
        SpinnerJump,
        SpinnerDrill,
        PropellerUse,
        PropellerDrill,
        MegaMushroomStart,
        EnteringPipe,
        HoldStationary,
        HoldJump,
        HoldFall,
        Dead,
        DeadLava,
        DeadPit,
        Respawning,
        Pushing,
        Swimming,
        SwimmingHold,
        SwimmingKnockback
    }

    #region Action Definitions
    public abstract unsafe class ActionBase {
        public abstract string AnimatorBool { get; }
        public abstract ActionFlags DefaultFlags(Frame f, EntityRef marioEntity);
        public abstract PlayerAction? CancelAction { get; }

        /**
         * <summary>This is called when the action is set.</summary>
         */
        public virtual void OnEnter(Frame f, MarioPlayerPhysicsInfo physics, EntityRef[] entityRefs, VersusStageData stage) { }
        /**
         * <summary>This is called every frame while the player does the action.</summary>
         */
        public virtual void OnUpdate(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) { }

        /**
         * <summary>This is called when the action is exit.</summary>
         */
        public virtual void OnExit(Frame f, PlayerAction incomingAction, MarioPlayerPhysicsInfo physics, EntityRef[] marioEntity, VersusStageData stage) { }

        /**
         * <summary>Mainly focuses on the side-to-side movement of the action such as running.</summary>
         */
        public virtual void GetMovementSpeed(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) => ActionHelpers.DefaultActionMovement(f, ref filter, physics);

        /**
         * <summary>The gravity for the action. This is the default for actions.</summary>
         */
        public virtual FP GetGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) => ActionHelpers.DefaultActionGravity(f, ref filter, physics);

        /**
        * <summary>The max falling speed for the action. This is the default for actions.</summary>
        */
        public virtual FP GetTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) => ActionHelpers.DefaultActionTV(f, ref filter, physics);
        public virtual void HandleFacingDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) => ActionHelpers.DefaultActionDirection(f, ref filter, physics);

        public int GetAnimatorHash => Animator.StringToHash(AnimatorBool);
    }

    public unsafe class ActionStationary : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.None;
        public override PlayerAction? CancelAction => PlayerAction.Freefall;
        public override void OnUpdate(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var inputs = filter.Inputs;

            // crouch check
            if (inputs.Down.IsDown) {
                PlayerAction crouchAction = mario->CurrentPowerupState == PowerupState.BlueShell ? PlayerAction.BlueShellCrouch : PlayerAction.Crouch;
                mario->SetAction(f, new EntityRef[] { filter.Entity }, physics, crouchAction, 0);
                f.Events.MarioPlayerCrouched(filter.Entity, mario->CurrentPowerupState);
                return;
            }
        }
    }

    public unsafe class ActionCrouching : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.UseCrouchHitbox;
        public override PlayerAction? CancelAction => PlayerAction.CrouchAir;
        public override void OnExit(Frame f, PlayerAction incomingAction, MarioPlayerPhysicsInfo physics, EntityRef[] marioEntity = default, VersusStageData stage = default) {
            ActionSystem.actionList.TryGetValue(incomingAction, out var incomingActionData);
            if (incomingActionData.DefaultFlags(f, marioEntity[0]).HasFlag(ActionFlags.Airborne)) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity[0]);
                physicsObject->Velocity.Y = physics.CrouchOffEdgeVelocity;
            }
        }
    }

    public unsafe class ActionNormalKnockback : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionHelpers.AirborneFlagIfAirborne(f, marioEntity);
        public override PlayerAction? CancelAction => PlayerAction.NormalKnockbackAir;
        public override void OnEnter(Frame f, MarioPlayerPhysicsInfo physics, EntityRef[] entityRefs, VersusStageData stage) {
            //if ()
        }

        // disable the ability to change direction
        public override void HandleFacingDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) { }
    }

    public unsafe class ActionWalking : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.None;
        public override PlayerAction? CancelAction => null;
    }

    public unsafe class ActionGroundpound : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.Airborne;
        public override PlayerAction? CancelAction => PlayerAction.GroundpoundGround;
        public override FP GetTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            filter.PhysicsObject->Velocity.X = 0;
            return physics.TerminalVelocityGroundpound;
        }
    }

    public unsafe class ActionDead : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.Custscene;
        public override PlayerAction? CancelAction => null;
        public override void OnEnter(Frame f, MarioPlayerPhysicsInfo physics, EntityRef[] entityRefs, VersusStageData stage) {
            EntityRef marioRef = entityRefs[0];
            EntityRef attackerRef = entityRefs[1];
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioRef);
            QuantumUtils.Decrement(ref mario->Lives);
            f.Unsafe.GetPointer<Interactable>(marioRef)->ColliderDisabled = true;

            if (mario->ActionArg == 0) {
                f.Signals.OnMarioPlayerDropObjective(marioRef, 1, attackerRef);
            }

            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                holdable->DropWithoutThrowing(f, mario->HeldEntity);
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioRef);
            physicsObject->IsFrozen = true;
            physicsObject->DisableCollision = true;
            physicsObject->CurrentData = default;

            f.Signals.OnMarioPlayerDied(marioRef);
            f.Events.MarioPlayerDied(marioRef, false);
        }

        public override void OnUpdate(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var marioEntity = filter.Entity;

            if (++mario->ActionTimer > 180) {
                mario->SetAction(f, new EntityRef[] { marioEntity }, physics, PlayerAction.Respawning, 0);
            }
        }

        public override FP GetTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var physicsObject = filter.PhysicsObject;
            bool isUnderwater = false;
            if (physicsObject->IsUnderwater) {
                var contacts = f.ResolveHashSet(physicsObject->LiquidContacts);
                foreach (var contact in contacts) {
                    if (f.Unsafe.GetPointer<Liquid>(contact)->LiquidType == LiquidType.Water) {
                        isUnderwater = true;
                        break;
                    }
                }
            }
            if (isUnderwater) {
                return -Constants.OnePixelPerFrame;
            } else {
                return -8;
            }
        }
    }

    public unsafe class ActionDeadLava : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags(Frame f, EntityRef marioEntity) => ActionFlags.Custscene;
        public override PlayerAction? CancelAction => null;
        public override void OnEnter(Frame f, MarioPlayerPhysicsInfo physics, EntityRef[] entityRefs, VersusStageData stage) {
            EntityRef marioRef = entityRefs[0];
            EntityRef attackerRef = entityRefs[1];
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioRef);
            QuantumUtils.Decrement(ref mario->Lives);
            f.Unsafe.GetPointer<Interactable>(marioRef)->ColliderDisabled = true;

            if (mario->ActionArg == 0) {
                f.Signals.OnMarioPlayerDropObjective(marioRef, 1, attackerRef);
            }

            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
                holdable->DropWithoutThrowing(f, mario->HeldEntity);
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioRef);
            physicsObject->IsFrozen = true;
            physicsObject->DisableCollision = true;
            physicsObject->CurrentData = default;

            f.Signals.OnMarioPlayerDied(marioRef);
            f.Events.MarioPlayerDied(marioRef, true);
        }

        public override void OnUpdate(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics, VersusStageData stage) {
            var mario = filter.MarioPlayer;
            var marioEntity = filter.Entity;

            if (++mario->ActionTimer > 180) {
                mario->SetAction(f, new EntityRef[] {marioEntity}, physics, PlayerAction.Respawning, 0);
            }
        }

        public override FP GetTerminalVelocity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) => -8;
    }
    #endregion

    #region Action Helpers

    public unsafe class ActionHelpers {
        // terminal velocities
        public static FP DefaultSwimmingTV(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var physicsObject = filter.PhysicsObject;
            ref var inputs = ref filter.Inputs;
            physicsObject->Velocity.Y = FPMath.Min(physicsObject->Velocity.Y, physics.SwimMaxVerticalVelocity);
            return inputs.Jump.IsDown ? physics.SwimTerminalVelocityButtonHeld : physics.SwimTerminalVelocity;
        }

        public static FP DefaultActionTV(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            FP terminalVelocityModifier = mario->CurrentPowerupState switch {
                PowerupState.MiniMushroom => physics.TerminalVelocityMiniMultiplier,
                PowerupState.MegaMushroom => physics.TerminalVelocityMegaMultiplier,
                _ => 1,
            };
            return physics.TerminalVelocity * terminalVelocityModifier;
        }

        // gravities
        public static FP DefaultSwimmingGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            int stage = mario->GetGravityStage(physicsObject, physics);
            bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
            bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;


            FP[] accArr = physics.GravitySwimmingAcceleration;
            FP acc = accArr[stage];

            ref var inputs = ref filter.Inputs;
            if (stage == 0) {
                acc = accArr[^1];
            }

            return acc;
        }

        public static FP DefaultActionGravity(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            int stage = mario->GetGravityStage(physicsObject, physics);
            bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
            bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;


            FP[] accArr = mega ? physics.GravityMegaAcceleration : (mini ? physics.GravityMiniAcceleration : physics.GravityAcceleration);
            FP acc = accArr[stage];

            ref var inputs = ref filter.Inputs;
            if (stage == 0 && !(inputs.Jump.IsDown && mario->ForceJumpTimer > 0)) {
                acc = accArr[^1];
            }

            return acc;
        }

        // side-to-side movement, i.e running
        public static void DefaultActionMovement(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;
            ref var inputs = ref filter.Inputs;

            bool mega = mario->CurrentPowerupState == PowerupState.MegaMushroom;
            bool run = (inputs.Sprint.IsDown || mega || mario->IsPropellerFlying) && (mega || !mario->IsSpinnerFlying);

            int maxStage;
            if (run) {
                maxStage = physics.RunSpeedStage;
            } else {
                maxStage = physics.WalkSpeedStage;
            }

            int stage = mario->GetSpeedStage(physicsObject, physics);
            FP[] maxArray = physics.WalkMaxVelocity;

            FP acc;
            if (physicsObject->IsOnSlipperyGround) {
                acc = physics.WalkIceAcceleration[stage];
            } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                acc = physics.WalkMegaAcceleration[stage];
            } else {
                acc = physics.WalkAcceleration[stage];
            }

            FP xVel = physicsObject->Velocity.X;
            FP xVelAbs = FPMath.Abs(xVel);
            int sign = FPMath.SignInt(xVel);
            bool uphill = FPMath.Abs(physicsObject->FloorAngle) > physics.SlideMinimumAngle && FPMath.SignInt(physicsObject->FloorAngle) != sign;

            if (!physicsObject->IsTouchingGround) {
                mario->FastTurnaroundFrames = 0;
            }

            if (mario->FastTurnaroundFrames > 0) {
                physicsObject->Velocity.X = 0;
                if (QuantumUtils.Decrement(ref mario->FastTurnaroundFrames)) {
                    mario->IsTurnaround = true;
                }
            } else if (mario->IsTurnaround && !physicsObject->IsOnSlipperyGround) {
                // Can't fast turnaround on ice.
                mario->IsTurnaround = physicsObject->IsTouchingGround && !mario->IsCrouching && xVelAbs < physics.WalkMaxVelocity[1] && !physicsObject->IsTouchingLeftWall && !physicsObject->IsTouchingRightWall;
                mario->IsSkidding = mario->IsTurnaround;

                physicsObject->Velocity.X += (physics.FastTurnaroundAcceleration * (mario->FacingRight ? -1 : 1) * f.DeltaTime);
            } else if (inputs.Left ^ inputs.Right) {

                // We can walk here
                int direction = inputs.Left ? -1 : 1;
                if (mario->IsSkidding) {
                    direction = -sign;
                }

                bool reverse = physicsObject->Velocity.X != 0 && (direction != sign);

                // Check that we're not going above our limit
                FP max = maxArray[maxStage];
                FP maxAcceleration = FPMath.Abs(max - xVelAbs) * f.UpdateRate;
                acc = FPMath.Clamp(acc, -maxAcceleration, maxAcceleration);
                if (xVelAbs > max) {
                    acc = -acc;
                }

                if (reverse) {
                    mario->IsTurnaround = false;
                    if (physicsObject->IsTouchingGround) {
                        if (xVelAbs >= physics.SkiddingMinimumVelocity && !mario->HeldEntity.IsValid && mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                            mario->IsSkidding = true;
                            mario->FacingRight = sign == 1;
                        }

                        if (mario->IsSkidding) {
                            if (physicsObject->IsOnSlipperyGround) {
                                acc = physics.SkiddingIceDeceleration;
                            } else if (xVelAbs > maxArray[physics.RunSpeedStage]) {
                                acc = physics.SkiddingStarmanDeceleration;
                            } else {
                                acc = physics.SkiddingDeceleration;
                            }

                            mario->SlowTurnaroundFrames = 0;
                        } else {
                            if (physicsObject->IsOnSlipperyGround) {
                                acc = physics.SlowTurnaroundIceAcceleration;
                            } else {
                                mario->SlowTurnaroundFrames = (byte) FPMath.Clamp(mario->SlowTurnaroundFrames + 1, 0,
                                    physics.SlowTurnaroundAcceleration.Length - 1);
                                acc = mario->CurrentPowerupState == PowerupState.MegaMushroom
                                    ? physics.SlowTurnaroundMegaAcceleration[mario->SlowTurnaroundFrames]
                                    : physics.SlowTurnaroundAcceleration[mario->SlowTurnaroundFrames];
                            }
                        }
                    } else {
                        // TODO: change 0.85 to a constant?
                        acc = physics.WalkAcceleration[0] * Constants._0_85;
                    }
                } else {
                    mario->SlowTurnaroundFrames = 0;
                    mario->IsSkidding &= !mario->IsTurnaround;
                }

                FP newX = xVel + (acc * f.DeltaTime * direction);

                if ((xVel < max && newX > max) || (xVel > -max && newX < -max)) {
                    newX = FPMath.Clamp(newX, -max, max);
                }

                if (mario->IsSkidding && !mario->IsTurnaround && (FPMath.Sign(newX) != sign || xVelAbs < FP._0_05)) {
                    // Turnaround
                    mario->FastTurnaroundFrames = 10;
                    newX = 0;
                }

                physicsObject->Velocity.X = newX;

            } else if (physicsObject->IsTouchingGround) {
                // Not holding anything, sliding, or holding both directions. decelerate
                mario->IsSkidding = false;
                mario->IsTurnaround = false;

                FP angle = FPMath.Abs(physicsObject->FloorAngle);
                if (mario->IsInKnockback) {
                    if (physicsObject->IsOnSlipperyGround) {
                        acc = mario->KnockForwards ? -physics.StomachKnockbackIceDeceleration : -physics.SittingKnockbackIceDeceleration;
                    } else {
                        acc = mario->KnockForwards ? -physics.StomachKnockbackDeceleration : -physics.SittingKnockbackDeceleration;
                    }
                } else if (mario->IsSliding) {
                    if (angle > physics.SlideMinimumAngle) {
                        // Uphill / downhill
                        acc = (angle > 30 ? physics.SlideFastAcceleration : physics.SlideSlowAcceleration) * (uphill ? -1 : 1);
                    } else {
                        // Flat ground
                        acc = -physics.WalkAcceleration[0];
                    }
                } else if (physicsObject->IsOnSlipperyGround) {
                    acc = -physics.WalkButtonReleaseIceDeceleration[stage];
                } else {
                    acc = -physics.WalkButtonReleaseDeceleration;
                }

                FP newX = xVel + acc * f.DeltaTime * sign;
                FP target = (angle > 30 && physicsObject->IsOnSlideableGround) ? FPMath.Sign(physicsObject->FloorAngle) * physics.WalkMaxVelocity[0] : 0;
                if ((sign == -1) ^ (newX <= target)) {
                    newX = target;
                }

                if (mario->IsSliding) {
                    newX = FPMath.Clamp(newX, -physics.SlideMaxVelocity, physics.SlideMaxVelocity);
                }

                physicsObject->Velocity.X = newX;

                if (newX != 0) {
                    mario->FacingRight = newX > 0;
                }
            }
        }

        public static void DefaultActionDirection(Frame f, ref Filter filter, MarioPlayerPhysicsInfo physics) {
            using var profilerScope = HostProfiler.Start("MarioPlayerSystem.HandleFacingDirection");
            var mario = filter.MarioPlayer;
            var physicsObject = filter.PhysicsObject;

            ref var inputs = ref filter.Inputs;
            bool rightOrLeft = (inputs.Right.IsDown ^ inputs.Left.IsDown);

            if (!physicsObject->IsTouchingGround) {
                if (rightOrLeft) {
                    mario->FacingRight = inputs.Right.IsDown;
                }
            } else {
                mario->FacingRight = physicsObject->Velocity.X > 0;
            }
        }

        public static bool IsActionKnockbackAction(PlayerAction testAction) {
            return testAction is PlayerAction.NormalKnockback or PlayerAction.HardKnockback or PlayerAction.NormalKnockbackAir or PlayerAction.HardKnockbackAir;
        }

        public static ActionFlags AirborneFlagIfAirborne(Frame f, EntityRef marioEntity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
            return physicsObject->IsTouchingGround ? ActionFlags.Airborne : ActionFlags.None;
        }
    }

    #endregion

    public class ActionSystem {
        public static Dictionary<PlayerAction, ActionBase> actionList = new Dictionary<PlayerAction, ActionBase>() {
            {PlayerAction.Walking, new ActionWalking()},
            {PlayerAction.Groundpound, new ActionGroundpound()},
            {PlayerAction.Dead, new ActionDead()},
            {PlayerAction.DeadLava, new ActionDeadLava()}
        };
    }
}
