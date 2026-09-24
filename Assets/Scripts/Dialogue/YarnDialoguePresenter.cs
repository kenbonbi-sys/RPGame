using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Yarn.Markup;
using Yarn.Unity;

namespace RPG
{
    /// <summary>
    /// Shows Yarn lines and choices in the game's own dialogue box (<see cref="DialogueUI"/>):
    /// portrait, typewriter, blips. Yarn markup is turned into TextMeshPro rich text:
    /// [k]…[/k] key hints (gold), [warn]…[/warn] dangerous moves (red), [b] / [i] as usual.
    /// </summary>
    public class YarnDialoguePresenter : DialoguePresenterBase
    {
        /// <summary>Called when the runner has finished a conversation (normally or stopped).</summary>
        public System.Action Completed;

        public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

        public override YarnTask OnDialogueCompleteAsync()
        {
            Completed?.Invoke();
            return YarnTask.CompletedTask;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            var ui = DialogueUI.I;
            if (ui == null) return;
            ui.ShowLine(line.CharacterName ?? "", ToRichText(line.TextWithoutCharacterName));
            while (!ui.AdvanceRequested && !token.NextContentToken.IsCancellationRequested)
                await YarnTask.Yield();
        }

        public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
        {
            var ui = DialogueUI.I;
            if (ui == null || options.Length == 0) return null;
            var shown = new List<DialogueOption>();
            var labels = new List<string>();
            foreach (var o in options)
            {
                if (!o.IsAvailable) continue;
                shown.Add(o);
                labels.Add(ToRichText(o.Line.TextWithoutCharacterName));
            }
            if (shown.Count == 0) return null;
            ui.ShowOptions(labels);
            while (ui.ChosenOption < 0 && !token.NextContentToken.IsCancellationRequested)
                await YarnTask.Yield();
            int pick = ui.ChosenOption;
            ui.HideOptions();
            return pick >= 0 && pick < shown.Count ? shown[pick] : null;
        }

        // ------------------------------------------------------------------ markup
        static readonly Dictionary<string, (string open, string close)> Tags = new Dictionary<string, (string, string)>
        {
            { "k", ("<color=#ffe07a>", "</color>") },
            { "warn", ("<color=#ff9a7a>", "</color>") },
            { "b", ("<b>", "</b>") },
            { "i", ("<i>", "</i>") },
        };

        /// <summary>Plain text + Yarn markup attributes → TextMeshPro rich text.</summary>
        public static string ToRichText(MarkupParseResult text)
        {
            string s = text.Text ?? "";
            var inserts = new List<(int pos, int order, string tag)>();
            foreach (var a in text.Attributes)
            {
                if (!Tags.TryGetValue(a.Name, out var t)) continue;
                inserts.Add((a.Position, 1, t.open));
                inserts.Add((a.Position + a.Length, 0, t.close));   // closes go before opens at the same spot
            }
            if (inserts.Count == 0) return s;
            inserts.Sort((x, y) => x.pos != y.pos ? x.pos.CompareTo(y.pos) : x.order.CompareTo(y.order));
            var sb = new StringBuilder(s.Length + inserts.Count * 12);
            int at = 0;
            foreach (var ins in inserts)
            {
                int p = Mathf.Clamp(ins.pos, 0, s.Length);
                sb.Append(s, at, p - at);
                sb.Append(ins.tag);
                at = p;
            }
            sb.Append(s, at, s.Length - at);
            return sb.ToString();
        }
    }
}
