param(
	[string]$SiteName,
	[switch]$Debug,
	[switch]$Dev,
	[switch]$Help
)

# =============================================================================================================== #
$dotnet = Get-Command dotnet | Select-Object -ExpandProperty Version
$versionNumber = [int]$dotnet.Major
if ($versionNumber -lt 10) {
	Write-Host "`n✖  PowerPress requires .NET 10 or higher." -ForegroundColor Red
	Write-Host "   Download installer from: https://dotnet.microsoft.com/en-us/download/dotnet"
	Write-Host "   Or run: choco install dotnet"
	exit 1
}
else {
	Write-Host "✔  .NET version is $dotnet" -ForegroundColor Green
}

# Load compiled C# classes
Import-Module $PSScriptRoot\ClassLoader.psm1
Import-Classes

# Store location the script was called from
$scriptLocation = Get-Location

# Create instances of the classes we need to use throughout the script that do not need the local site config passed in at construction
$Logger = [PowerPress.Logger]::new()
$UI = [PowerPress.UserInput]::new()
$DepsHandler = [PowerPress.Dependencies]::new()
$CredsHandler = [PowerPress.BitwardenHandler]::new()
$FileHandler = [PowerPress.FileHandler]::new()

# Make sure site name was provided and show help if not
if ($Help -or [string]::IsNullOrEmpty($SiteName)) {
	$Logger.DisplaySectionHeader("PowerPress Help")
	Write-Host "Usage:" -ForegroundColor Cyan
	Write-Host "`tpowerpress site-name" -ForegroundColor Gray
	Write-Host "`nArguments:" -ForegroundColor Cyan
	Write-Host "`tsite-name `t The kebab-case name of your site." -ForegroundColor Gray
	Write-Host "`nOptions:" -ForegroundColor Cyan
	Write-Host "`t-Debug `t`t PowerPress debug mode. Additional debug information will be displayed during setup." -ForegroundColor Gray
	Write-Host "`t-Dev `t`t Double-E Design developer mode. PowerPress will use composer.dev.json from the Canvas repo and `n`t`t`t Composer will symlink local copies of Double-E Design packages instead of downloading them from Packagist or GitHub. `n`t`t`t Intended for for developing and testing changes to those packages." -ForegroundColor Gray
	Write-Host ""
	$Logger.DisplaySectionFooter()
	exit 0
}

# If the -Debug flag is set, set the environment variable
if ($Debug) {
	$env:POWERPRESS_DEBUG = "1"
	$Logger.SuccessMessage("PowerPress Debug mode enabled");
}

# If the -Dev flag is set, set the environment variable
if ($Dev) {
	$env:POWERPRESS_DEV = "1"
	$Logger.SuccessMessage("Dev mode enabled for site setup");
}


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Welcome to PowerPress")
Write-Host "PowerPress is a PowerShell utility to set up Composer-managed WordPress sites `nfor local development on Windows machines." -ForegroundColor Gray
Write-Host "`nOut of the box, it can be used for new sites or for updating existing sites to `nuse Double-E Design's current and tooling and base suite of plugins." -ForegroundColor Gray
Write-Host "`nPowerPress is open source. You can find the latest documentation, report issues, `ncontribute fixes and improvements, and/or fork the repo to modify it to suit your needs." -ForegroundColor Gray
Write-Host "Repo URL: https://github.com/doubleedesign/powerpress" -ForegroundColor Gray
Write-Host "`nPress 'Enter' to start." -ForegroundColor Cyan
do {
	$key = [Console]::ReadKey("noecho")
} while ($key.Key -ne "Enter")
$Logger.DisplaySectionFooter()

# Check dependencies and PHP extensions before doing anything else
$Logger.DisplaySectionHeader("Prerequisites")
$DepsHandler.CheckDependencies()
$DepsHandler.CheckPermissions()

# BitWarden CLI is treated a little differently than other dependencies because we need to check env variables as well as the command,
# and it makes sense to log in at the same time to avoid checking "can access Bitwarden" multiple times
$useBitwarden = $CredsHandler.MaybeLogIn()
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Base Config");
$username = $env:USERNAME
$defaultProjectsDir = "C:\Users\$username\ClientSites"
$PROJECTS_DIR = $UI.PromptForText("Enter the path to your website projects directory", $defaultProjectsDir)
$Logger.InfoMessage("Using projects directory: $PROJECTS_DIR");
$SiteDir = Join-Path $PROJECTS_DIR $SiteName
$Logger.InfoMessage("Site directory will be: $SiteDir");

# If directory exists, prompt to delete or exit
$continue = $FileHandler.MaybeDeleteFolder($SiteDir, "Directory $SiteDir already exists. Do you want to delete it and start fresh?")
if (-not $continue) {
	$Logger.WarningMessage("Cannot initialise new site because there is an existing directory at $SiteDir.");
	$Logger.WarningMessage("Exiting script.");
	exit 0
}
$continue = $FileHandler.MaybeCreateFolder($SiteDir)
if (-not $continue) {
	$Logger.ErrorMessage("Failed to create site directory. Exiting script.");
	exit 1
}

$ProductionUrl = $UI.PromptForText("Enter the production URL for this site (without https://)", "")
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Database")
$dbHost = $UI.PromptForText("Enter local database hostname", "127.0.0.1")
$dbPort = $UI.PromptForText("Enter local database server port", "3309")
$dbUser = $UI.PromptForText("Enter local database username", "root")
$dbPass = $UI.PromptForText("Enter local database password", "")

# Save config as a global object so it can be easily passed around to different functions in modules called after its creation
$global:SiteConfig = [LocalSiteConfig]::new(
	$SiteName,
	$SiteDir,
	$ProductionUrl,
	$dbHost,
	$dbPort,
	$dbUser,
	$dbPass
)

$willImportExistingDb = $UI.PromptForYesOrNo(
	"Do you want to import an existing database from a SQL file?",
	"Yes, I want to import an existing database",
	"No, set up as a new install",
	$false
);

if (-not $willImportExistingDb) {
	$defaultAdminEmail = "leesa@doubleedesign.com.au"
	$AdminEmail = $UI.PromptForText("Enter the admin email for this site", $defaultAdminEmail)
	$global:SiteConfig.AddWordPressAdmin($AdminEmail)
}

# Output all the config values for info and debugging
$Logger.InfoMessage("`nFinal configuration:");
$Logger.DisplayJsonTable($global:SiteConfig.GetJsonString())
if ($willImportExistingDb) {
	$Logger.WarningMessage("The site name will be overridden by your database import")
}

# Now that the config is ready, we can instantiate the database handler
$DbHandler = [PowerPress.DatabaseHandler]::new($global:SiteConfig)
# ...and update the file handler
$FileHandler.SetConfig($global:SiteConfig)

# Handle database creation if required
$dbExists = $DbHandler.DbExists()
if ($DbHandler.DbExists() -eq $true) {
	$DbHandler.MaybeDropDb()
	$dropped = $DbHandler.DbExists()
	if ($dropped -eq $true) {
		$DbHandler.MaybeCreateDb()
	}
}
else {
	$DbHandler.MaybeCreateDb()
}
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Installation")
# Instantiate the classes that we need for these steps
$Canvas = [PowerPress.CanvasRepo]::new($global:SiteConfig)
$Composer = [PowerPress.ComposerHandler]::new($global:SiteConfig)
$WpHandler = [PowerPress.WordPressHandler]::new($global:SiteConfig)
# Initialise WordPress site foundation from template repo and update Composer and WordPress config
$Canvas.Init()
$Composer.Init()
$WpHandler.UpdateConfig()

# Determine whether to use composer.dev.json and local packages based on env variable set by the -Dev flag, and make the necessary updates
$willUseDevComposerJson = $env.POWERPRESS_DEV -eq "1"
if ($willUseDevComposerJson) {
	$Logger.InfoMessage("Running setup in dev mode. composer.dev.json and local copies of Double-E Design packages will be used where applicable.");

	# Prompt to confirm or change local packages directory, and then update composer.dev.json accordingly
	$LOCAL_PACKAGES_DIR = $UI.PromptForText("Enter the path to your local packages directory for Comet Components, Double-E Base Plugin, etc.", "C:\Users\$username\PhpStormProjects")
	$Composer.UpdateDevRepositories($LOCAL_PACKAGES_DIR)

	# Update session env variable so install/update/etc use this file instead of composer.json
	$env:COMPOSER = "composer.dev.json";
}
else {
	$Logger.InfoMessage("Running setup in standard mode. Double-E Design packages will be downloaded from their published repositories.");
}

Run-Composer-Install

# Import existing database
if ($willImportExistingDb) {
	try {
		$DbHandler.MaybeImportDb()
		$WpHandler.RunCliCommand("rewrite flush")
	}
	catch {
		$Logger.ErrorMessage("Failed to import database");
		$Logger.ErrorMessage($_);
		$Logger.InfoMessage("Proceeding as new install instead");
		Run-WordPress-Installation
	}
}
else {
	Run-WordPress-Installation
}

# TODO find-and-replace of the site URL using WP-CLI

Run-Postinstall-Cleanup
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Plugins, Themes, and Uploads")
# Add ACF Pro
$defaultAcfProPath = "C:\Users\$username\PhpStormProjects\advanced-custom-fields-pro"
$acfProPath = $UI.PromptForText("Enter the path to your local copy of the Advanced Custom Fields Pro plugin", $defaultAcfProPath)
Copy-Plugin-From-Local-Path -sourcePath $acfProPath

# Optionally import wp-content from an existing backup
$importContentChoice = $UI.PromptForYesOrNo(
	"Do you want to import plugins, themes, and uploads from an existing wp-content folder?",
	"Yes, I want to import wp-content from an existing site backup",
	"No, this is a new site install",
	$false
);
$importingWpContent = $importContentChoice -eq $true
if ($importingWpContent) {
	$pathToContent = $UI.PromptForText("Enter the full path to the wp-content folder you want to import from: ")
	$pathToContent = $pathToContent.Trim('"')
	if (-not (Test-Path $pathToContent)) {
		$Logger.WarningMessage("Path not found: $pathToContent. Skipping wp-content import.");
	}
	else {
		$pluginsToImport = Get-ChildItem -Path (Join-Path $pathToContent "plugins") -Directory
		foreach ($plugin in $pluginsToImport) {
			$sourcePath = $plugin.FullName
			Copy-Plugin-From-Local-Path -sourcePath $sourcePath
		}

		$themesToImport = Get-ChildItem -Path (Join-Path $pathToContent "themes") -Directory
		foreach ($theme in $themesToImport) {
			$sourcePath = $theme.FullName
			Copy-Theme-From-Local-Path -sourcePath $sourcePath
		}

		# Copy uploads folder if it exists in the backup
		$sourceUploadsPath = Join-Path $pathToContent "uploads"
		Copy-Uploads-Directory-From-Local-Path -sourcePath $sourceUploadsPath
	}
}

# If the plugins directory now contains certain things, remove things that are not compatible or duplicate the functionality
$folderMap = @{
	"classic-editor" = @("comet-plugin-blocks", "comet-calendar")
	"gravity-forms" = @("ninja-forms", "doublee-ninja-markup")
	"autodescription" = @("yoast-seo")
	"simply-disable-comments" = @("disable-comments")
}
foreach ($triggerFolder in $folderMap.Keys) {
	foreach ($conflictingFolder in $folderMap[$triggerFolder]) {
		$WpHandler.MaybeRemovePlugin($triggerFolder, $conflictingFolder);
	}
}

# If a theme was not imported, create a new child theme
if (-not $importingWpContent) {
	Create-And-Activate-Child-Theme
}

# Activate plugins in the appropriate order (accounting for dependencies some of them have)
Set-Location $global:SiteConfig.WpDir
$plugins = @(
	"advanced-custom-fields-pro",
	"doublee-local-dev",
	"doublee-base-plugin",
	"doublee-tinymce",
	"acf-advanced-image-field",
	"ninja-forms",
	"doublee-ninja-markup",
	"autodescription",
	"simply-disable-comments",
	"doublee-breadcrumbs"
	"comet-plugin-blocks",
	"comet-calendar"
)
$pluginsToComposerUpdate = @(
	"doublee-base-plugin",
	"doublee-tinymce",
	"doublee-breadcrumbs",
	"comet-plugin-blocks"
)

# Make sure certain plugins have their correct Composer deps (there can be discrepancies if dev ones were left behind and committed accidentally)
foreach ($plugin in $pluginsToComposerUpdate) {
	$pluginPath = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$plugin"
	if (Test-Path $pluginPath) {
		$Logger.InfoMessage("Installing Composer dependencies for $plugin");
		Set-Location $pluginPath
		if (-not $willUseDevComposerJson) {
			Run-Composer-Command-With-Custom-Output-Handling -command "install --no-dev --prefer-dist --no-cache"
		}
		else {
			Run-Composer-Command-With-Custom-Output-Handling -command "install --no-cache"
		}

		Set-Location $global:SiteConfig.WpDir
	}
}

$Logger.InfoMessage("Activating plugins");
foreach ($plugin in $plugins) {
	$pluginPath = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$plugin"
	if (Test-Path $pluginPath) {
		$WpHandler.RunCliCommand("plugin activate $plugin")
	}
	else {
		$Logger.WarningMessage("Plugin $plugin not found in expected path $pluginPath. Skipping activation.");
	}
}

$defaultAcfProKey = [Environment]::GetEnvironmentVariable("ACF_PRO_KEY", "User")
if ( [string]::IsNullOrEmpty($defaultAcfProKey)) {
	$acfProKey = $UI.PromptForText("Enter your ACF Pro Developer licence key")
}
else {
	$acfProKey = $defaultAcfProKey
	$Logger.InfoMessage("Using ACF Pro licence key from user environment variables: $defaultAcfProKey");
	$Logger.InfoMessage("This is assumed to be a key for a lifetime developer licence. If it isn't, just go and resave it in the admin after setup is complete.");
}
$tomorrow = [DateTimeOffset]::new((Get-Date).AddDays(1).ToUniversalTime()).ToUnixTimeSeconds()
$WpHandler.RunCliCommand("option update acf_pro_license '$acfProKey'")
# FIXME these are erroring
$WpHandler.RunCliCommand("option patch insert acf_pro_license_status status 'active'")
$WpHandler.RunCliCommand("option patch insert acf_pro_license_status lifetime 1")
$WpHandler.RunCliCommand("option patch insert acf_pro_license_status refunded 0")
$WpHandler.RunCliCommand("option patch insert acf_pro_license_status name 'Developer'")
$WpHandler.RunCliCommand("option patch insert acf_pro_license_status next_check $tomorrow")

# Check if Ninja Forms is active
wp plugin is-active ninja-forms
$ninjaFormsActive = $LASTEXITCODE -eq 0
if ($ninjaFormsActive) {
	$Logger.InfoMessage("Setting default Ninja Forms settings");
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings date_format 'd/m/Y'")
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings currency 'AUD'")
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings show_welcome 0") # FIXME this one isn't working
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings disable_admin_notices 1")
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings builder_dev_mode 1")
	$WpHandler.RunCliCommand("option patch insert ninja_forms_settings opinionated_styles ''")
}
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("IDE and Deployment")
$PhpStormConfigHandler = [PowerPress.PhpStormHandler]::new($global:SiteConfig)
$PhpStormConfigHandler.UpdateWorkspaceConfig()
$PhpStormConfigHandler.UpdateDeploymentConfig()
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Documentation")
$FileHandler.UpdateProjectReadme()
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Local web server")
$Logger.InfoMessage("Registering site in Laravel Herd");
Set-Location $global:SiteConfig.WpDir
$SiteSlug = $global:SiteConfig.SiteSlug
herd link $SiteSlug
herd secure $SiteSlug
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
$Logger.DisplaySectionHeader("Version control")
Set-Location $SiteDir
git init
git add .
git commit -m "Set up site from WordPress Canvas template using PowerPress"
$Logger.DisplaySectionFooter()


# =============================================================================================================== #
Write-Host "`n ============================ Setup Complete ================================" -ForegroundColor Green
$siteUrl = $global:SiteConfig.SiteUrl
Write-Host "Admin URL: $siteUrl/wp-admin" -ForegroundColor Cyan
$credentialsSaved = $False
if (-not $willImportExistingDb -and $useBitwarden) {
	$credentialsSaved = $CredsHandler.MaybeSaveCredentials($global:SiteConfig:SiteName,$global:SiteConfig:SiteUrl,$global:SiteConfig.AdminUser,$global:SiteConfig.AdminPassword);
}

Write-Host "You might still need to:" -ForegroundColor Cyan
Write-Host "`t - Update README.md with project-specific details" -ForegroundColor Cyan
Write-Host "`t - Enter your FTP username and password in PhpStorm's deployment settings" -ForegroundColor Cyan
if (-not $willImportExistingDb -and -not $credentialsSaved) {
	$adminUser = $global:SiteConfig.AdminUser
	$adminPassword = $global:SiteConfig.AdminPassword
	Write-Host "`t - Save your admin username and password ( $adminUser | $adminPassword ) to your password manager or another safe location" -ForegroundColor Cyan
}
if (-not $importingWpContent) {
	Write-Host "`t - Create a project plugin for custom post types, taxonomies, and other functionality" -ForegroundColor Cyan
	Write-Host "`t   Template: https://github.com/doubleedesign/wp-plugin-template" -ForegroundColor Cyan
}
else {
	Write-Host "`t - Add imported plugins to composer.json" -ForegroundColor Cyan
	Write-Host "`t 	 Tip: WP.org repo plugins can be installed from https://wpackagist.org/" -ForegroundColor Cyan
	# TODO: Only show this if the imported wp-content did have Yoast
	Write-Host "`t - Migrate Yoast SEO data to The SEO Framework" -ForegroundColor Cyan
}
Write-Host "`t - Set up your SEO Framework configuration" -ForegroundColor Cyan
Write-Host "`t - Double-check your permalink settings" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Green

$siteDir = $global:SiteConfig.SiteDir
Set-Location $siteDir
try {
	$Logger.InfoMessage("Launching WP Admin in browser");
	Start-Process "$siteUrl/wp-admin"
}
catch {
	$Logger.WarningMessage("Could not automatically open the admin URL in your browser. `nTry clicking the link above or copying and pasting it into your browser instead.");
}

try {
	$phpStormPath = "C:\Users\$username\AppData\Local\Programs\PhpStorm\bin\phpstorm64.exe"
	Start-Process $phpStormPath $global:SiteConfig.SiteDir
}
catch {
	$Logger.WarningMessage("Could not automatically open the project in PhpStorm :(");
}

# Cleanup
$CredsHandler.MaybeLogOut()
Set-Location $scriptLocation