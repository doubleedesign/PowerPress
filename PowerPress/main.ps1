param(
	[string]$SiteName,
	[switch]$Debug,
	[switch]$Dev,
	[switch]$Help
)

# Initial module imports that don't rely on config being set up yet and need to be used before that
# Note: -Force is just to ensure the latest is loaded, so if the script is re-run in the same PowerShell session during development it picks up changes
Import-Module $PSScriptRoot\Console.psm1 -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\Dependencies.psm1 -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\FileHandler.psm1 -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\CredentialHandler.psm1  -WarningAction SilentlyContinue -Force

# Make sure site name was provided and show help if not
if ($Help -or [string]::IsNullOrEmpty($SiteName)) {
	Display-Section-Header -Title "PowerPress Help"
	Write-Host "Usage:" -ForegroundColor Cyan
	Write-Host "`tpowerpress site-name" -ForegroundColor Gray
	Write-Host "`nArguments:" -ForegroundColor Cyan
	Write-Host "`tsite-name `t The kebab-case name of your site." -ForegroundColor Gray
	Write-Host "`nOptions:" -ForegroundColor Cyan
	Write-Host "`t-Debug `t`t PowerPress debug mode. Additional debug information will be displayed during setup." -ForegroundColor Gray
	Write-Host "`t-Dev `t`t Double-E Design developer mode. PowerPress will use composer.dev.json from the Canvas repo and `n`t`t`t Composer will symlink local copies of Double-E Design packages instead of downloading them from Packagist or GitHub. `n`t`t`t Intended for for developing and testing changes to those packages." -ForegroundColor Gray
	Write-Host ""
	Display-Section-Footer
	exit 0
}

# If the -Debug flag is set, set the environment variable
if ($Debug) {
	$env:POWERPRESS_DEBUG = "1"
	SuccessMessage "PowerPress Debug mode enabled"
}

# If the -Dev flag is set, set the environment variable
if ($Dev) {
	$env:POWERPRESS_DEV = "1"
	SuccessMessage "Dev mode enabled for site setup"
}

Display-Section-Header -Title "Welcome to PowerPress"
Write-Host "PowerPress is a PowerShell utility to set up Composer-managed WordPress sites `nfor local development on Windows machines." -ForegroundColor Gray
Write-Host "`nOut of the box, it can be used for new sites or for updating existing sites to `nuse Double-E Design's current and tooling and base suite of plugins." -ForegroundColor Gray
Write-Host "`nPowerPress is open source. You can find the latest documentation, report issues, `ncontribute fixes and improvements, and/or fork the repo to modify it to suit your needs." -ForegroundColor Gray
Write-Host "Repo URL: https://github.com/doubleedesign/powerpress" -ForegroundColor Gray
Write-Host "`nPress 'Enter' to start." -ForegroundColor Cyan
do {
	$key = [Console]::ReadKey("noecho")
} while($key.Key -ne "Enter")
Display-Section-Footer

# Check dependencies and PHP extensions before doing anything else
Display-Section-Header -Title "Prerequisites"
Check-Permissions
Check-Dependencies
Check-Php-Extensions

$env:PHP_CLI_OPTS = "-d display_errors=0 -d error_reporting=0"
WarningMessage "PHP errors and warnings have been suppressed in the PHP_CLI_OPTS environment variable."
WarningMessage "Individual commands may not be affected by this due to how they work internally."

# Load C# classes if they haven't already been loaded in this session
$classes = @('LocalSiteConfig')
foreach ($class in $classes) {
	if (-not ([System.Management.Automation.PSTypeName]$class).Type) {
		try {
			Add-Type -Path (Join-Path $PSScriptRoot ".\$class.cs")
			SuccessMessage "Loaded class: $class"
		}
		catch {
			ErrorMessage "Failed to load class: $_"
			exit 1
		}
	}
}
# BitWarden CLI is treated a little differently than other dependencies because we need to check env variables as well as the command,
# and it makes sense to log in at the same time to avoid checking "can access Bitwarden" multiple times
$useBitwarden = Maybe-Log-Into-Bitwarden
Display-Section-Footer

# Store location the script was called from
$scriptLocation = Get-Location

# Get basic config to enable setup
Display-Section-Header -Title "Base config"
$username = $env:USERNAME
$defaultProjectsDir = "C:\Users\$username\ClientSites"
$PROJECTS_DIR = Prompt-For-Text -Message "Enter the path to your website projects directory" -DefaultValue $defaultProjectsDir
if ([string]::IsNullOrEmpty($PROJECTS_DIR)) {
	$PROJECTS_DIR = $defaultProjectsDir
}
InfoMessage "Using projects directory: $PROJECTS_DIR"
$SiteDir = Join-Path $PROJECTS_DIR $SiteName
InfoMessage "Site directory will be: $SiteDir"

# If directory exists, prompt to delete or exit
$continue = Maybe-Delete-Folder -folderPath $SiteDir -promptMessage "Directory $SiteDir already exists. Do you want to delete it and start fresh?"
if (-not $continue) {
	WarningMessage "Cannot initialise new site because there is an existing directory at $SiteDir."
	WarningMessage "Exiting script."
	exit 0
}
$continue = Create-Folder -folderPath $SiteDir
if (-not $continue) {
	ErrorMessage "Failed to create site directory. Exiting script."
	exit 1
}

$ProductionUrl = Prompt-For-Text -Message "Enter the production URL for this site (without https://)"
if([string]::IsNullOrEmpty($productionUrl)) {
	$ProductionUrl = ""
}
Display-Section-Footer

Display-Section-Header -Title "Database"
$dbHost = Prompt-For-Text -Message "Enter local database hostname" -DefaultValue "127.0.0.1"
$dbPort = Prompt-For-Text -Message "Enter local database server port" -DefaultValue "3309"
$dbUser = Prompt-For-Text -Message "Enter local database username" -DefaultValue "root"
$dbPass = Prompt-For-Text -Message "Enter local database password" -DefaultValue ""

$importDbChoice = Prompt-For-YesOrNo `
    -Message "Do you want to import an existing database from a .sql file?" `
    -YesOption "Yes, I want to import from an existing site" `
    -NoOption "No, this is a new site install" `
    -DefaultYes $false
$willImportExistingDb = $importDbChoice -eq $true
if($willImportExistingDb) {
	SuccessMessage "You will be prompted for the path to your SQL file later in the script"
}
else {
	SuccessMessage "A new database will be created"
}

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

if(-not $willImportExistingDb) {
	$defaultAdminEmail = "leesa@doubleedesign.com.au"
	$AdminEmail = Prompt-For-Text "Enter the admin email for this site" -DefaultValue $defaultAdminEmail
	$global:SiteConfig.AddWordPressAdmin($AdminEmail);
}

# Output all the config values for info and debugging
InfoMessage "`nFinal configuration:"
Display-Json-Table -JsonString $global:SiteConfig.GetJsonString()
if($willImportExistingDb) {
	WarningMessage "The site name will be overridden by your database import"
}

# Import the modules that will use the config, 
# with -Force to ensure changes are reflected if re-running in the same PowerShell session during development
Import-Module $PSScriptRoot\DatabaseHandler.psm1  -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\ComposerHandler.psm1  -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\CanvasRepoHandler.psm1  -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\WordPressHandler.psm1  -WarningAction SilentlyContinue -Force
Import-Module $PSScriptRoot\PhpStormConfigHandler.psm1  -WarningAction SilentlyContinue -Force

# Handle database creation if required
# Note: We cannot run Maybe-Import-Database yet because that uses some wp-cli commands which are not available until after WP core is added.
# I just like to run these fairly early so we catch MySQL errors before downloading any code.
Maybe-Drop-Existing-Database
Create-Database-If-Not-Exists
Display-Section-Footer

Display-Section-Header -Title "Installation"
# Initialise WordPress site foundation from template repo and update Composer and WordPress config
Initialise-From-Template-Repo
Composer-Json-Initial-Update -composerJsonPath (Join-Path $global:SiteConfig.WpDir "composer.json")
Update-WpConfig

# Determine whether to use composer.dev.json and local packages based on env variable set by the -Dev flag, and make the necessary updates
$willUseDevComposerJson = $env.POWERPRESS_DEV -eq "1"
if($willUseDevComposerJson) {
	InfoMessage "Running setup in dev mode. composer.dev.json and local copies of Double-E Design packages will be used where applicable."
	Composer-Json-Initial-Update -composerJsonPath (Join-Path $global:SiteConfig.WpDir "composer.dev.json")
	$pathToLocalPackages = "C:\Users\$username\PhpStormProjects"
	$LOCAL_PACKAGES_DIR = Prompt-For-Text -Message "Enter the path to your local packages directory for Comet Components, Double-E Base Plugin, etc." -DefaultValue $pathToLocalPackages
	if ([string]::IsNullOrEmpty($LOCAL_PACKAGES_DIR)) {
		$LOCAL_PACKAGES_DIR = $pathToLocalPackages
	}
	InfoMessage "Using local packages directory: $LOCAL_PACKAGES_DIR"
	Composer-Json-Repositories-Update -composerJsonPath (Join-Path $global:SiteConfig.WpDir "composer.dev.json") -pathToLocalPackages $LOCAL_PACKAGES_DIR
	$env:COMPOSER = "composer.dev.json";
}
else {
	InfoMessage "Running setup in standard mode. Double-E Design packages will be downloaded from their published repositories."
	$composerJsonDevPath = Join-Path $global:SiteConfig.SiteDir "composer.dev.json"
	Remove-Item -Path $composerJsonDevPath -Force | Out-Null
}

Run-Composer-Install

# Import existing database
if($willImportExistingDb) {
	try {
		Maybe-Import-Database
		wp rewrite flush
	}
	catch {
		ErrorMessage "Failed to import database"
		ErrorMessage $_
		InfoMessage "Proceeding as new install instead"
		Run-WordPress-Installation
	}
}
else {
	Run-WordPress-Installation
}

Run-Postinstall-Cleanup
Display-Section-Footer

Display-Section-Header -Title "Plugins, Themes, and Uploads"
# Add ACF Pro
$defaultAcfProPath = "C:\Users\$username\PhpStormProjects\advanced-custom-fields-pro"
$acfProPath = Prompt-For-Text -Message "`Enter the path to your local copy of the Advanced Custom Fields Pro plugin" -DefaultValue $defaultAcfProPath
if ([string]::IsNullOrEmpty($acfProPath)) {
	$acfProPath = $defaultAcfProPath
}
Copy-Plugin-From-Local-Path -sourcePath $acfProPath

# Optionally import wp-content from an existing backup
$importContentChoice = `Prompt-For-YesOrNo `
	-Message "Do you want to import plugins, themes, and uploads from an existing wp-content folder?" `
	-YesOption "Yes, I want to import wp-content from an existing site backup" `
	-NoOption "No, this is a new site install" `
	-DefaultYes $false
$importingWpContent = $importContentChoice -eq $true
if ($importingWpContent) {
	$pathToContent = Prompt-For-Text -Message "Enter the full path to the wp-content folder you want to import from: "
	$pathToContent = $pathToContent.Trim('"')
	if (-not (Test-Path $pathToContent)) {
		WarningMessage "Path not found: $pathToContent. Skipping wp-content import."
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
		Maybe-Remove-Plugin -ifInstalled $triggerFolder -thenRemove $conflictingFolder
	}
}

# If a theme was not imported, create a new child theme
if(-not $importingWpContent) {
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
		InfoMessage "Installing Composer dependencies for $plugin"
		Set-Location $pluginPath
		if (-not $willUseDevComposerJson) {
			Run-Composer-Command-With-Custom-Output-Handling -command "install --no-dev --prefer-dist --no-cache"
		} else {
			Run-Composer-Command-With-Custom-Output-Handling -command "install --no-cache"
		}
	
		Set-Location $global:SiteConfig.WpDir
	}
}

InfoMessage "Activating plugins"
foreach($plugin in $plugins) {
	$pluginPath = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$plugin"
	if (Test-Path $pluginPath) {
		Run-Wp-Cli-Command-With-Custom-Output -command "plugin activate $plugin"
	}
	else {
		WarningMessage "Plugin $plugin not found in expected path $pluginPath. Skipping activation."
	}
}

$defaultAcfProKey = [Environment]::GetEnvironmentVariable("ACF_PRO_KEY", "User")
if([string]::IsNullOrEmpty($defaultAcfProKey)) {
	$acfProKey = Prompt-For-Text -Message "Enter your ACF Pro Developer licence key"
}
else {
	$acfProKey = $defaultAcfProKey
	InfoMessage "Using ACF Pro licence key from user environment variables: $defaultAcfProKey"
	InfoMessage "This is assumed to be a key for a lifetime developer licence. If it isn't, just go and resave it in the admin after setup is complete."
}
$tomorrow = [DateTimeOffset]::new((Get-Date).AddDays(1).ToUniversalTime()).ToUnixTimeSeconds()
Run-Wp-Cli-Command-With-Custom-Output -command "option update acf_pro_license '$acfProKey'"
# FIXME these are erroring
Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert acf_pro_license_status status 'active'"
Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert acf_pro_license_status lifetime 1"
Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert acf_pro_license_status refunded 0"
Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert acf_pro_license_status name 'Developer'"
Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert acf_pro_license_status next_check $tomorrow"

# Check if Ninja Forms is active
wp plugin is-active ninja-forms
$ninjaFormsActive = $LASTEXITCODE -eq 0
if ($ninjaFormsActive) {
	InfoMessage "Setting default Ninja Forms settings"
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings date_format 'd/m/Y'"
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings currency 'AUD'"
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings show_welcome 0" # FIXME this one isn't working
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings disable_admin_notices 1"
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings builder_dev_mode 1"
	Run-Wp-Cli-Command-With-Custom-Output -command "option patch insert ninja_forms_settings opinionated_styles ''"
}
Display-Section-Footer

Display-Section-Header -Title "IDE and Deployment"
Update-PhpStorm-Workspace-Config
Maybe-Update-PhpStorm-Deployment-Config
Display-Section-Footer

Display-Section-Header -Title "Documentation"
Update-Project-Readme # FIXME the find-and-replace isn't working
Display-Section-Footer

Display-Section-Header -Title "Local web server"
InfoMessage "Registering site in Laravel Herd"
Set-Location $global:SiteConfig.WpDir
$SiteSlug = $global:SiteConfig.SiteSlug
herd link $SiteSlug 
herd secure $SiteSlug
Display-Section-Footer

Display-Section-Header -Title "Version control"
Set-Location $SiteDir
git init
git add .
git commit -m "Set up site from WordPress Canvas template using PowerPress"
Display-Section-Footer

Write-Host "`n ============================ Setup Complete ================================" -ForegroundColor Green
$siteUrl = $global:SiteConfig.SiteUrl
Write-Host "Admin URL: $siteUrl/wp-admin" -ForegroundColor Cyan
$credentialsSaved = $False
if(-not $willImportExistingDb -and $useBitwarden) {
	$credentialsSaved = Maybe-Save-Credentials
}

Write-Host "You might still need to:" -ForegroundColor Cyan
Write-Host "`t - Update README.md with project-specific details" -ForegroundColor Cyan
Write-Host "`t - Enter your FTP username and password in PhpStorm's deployment settings" -ForegroundColor Cyan
if(-not $willImportExistingDb -and -not $credentialsSaved) {
	$adminUser = $global:SiteConfig.AdminUser
	$adminPassword = $global:SiteConfig.AdminPassword
	Write-Host "`t - Save your admin username and password ( $adminUser | $adminPassword ) to your password manager or another safe location" -ForegroundColor Cyan
}
if(-not $importingWpContent) {
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
	InfoMessage "Launching WP Admin in browser"
	Start-Process "$siteUrl/wp-admin"
}
catch {
	WarningMessage "Could not automatically open the admin URL in your browser. `nTry clicking the link above or copying and pasting it into your browser instead."
}

try {
	$phpStormPath = "C:\Users\$username\AppData\Local\Programs\PhpStorm\bin\phpstorm64.exe"
	Start-Process $phpStormPath $global:SiteConfig.SiteDir
}
catch {
	WarningMessage "Could not automatically open the project in PhpStorm :("
}

# Cleanup
Maybe-Log-Out-Of-Bitwarden
Set-Location $scriptLocation