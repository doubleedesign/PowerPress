namespace PowerPress;

public class UserInput : ConsoleBase {
	/// <summary>
	///     Prompts for text input.
	///     Returns defaultValue if the user presses Enter without typing anything.
	/// </summary>
	public string PromptForText(string message, string defaultValue = "") {
		string formatted = message.Trim();
		formatted += string.IsNullOrWhiteSpace(defaultValue)
			? " (default is empty)"
			: $" (default is '{defaultValue}')";

		(string msg, string caller) = this.FormatWithCaller(formatted);

		this.Write($"\n❔ {msg}", ConsoleColor.Cyan);
		this.Write($" {caller}\n", ConsoleColor.DarkGray);

		string? input = Console.ReadLine();

		if (string.IsNullOrWhiteSpace(input)) {
			// Move cursor up, clear line, reprint default value (mirrors PS behaviour)
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			Console.Write(new string(' ', Console.BufferWidth));
			Console.SetCursorPosition(0, Console.CursorTop);

			// Return the default value
			this.WriteLine(defaultValue, ConsoleColor.Gray);
			return defaultValue;
		}

		return input.Trim();
	}

	/// <summary>
	///     Interactive up/down arrow prompt.
	///     Returns true if the user selected yesOption, false if noOption.
	/// </summary>
	public bool PromptForYesOrNo(string message, string yesOption, string noOption, bool defaultYes = false) {
		string[] options = new[] { yesOption, noOption };
		int selected = defaultYes ? 0 : 1;

		(string msg, string caller) = this.FormatWithCaller(message);

		this.Write($"\n❔ {msg}", ConsoleColor.Cyan);
		this.Write($" {caller}\n", ConsoleColor.DarkGray);

		while (true) {
			for (int i = 0; i < options.Length; i++) {
				if (i == selected)
					this.WriteLine($"   ● {options[i]}", ConsoleColor.Yellow);
				else
					this.WriteLine($"   ○ {options[i]}", ConsoleColor.Gray);
			}

			ConsoleKeyInfo key = Console.ReadKey(true);

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

	public string PromptForSelection(string message, Dictionary<string, string> options) {
		List<KeyValuePair<string, string>> items = options.ToList();
		int selected = 0;

		(string msg, string caller) = this.FormatWithCaller(message);

		this.Write($"\n❔ {msg}", ConsoleColor.Cyan);
		this.Write($" {caller}\n", ConsoleColor.DarkGray);

		while (true) {
			for (int i = 0; i < items.Count; i++) {
				if (i == selected)
					this.WriteLine($"   ● {items[i].Key}", ConsoleColor.Yellow);
				else
					this.WriteLine($"   ○ {items[i].Key}", ConsoleColor.Gray);
			}

			ConsoleKeyInfo key = Console.ReadKey(true);

			switch (key.Key) {
				case ConsoleKey.UpArrow:
					selected = (selected - 1 + items.Count) % items.Count;
					break;
				case ConsoleKey.DownArrow:
					selected = (selected + 1) % items.Count;
					break;
				case ConsoleKey.Enter:
					return items[selected].Value;
			}

			// Move cursor back up to redraw options
			Console.SetCursorPosition(0, Console.CursorTop - items.Count);
		}
	}
}