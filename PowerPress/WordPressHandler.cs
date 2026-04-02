namespace PowerPress;

public class WordPressHandler {
	private readonly LocalSiteConfig config;
	private readonly FileHandler fileHandler = new();
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();
	private readonly ComposerHandler composerHandler;

	public WordPressHandler(LocalSiteConfig config) {
		this.config = config;
		this.composerHandler = new ComposerHandler(config);
	}

	public void UpdateConfig() {
		if (this.config.SiteDir is null) {
			this.logger.ErrorMessage("Cannot update config because site directory is not set");
			return;
		}

		if (this.config.WpDir is null) {
			this.logger.ErrorMessage("Cannot update config because WordPress directory is not set");
			return;
		}

		if (!File.Exists(Path.Combine(this.config.WpDir, "wp-config.php"))) {
			this.logger.ErrorMessage("Cannot update wp-config.php because it does not exist at the expected location");
		}

		this.logger.InfoMessage("Updating wp-config.php");
		string wpDirShort = this.config.WpDir.Replace(this.config.SiteDir, "");
		this.fileHandler.FindAndReplaceRegex(
			Path.Combine(wpDirShort, "wp-config.php"),
			@"define\('DB_NAME', '.*?'\);",
			$"define('DB_NAME', '{this.config.DbName}');"
		);
	}

	public void MaybeRemovePlugin(string ifInstalled, string thenRemove) {
		if (this.config.WpDir is null) {
			this.logger.ErrorMessage("Cannot remove plugin because WordPress directory is not set in config");
			return;
		}

		if (Directory.Exists(Path.Combine(this.config.WpDir, ifInstalled))) {
			this.fileHandler.MaybeDeleteFolder(Path.Combine(this.config.WpDir, thenRemove));
			this.composerHandler.RemoveDependency(thenRemove);
		}
	}

	public void RunCliCommand(string command, bool exitOnFail = false) {
		if (this.config.WpDir is null) {
			this.logger.ErrorMessage("Cannot run WP-CLI command because the WordPress directory is not set in the config");
			return;
		}

		string[] args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		Directory.SetCurrentDirectory(this.config.WpDir);
		CommandResult result = this.ps.RunCommand("wp", ["--skip-plugins", "--skip-themes", ..args]);

		if (!result.Success) {
			this.logger.ErrorMessage(result.Output.First());
			if (exitOnFail) {
				Environment.Exit(1);
			}

			return;
		}

		this.logger.SuccessMessage(result.Output.First());
	}
}