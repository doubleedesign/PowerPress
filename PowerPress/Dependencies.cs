namespace PowerPress;

public class Dependencies {
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();

	public void CheckDependencies() {
		bool psVersion = this.CheckPowerShellVersion();
		bool main = this.CheckMainCliCommands();
		bool wpCli = this.CheckWpCliCommands();
		bool php = this.CheckPhpExtensions();

		if (!psVersion || !main || !wpCli || !php) {
			Environment.Exit(1);
		}
	}

	private bool CheckPowerShellVersion() {
		Version version = this.ps.GetVersion();

		if (version.Major < 7) {
			this.logger.ErrorMessage(
				"This script requires PowerShell 7 or higher. Please update your PowerShell version and try again."
			);

			this.logger.InfoMessage(
				"If you are running via the terminal, install PowerShell 7 from the Microsoft Store, via Chocolatey, or another method and run this script using that instead of 'Windows PowerShell'."
			);

			return false;
		}

		this.logger.SuccessMessage($"PowerShell version is {version}");
		return true;
	}

	private bool CheckMainCliCommands() {
		string[] commands = ["php", "mysql", "composer", "git", "herd", "wp"];
		List<string> missing = [];

		foreach (string command in commands) {
			if (!this.ps.GetCommand(command).Any()) {
				this.logger.ErrorMessage($"{command} is not available");
				missing.Add(command);
			}
			else {
				this.logger.SuccessMessage($"{command} is available");
			}
		}

		if (missing.Count > 0) {
			this.logger.WarningMessage("Please install the missing dependencies and ensure they are in your PATH.");
			return false;
		}

		return true;
	}

	private bool CheckWpCliCommands() {
		string[] commands = ["core", "scaffold", "option", "db", "search-replace", "plugin", "theme", "rewrite"];
		List<string> missing = [];

		foreach (string command in commands) {
			CommandResult result = this.ps.RunCommand("wp", ["help", command]);
			if (!result.Success) {
				this.logger.ErrorMessage($"WP-CLI command {command} is not available");
				missing.Add(command);
			}
			else {
				this.logger.SuccessMessage($"WP-CLI command {command} is available");
			}
		}

		if (missing.Count > 0) {
			this.logger.WarningMessage(
				$"WP-CLI is missing some required commands:  {string.Join(", ", missing)}. \n " +
				$"  If you are managing WP-CLI via composer, try globally installing the following packages:");

			if (missing.Count > 1 && missing.Contains("wp search-replace")) {
				Console.WriteLine("   wp-cli/search-replace-command");
			}

			if (missing.Count > 0 && !missing.Contains("wp search-replace")) {
				Console.WriteLine("   wp-cli/wp-cli-bundle");
			}

			return false;
		}

		return true;
	}

	private bool CheckPhpExtensions() {
		string[] extensions = ["curl", "openssl"];
		List<string> missing = [];

		foreach (string extension in extensions) {
			CommandResult result = this.ps.RunCommand("php", ["-m"]);
			if (!result.Output.Any(line => line.Equals(extension, StringComparison.OrdinalIgnoreCase))) {
				this.logger.ErrorMessage($"PHP extension {extension} is not installed or enabled");
				missing.Add(extension);
			}
			else {
				this.logger.SuccessMessage($"PHP extension {extension} is installed");
			}
		}

		if (missing.Count > 0) {
			this.logger.WarningMessage("Please install the missing PHP extensions and try again.");
			return false;
		}

		return true;
	}

	public void CheckPermissions() {
		bool scripts = this.CanExecuteScripts();
		bool symlinks = this.CanCreateSymlinks();

		if (!scripts && !symlinks) {
			Environment.Exit(0);
		}
	}

	private bool CanExecuteScripts() {
		string executionPolicy = this.ps.GetExecutionPolicy("CurrentUser") ?? "Undefined";

		if (executionPolicy != "Bypass" && executionPolicy != "Unrestricted") {
			this.logger.ErrorMessage(
				$"Current user execution policy is set to '{executionPolicy}'. You will not be able to complete the WordPress install properly."
			);

			this.logger.InfoMessage("To update the execution policy, open PowerShell as an administrator and run:\n" +
			                        "   Set-ExecutionPolicy Bypass -Scope CurrentUser"
			);

			return false;
		}

		this.logger.SuccessMessage($"Current user execution policy is '{executionPolicy}'");

		return true;
	}

	private bool CanCreateSymlinks() {
		CommandResult result = this.ps.RunCommand("whoami", ["/priv"]);

		// Look for a line containing "SeCreateSymbolicLink"
		string? line = result.Output.Select(line => line.Trim()).ToList()
			.Find(line => line.StartsWith("SeChangeNotifyPrivilege", StringComparison.OrdinalIgnoreCase));

		if (line == null) {
			this.logger.ErrorMessage("Symlink permission setting not found.");
			return false;
		}

		bool enabled = line.EndsWith("Enabled", StringComparison.OrdinalIgnoreCase);
		if (enabled) {
			this.logger.SuccessMessage("You can create symbolic links in the current context");
			return true;
		}

		this.logger.WarningMessage("You do not have permission to create symbolic links in the current context." +
		                           "You may run into issues linking local Composer packages if you choose to install dependencies with that option."
		);
		this.logger.InfoMessage(
			"To enable symbolic link creation, make sure Windows Developer Mode is enabled and run PowerShell with administrator privileges.");

		return false;
	}
}