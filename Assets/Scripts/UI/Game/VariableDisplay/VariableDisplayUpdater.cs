using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NSMB.UI.Game.VariableDisplay {
    public class VariableDisplayUpdater : MonoBehaviour {
        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private TMP_Text variablesText;

        //---Private Variables
        private bool isToggled;
        private StringBuilder stringBuilder = new();

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public unsafe void Start() {
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        }

        private string bitSetToString(BitSet64 bitSet64, int length) {
            string str = "";
            for (int i = 0; i < length; i++) {
                str += bitSet64.IsSet(i) ? '-' : 'O';
            }
            return str;
        }

        public unsafe void OnUpdateView(CallbackUpdateView e) {
            Frame f = e.Game.Frames.Predicted;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stringBuilder.Clear();

            // for stars
            var nextStarFrames = f.Global->BigStarSpawnTimer;
            var nextStarSec = nextStarFrames / f.UpdateRate;
            stringBuilder.AppendLine("Next star in: " + nextStarFrames + " (" + nextStarSec + " sec.)");
            stringBuilder.AppendLine("Star spawnpoint data: "+bitSetToString(f.Global->UsedStarSpawns, stage.BigStarSpawnpoints.Length));
            stringBuilder.AppendLine("Remaining star spawns: "+(stage.BigStarSpawnpoints.Length - f.Global->UsedStarSpawns.GetSetCount()));

            variablesText.SetText(stringBuilder);
        }
    }
}
