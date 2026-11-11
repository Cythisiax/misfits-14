using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Misfits.Fire;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class TileFireComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool ExtinguishInstantly = true;

    [DataField, AutoNetworkedField]
    public float VaporExtinguishSeconds = 2f;

    [DataField, AutoNetworkedField]
    public float FireStacks = 1.5f;

    [DataField, AutoNetworkedField]
    public float StandingDamage = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan TouchCooldown = TimeSpan.FromSeconds(0.5f);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SpawnedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public TimeSpan BigFireDuration = TimeSpan.FromSeconds(0.5f);

    // #Cythisiax Added - fire levels and spread (Gruntz FireDef ladder), see TileFireSystem header
    [DataField, AutoNetworkedField]
    public int Level;

    [DataField]
    public int StartLevel = 2;

    [DataField]
    public int MaxLevel = 7;

    [DataField]
    public int ReferenceLevel = 3;

    [DataField]
    public TimeSpan StepInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStep;

    [DataField]
    public float GrowChance = 0.35f;

    [DataField]
    public float DyingFraction = 0.25f;

    [DataField]
    public bool Eternal;

    [DataField]
    public bool Spreads = true;

    [DataField]
    public int SpreadAtLevel = 3;

    [DataField]
    public float SpreadChance = 0.12f;

    [DataField]
    public float SpreadFuelFraction = 0.8f;

    [DataField]
    public EntProtoId? SpreadPrototype;

    [DataField]
    public float RainSpreadMultiplier = 0.5f;

    [DataField]
    public List<string> WetWeathers = new() { "Rain", "Storm", "Hail" };

    [DataField]
    public int BloodClearLevel = 4;

    [DataField]
    public float DouseVolume = 5f;

    [DataField]
    public float FuelSecondsPerUnit = 0.5f;

    [DataField]
    public float MaxFuelBonusSeconds = 30f;

    [DataField]
    public float ScorchSeconds = 300f;
}

[Serializable, NetSerializable]
public enum TileFireVisuals : byte
{
    One,
    Two,
    Three,
    Four,
}
