using UnityEngine;
using CommandTerminal;

public static class Commands {
    [RegisterCommand(Help = "Adds 2 numbers")]
    static void CommandAdd(CommandArg[] args) {
        int a = args[0].Int;
        int b = args[1].Int;

        if (Terminal.IssuedError) return; // Error will be handled by Terminal

        int result = a + b;
        Terminal.Log("{0} + {1} = {2}", a, b, result);
    }

    [RegisterCommand(Help = "Adds 2 numbers", MinArgCount = 1)]
    static void Log(CommandArg[] args) {
        string text = "";
        foreach(var arg in args) {
            text += arg + " ";
        }
        Debug.Log(text);
    }
}

