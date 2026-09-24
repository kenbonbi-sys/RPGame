namespace RPG
{
    /// <summary>Controls overview (F1). Opens at start, closes with any key.</summary>
    public class HelpPanelUI : UIPanel
    {
        float shownAt;

        protected override void OnShow() => shownAt = UnityEngine.Time.unscaledTime;

        protected override void Update()
        {
            base.Update();
            if (IsOpen && UnityEngine.Time.unscaledTime - shownAt > 0.3f && InputReader.AnyKeyOrClick && !InputReader.ToggleHelp)
                Close();
        }
    }
}
