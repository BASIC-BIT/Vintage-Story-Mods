using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterHungerSnapshot
{
    [ProtoMember(1)]
    public bool Available { get; set; }

    [ProtoMember(2)]
    public float Saturation { get; set; }

    [ProtoMember(3)]
    public float MaxSaturation { get; set; }

    [ProtoMember(4)]
    public float FruitLevel { get; set; }

    [ProtoMember(5)]
    public float VegetableLevel { get; set; }

    [ProtoMember(6)]
    public float ProteinLevel { get; set; }

    [ProtoMember(7)]
    public float GrainLevel { get; set; }

    [ProtoMember(8)]
    public float DairyLevel { get; set; }

    [ProtoMember(9)]
    public float SaturationLossDelayFruit { get; set; }

    [ProtoMember(10)]
    public float SaturationLossDelayVegetable { get; set; }

    [ProtoMember(11)]
    public float SaturationLossDelayProtein { get; set; }

    [ProtoMember(12)]
    public float SaturationLossDelayGrain { get; set; }

    [ProtoMember(13)]
    public float SaturationLossDelayDairy { get; set; }
}
