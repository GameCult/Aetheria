/// <summary>
/// Handles parsing and execution of console commands, as well as collecting log output.
/// </summary>
using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class ConsoleController
{
	public static Component MessageReceiver;
    private static ConsoleController _instance;
    private static Regex _permittedCharacters = new Regex("[^a-zA-Z0-9 -]");
    private static Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

    private ConsoleController()
    {
    }

    public static ConsoleController Instance
    {
        get => _instance ?? (_instance = new ConsoleController());
    }

    public static void ClearCommands()
    {
	    _commands.Clear();
    }

    public static void AddCommand(string command, Action<string[]> action)
    {
	    var lower = command.ToLowerInvariant();
	    if(!_commands.ContainsKey(lower))
		    _commands.Add(lower, action);
    }

    #region Event declarations
    // Used to communicate with ConsoleView
    public delegate void LogChangedHandler(string[] log);
	public event LogChangedHandler LogChanged;
	
	public delegate void VisibilityChangedHandler(bool visible);
	public event VisibilityChangedHandler VisibilityChanged;
	#endregion

	/// <summary>
	/// How many log lines should be retained?
	/// Note that strings submitted to appendLogLine with embedded newlines will be counted as a single line.
	/// </summary>
	private const int ScrollbackSize = 100;

	private readonly Queue<string> _scrollback = new Queue<string>(ScrollbackSize);
	private readonly List<string> _commandHistory = new List<string>();

	public string[] Log { get; private set; } //Copy of scrollback as an array for easier use by ConsoleView
	
	public void AppendLogLine(string line) {
		if (_scrollback.Count >= ScrollbackSize) {
			_scrollback.Dequeue();
		}
		_scrollback.Enqueue(line);
		
		Log = _scrollback.ToArray();
		LogChanged?.Invoke(Log);
	}
	
	public void RunCommandString(string commandString) {
		AppendLogLine($"$ {commandString}");
		
		string[] commandSplit = ParseArguments(commandString);
		string[] args = {"",""};
		if (commandSplit.Length < 1) {
			AppendLogLine($"Unable to process command '{commandString}'");
			return;
			
		}
		if (commandSplit.Length >= 2) {
			int numArgs = commandSplit.Length - 1;
			args = new string[numArgs];
			Array.Copy(commandSplit, 1, args, 0, numArgs);
		}

		var cmd = commandSplit[0].ToLower();
		if(_commands.ContainsKey(cmd))
			_commands[cmd].Invoke(args);
		else AppendLogLine("Invalid command!");
		//MessageReceiver.SendMessage(commandSplit[0].ToLower(), args, SendMessageOptions.DontRequireReceiver);
		_commandHistory.Add(commandString);
	}
	
	static string[] ParseArguments(string commandString)
	{
		var parmChars = new LinkedList<char>(commandString.ToCharArray());
		var inQuote = false;
		var node = parmChars.First;
		while (node != null)
		{
			var next = node.Next;
			if (node.Value == '"') {
				inQuote = !inQuote;
				parmChars.Remove(node);
			}
			if (!inQuote && node.Value == ' ') {
				node.Value = '\n';
			}
			node = next;
		}
		var parmCharsArr = new char[parmChars.Count];
		parmChars.CopyTo(parmCharsArr, 0);
		var args = new string(parmCharsArr).Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < args.Length; i++)
		{
			args[i] = _permittedCharacters.Replace(args[i], "");
		}
		return args;
	}
}
