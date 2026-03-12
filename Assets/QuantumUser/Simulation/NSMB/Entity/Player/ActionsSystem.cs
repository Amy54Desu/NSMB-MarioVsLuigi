using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Filter = Quantum.MarioPlayerSystem.Filter;
using UnityEngine;

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
    }

    public enum PlayerAction {
        Stationary,
        Walking,
        Jump,
        DoubleJump,
        TripleJump,
        Wallslide,
        Wallkick,
        Crouch,
        Sliding,
        Groundpound,
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
        EnterPipe,
        HoldStationary,
        HoldJump,
        HoldFall,
        Dead,
        DeadLava,
        DeadPit,
        Respawning,
        Pushing,
        Swimming,
        SwimmingKnockback
    }

    public abstract class ActionBase {
        public abstract string AnimatorBool { get; }
        public abstract ActionFlags DefaultFlags { get; }
        public abstract PlayerAction? CancelAction { get; }

        /**
         * <summary>This is called when the action is set.</summary>
         */
        public virtual void OnSet(Frame f, ref Filter filter) { }

        /**
         * <summary>This is called every frame while the player does the action.</summary>
         */
        public virtual void OnUpdate(Frame f, ref Filter filter) { }

        /**
         * <summary>This is called when the action is exit.</summary>
         */
        public virtual void OnExit(Frame f, ref Filter filter) { }

        public int GetAnimatorHash => Animator.StringToHash(AnimatorBool);
    }

    public class ActionStationary : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags => ActionFlags.None;
        public override PlayerAction? CancelAction => null;
    }

    public class ActionWalking : ActionBase {
        public override string AnimatorBool => "s";
        public override ActionFlags DefaultFlags => ActionFlags.None;
        public override PlayerAction? CancelAction => null;
    }



    public class ActionSystem {
        public static Dictionary<PlayerAction, ActionBase> actionList = new Dictionary<PlayerAction, ActionBase>() {
            {PlayerAction.Walking, new ActionWalking()}
        };
    }
}
