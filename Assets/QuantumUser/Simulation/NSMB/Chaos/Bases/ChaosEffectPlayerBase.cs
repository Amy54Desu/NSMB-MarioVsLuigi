using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Linq;
using UnityEngine;

namespace Quantum {
    public abstract unsafe class ChaosEffectPlayerBase : ChaosEffectBase {
        public MarioPlayer* AssignedPlayer;
    }
}