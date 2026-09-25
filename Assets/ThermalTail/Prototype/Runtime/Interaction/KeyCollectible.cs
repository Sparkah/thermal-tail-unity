namespace ThermalTail.Prototype
{
    public sealed class KeyCollectible : CollectibleActor
    {
        protected override void ApplyReward() { Session.AddKey(); }
    }
}
