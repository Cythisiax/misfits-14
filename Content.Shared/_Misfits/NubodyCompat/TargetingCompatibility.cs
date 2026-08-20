using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.NubodyCompat;

/// <summary>
/// Legacy combat hit regions retained for fork projectile/UI messages. NuBody itself is organ-based.
/// </summary>
// #Cythisiax Added - keeps non-medical targeted combat data outside the upstream-owned NuBody tree.
[Flags, Serializable, NetSerializable]
public enum TargetBodyPart : ushort
{
    Head = 1,
    Torso = 1 << 1,
    Groin = 1 << 2,
    LeftArm = 1 << 3,
    LeftHand = 1 << 4,
    RightArm = 1 << 5,
    RightHand = 1 << 6,
    LeftLeg = 1 << 7,
    LeftFoot = 1 << 8,
    RightLeg = 1 << 9,
    RightFoot = 1 << 10,
    Hands = LeftHand | RightHand,
    Arms = LeftArm | RightArm,
    Legs = LeftLeg | RightLeg,
    Feet = LeftFoot | RightFoot,
    All = Head | Torso | Groin | LeftArm | LeftHand | RightArm | RightHand | LeftLeg | LeftFoot | RightLeg | RightFoot,
}

// #Cythisiax Added - preserves the analyzer wire format until its UI is converted to organ data.
[Serializable, NetSerializable]
public enum TargetIntegrity : byte
{
    Healthy,
    LightlyWounded,
    ModeratelyWounded,
    HeavilyWounded,
    CriticallyWounded,
    Dead,
    Disabled,
    Severed,
}
