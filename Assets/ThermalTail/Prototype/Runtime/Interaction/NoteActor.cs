namespace ThermalTail.Prototype
{
    public sealed class NoteActor : CollectibleActor
    {
        public NoteData Note;
        protected override string Id => Note != null ? Note.Id : CollectionId;
        protected override bool CanCollect => Note != null;
        protected override void ApplyReward() { Session.AddNote(Note); }
    }
}
