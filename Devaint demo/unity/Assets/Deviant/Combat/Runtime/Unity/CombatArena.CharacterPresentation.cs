namespace Deviant.Combat
{
    public partial class CombatArena
    {
        CharacterPresenter presentation;
        CharacterPresentationBinding characterBinding;
        internal CharacterPresentationBinding FeelCharacterBinding => characterBinding;
        public CharacterPresenter Presentation
        {
            get
            {
                if (presentation == null)
                {
                    characterBinding = new CharacterPresentationBinding(this);
                    presentation = new CharacterPresenter(characterBinding);
                }
                return presentation;
            }
        }
        public void CaptureCharacterAttack(CombatCast cast)
        {
            _ = Presentation;
            characterBinding.Capture(cast);
            FeelFeedback?.CaptureFeelCast(cast);
            Audio?.Attack(cast);
        }
        void ReleaseCharacterPresentation()
        {
            presentation?.ReleaseBinding();
        }
    }
}
