using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace SignalLost.Core
{
    /// <summary>
    /// The player's keyboard and mouse bindings (11-07), changed on the Controls page of Settings.
    /// <para>
    /// Every gameplay component makes its own copy of the input actions and passes it to <see cref="Register"/>; the
    /// bindings here are applied to every registered copy straight away and whenever they change. Menus that read
    /// keys directly ask <see cref="WasPressed"/> instead of naming a key.
    /// </para>
    /// <para>
    /// <b>Save system hand-off:</b> the whole state is <see cref="Export"/> (a <see cref="SaveData"/>, or JSON via
    /// <see cref="ToJson"/>) and <see cref="Import"/>. Until saving lands the data is kept in PlayerPrefs by
    /// <see cref="Store"/> and <see cref="Load"/>; replace those two with the save system's calls. Only bindings
    /// that differ from the defaults are written, by stable Ids (see <see cref="All"/>), never by key position.
    /// </para>
    /// </summary>
    public static class KeyBindings
    {
        /// <summary>One rebindable control: an Id the save data uses, a label for the menu, and where it lives.</summary>
        public sealed class Binding
        {
            public string Id, Label, Action, Part, Default, Gamepad;
            /// <summary>The key or button now in use (an Input System path such as "&lt;Keyboard&gt;/space").</summary>
            public string Path => overrides.TryGetValue(Id, out string path) ? path : Default;
        }

        /// <summary>The data the save system stores: just the bindings changed from their defaults.</summary>
        [Serializable]
        public sealed class SaveData
        {
            public int version = 1;
            public List<Entry> overrides = new List<Entry>();

            [Serializable]
            public struct Entry
            {
                public string id;   // a Binding.Id, e.g. "jump"
                public string path; // an Input System path, e.g. "<Keyboard>/v"
            }
        }

        /// <summary>
        /// Every rebindable control, in menu order. <see cref="Binding.Action"/> is "Map/Action" in
        /// InputSystem_Actions (null: a menu key handled here), <see cref="Binding.Part"/> the composite part
        /// (Move's up/down/left/right). Never rename an Id: save data uses them.
        /// </summary>
        public static readonly Binding[] All =
        {
            new Binding { Id = "move_forward", Label = "Move forward", Action = "Player/Move", Part = "up", Default = "<Keyboard>/w", Gamepad = "Left stick" },
            new Binding { Id = "move_back", Label = "Move back", Action = "Player/Move", Part = "down", Default = "<Keyboard>/s", Gamepad = "Left stick" },
            new Binding { Id = "move_left", Label = "Move left", Action = "Player/Move", Part = "left", Default = "<Keyboard>/a", Gamepad = "Left stick" },
            new Binding { Id = "move_right", Label = "Move right", Action = "Player/Move", Part = "right", Default = "<Keyboard>/d", Gamepad = "Left stick" },
            new Binding { Id = "sprint", Label = "Sprint", Action = "Player/Sprint", Default = "<Keyboard>/leftShift", Gamepad = "Stick click" },
            new Binding { Id = "jump", Label = "Jump", Action = "Player/Jump", Default = "<Keyboard>/space", Gamepad = "A" },
            new Binding { Id = "crouch", Label = "Crouch / slide / jump smash", Action = "Player/Crouch", Default = "<Keyboard>/c", Gamepad = "B" },
            new Binding { Id = "attack", Label = "Laser combo", Action = "Player/Attack", Default = "<Mouse>/leftButton", Gamepad = "X" },
            new Binding { Id = "charge", Label = "Charge bolt (hold)", Action = "Player/ChargeShot", Default = "<Mouse>/rightButton", Gamepad = "RT" },
            new Binding { Id = "swap", Label = "Swap scanner / laser", Action = "Player/ToggleScanner", Default = "<Keyboard>/q", Gamepad = "LB" },
            new Binding { Id = "drink", Label = "Drink from the flask", Action = "Player/Drink", Default = "<Keyboard>/f", Gamepad = "D-pad up" },
            new Binding { Id = "interact", Label = "Talk / pick up / next line", Action = "Player/Interact", Default = "<Keyboard>/e", Gamepad = "Y" },
            new Binding { Id = "inventory", Label = "Pockets (inventory)", Action = null, Default = "<Keyboard>/tab", Gamepad = "View" },
            new Binding { Id = "cheat", Label = "Cheat mode (testing)", Action = null, Default = "<Keyboard>/end", Gamepad = "-" }
        };

        /// <summary>Raised after any binding changes (rebind, reset, import).</summary>
        public static event Action Changed;

        private const string PrefsKey = "SignalLost.KeyBindings";
        private static readonly Dictionary<string, string> overrides = new Dictionary<string, string>();
        private static readonly List<InputActionAsset> registered = new List<InputActionAsset>();
        private static readonly Dictionary<string, InputAction> menuKeys = new Dictionary<string, InputAction>();
        private static bool loaded;

        public static Binding Find(string id)
        {
            foreach (Binding binding in All) if (binding.Id == id) return binding;
            return null;
        }

        /// <summary>The key now bound to <paramref name="id"/>, for on-screen hints ("E", "Left Click").</summary>
        public static string Display(string id)
        {
            EnsureLoaded();
            Binding binding = Find(id);
            return binding == null ? "?" : DisplayPath(binding.Path);
        }

        /// <summary>"[E]": the bound key in brackets, for button hints.</summary>
        public static string Hint(string id) => "[" + Display(id) + "]";

        public static string DisplayPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";
            string text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return text switch
            {
                "Left Button" => "Left Click",
                "Right Button" => "Right Click",
                "Middle Button" => "Middle Click",
                _ => text
            };
        }

        /// <summary>True on the frame the key bound to <paramref name="id"/> goes down (for menus reading keys themselves).</summary>
        public static bool WasPressed(string id)
        {
            EnsureLoaded();
            if (!menuKeys.TryGetValue(id, out InputAction action))
            {
                Binding binding = Find(id);
                if (binding == null) return false;
                action = new InputAction(id, InputActionType.Button, binding.Path);
                action.Enable();
                menuKeys[id] = action;
            }
            return action.WasPressedThisFrame();
        }

        /// <summary>Applies the bindings to a component's copy of the input actions now and after every change.</summary>
        public static void Register(InputActionAsset asset)
        {
            if (asset == null) return;
            EnsureLoaded();
            registered.RemoveAll(a => a == null);
            if (!registered.Contains(asset)) registered.Add(asset);
            ApplyTo(asset);
        }

        /// <summary>
        /// Binds <paramref name="id"/> to <paramref name="path"/>. If another control already uses that key, the two
        /// swap, so nothing is left with two jobs. Returns the Id of the control that swapped, or null.
        /// </summary>
        public static string Rebind(string id, string path)
        {
            EnsureLoaded();
            Binding binding = Find(id);
            if (binding == null || string.IsNullOrEmpty(path) || binding.Path == path) return null;
            string old = binding.Path, swapped = null;
            foreach (Binding other in All)
            {
                if (other == binding || other.Path != path) continue;
                Set(other, old);
                swapped = other.Id;
            }
            Set(binding, path);
            Commit();
            return swapped;
        }

        public static void ResetAll()
        {
            EnsureLoaded();
            overrides.Clear();
            Commit();
        }

        /// <summary>The changed bindings, ready for the save system.</summary>
        public static SaveData Export()
        {
            EnsureLoaded();
            var data = new SaveData();
            foreach (Binding binding in All)
                if (overrides.TryGetValue(binding.Id, out string path))
                    data.overrides.Add(new SaveData.Entry { id = binding.Id, path = path });
            return data;
        }

        /// <summary>Replaces every binding with <paramref name="data"/> (null: the defaults). Unknown Ids are skipped.</summary>
        public static void Import(SaveData data)
        {
            loaded = true;
            overrides.Clear();
            if (data?.overrides != null)
                foreach (SaveData.Entry entry in data.overrides)
                {
                    Binding binding = Find(entry.id);
                    if (binding != null && !string.IsNullOrEmpty(entry.path) && entry.path != binding.Default) overrides[binding.Id] = entry.path;
                }
            Commit();
        }

        public static string ToJson() => JsonUtility.ToJson(Export());
        public static void FromJson(string json) => Import(string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json));

        // ---------- Listening for a new key ----------

        private static IDisposable listener;
        private static int listenEndedFrame = -1;

        /// <summary>True while the Controls page waits for a key.</summary>
        public static bool Listening => listener != null;

        /// <summary>
        /// True if Esc this frame belonged to the key prompt (it cancels the prompt), so menus shouldn't also close.
        /// </summary>
        public static bool EscapeConsumed => Listening || listenEndedFrame == Time.frameCount;

        /// <summary>
        /// Waits for the next keyboard key or mouse button and hands its path to <paramref name="done"/>; Esc cancels
        /// (null). Only one prompt at a time: a new one replaces the old.
        /// </summary>
        public static void ListenForKey(Action<string> done)
        {
            StopListening();
            listener = InputSystem.onAnyButtonPress
                .Where(control => control.device is Keyboard || control.device is Mouse)
                .CallOnce(control =>
                {
                    listener = null;
                    listenEndedFrame = Time.frameCount;
                    bool cancel = control is KeyControl key && key.keyCode == Key.Escape;
                    done?.Invoke(cancel ? null : PathOf(control));
                });
        }

        public static void StopListening()
        {
            listener?.Dispose();
            listener = null;
        }

        /// <summary>A control's binding path: "&lt;Keyboard&gt;/space", "&lt;Mouse&gt;/rightButton".</summary>
        public static string PathOf(InputControl control) =>
            "<" + control.device.layout + ">" + control.path.Substring(control.device.path.Length);

        // ---------- Stand-in storage until the save system ----------

        /// <summary>Writes the bindings somewhere lasting. Swap for the save system.</summary>
        private static void Store()
        {
            PlayerPrefs.SetString(PrefsKey, ToJson());
            PlayerPrefs.Save();
        }

        /// <summary>Reads the bindings back. Swap for the save system (call <see cref="Import"/> with its data).</summary>
        private static void Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            try { FromJson(json); }
            catch (ArgumentException) { Import(null); } // unreadable: back to the defaults
        }

        // ---------- Internals ----------

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Load();
        }

        private static void Set(Binding binding, string path)
        {
            if (path == binding.Default) overrides.Remove(binding.Id);
            else overrides[binding.Id] = path;
        }

        private static void Commit()
        {
            registered.RemoveAll(a => a == null);
            foreach (InputActionAsset asset in registered) ApplyTo(asset);
            foreach (InputAction action in menuKeys.Values) action.Dispose();
            menuKeys.Clear();
            Store();
            Changed?.Invoke();
        }

        private static void ApplyTo(InputActionAsset asset)
        {
            foreach (Binding binding in All)
            {
                if (binding.Action == null) continue;
                InputAction action = asset.FindAction(binding.Action);
                if (action == null) continue;
                int index = BindingIndex(action, binding);
                if (index < 0) continue;
                if (overrides.TryGetValue(binding.Id, out string path)) action.ApplyBindingOverride(index, path);
                else action.RemoveBindingOverride(index);
            }
        }

        /// <summary>The first keyboard or mouse binding of the action (or of its composite part).</summary>
        public static int BindingIndex(InputAction action, Binding binding)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding candidate = action.bindings[i];
                if (candidate.isComposite) continue;
                bool partMatches = binding.Part == null
                    ? !candidate.isPartOfComposite
                    : candidate.isPartOfComposite && string.Equals(candidate.name, binding.Part, StringComparison.OrdinalIgnoreCase);
                if (!partMatches) continue;
                string path = candidate.path ?? "";
                if (path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")) return i;
            }
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            StopListening();
            listenEndedFrame = -1;
            foreach (InputAction action in menuKeys.Values) action.Dispose();
            menuKeys.Clear();
            registered.Clear();
            overrides.Clear();
            loaded = false;
            Changed = null;
        }
    }
}
