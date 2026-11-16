namespace Deviant.ThemePreview
{
    // Preview-only focus state: no references to inventory, saves or run state.
    public sealed class PreviewFocus
    {
        public int Tab { get; private set; }
        public int Depth { get; private set; }
        public int Slot { get; private set; }
        public int Ink { get; private set; }
        public bool Closed { get; private set; }
        public int[] Applied { get; } = { -1, 0, 2, 3, 4, -1 };
        public void SwitchTab(int step) { Tab = (Tab + step + 3) % 3; Depth = 0; Closed = false; }
        public void Move(int step)
        {
            if (Closed) return;
            if (Depth == 0) Tab = (Tab + step + 3) % 3;
            else if (Depth == 1) Slot = (Slot + step + 6) % 6;
            else Ink = (Ink + step + 5) % 5;
        }
        public void Confirm()
        {
            if (Closed) { Closed = false; return; }
            if (Tab != 1) return;
            if (Depth < 2) Depth++;
            else { Applied[Slot] = Ink; Depth = 1; }
        }
        public void Back() { if (Depth > 0) Depth--; else Closed = !Closed; }
    }
}
