using UnityEngine;
using System.Text;
using System.Collections;
using UnityEngine.Assertions;

namespace CommandTerminal
{
    public class Terminal : MonoBehaviour
    {
        [Range(0, 1)]
        [SerializeField]
        float WindowHeight = 0.33f;

        [Range(100, 1000)]
        [SerializeField]
        float ToggleSpeed = 500;

        [SerializeField] string HotKey = "`";
        [SerializeField] bool Animated = true;
        [SerializeField] float ScrollSensitivity = 200;
        [SerializeField] Font ConsoleFont;
        [SerializeField] int MaxLogCount = 512;
        [SerializeField] Color BackgroundColor = Color.black;
        [SerializeField] Color ForegroundColor = Color.white;
        [SerializeField] Color ShellMessageColor = Color.white;
        [SerializeField] Color InputColor = Color.cyan;
        [SerializeField] Color WarningColor = Color.yellow;
        [SerializeField] Color ErrorColor = Color.red;

        bool open;
        bool initial_open; // Used to focus on TextField when console opens
        Rect window;
        float current_window_position;
        float real_window_size;
        string command_text;
        Vector2 scroll_position;
        GUIStyle window_style;
        GUIStyle label_style;
        GUIStyle input_style;

        public static ConsoleLogger Logger { get; private set; }
        public static CommandShell Shell { get; private set; }
        public static CommandHistory History { get; private set; }

        public static bool IssuedError {
            get {
                return Shell.IssuedErrorMessage != null;
            }
        }

        public bool IsIdle {
            get { return !open && current_window_position <= -real_window_size; }
        }

        public static void Log(string format, params object[] message) {
            Log(TerminalLogType.ShellMessage, format, message);
        }

        public static void Log(TerminalLogType type, string format, params object[] message) {
            Logger.HandleLog(string.Format(format, message), type);
        }

        void OnEnable() {
            Logger = new ConsoleLogger(MaxLogCount);
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

            real_window_size = Screen.height * WindowHeight;
            current_window_position = -real_window_size;
            StartCoroutine(SetupStyles());
            Shell.RegisterCommands();

            if (IssuedError) {
                Log(TerminalLogType.Error, "Error: {0}", Shell.IssuedErrorMessage);
            }
        }

        void OnGUI() {
            if (!open && Event.current.Equals(Event.KeyboardEvent(HotKey))) {
                open = true;
                initial_open = true;
            }

            if (IsIdle) {
                // Don't render if console is off screen
                return;
            }

            float position_delta = ToggleSpeed * Time.deltaTime;

            if (open) {
                if (current_window_position + position_delta < 0) {
                    // Animate console opening
                    DrawConsoleRect(position_delta, 0);
                } else {
                    // Clamp position
                    DrawConsoleRect(0, 0, fixed_pos: true);
                }
            } else if (!open && current_window_position > -real_window_size) {
                // Animate console closing
                DrawConsoleRect(-position_delta, -real_window_size);
            }

            window = GUILayout.Window(88, window, DrawConsole, "", window_style);
        }

        IEnumerator SetupStyles() {
            SetupWindow();
            yield return null;
            SetupInput();
            yield return null;
            SetupLabels();
        }

        void SetupWindow() {
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
            input_style.fixedHeight = ConsoleFont.fontSize * 1.4f;
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

        void DrawConsoleRect(float position_delta, float target_position, bool fixed_pos = false) {
            if (Animated || fixed_pos) {
                current_window_position += position_delta;
            } else {
                current_window_position = target_position;
            }

            window = new Rect(0, current_window_position, Screen.width, real_window_size);
        }

        void DrawConsole(int Window2D) {
            GUILayout.BeginVertical();

            scroll_position = GUILayout.BeginScrollView(scroll_position, false, false, GUIStyle.none, GUIStyle.none);
            DrawLogs();
            GUILayout.EndScrollView();

            if (Event.current.Equals(Event.KeyboardEvent("escape"))) {
                open = false;
            } else if (Event.current.Equals(Event.KeyboardEvent("return"))) {
                EnterCommand();
            } else if (Event.current.Equals(Event.KeyboardEvent("up"))) {
                command_text = History.Previous();
            } else if (Event.current.Equals(Event.KeyboardEvent("down"))) {
                command_text = History.Next();
            } else if (Event.current.Equals(Event.KeyboardEvent(HotKey))) {
                open = !open;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(">", input_style, GUILayout.Width(ConsoleFont.fontSize));

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
