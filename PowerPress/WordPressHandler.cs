namespace PowerPress;

public class WordPressHandler {
	private readonly LocalSiteConfig config;
	private readonly FileHandler fileHandler = new();
	private readonly Logger logger = new();

	public WordPressHandler(LocalSiteConfig config) {
		this.config = config;
	}

	public void MaybeRemovePlugin(string ifInstalled, string thenRemove) {
		if (this.config.WpDir is null) {
			this.logger.ErrorMessage("Cannot remove plugin because WordPress directory is not set in config");
			return;
		}

		if (Directory.Exists(Path.Combine(this.config.WpDir, ifInstalled))) {
			this.fileHandler.MaybeDeleteFolder(Path.Combine(this.config.WpDir, thenRemove));
		}
	}
}