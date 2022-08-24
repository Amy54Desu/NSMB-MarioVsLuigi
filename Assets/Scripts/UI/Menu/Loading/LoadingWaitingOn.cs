using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Pun;
public class LoadingWaitingOn : MonoBehaviour {
    private TMP_Text text;
    public string emptyText = "NOW LOADING...", iveLoadedText = "Waiting for others...", readyToStartText = "スタートちゅう...", spectatorText = "Joining as Spectator...";
    void Start() {
        text = GetComponent<TMP_Text>();
    }

    void Update() {
        if (!GameManager.Instance) 
            return;
        if (GlobalController.Instance.joinedAsSpectator) {
            text.text = spectatorText;
            return;
        }
        if (GameManager.Instance.loaded) {
            text.text = readyToStartText;
            return;
        }
        if (GameManager.Instance.loadedPlayers.Count == 0) 
            return;
        text.text = iveLoadedText;
    }
}
