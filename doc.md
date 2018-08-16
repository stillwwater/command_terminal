*Documentation for internal features of the Terminal. For documentation on how to register commands, please refer to the [README](./README.md).*

### Terminal structure:

|              | Description                                                        |
|:-------------|:-------------------------------------------------------------------|
| Buffer       | Handles incoming logs                                              |
| Autocomplete | Keeps a list of known words and uses it to autocomplete text       |
| Shell        | Responsible for parsing and executing commands                     |
| History      | Keeps a list of issued commands and can traverse through that list |

### Variables:

```csharp
Terminal.Shell.SetVariable("level", SceneManager.GetActiveScene().name);
```

In the console:

```
> print $level
Main

> set greet Hello World!
> print $greet
Hello World!

> set
LEVEL  : Main
GREET  : Hello World!
```

### Add words to autocomplete:

```csharp
Terminal.Autocomplete.Register("foo");
```

### Run a command:

```csharp
Terminal.Shell.RunCommand("print Hello World!"));
```

### Log without adding to Unity debug logs:

```csharp
Terminal.Log("Value of foo: {0}", foo);
```

### Clear logs:

```csharp
Terminal.Buffer.Clear();
```

### Modify the command history:

```csharp
Terminal.History.Clear();     // Clear history
Terminal.History.Push("foo"); // Add item to history

string a = Terminal.History.Next();     // Get next item
string b = Terminal.History.Previous(); // Get previous item
```
