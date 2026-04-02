using System.Collections.ObjectModel;
using System.Management.Automation;

namespace PowerPress;

public class PowerShellBridge {
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
	///     Run the specified command with the given non-named arguments and return the output and errors.
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

		List<string> results = this.ps.Invoke()
			.Select(r => r.ToString())
			.ToList();

		// Merge errors into output, mirroring 2>&1 in PowerShell
		List<string> errors = this.ps.Streams.Error
			.Select(e => e.ToString())
			.ToList();

		this.ps.Commands.Clear();
		this.ps.Streams.Error.Clear();

		return new CommandResult(errors.Count == 0, results.Concat(errors).ToList());
	}
}