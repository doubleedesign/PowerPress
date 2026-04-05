using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation;

namespace PowerPress;

public class PowerShellBridge {
	private readonly Logger logger = new();
	private readonly PowerShell ps = PowerShell.Create();

	public string? GetExecutionPolicy(string scope) {
		this.ps.AddCommand("Get-ExecutionPolicy").AddParameter("Scope", scope);

		Collection<PSObject>? result = this.ps.Invoke();

		this.ps.Commands.Clear();
		this.ps.Streams.Error.Clear();

		return result[0].ToString();
	}

	public Version GetVersion() {
		CommandResult result = this.RunCommand("pwsh", ["-v"]);

		this.ps.Commands.Clear();
		this.ps.Streams.Error.Clear();

		string trimmed = result.Output.First().Replace("PowerShell", "").Trim();

		return Version.Parse(trimmed);
	}

	/// <summary>
	///     Run Get-Command and return the result
	/// </summary>
	/// <param name="command"></param>
	public IEnumerable<CommandInfo> GetCommand(string command) {
		Collection<CommandInfo>? result = this.ps.AddCommand("Get-Command")
			.AddArgument(command)
			.Invoke<CommandInfo>();

		this.ps.Commands.Clear();
		this.ps.Streams.Error.Clear();

		return result;
	}

	/// <summary>
	///     Run a simple command with the given non-named arguments and return the output and errors.
	///     Intended for CLI commands that give simple output,
	///     not those that give progress updates like composer install or git clone.
	/// </summary>
	/// <example>
	///     RunCommand("wp", ["help", "core"]) would run "wp help core".
	/// </example>
	/// <param name="command">The command to run, e.g. wp</param>
	/// <param name="arguments">The arguments to pass, e.g. [help, core]</param>
	/// <returns>A CommandResult containing the output and any errors</returns>
	public CommandResult RunCommand(string command, string[] arguments) {
		this.ps.AddCommand(command);
		foreach (string argument in arguments) {
			this.ps.AddArgument(argument);
		}

		this.logger.DebugMessage($"Running command: {command} {string.Join(" ", arguments)}");

		List<string> results = this.ps.Invoke()
			.Select(r => r.ToString())
			.ToList();

		// Merge errors into output, mirroring 2>&1 in PowerShell
		List<string> errors = this.ps.Streams.Error
			.Select(e => e.ToString())
			.ToList();

		this.ps.Commands.Clear();
		this.ps.Streams.Error.Clear();

		if (errors.Count > 0) {
			// Filter out errors we don't want to treat as errors,
			// logging them to the console appropriately along the way (or in a small number of cases, ignoring them)
			errors = errors.Where(e => {
				string trimmed = e.Trim();

				if (trimmed.StartsWith("Comet Components core config") || trimmed.StartsWith("PHP Notice:  Function add_theme_support( 'title-tag' ) was called <strong>incorrectly</strong>.")) {
					return false;
				}

				if (trimmed.StartsWith("Warning:") || trimmed.StartsWith("PHP Warning:")) {
					this.logger.WarningMessage(e);
					return false;
				}

				if (trimmed.StartsWith("Notice:") || trimmed.StartsWith("PHP Notice:")) {
					this.logger.WarningMessage(e);
					return false;
				}

				return true;
			}).ToList();
		}

		if (errors.Count > 0) {
			this.logger.DebugMessage($"{errors.Count} errors/warnings not filtered out:\n {string.Join("\n", errors)}");
		}

		return new CommandResult(errors.Count == 0, results.Concat(errors).ToList());
	}

	public CommandResult RunProcess(string command, string args, string workingDirectory, bool verbose = true) {
		ProcessStartInfo psi = new() {
			FileName = command,
			Arguments = command == "pwsh.exe" ? $"-NoProfile {args}" : args,
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = !verbose,
			RedirectStandardError = !verbose
		};

		using Process process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process: {command}");

		// Collect and handle output before exiting the process if applicable
		if (!verbose) {
			string stdout = process.StandardOutput.ReadToEnd();
			string stderr = process.StandardError.ReadToEnd();
			process.WaitForExit();

			List<string> output = stdout
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(l => l.TrimEnd('\r'))
				.ToList();

			if (!string.IsNullOrEmpty(stderr)) {
				output.AddRange(stderr
					.Split('\n', StringSplitOptions.RemoveEmptyEntries)
					.Select(l => l.TrimEnd('\r')));
			}

			process.StandardOutput.Close();
			process.StandardError.Close();

			return new CommandResult(process.ExitCode == 0, output);
		}

		process.WaitForExit();

		return new CommandResult(process.ExitCode == 0, new List<string>(
			process.ExitCode == 0 ? [$"{command} command completed successfully"] : [$"{command} command did not complete successfully"])
		);
	}
}