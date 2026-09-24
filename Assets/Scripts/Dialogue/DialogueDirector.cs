using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

namespace RPG
{
    /// <summary>
    /// Runs conversations (plan §09: Yarn Spinner, one .yarn file per NPC). Owns a Yarn
    /// DialogueRunner built at start-up from GameDatabase.dialogue, shows it through
    /// <see cref="YarnDialoguePresenter"/> and saves the Yarn variables with the game.
    /// NPCs without a Yarn node fall back to their plain line list.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class DialogueDirector : MonoBehaviour, ISaveable
    {
        public static DialogueDirector I { get; private set; }

        [Tooltip("Leave empty to use GameDatabase.dialogue.")]
        public YarnProject project;
        [Tooltip("Language of the lines (falls back to the project's base language).")]
        public string language = "vi";

        public bool IsRunning => current != null;
        public DialogueRunner Runner => runner;

        DialogueRunner runner;
        YarnDialoguePresenter presenter;
        NPC current;
        Action onDone;
        Coroutine fallback;

        void Awake()
        {
            I = this;
            if (project == null && GameManager.I != null && GameManager.I.db != null) project = GameManager.I.db.dialogue;
            if (project != null) CreateRunner();
            else Debug.LogWarning("[Dialogue] No Yarn project assigned; NPCs use their fallback lines.");
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void CreateRunner()
        {
            // configure while inactive so the runner's Awake already sees the project
            var go = new GameObject("[Dialogue Runner]");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            presenter = go.AddComponent<YarnDialoguePresenter>();
            presenter.Completed = OnYarnCompleted;
            runner = go.AddComponent<DialogueRunner>();
            runner.SetProject(project);
            runner.DialoguePresenters = new DialoguePresenterBase[] { presenter };
            go.SetActive(true);
            if (runner.LineProvider is LineProviderBehaviour lp) lp.LocaleCode = language;
        }

        public bool HasNode(string node) => runner != null && !string.IsNullOrEmpty(node) && Array.IndexOf(project.NodeNames, node) >= 0;

        /// <summary>Starts a conversation with an NPC. <paramref name="done"/> runs when it ends.</summary>
        public bool Talk(NPC npc, Action done = null)
        {
            if (IsRunning || npc == null || DialogueUI.I == null) return false;
            bool yarn = HasNode(npc.yarnNode);
            if (!yarn && npc.fallbackLines.Count == 0) return false;
            current = npc;
            onDone = done;
            GameEvents.RaiseDialogueStarted(npc.npcId);   // Talk objectives complete here, before the script reads them
            DialogueUI.I.Begin(npc);
            if (yarn) StartCoroutine(StartWhenIdle(npc.yarnNode));
            else fallback = StartCoroutine(PlayFallback(npc.fallbackLines));
            return true;
        }

        /// <summary>
        /// The runner reports a finished conversation to its presenters a moment before it is idle
        /// again; a new StartDialogue in that gap would be ignored, so wait for it.
        /// </summary>
        IEnumerator StartWhenIdle(string node)
        {
            while (runner.IsDialogueRunning) yield return null;
            runner.StartDialogue(node).Forget();
        }

        IEnumerator PlayFallback(List<DialogueLine> lines)
        {
            var ui = DialogueUI.I;
            foreach (var l in lines)
            {
                ui.ShowLine(l.speaker, l.text);
                while (!ui.AdvanceRequested) yield return null;
            }
            fallback = null;
            Finish();
        }

        void OnYarnCompleted() => Finish();

        void Finish()
        {
            if (current == null) return;
            var npc = current;
            var cb = onDone;
            current = null;
            onDone = null;
            if (DialogueUI.I != null) DialogueUI.I.End();
            GameEvents.RaiseDialogueEnded(npc.npcId);
            cb?.Invoke();
        }

        /// <summary>Ends the current conversation at once (tests, AutoShot, skipping).</summary>
        public void Skip()
        {
            if (current == null) return;
            if (fallback != null)
            {
                StopCoroutine(fallback);
                fallback = null;
                Finish();
            }
            else if (runner != null && runner.IsDialogueRunning) runner.Stop().Forget();
            else Finish();
        }

        // ------------------------------------------------------------------ save
        [Serializable]
        class Var<T>
        {
            public string name;
            public T value;
        }

        [Serializable] class FloatVar : Var<float> { }
        [Serializable] class StringVar : Var<string> { }
        [Serializable] class BoolVar : Var<bool> { }

        [Serializable]
        class SaveState
        {
            public List<FloatVar> floats = new List<FloatVar>();
            public List<StringVar> strings = new List<StringVar>();
            public List<BoolVar> bools = new List<BoolVar>();
        }

        public string SaveKey => "dialogue";

        public string CaptureState()
        {
            var st = new SaveState();
            if (runner != null)
            {
                var (floats, strings, bools) = runner.VariableStorage.GetAllVariables();
                foreach (var kv in floats) st.floats.Add(new FloatVar { name = kv.Key, value = kv.Value });
                foreach (var kv in strings) st.strings.Add(new StringVar { name = kv.Key, value = kv.Value });
                foreach (var kv in bools) st.bools.Add(new BoolVar { name = kv.Key, value = kv.Value });
            }
            return JsonUtility.ToJson(st);
        }

        public void RestoreState(string json)
        {
            if (runner == null) return;
            var st = JsonUtility.FromJson<SaveState>(json);
            var floats = new Dictionary<string, float>();
            var strings = new Dictionary<string, string>();
            var bools = new Dictionary<string, bool>();
            foreach (var v in st.floats) floats[v.name] = v.value;
            foreach (var v in st.strings) strings[v.name] = v.value;
            foreach (var v in st.bools) bools[v.name] = v.value;
            runner.VariableStorage.SetAllVariables(floats, strings, bools, true);
        }
    }
}
