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
		this.fileHandler.FindAndReplaceRegex(
			Path.Combine(this.config.WpDir, "wp-config.php"),
			@"define\('DB_NAME', '.*?'\);",
			$"define('DB_NAME', '{this.config.DbName}');"
		);

		string[] lines = File.ReadAllLines(Path.Combine(this.config.WpDir, "wp-config.php"));
		List<string> errors = [];
		if (lines[22].Trim() != $"define('DB_NAME', '{this.config.DbName}');") {
			errors.Add("DB_NAME");
		}

		if (lines[25].Trim() != $"define('DB_USER', '{this.config.DbUser}');") {
			errors.Add("DB_USER");
		}

		if (lines[31].Trim() != $"define('DB_HOST', '{this.config.DbHost}:{this.config.DbPort}');") {
			errors.Add("DB_HOST/DB_PORT");
		}

		if (errors.Count > 0) {
			this.logger.ErrorMessage($"Problem updating wp-config.php. The following values were not updated correctly: {string.Join(", ", errors)}");
			Environment.Exit(1);
		}

		this.logger.SuccessMessage("wp-config.php updated successfully");
	}

	public CommandResult RunCliCommand(string command, bool skipPluginsAndThemes = true, bool exitOnFail = false) {
		string[] args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		Directory.SetCurrentDirectory(this.config.WpDir);
		CommandResult result = skipPluginsAndThemes
			? this.ps.RunCommand("wp", ["--skip-plugins", "--skip-themes", ..args])
			: this.ps.RunCommand("wp", [..args]);

		if (!result.Success) {
			this.logger.ErrorMessage(result.Output.First());
			if (exitOnFail) {
				Environment.Exit(1);
			}

			return result;
		}

		if (result.Output.Count > 0 && result.Output.First().Equals("Success: Updated 'ninja_forms_settings' option.")) {
			string optionName = args.Reverse().Skip(1).First();
			string optionValue = args.Reverse().First();
			this.logger.SuccessMessage($"Updated 'ninja_forms_settings' option {optionName} to {optionValue}");

			return result;
		}

		this.logger.SuccessMessage(result.Output.Count > 0 ? result.Output.First() : $"WP-CLI command {command} completed successfully");

		return result;
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
				$"--title=\"{this.config.SiteName}\"",
				$"--admin_user={this.config.AdminUser}",
				$"--admin_email={this.config.AdminEmail}",
				$"--admin_password={this.config.AdminPassword}"
			);
			this.RunCliCommand(installCommand);
		}
		catch (Exception e) {
			this.logger.ErrorMessage("Error installing WordPress");
			this.logger.ErrorMessage(e.Message);
			Environment.Exit(1);
		}

		// FIXME: The output of this is displaying a success message as an error message, but not all RunCliCommand usages are
		// Set default permalink structure
		// Required for the REST API to work for automated tests out of the box
		this.RunCliCommand("rewrite structure '/%postname%/'");

		// TODO: Check if still true and fix: When running WP-CLI using Git for Windows's shell interpreter, it causes the rewrite to have /C:/Program%20Files/Git/ in it

		// Flush rewrite rules
		// FIXME: The output of this is displaying a success message as an error message, but not all RunCliCommand usages are
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
				$"--theme_name=\"{siteName}\"",
				"--parent_theme=comet-canvas-blocks",
				$"--author=\"{authorName}\"",
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

	public void MaybeActivatePlugin(string plugin) {
		if (!Directory.Exists(Path.Combine(this.config.WpDir, "wp-content", "plugins", plugin))) {
			this.logger.WarningMessage($"Plugin {plugin} not found. Skipping activation.");
			return;
		}

		this.RunCliCommand($"plugin activate {plugin}");
	}

	public void MaybeRemovePlugin(string ifInstalled, string thenRemove) {
		if (!Directory.Exists(Path.Combine(this.config.WpDir, ifInstalled))) {
			this.logger.InfoMessage($"Plugin {ifInstalled} is not present, skipping removal");
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

		this.fileHandler.CopyDirectory(source, dest);
	}

	public void UpdateSiteUrl(string oldUrl, string newUrl) {
		this.RunCliCommand($"search-replace {oldUrl} {newUrl} --skip-columns=guid");
		this.RunCliCommand("rewrite flush");

		// Check that site URL in wp_options matches the new URL
		CommandResult result = this.RunCliCommand("option get siteurl", exitOnFail: false);
		if (result.Output.First() == newUrl) {
			this.logger.SuccessMessage("Site URL updated successfully");
		}
		else {
			this.logger.ErrorMessage($"Site URL update may have failed. Expected {newUrl} but got {result.Output.First()}");
		}
	}

	public void DangerouslyRunFunction(string func, string[] args, bool echo = false) {
		// We don't want to load all themes and plugins because if they error it breaks this even though we might not need them loaded.
		// Instead, just load ACF if the function is an ACF one
		string prefixCode = "";
		if (func.StartsWith("acf_")) {
			prefixCode = "include_once WP_PLUGIN_DIR . '/advanced-custom-fields-pro/acf.php';";
		}
		// ...and add similar handling here as needed in the future.

		string argList = string.Join(", ", args.Select(a => $"\"{a}\""));
		string phpCode = echo
			? $"{prefixCode} echo {func}({argList});"
			: $"{prefixCode} \n{func}({argList});";

		this.RunCliCommandWithPrefixedPhp(phpCode);
	}

	private void RunCliCommandWithPrefixedPhp(string phpCode, bool exitOnFail = false) {
		string tempFile = Path.Combine(Path.GetTempPath(), $"wp_eval_{Guid.NewGuid():N}.php");

		try {
			this.logger.InfoMessage("Creating temporary PHP script to run:\n" + phpCode);
			File.WriteAllText(tempFile, $"<?php \n{phpCode}");
			this.RunCliCommand($"eval-file {tempFile}", true, exitOnFail);
		}
		finally {
			if (File.Exists(tempFile)) {
				File.Delete(tempFile);
			}

			if (!File.Exists(tempFile)) {
				this.logger.SuccessMessage("Deleted temp file");
			}
		}
	}
}