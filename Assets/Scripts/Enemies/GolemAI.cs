namespace RPG
{
    /// <summary>
    /// Golem Đá Nhỏ (Hang Pha Lê): a squat golem of cave rock with crystals grown through it.
    /// Slow and hard to hurt with blades; it raises both fists (a ring on the ground warns) and
    /// brings them down in front of it, stunning whoever is under them (Choáng). Its slam is the
    /// mud man's (<see cref="MudManAI"/>) in rock, and it does not split when it falls: it crumbles.
    /// </summary>
    public class GolemAI : MudManAI
    {
        protected override string SlamVfx => "rock_impact";
        protected override string SlamSound => "sfx_boss_stomp";
        protected override string SlamName => "Nắm Đấm Đá";
    }
}
