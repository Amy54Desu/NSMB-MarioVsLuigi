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

        public ChaosEffectType Type;

        private FP GetRandomStartTimeBetweenArray(Frame f) {
            return f.RNG->NextInclusive(EffectTimes[0], EffectTimes[EffectTimes.Length]);
        }

        private FP GetRandomStartTimeArray(Frame f) {
            return EffectTimes[f.RNG->Next(0, EffectTimes.Length)];
        }

        public FP GetStartTime(Frame f) {
            if (RandomTimeBetweenArray) {
                return GetRandomStartTimeBetweenArray(f);
            } else {
                return GetRandomStartTimeArray(f);
            }
        }

        private int GetRandomResetCountBetweenArray(Frame f) {
            return f.RNG->NextInclusive(MaxResetTimes[0], MaxResetTimes[MaxResetTimes.Length]);
        }

        private int GetRandomResetCountArray(Frame f) {
            return MaxResetTimes[f.RNG->Next(0, MaxResetTimes.Length)];
        }

        public int GetResetNum(Frame f) {
            if (RandomTimeBetweenArray) {
                return GetRandomResetCountBetweenArray(f);
            } else {
                return GetRandomResetCountArray(f);
            }
        }

        public virtual void OnEffectEnable(Frame f) { }

        public virtual void OnEffectDisable(Frame f) { }

        public virtual void OnEffectTimerReset(Frame f) { }

        public virtual void OnUpdate(Frame f) { }

        public bool CanBaseEffectPlay(Frame f) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            if (stage.BannedChaosEffects.Contains(this)) {
                return false;
            }

            return CanEffectPlay(f);
        }

        public virtual bool CanEffectPlay(Frame f) => true;
    }
}