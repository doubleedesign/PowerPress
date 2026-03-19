namespace PowerPress;

// ReSharper disable RedundantUsingDirective
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

public class UserInput : ConsoleBase {
	/// <summary>
	///     Prompts for text input.
	///     Returns defaultValue if the user presses Enter without typing anything.
	/// </summary>
	public string PromptForText(string message, string defaultValue = "") {
		var formatted = message.Trim();
		formatted += string.IsNullOrWhiteSpace(defaultValue)
			? " (default is empty)"
			: $" (default is '{defaultValue}')";

		var (msg, caller) = FormatWithCaller(formatted);

		WriteLine($"\n❓ {msg}", ConsoleColor.Cyan);
		WriteLine($" {caller}", ConsoleColor.DarkGray);

		var input = Console.ReadLine();

		if (string.IsNullOrWhiteSpace(input)) {
			// Move cursor up, clear line, reprint default value (mirrors PS behaviour)
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			Console.Write(new string(' ', Console.BufferWidth));
			Console.SetCursorPosition(0, Console.CursorTop);
			WriteLine(defaultValue, ConsoleColor.Gray);
			return defaultValue;
		}

		return input.Trim();
	}

	/// <summary>
	///     Interactive up/down arrow prompt.
	///     Returns true if the user selected yesOption, false if noOption.
	/// </summary>
	public bool PromptForYesOrNo(string message, string yesOption, string noOption, bool defaultYes = false) {
		var options = new[] { yesOption, noOption };
		var selected = defaultYes ? 0 : 1;

		var (msg, caller) = FormatWithCaller(message);

		WriteLine($"\n❔ {msg}", ConsoleColor.Cyan);
		WriteLine($" {caller}", ConsoleColor.DarkGray);

		while (true) {
			for (var i = 0; i < options.Length; i++) {
				if (i == selected)
					WriteLine($"   ● {options[i]}", ConsoleColor.Yellow);
				else
					WriteLine($"   ○ {options[i]}", ConsoleColor.Gray);
			}

			var key = Console.ReadKey(true);

			switch (key.Key) {
				case ConsoleKey.UpArrow:
					selected = (selected - 1 + options.Length) % options.Length;
					break;
				case ConsoleKey.DownArrow:
					selected = (selected + 1) % options.Length;
					break;
				case ConsoleKey.Enter:
					return selected == 0;
			}

			// Move cursor back up to redraw options
			Console.SetCursorPosition(0, Console.CursorTop - options.Length);
		}
	}
}