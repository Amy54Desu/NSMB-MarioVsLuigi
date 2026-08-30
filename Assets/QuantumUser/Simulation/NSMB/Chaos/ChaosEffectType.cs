[System.Flags]
public enum ChaosEffectType : ushort {
    None = 0,
    
    // determine who can get the effect
    Player = 1 << 0,
    Global = 1 << 2,
    Enemy = 1 << 3,

    // 
    UnderwaterOverride = 1 << 4,
}