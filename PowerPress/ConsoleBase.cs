using System.Reflection;
using System.Text;

namespace PowerPress;

// ReSharper disable RedundantUsingDirective
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FormattedMessage = (string Message, string Caller);

public abstract class ConsoleBase {
	private static readonly HashSet<string> ExcludedFrames = new(StringComparer.OrdinalIgnoreCase) {
		"RunWpCliCommand",
		"RunComposerCommand",
		"RemoveWithWait",
		"Log",
		"Success",
		"Warning",
		"Error",
		"Info",
		"Debug",
		"PromptForText",
		"PromptForYesOrNo"
	};

	protected ConsoleBase() {
		// Ensure console encoding is UTF-8 to support emojis
		Console.OutputEncoding = Encoding.UTF8;
	}

	protected void Write(string text, ConsoleColor colour) {
		ConsoleColor original = Console.ForegroundColor;
		Console.ForegroundColor = colour;
		Console.Write(text);
		Console.ForegroundColor = original;
	}

	protected void WriteLine(string text, ConsoleColor colour) {
		ConsoleColor original = Console.ForegroundColor;
		Console.ForegroundColor = colour;
		Console.WriteLine(text);
		Console.ForegroundColor = original;
	}

	protected FormattedMessage FormatWithCaller(string message, int traceLevels = 1) {
		int minWidth = 120;
		int maxWidth = 200;
		int windowWidth = Console.WindowWidth;
		int maybeWidth = Math.Max(windowWidth, minWidth);
		int lineWidth = Console.IsOutputRedirected
			? minWidth
			: Math.Min(maybeWidth, maxWidth);

		List<string> callers = this.GetCallingFunctionNames(traceLevels);
		string caller = string.Concat(callers.Select(c => $"[{c}]"));

		// If the combined line would wrap, push caller to the next line right-aligned
		if (message.Length + caller.Length + 6 > lineWidth) {
			string rightAligned = caller.PadLeft(Math.Max(caller.Length, lineWidth - 6));
			return (message.Trim(), $"\n{rightAligned}");
		}

		string paddedMessage = message.Trim().PadRight(lineWidth - caller.Length - 6);
		return (paddedMessage, caller);
	}

	private List<string> GetCallingFunctionNames(int traceLevels) {
		List<string> result = new();
		int count = 0;

		// Skip frames 0 (this method) and 1 (FormatWithCaller / the public logger method)
		StackFrame[] frames = new StackTrace(true).GetFrames() ?? Array.Empty<StackFrame>();

		foreach (StackFrame frame in frames.Skip(2)) {
			MethodBase? method = frame.GetMethod();
			if (method == null) continue;

			string name = method.Name;
			if (ExcludedFrames.Contains(name)) continue;

			// Include line number when the entry point is the top-level script
			string? fileName = frame.GetFileName();
			if (!string.IsNullOrEmpty(fileName) &&
			    fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)) {
				result.Add($"{Path.GetFileName(fileName)}:{frame.GetFileLineNumber()}");
			}
			else {
				result.Add(name);
			}

			if (++count >= traceLevels) break;
		}

		return result;
	}
}