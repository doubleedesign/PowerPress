using System.Text.Json;

namespace PowerPress;

public class WordPressHandler {
	private readonly ComposerHandler composerHandler;
	private readonly LocalSiteConfig config;
	private readonly FileHandler fileHandler = new();
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();
	private readonly UserInput ui = new();

	public WordPressHandler(LocalSiteConfig config) {
		this.config = config;
		this.composerHandler = new ComposerHandler(config);
	}

	public void UpdateConfig() {
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

	public void RunCliCommand(string command, bool exitOnFail = false) {
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

	public void RunInstall() {
		if (!File.Exists(Path.Combine(this.config.WpDir, "wp-config.php"))) {
			this.logger.ErrorMessage("Cannot run installation because wp-config was not found");
			Environment.Exit(1);
		}

		try {
			string installCommand = string.Join(" ",
				"core install",
				$"--url={this.config.SiteUrl}",
				$"--title={this.config.SiteName}",
				$"--admin_user={this.config.AdminUser}",
				$"--admin_email={this.config.AdminEmail}",
				$"--admin_password={this.config.AdminPassword}"
			);
			this.RunCliCommand(installCommand, true);
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Error installing WordPress");
			this.logger.ErrorMessage(ex.Message);
			Environment.Exit(1);
		}

		// Set default permalink structure
		// Required for the REST API to work for automated tests out of the box
		this.RunCliCommand("rewrite structure '/%postname%/'");

		// TODO: Check if still true and fix: When running WP-CLI using Git for Windows's shell interpreter, it causes the rewrite to have /C:/Program%20Files/Git/ in it

		// Flush rewrite rules
		this.RunCliCommand("rewrite flush");
	}

	public void CreateAndActivateChildTheme() {
		Directory.SetCurrentDirectory(this.config.WpDir);

		string authorName = this.ui.PromptForText("Enter the author name for the child theme", "Double-E Design");
		string authorUri = this.ui.PromptForText("Enter the author URI for the child theme", "https://www.doubleedesign.com.au");

		string siteName = this.config.SiteName;
		string themeDirectoryName = this.config.SiteSlug;
		string themeUri = this.config.ProductionUrl;

		this.logger.InfoMessage("Child theme configuration:");
		Dictionary<string, string> themeConfig = new() {
			{ "Name", siteName },
			{ "Template", "comet-canvas-blocks" },
			{ "Author", authorName },
			{ "Author URI", authorUri },
			{ "Theme URI", themeUri }
		};
		this.logger.DisplayJsonTable(JsonSerializer.Serialize(themeConfig));

		try {
			string command = string.Join(" ",
				"scaffold child-theme",
				themeDirectoryName,
				$"--theme_name={siteName}",
				"--parent_theme=comet-canvas-blocks",
				$"--author={authorName}",
				$"--author_uri={authorUri}",
				$"--theme_uri={themeUri}",
				"--activate"
			);
			this.RunCliCommand(command);
		}
		catch (Exception e) {
			this.logger.ErrorMessage(e.Message);
		}
	}

	public void RunPostinstallCleanup() {
		// Go into the themes directory and delete default themes (anything starting with twenty*)
		string themesDir = Path.Combine(this.config.WpDir, "wp-content", "themes");
		foreach (string dir in Directory.GetDirectories(themesDir, "twenty*")) {
			this.fileHandler.MaybeDeleteFolder(dir);
		}

		// Go into the plugins directory and delete Akismet and Hello Dolly (if they exist)
		string pluginsDir = Path.Combine(this.config.WpDir, "wp-content", "plugins");
		string[] defaultPlugins = ["akismet", "hello.php"];
		foreach (string plugin in defaultPlugins) {
			string pluginPath = Path.Combine(pluginsDir, plugin);
			this.fileHandler.MaybeDeleteFolder(pluginPath);
		}
	}

	public void MaybeRemovePlugin(string ifInstalled, string thenRemove) {
		if (!Directory.Exists(Path.Combine(this.config.WpDir, ifInstalled))) {
			this.logger.WarningMessage($"Plugin {ifInstalled} is not present, skipping removal");
			return;
		}

		this.fileHandler.MaybeDeleteFolder(Path.Combine(this.config.WpDir, thenRemove));
		this.composerHandler.RemoveDependency(thenRemove);
	}

	public void CopyPluginFromLocalPath(string source) {
		string pluginDirName = Path.GetFileName(source);
		string dest = Path.Combine(this.config.WpDir, "wp-content", "plugins", pluginDirName);

		if (Directory.Exists(dest)) {
			this.logger.InfoMessage($"Plugin {pluginDirName} directory already exists, skipping copy");
			return;
		}

		this.fileHandler.CopyDirectory(source, dest);
	}

	public void CopyThemeFromLocalPath(string source) {
		string themeDirName = Path.GetFileName(source);
		string dest = Path.Combine(this.config.WpDir, "wp-content", "themes", themeDirName);

		if (Directory.Exists(dest)) {
			this.logger.InfoMessage($"Theme {themeDirName} directory already exists, skipping copy");
			return;
		}

		this.fileHandler.CopyDirectory(source, dest);
	}

	public void CopyUploadsFromLocalPath(string source) {
		string dest = Path.Combine(this.config.WpDir, "wp-content", "uploads");

		if (Directory.Exists(dest)) {
			this.logger.InfoMessage("Uploads directory already exists, skipping copy");
			return;
		}

		this.fileHandler.CopyDirectory(source, dest);
	}
}