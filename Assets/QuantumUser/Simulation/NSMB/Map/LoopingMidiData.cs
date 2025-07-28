using MidiPlayerTK;
using Quantum;

public class LoopingMidiData : AssetObject {

#if QUANTUM_UNITY
    public MidiFilePlayer midiPlayer;
#endif
    public float loopStartSeconds;
    public float loopEndSeconds;
    public float speedupFactor = 1.25f;
}