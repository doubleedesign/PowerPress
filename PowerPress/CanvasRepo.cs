using System.Diagnostics;

namespace PowerPress;

public class CanvasRepo {
	private readonly LocalSiteConfig config;
	private readonly FileHandler fileHandler;
	private readonly Logger logger = new();

	public CanvasRepo(LocalSiteConfig config) {
		this.config = config;
		this.fileHandler = new FileHandler();
		this.fileHandler.SetConfig(config);
	}

	public void Init() {
		if (!Directory.Exists(this.config.SiteDir)) {
			this.logger.ErrorMessage($"Site directory does not exist: {this.config.SiteDir}");
			Environment.Exit(1);
		}

		Directory.SetCurrentDirectory(this.config.SiteDir);
		string location = Directory.GetCurrentDirectory();
		this.logger.DebugMessage($"Working from {location}");

		// Clone template repo into the site directory
		this.logger.InfoMessage("Cloning template repository from GitHub");
		Process.Start(new ProcessStartInfo {
			FileName = "git",
			// Note: the dot clones the contents directly in, so we don't get a wordpress-canvas folder inside the project folder
			Arguments = "clone https://github.com/doubleedesign/wordpress-canvas .",
			WorkingDirectory = this.config.SiteDir,
			UseShellExecute = false
		})?.WaitForExit();

		// Confirm successful clone
		if (Directory.Exists(Path.Combine(this.config.SiteDir, ".git"))) {
			this.logger.SuccessMessage("Successfully cloned template repository into site directory");
		}
		else {
			this.logger.ErrorMessage("Failed to clone template repository into site directory");
			Environment.Exit(1);
		}

		// Delete template repo's git directory and some other files we don't need or are going to refresh anyway
		string[] toDelete = [".git", "sql", "composer.lock", "composer.dev.lock", "app/wp-content/uploads"];
		foreach (string item in toDelete) {
			string path = Path.Combine(this.config.SiteDir, item);
			this.fileHandler.MoveToRecycleBin(path);
		}
	}
}