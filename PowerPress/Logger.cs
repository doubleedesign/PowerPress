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
		(string msg, string caller) = this.FormatWithCaller(message, traceLevels);
		this.Write("✔  ", ConsoleColor.Green);
		this.Write($"{msg} ", ConsoleColor.Green);
		this.Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void WarningMessage(string message, int traceLevels = 1) {
		(string msg, string caller) = this.FormatWithCaller(message, traceLevels);
		this.Write("⚠️ ", ConsoleColor.Yellow);
		this.Write($"{msg} ", ConsoleColor.Yellow);
		this.Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void ErrorMessage(string message, int traceLevels = 1) {
		(string msg, string caller) = this.FormatWithCaller(message, traceLevels);
		this.Write("✖  ", ConsoleColor.Red);
		this.Write($"{msg} ", ConsoleColor.Red);
		this.Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	public void InfoMessage(string message, int traceLevels = 1) {
		(string msg, string caller) = this.FormatWithCaller(message, traceLevels);
		this.Write("📝 ", ConsoleColor.Blue);
		this.Write($"{msg} ", ConsoleColor.Blue);
		this.Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	/// <summary>
	///     Outputs the given additional debugging output when the POWERPRESS_DEBUG environment variable is set.
	/// </summary>
	public void DebugMessage(string message, int traceLevels = 1) {
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POWERPRESS_DEBUG"))) {
			return;
		}

		(string msg, string caller) = this.FormatWithCaller($"[DEBUG] {message}", traceLevels);
		this.Write("🐞 ", ConsoleColor.White);
		this.Write($"{msg} ", ConsoleColor.White);
		this.Write(caller, ConsoleColor.DarkGray);
		Console.WriteLine();
	}

	/// <summary>
	///     Pretty-prints a JSON string as a key/value list.
	/// </summary>
	public void DisplayJsonTable(string json) {
		try {
			using JsonDocument doc = JsonDocument.Parse(json);
			foreach (JsonProperty prop in doc.RootElement.EnumerateObject()) {
				this.WriteLine($"{prop.Name}: {prop.Value}", ConsoleColor.Gray);
			}
		}
		catch {
			this.ErrorMessage("Failed to parse JSON", 2);
		}
	}

	public void DisplaySectionHeader(string title) {
		string trimmed = title.Trim();
		int totalPadding = DividerWidth - trimmed.Length - 2;
		int leftPadding = (int)Math.Floor(totalPadding / 2.0);
		int rightPadding = (int)Math.Ceiling(totalPadding / 2.0);

		string output = new string('=', leftPadding) + $" {trimmed} " + new string('=', rightPadding);
		this.WriteLine($"{output}", ConsoleColor.Magenta);
	}

	public void DisplaySectionFooter() {
		this.WriteLine(new string('=', DividerWidth) + "\n", ConsoleColor.Magenta);
	}
}