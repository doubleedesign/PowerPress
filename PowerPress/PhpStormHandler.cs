namespace PowerPress;

public class PhpStormHandler {
	private readonly LocalSiteConfig config;
	private readonly FileHandler fileHandler;
	private readonly Logger logger = new();
	private readonly UserInput ui = new();

	public PhpStormHandler(LocalSiteConfig config) {
		this.config = config;
		this.fileHandler = new FileHandler();
		this.fileHandler.SetConfig(config);
	}

	public void UpdateWorkspaceConfig() {
		// Find workspace.xml in site directory -> .idea
		string workspaceXmlPath = Path.Combine(this.config.SiteDir, ".idea", "workspace.xml");
		if (!File.Exists(workspaceXmlPath)) {
			this.logger.WarningMessage($"workspace.xml not found at {workspaceXmlPath}, skipping update");
			return;
		}

		// Replace all instances of the canonical path with the site directory
		try {
			string canonicalPath1 = "C:/Users/leesa/PhpStormProjects/wordpress-canvas";
			string canonicalPath2 = @"C:\Users\leesa\PhpStormProjects\wordpress-canvas";

			string formatted1 = this.config.SiteDir.Replace('\\', '/');
			string formatted2 = this.config.SiteDir.Replace('/', '\\');

			this.fileHandler.FindAndReplaceText(workspaceXmlPath, canonicalPath1, formatted1);
			this.fileHandler.FindAndReplaceText(workspaceXmlPath, canonicalPath2, formatted2);
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Failed to update PhpStorm workspace config");
			this.logger.ErrorMessage(ex.Message);
		}

		// Check that the file does not contain wordpress-canvas anymore
		try {
			string content = File.ReadAllText(workspaceXmlPath);
			if (content.Contains("wordpress-canvas")) {
				this.logger.WarningMessage(".idea/workspace.xml still contains references to wordpress-canvas, please check the file manually");
			}
			else {
				this.logger.SuccessMessage("PhpStorm workspace config updated successfully");
			}
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Failed to verify PhpStorm workspace config");
			this.logger.ErrorMessage(ex.Message);
		}
	}

	public void UpdateDeploymentConfig() {
		string serverIp = this.ui.PromptForText("Enter the IP address or hostname of your production server");
		if (string.IsNullOrEmpty(serverIp)) {
			this.logger.WarningMessage("No server IP entered, skipping PhpStorm deployment config update");
			return;
		}

		string deploymentXmlPath = Path.Combine(".idea", "deployment.xml");
		string webServersXmlPath = Path.Combine(".idea", "webServers.xml");


		// Find YOUR_SERVER_IP in the files and replace it with the provided IP
		try {
			this.fileHandler.FindAndReplaceText(deploymentXmlPath, "YOUR_SERVER_IP", serverIp);
			this.fileHandler.FindAndReplaceText(webServersXmlPath, "YOUR_SERVER_IP", serverIp);
			// TODO: Some kind of verification
			this.logger.SuccessMessage($"Updated PhpStorm deployment config with server IP {serverIp}");
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Failed to update PhpStorm deployment config");
			this.logger.ErrorMessage(ex.Message);
		}

		// Find SOME_UNIQUE_ID and replace it with a random GUID
		string randomId = Guid.NewGuid().ToString();
		try {
			this.fileHandler.FindAndReplaceText(webServersXmlPath, "SOME_UNIQUE_ID", randomId);
			// TODO: Some kind of verification
			this.logger.SuccessMessage($"Updated PhpStorm deployment config with unique ID {randomId}");
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Failed to update PhpStorm deployment config with unique ID");
			this.logger.ErrorMessage(ex.Message);
		}

		// Replace https://your-production-url with the production URL
		string? productionUrl = this.config.ProductionUrl;
		if (string.IsNullOrEmpty(productionUrl)) {
			this.logger.WarningMessage("No production URL found in config for PhpStorm deployment config");
			return;
		}

		try {
			this.fileHandler.FindAndReplaceText(webServersXmlPath, "https://your-production-url", $"https://{productionUrl}");
			// TODO: Some kind of verification
			this.logger.SuccessMessage($"Updated PhpStorm deployment config with production URL https://{productionUrl}");
		}
		catch (Exception ex) {
			this.logger.ErrorMessage("Failed to update PhpStorm deployment config with production URL");
			this.logger.ErrorMessage(ex.Message);
		}
	}
}