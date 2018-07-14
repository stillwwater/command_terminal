using UnityEngine;
using System.Text;
using System.Collections;
using UnityEngine.Assertions;

namespace CommandTerminal
{
    public enum TerminalState
    {
        Close,
        OpenSmall,
        OpenFull
    }

    public class Terminal : MonoBehaviour
    {
        [Header("Window")]
        [Range(0, 1)]
        [SerializeField]
        float MaxHeight = 0.7f;

        [Range(100, 1000)]
        [SerializeField]
        float ToggleSpeed = 360;

        [SerializeField] string ToggleHotkey      = "`";
        [SerializeField] string ToggleFullHotkey  = "#`";
        [SerializeField] float ScrollSensitivity  = 200;
        [SerializeField] int MaxLogCount          = 512;

        [Header("Input")]
        [SerializeField] Font ConsoleFont;
        [SerializeField] string InputCaret        = ">";

        [Header("Theme")]
        [SerializeField] Color BackgroundColor    = Color.black;
        [SerializeField] Color ForegroundColor    = Color.white;
        [SerializeField] Color ShellColor         = Color.white;
        [SerializeField] Color InputColor         = Color.cyan;
        [SerializeField] Color WarningColor       = Color.yellow;
        [SerializeField] Color ErrorColor         = Color.red;

        TerminalState state;
        TextEditor editor_state;
        bool input_fix;
        bool move_cursor;
        bool initial_open; // Used to focus on TextField when console opens
        Rect window;
        float current_open_t;
        float open_target;
        float real_window_size;
        string command_text;
        string cached_command_text;
        Vector2 scroll_position;
        GUIStyle window_style;
        GUIStyle label_style;
        GUIStyle input_style;

        public static CommandLog Logger { get; private set; }
        public static CommandShell Shell { get; private set; }
        public static CommandHistory History { get; private set; }
        public static CommandAutocomplete Autocomplete { get; private set; }

        public static bool IssuedError {
            get { return Shell.IssuedErrorMessage != null; }
        }

        public bool IsClosed {
            get { return state == TerminalState.Close && Mathf.Approximately(current_open_t, open_target); }
        }

        public static void Log(string format, params object[] message) {
            Log(TerminalLogType.ShellMessage, format, message);
        }

        public static void Log(TerminalLogType type, string format, params object[] message) {
            Logger.HandleLog(string.Format(format, message), type);
        }

        public void SetState(TerminalState new_state) {
            input_fix = true;
            cached_command_text = command_text;
            command_text = "";

            switch (new_state) {
                case TerminalState.Close:
                    open_target = 0;
                    break;
                case TerminalState.OpenSmall:
                    open_target = Screen.height * MaxHeight / 3;
                    if (current_open_t > open_target) {
                        // Prevent resizing from OpenFull to OpenSmall if window y position
                        // is greater than OpenSmall's target
                        open_target = 0;
                        state = TerminalState.Close;
                        return;
                    }
                    real_window_size = open_target;
                    scroll_position.y = int.MaxValue;
                    break;
                case TerminalState.OpenFull:
                default:
                    real_window_size = Screen.height * MaxHeight;
                    open_target = real_window_size;
                    break;
            }

            state = new_state;
        }

        void OnEnable() {
            Logger = new CommandLog(MaxLogCount);
            Shell = new CommandShell();
            History = new CommandHistory();
            Autocomplete = new CommandAutocomplete();

            // Hook Unity log events
            Application.logMessageReceived += HandleUnityLog;
        }

        void OnDisable() {
            Application.logMessageReceived -= HandleUnityLog;
        }

        void Start() {
            if (ConsoleFont == null) {
                ConsoleFont = Font.CreateDynamicFontFromOSFont("Courier New", 16);
                Debug.LogWarning("Command Console Warning: Please assign a font.");
            }

            command_text = "";
            cached_command_text = command_text;
            Assert.AreNotEqual(ToggleHotkey.ToLower(), "return", "Return is not a valid ToggleHotkey");

            SetupWindow();
            SetupInput();
            SetupLabels();

            Shell.RegisterCommands();

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }

            foreach (var command in Shell.Commands) {
                Autocomplete.Register(command.Key);
            }
        }

        void OnGUI() {
            if (Event.current.Equals(Event.KeyboardEvent(ToggleHotkey))) {
                SetState(TerminalState.OpenSmall);
                initial_open = true;
            } else if (Event.current.Equals(Event.KeyboardEvent(ToggleFullHotkey))) {
                SetState(TerminalState.OpenFull);
                initial_open = true;
            }

            if (IsClosed) {
                return;
            }

            HandleOpenness();
            window = GUILayout.Window(88, window, DrawConsole, "", window_style);
        }

        void SetupWindow() {
            real_window_size = Screen.height * MaxHeight / 3;
            window = new Rect(0, current_open_t - real_window_size, Screen.width, real_window_size);

            // Set background color
            Texture2D background_texture = new Texture2D(1, 1);
            background_texture.SetPixel(0, 0, BackgroundColor);
            background_texture.Apply();

            window_style = new GUIStyle();
            window_style.normal.background = background_texture;
            window_style.padding = new RectOffset(4, 4, 4, 4);
        }

        void SetupLabels() {
            label_style = new GUIStyle();
            label_style.font = ConsoleFont;
            label_style.normal.textColor = ForegroundColor;
            label_style.wordWrap = true;
        }

        void SetupInput() {
            input_style = new GUIStyle();
            input_style.padding = new RectOffset(4, 4, 4, 4);
            input_style.font = ConsoleFont;
            input_style.fixedHeight = ConsoleFont.fontSize * 1.6f;
            input_style.normal.textColor = InputColor;

            var dark_background = new Color();
            dark_background.r = BackgroundColor.r - 0.2f;
            dark_background.g = BackgroundColor.g - 0.2f;
            dark_background.b = BackgroundColor.b - 0.2f;
            dark_background.a = 0.5f;

            Texture2D input_background_texture = new Texture2D(1, 1);
            input_background_texture.SetPixel(0, 0, dark_background);
            input_background_texture.Apply();
            input_style.normal.background = input_background_texture;
        }

        void DrawConsole(int Window2D) {
            GUILayout.BeginVertical();

            scroll_position = GUILayout.BeginScrollView(scroll_position, false, false, GUIStyle.none, GUIStyle.none);
            GUILayout.FlexibleSpace();
            DrawLogs();
            GUILayout.EndScrollView();

            if (move_cursor) {
                CursorToEnd();
                move_cursor = false;
            }

            if (Event.current.Equals(Event.KeyboardEvent("escape"))) {
                SetState(TerminalState.Close);
            } else if (Event.current.Equals(Event.KeyboardEvent("return"))) {
                EnterCommand();
            } else if (Event.current.Equals(Event.KeyboardEvent("up"))) {
                command_text = History.Previous();
                move_cursor = true;
            } else if (Event.current.Equals(Event.KeyboardEvent("down"))) {
                command_text = History.Next();
            } else if (Event.current.Equals(Event.KeyboardEvent(ToggleHotkey))) {
                if (state == TerminalState.OpenSmall) {
                    SetState(TerminalState.Close);
                } else {
                    SetState(TerminalState.OpenSmall);
                }
            } else if (Event.current.Equals(Event.KeyboardEvent(ToggleFullHotkey))) {
                if (state == TerminalState.OpenFull) {
                    SetState(TerminalState.Close);
                } else {
                    SetState(TerminalState.OpenFull);
                }
            } else if (Event.current.Equals(Event.KeyboardEvent("tab"))) {
                CompleteCommand();
                move_cursor = true; // Wait till next draw call
            }

            GUILayout.BeginHorizontal();

            if (InputCaret != "") {
                GUILayout.Label(InputCaret, input_style, GUILayout.Width(ConsoleFont.fontSize));
            }

            GUI.SetNextControlName("command_text_field");
            command_text = GUILayout.TextField(command_text, input_style);

            if (input_fix && command_text.Length > 0) {
                command_text = cached_command_text; // Otherwise the TextField picks up the ToggleHotkey character event
                input_fix = false;                  // Prevents checking string Length every draw call
            }

            if (initial_open) {
                GUI.FocusControl("command_text_field");
                initial_open = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void DrawLogs() {
            foreach (var log in Logger.Logs) {
                label_style.normal.textColor = GetLogColor(log.type);
                GUILayout.Label(log.message, label_style);
            }
        }

        void HandleOpenness() {
            float dt = ToggleSpeed * Time.deltaTime;

            if (current_open_t < open_target) {
                current_open_t += dt;
                if (current_open_t > open_target) current_open_t = open_target;
            } else if (current_open_t > open_target) {
                current_open_t -= dt;
                if (current_open_t < open_target) current_open_t = open_target;
            } else {
                return; // Already at target
            }

            window = new Rect(0, current_open_t - real_window_size, Screen.width, real_window_size);
        }

        void EnterCommand() {
            Log(TerminalLogType.Input, "{0}", command_text);
            Shell.RunCommand(command_text);
            History.Push(command_text);

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }

            command_text = "";
            scroll_position.y = int.MaxValue;
        }

        void CompleteCommand() {
            string[] completion_buffer = Autocomplete.Complete(command_text);
            int completion_length = completion_buffer.Length;

            if (completion_length == 1) {
                command_text = completion_buffer[0];
            } else if (completion_length > 1) {
                Log(TerminalLogType.Input, string.Join("    ", completion_buffer));
                scroll_position.y = int.MaxValue;
            }
        }

        void CursorToEnd() {
            if (editor_state == null) {
                editor_state = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            }

            editor_state.MoveCursorToPosition(new Vector2(999, 999));
        }

        void HandleUnityLog(string message, string stack_trace, LogType type) {
            Logger.HandleLog(message, stack_trace, (TerminalLogType)type);
            scroll_position.y = int.MaxValue;
        }

        Color GetLogColor(TerminalLogType type) {
            switch (type) {
                case TerminalLogType.Message: return ForegroundColor;
                case TerminalLogType.Warning: return WarningColor;
                case TerminalLogType.Input: return InputColor;
                case TerminalLogType.ShellMessage: return ShellColor;
                default: return ErrorColor;
            }
        }
    }
}
