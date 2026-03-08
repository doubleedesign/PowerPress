#nullable enable
using System.Text.Json;
using System.Linq;
using System;

public class LocalSiteConfig {
	public string? SiteName { get; private set; }
	public string? SiteSlug { get; private set; }
	public string? SiteShortName { get; private set; }
	public string? SiteDir { get; private set; }
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

	
	public LocalSiteConfig(string siteName, string siteDir, string productionUrl, string dbHost, string dbPort, string dbUser, string dbPass) {
		SiteName = ToTitleCase(siteName);
		SiteSlug = siteName;
		SiteShortName = siteName.Split("-")[0];
		SiteDir = siteDir;
		SiteUrl = "https://" + siteName + ".test";
		WpDir = SiteDir + "\\" + "app";

		DbName = SiteShortName + "_dev";
		DbUser = dbUser;
		DbPassword = dbPass;
		DbHost = dbHost;
		DbPort = dbPort;

		ProductionUrl = productionUrl.Contains("https://") ? productionUrl : "https://" + productionUrl;
	}

	public void AddWordPressAdmin(string adminEmail) {
		AdminUser = SiteShortName + "-developer";
		AdminPassword = GeneratePassword(12);
		AdminEmail = adminEmail;
	}

	public string GetJsonString() {
		return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
	}
	
	private string ToTitleCase(string str) {
		return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower()).Replace("-", " ");
	}
	
	private string GeneratePassword(int length) {
		const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890@#-_.,+=~";
		var random = new Random();
		return new string(
			Enumerable.Repeat(validChars, length)
				.Select(s => s[random.Next(s.Length)])
				.ToArray()
			);
	}
}