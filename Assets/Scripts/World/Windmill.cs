using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The windmill on Thủ Lĩnh Hắc Phong's hill: its sails turn as hard as the wind blows there
    /// (<see cref="Wind"/>), a lazy creak in a calm, a blur in a gust. The sails are drawn in
    /// <see cref="frames"/> a quarter turn apart (the pixel art keeps its lines). Screens only.
    /// </summary>
    public class Windmill : MonoBehaviour
    {
        public SpriteRenderer sails;
        [Tooltip("The sails through a quarter turn (they look the same every 90°).")]
        public Sprite[] frames = new Sprite[0];
        [Tooltip("Turns a second in a calm and at full wind.")]
        public float calmTurns = 0.08f, gustTurns = 0.9f;

        float turn, speed;

        void Update()
        {
            if (!GameSession.HasScreen || sails == null || frames == null || frames.Length == 0) return;
            float w = Wind.At(transform.position).magnitude;
            speed = Mathf.Lerp(speed, Mathf.Lerp(calmTurns, gustTurns, w), 1f - Mathf.Exp(-Time.deltaTime * 1.2f));
            turn = Mathf.Repeat(turn + speed * Time.deltaTime, 1f);
            // a quarter turn per run of frames: four runs a full turn
            int i = Mathf.FloorToInt(Mathf.Repeat(turn * 4f, 1f) * frames.Length) % frames.Length;
            if (sails.sprite != frames[i]) sails.sprite = frames[i];
        }
    }
}
