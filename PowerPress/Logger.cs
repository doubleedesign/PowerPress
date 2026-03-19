namespace PowerPress;

// ReSharper disable RedundantUsingDirective
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using FormattedMessage = (string Message, string Caller);

public class Logger : ConsoleBase {
	private const int DividerWidth = 100;

	public void SuccessMessage(string message, int traceLevels = 1) {
		var (msg, caller) = FormatWithCaller(message, traceLevels);
		Write("✔  ", ConsoleColor.Green);
		Write($"{msg} ", ConsoleColor.Green);
		Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void WarningMessage(string message, int traceLevels = 1) {
		var (msg, caller) = FormatWithCaller(message, traceLevels);
		Write("⚠️ ", ConsoleColor.Yellow);
		Write($"{msg} ", ConsoleColor.Yellow);
		Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void ErrorMessage(string message, int traceLevels = 1) {
		var (msg, caller) = FormatWithCaller(message, traceLevels);
		Write("✖  ", ConsoleColor.Red);
		Write($"{msg} ", ConsoleColor.Red);
		Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void InfoMessage(string message, int traceLevels = 1) {
		var (msg, caller) = FormatWithCaller(message, traceLevels);
		Write("📝 ", ConsoleColor.Blue);
		Write($"{msg} ", ConsoleColor.Blue);
		Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	/// <summary>
	///     Outputs the given additional debugging output when the POWERPRESS_DEBUG environment variable is set.
	/// </summary>
	public void DebugMessage(string message, int traceLevels = 1) {
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POWERPRESS_DEBUG"))) {
			return;
		}

		var (msg, caller) = FormatWithCaller($"[DEBUG] {message}", traceLevels);
		Write("🐞 ", ConsoleColor.White);
		Write($"{msg} ", ConsoleColor.White);
		Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	/// <summary>
	///     Pretty-prints a JSON string as a key/value list.
	/// </summary>
	public void DisplayJsonTable(string json) {
		try {
			using var doc = JsonDocument.Parse(json);
			foreach (var prop in doc.RootElement.EnumerateObject()) {
				WriteLine($"{prop.Name}: {prop.Value}", ConsoleColor.Gray);
			}
		}
		catch {
			ErrorMessage("Failed to parse JSON", 2);
		}
	}

	public void DisplaySectionHeader(string title) {
		var trimmed = title.Trim();
		var totalPadding = DividerWidth - trimmed.Length - 2;
		var leftPadding = (int)Math.Floor(totalPadding / 2.0);
		var rightPadding = (int)Math.Ceiling(totalPadding / 2.0);

		var output = new string('=', leftPadding) + $" {trimmed} " + new string('=', rightPadding);
		WriteLine($"{output}", ConsoleColor.Magenta);
	}

	public void DisplaySectionFooter() {
		WriteLine(new string('=', DividerWidth) + "\n", ConsoleColor.Magenta);
	}
}