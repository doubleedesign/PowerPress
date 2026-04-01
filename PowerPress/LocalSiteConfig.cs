using System.Globalization;
using System.Text.Json;

namespace PowerPress;

public class LocalSiteConfig {
	public LocalSiteConfig(string siteName, string siteDir, string productionUrl, string dbHost = "127.0.0.1", string dbPort = "3309", string dbUser = "root", string dbPass = "") {
		this.SiteName = this.ToTitleCase(siteName);
		this.SiteSlug = siteName;
		this.SiteShortName = siteName.Split("-")[0];
		this.SiteDir = siteDir;
		this.SiteUrl = "https://" + siteName + ".test";
		this.WpDir = this.SiteDir + "\\" + "app";

		this.DbName = this.SiteShortName + "_dev";
		this.DbUser = dbUser;
		this.DbPassword = dbPass;
		this.DbHost = dbHost;
		this.DbPort = dbPort;

		this.ProductionUrl = productionUrl.Contains("https://") ? productionUrl : "https://" + productionUrl;
	}

	public string? SiteName { get; private set; }
	public string? SiteSlug { get; private set; }
	public string? SiteShortName { get; }
	public string? SiteDir { get; }
	public string? WpDir { get; private set; }
	public string? DbName { get; private set; }
	public string? DbUser { get; private set; }
	public string? DbPassword { get; private set; }
	public string? DbHost { get; private set; }
	public string? DbPort { get; private set; }
	public string? SiteUrl { get; private set; }
	public string? ProductionUrl { get; private set; }
	public string? AdminUser { get; private set; }
	public string? AdminPassword { get; private set; }
	public string? AdminEmail { get; private set; }

	public void AddWordPressAdmin(string adminEmail) {
		this.AdminUser = this.SiteShortName + "-developer";
		this.AdminPassword = this.GeneratePassword(12);
		this.AdminEmail = adminEmail;
	}

	public string GetJsonString() {
		return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
	}

	private string ToTitleCase(string str) {
		return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower()).Replace("-", " ");
	}

	private string GeneratePassword(int length) {
		const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@#-_.,+=~";
		Random random = new();
		return new string(
			Enumerable.Repeat(validChars, length)
				.Select(s => s[random.Next(s.Length)])
				.ToArray()
		);
	}
}