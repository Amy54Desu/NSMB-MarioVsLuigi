using Photon.Deterministic;
using UnityEngine;

namespace Quantum
{
    public unsafe class ChaosEffectSystem : SystemMainThread {
        public override void OnInit(Frame f) {
            f.Global->NextChaosEffectTimer = 20 * 60;
        }
        public override void Update(Frame f) {
            VersusStageData stage = null;

            if (QuantumUtils.Decrement(ref f.Global->NextChaosEffectTimer)) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                HandleNewChaos(f, stage);
            }
        }

        public void HandleNewChaos(Frame f, VersusStageData stage) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            var newChaosEffect = gamemode.GetRandomChaos(f);

            if (newChaosEffect.Type.HasFlag(ChaosEffectType.Player)) {
                // apply to all players
                FP remainingTime = newChaosEffect.GetStartTime(f);
                int resetCount = newChaosEffect.GetResetNum(f);
                var marioPlayerFilter = f.Filter<MarioPlayer>();
                while(marioPlayerFilter.NextUnsafe(out _, out var marioPlayer)) {
                    marioPlayer->AddChaosEffect(f, newChaosEffect, remainingTime, resetCount);
                }
            }
            f.Global->NextChaosEffectTimer = 20 * 60;
        }
    }
}
