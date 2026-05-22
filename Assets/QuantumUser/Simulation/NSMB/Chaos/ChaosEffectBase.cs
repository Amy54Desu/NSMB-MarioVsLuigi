using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Linq;
using UnityEngine;

namespace Quantum {
    public abstract unsafe class ChaosEffectBase : AssetObject, IOrderedAsset {

        int IOrderedAsset.Order => Order;

        public string NamePrefix, TranslationKey, DescriptionTranslationKey;
        public int Order;

        public FP EffectWeight;
        public FP[] EffectTimes;
        public int[] MaxResetTimes = { 1 };
        public int EffectVariationCount = 1;

        [Tooltip("When true the effect time will be randomly selected between the first element of the array and the last element of the array.")]
        public bool RandomTimeBetweenArray = false;
        public bool RandomResetBetweenArray = false;

        //---properties
        public bool IsEffectEnabled => remainingResets > 0;
        public int EffectVariation { get; private set; }

        //---private variables
        private int remainingResets;
        private FP remainingTime;

        public void EnableEffect(Frame f) {
            int resetCount;
            FP startingTime;
            if (RandomTimeBetweenArray) {
                startingTime = f.RNG->NextInclusive(EffectTimes.First(), EffectTimes.Last());
            } else {
                startingTime = EffectTimes[f.RNG->Next(0, EffectTimes.Length)];
            }

            if (RandomResetBetweenArray) {
                resetCount = f.RNG->NextInclusive(MaxResetTimes.First(), MaxResetTimes.Last());
            } else {
                resetCount = MaxResetTimes[f.RNG->Next(0, MaxResetTimes.Length)];
            }

            remainingTime = startingTime;
            remainingResets = resetCount;
            EffectVariation = f.RNG->Next(0, EffectVariationCount);
            OnEffectEnable(f);
        }

        public void DecrementEffectTimer(Frame f) {
            remainingTime -= f.DeltaTime;

            OnUpdate(f);
            if (remainingTime <= 0) {
                remainingResets--;
                OnEffectTimerReset(f);
            }
        }

        public abstract void OnEffectEnable(Frame f);

        public abstract void OnEffectDisable(Frame f);

        public abstract void OnEffectTimerReset(Frame f);

        public abstract void OnUpdate(Frame f);

        public bool CanBaseEffectPlay(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.BannedChaosEffects.Contains(this)) {
                return false;
            }

            return CanEffectPlay(f);
        }

        public abstract bool CanEffectPlay(Frame f);
    }
}