using Photon.Deterministic;
using UnityEngine;

namespace Quantum {
    public unsafe partial struct ChaosEffect {
        //---properties
        public readonly bool IsEffectEnabled => RemainingResets > 0;

        public void EnableEffect(Frame f, ChaosEffectBase chaosEffectBase, FP? startingTime = null, int? resetCount = null, int? variation = null) {
            ChaosEffectBase = chaosEffectBase;
            var assetBase = f.FindAsset(ChaosEffectBase);

            RemainingTime = startingTime ?? assetBase.GetStartTime(f);
            RemainingResets = resetCount ?? assetBase.GetResetNum(f);
            EffectVariation = variation ?? f.RNG->Next(0, assetBase.EffectVariationCount);
            assetBase.OnEffectEnable(f);
        }

        public void DecrementEffectTimer(Frame f) {
            var assetBase = f.FindAsset(ChaosEffectBase);
            RemainingTime -= f.DeltaTime;

            assetBase.OnUpdate(f);
            if (RemainingTime <= 0) {
                RemainingResets--;
                assetBase.OnEffectTimerReset(f);
            }
        }
    }
}
