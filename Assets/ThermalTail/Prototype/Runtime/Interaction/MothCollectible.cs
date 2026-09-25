namespace ThermalTail.Prototype
{
    public sealed class MothCollectible : CollectibleActor
    {
        protected override void ApplyReward() { Session.AddMoth(); }
    }
}
