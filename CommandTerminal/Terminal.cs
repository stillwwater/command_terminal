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
        [Range(0, 1)]
        [SerializeField]
        float MaxHeight = 0.7f;

        [Range(100, 1000)]
        [SerializeField]
        float ToggleSpeed = 360;

        [SerializeField] string HotKey            = "`";
        [SerializeField] string OpenBigHotKey     = "#`";
        [SerializeField] string InputCaret        = ">";
        [SerializeField] float ScrollSensitivity  = 200;
        [SerializeField] Font ConsoleFont;
        [SerializeField] int MaxLogCount          = 512;
        [SerializeField] Color BackgroundColor    = Color.black;
        [SerializeField] Color ForegroundColor    = Color.white;
        [SerializeField] Color ShellMessageColor  = Color.white;
        [SerializeField] Color InputColor         = Color.cyan;
        [SerializeField] Color WarningColor       = Color.yellow;
        [SerializeField] Color ErrorColor         = Color.red;

        TerminalState state;
        bool initial_open; // Used to focus on TextField when console opens
        Rect window;
        float current_open_t;
        float open_target;
        float real_window_size;
        string command_text;
        Vector2 scroll_position;
        GUIStyle window_style;
        GUIStyle label_style;
        GUIStyle input_style;

        public static ConsoleLog Logger { get; private set; }
        public static CommandShell Shell { get; private set; }
        public static CommandHistory History { get; private set; }

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
            switch (new_state) {
                case TerminalState.Close:
                    open_target = 0;
                    break;
                case TerminalState.OpenSmall:
                    open_target = Screen.height * MaxHeight / 3;
                    if (current_open_t <= open_target) {
                        real_window_size = open_target;
                    } else {
                        // Prevent resizing from OpenFull to OpenSmall if window y position
                        // is greater than OpenSmall's target
                        open_target = 0;
                        state = TerminalState.Close;
                        return;
                    }
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
            Logger = new ConsoleLog(MaxLogCount);
            Shell = new CommandShell();
            History = new CommandHistory();

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

            Assert.AreNotEqual(HotKey.ToLower(), "return", "Return is not a valid HotKey");

            SetupWindow();
            SetupInput();
            SetupLabels();

            Shell.RegisterCommands();

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }
        }

        void OnGUI() {
            if (Event.current.Equals(Event.KeyboardEvent(HotKey))) {
                SetState(TerminalState.OpenSmall);
                initial_open = true;
            } else if (Event.current.Equals(Event.KeyboardEvent(OpenBigHotKey))) {
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

            if (Event.current.Equals(Event.KeyboardEvent("escape"))) {
                SetState(TerminalState.Close);
            } else if (Event.current.Equals(Event.KeyboardEvent("return"))) {
                EnterCommand();
            } else if (Event.current.Equals(Event.KeyboardEvent("up"))) {
                command_text = History.Previous();
            } else if (Event.current.Equals(Event.KeyboardEvent("down"))) {
                command_text = History.Next();
            } else if (Event.current.Equals(Event.KeyboardEvent(HotKey))) {
                if (state == TerminalState.OpenSmall) {
                    SetState(TerminalState.Close);
                } else {
                    SetState(TerminalState.OpenSmall);
                }
            } else if (Event.current.Equals(Event.KeyboardEvent(OpenBigHotKey))) {
                if (state == TerminalState.OpenFull) {
                    SetState(TerminalState.Close);
                } else {
                    SetState(TerminalState.OpenFull);
                }
            }

            GUILayout.BeginHorizontal();

            if (InputCaret != "") {
                GUILayout.Label(InputCaret, input_style, GUILayout.Width(ConsoleFont.fontSize));
            }

            GUI.SetNextControlName("command_text_field");
            command_text = GUILayout.TextField(command_text, input_style);

            if (command_text == HotKey) {
                command_text = ""; // Otherwise the TextField picks up the HotKey character event
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

        void HandleUnityLog(string message, string stack_trace, LogType type) {
            Logger.HandleLog(message, stack_trace, (TerminalLogType)type);
            scroll_position.y = int.MaxValue;
        }

        Color GetLogColor(TerminalLogType type) {
            switch (type) {
                case TerminalLogType.Message: return ForegroundColor;
                case TerminalLogType.Warning: return WarningColor;
                case TerminalLogType.Input: return InputColor;
                case TerminalLogType.ShellMessage: return ShellMessageColor;
                default: return ErrorColor;
            }
        }
    }
}
