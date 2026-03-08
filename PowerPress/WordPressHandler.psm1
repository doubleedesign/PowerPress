function Update-WpConfig {
	$wpConfigPath = Join-Path $global:SiteConfig.WpDir "wp-config.php"

	if (-not (Test-Path $wpConfigPath)) {
		ErrorMessage "wp-config.php not found. Expected path: $wpConfigPath"
		exit 1
	}
	
	InfoMessage "Updating wp-config.php: $wpConfigPath"
	$content = Get-Content $wpConfigPath
	$dbName = $global:SiteConfig.DbName

	# Update the database name to match the site name
	$content = $content -replace "define\('DB_NAME', '.*?'\);", "define('DB_NAME', '$dbName');"

	# Save the updated content back to the file
	Set-Content -Path $wpConfigPath -Value $content
}

function Run-WordPress-Installation {
	$wpDir = $global:SiteConfig.WpDir
	Set-Location $wpDir
	
	DebugMessage "Working from $(Get-Location)"
	InfoMessage "Installing WordPress database tables"
	
	if (-not (Test-Path "wp-config.php")) {
		ErrorMessage "wp-config.php not found in $wpDir. Cannot proceed with WordPress installation."
		exit 1
	}
	
	try {
		$siteUrl = "$($global:SiteConfig.SiteUrl)"
		$siteName = "$($global:SiteConfig.SiteName)"
		$adminUser = $global:SiteConfig.AdminUser
		$adminPassword = "$($global:SiteConfig.AdminPassword)" 
		$adminEmail = "$($global:SiteConfig.AdminEmail)"
		$command = @(
			"core",
			"install",
			"--url=$siteUrl",
			"--title=$siteName",
			"--admin_user=$adminUser",
			"--admin_email=$adminEmail",
			"--admin_password=$adminPassword"
		)

		Run-Wp-Cli-Command-With-Custom-Output -command $command

		# Set default permalink structure
		# This is not only convenient, but required for the REST API to work for automated tests out of the box
		Run-Wp-Cli-Command-With-Custom-Output -command "rewrite structure '/%postname%/'"

		# When running WP-CLI using Git for Windows's shell interpreter, it causes the rewrite to have /C:/Program%20Files/Git/ in it
		$permalinkSetting = Run-Wp-Cli-Command-With-Custom-Output -command "option get permalink_structure"
		if ($permalinkSetting -ne "/%postname%/") {
			WarningMessage "Permalink structure is not set correctly, attempting to set it another way"
			Run-Wp-Cli-Command-With-Custom-Output -command "option update permalink_structure '/%postname%/'"
			Write-Host "Permalink structure is now: " -NoNewline -ForegroundColor Blue
			Run-Wp-Cli-Command-With-Custom-Output -command "option get permalink_structure"
			Write-Host "`n"
		}

		# Flush rewrite rules
		Run-Wp-Cli-Command-With-Custom-Output -command "rewrite flush"
	}
	catch {
		ErrorMessage "Error installing WordPress"
		ErrorMessage $_
	}
}

function Run-Postinstall-Cleanup {
	# Go into the themes directory and delete default themes (anything starting with twenty*)
	$themesDir = Join-Path $global:SiteConfig.WpDir "wp-content\themes"
	Get-ChildItem -Path $themesDir -Directory -Filter "twenty*" | ForEach-Object {
		Remove-With-Wait -path $_.FullName
	}

	# Go into the plugins directory and delete Akismet and Hello Dolly (if they exist)
	$pluginsDir = Join-Path $global:SiteConfig.WpDir "wp-content\plugins"
	$defaultPlugins = @("akismet", "hello.php")
	foreach ($plugin in $defaultPlugins) {
		$pluginPath = Join-Path $pluginsDir $plugin
		Remove-With-Wait -path $pluginPath
	}
}

function Copy-Plugin-From-Local-Path {
	param (
		[string]$sourcePath
	)
	
	if (-not (Test-Path $sourcePath)) {
		WarningMessage "Source plugin path not found: $sourcePath, skipping"
	}
	
	$destPath = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$(Split-Path $sourcePath -Leaf)"
	if (Test-Path $destPath) {
		InfoMessage "Plugin already exists in destination $destPath, skipping copy"
		return
	}
	
	try {
		robocopy $sourcePath $destPath /E /Z /NFL /NDL /NJH /nc /ns /np | Out-Null
		if (Test-Path $destPath) {
			SuccessMessage "Copied plugin from $sourcePath `n   to $destPath"
		}
	}
	catch {
		ErrorMessage "Failed to copy plugin: $_"
	}
}

function Copy-Theme-From-Local-Path {
	param (
		[string]$sourcePath
	)
	
	if (-not (Test-Path $sourcePath)) {
		WarningMessage "Source theme path not found: $sourcePath, skipping"
	}
	
	$destPath = Join-Path $global:SiteConfig.WpDir "wp-content\themes\$(Split-Path $sourcePath -Leaf)"
	if (Test-Path $destPath) {
		InfoMessage "Theme already exists in destination $destPath, skipping copy"
		return
	}
	
	try {
		robocopy $sourcePath $destPath /E /Z /NFL /NDL /NJH /nc /ns /np | Out-Null
		if (Test-Path $destPath) {
			SuccessMessage "Copied theme from $sourcePath to $destPath"
		}
	}
	catch {
		ErrorMessage "Failed to copy theme: $_"
	}
}

function Copy-Uploads-Directory-From-Local-Path {
	param (
		[string]$sourcePath
	)
	
	if (-not (Test-Path $sourcePath)) {
		WarningMessage "Source uploads path not found: $sourcePath, skipping"
	}
	
	$destPath = Join-Path $global:SiteConfig.WpDir "wp-content\uploads"
	try {
		robocopy $sourcePath $destPath /E /Z /NFL /NDL /NJH /nc /ns /np | Out-Null
		if (Test-Path $destPath) {
			SuccessMessage "Copied uploads from $sourcePath to $destPath"
		}
	}
	catch {
		ErrorMessage "Failed to copy uploads: $_"
	}
}

function Create-And-Activate-Child-Theme {
	$wpDir = $global:SiteConfig.WpDir
	Set-Location $wpDir
	
	$defaultAuthorName = "Double-E Design"
	$defaultAuthorUri = "https://www.doubleedesign.com.au"
	$authorName = Prompt-For-Text -Message "Enter the author name for the child theme" -DefaultValue $defaultAuthorName
	$authorUri = Prompt-For-Text -Message "Enter the author URI for the child theme" -DefaultValue $defaultAuthorUri
	$siteName = $global:SiteConfig.SiteName
	$themeDirectoryName = $global:SiteConfig.SiteSlug
	$themeUri = $global:SiteConfig.ProductionUrl
	
	InfoMessage "Child theme configuration:"
	$themeConfig = @{
		"Name" = $siteName
		"Template" = "comet-canvas-blocks"
		"Author" = $authorName
		"Author URI" = $authorUri
		"Theme URI" = $themeUri
	}
	$themeConfigJson = $themeConfig | ConvertTo-Json -Depth 3
	Display-Json-Table -JsonString $themeConfigJson
	
	try {
		$command = @(
			"scaffold",
			"child-theme",
			$themeDirectoryName,
			"--theme_name=$siteName",
			"--parent_theme=comet-canvas-blocks",
			"--author=$authorName",
			"--author_uri=$authorUri",
			"--theme_uri=$themeUri",
			"--activate"
		)

		Run-Wp-Cli-Command-With-Custom-Output -command $command
	}
	catch {
		ErrorMessage "Failed to create and/or activate child theme"
		ErrorMessage $_
	}
}

function Run-Wp-Cli-Command-With-Custom-Output {
	param (
		[object]$command # expected to be a string or string[]
	)

	$wpDir = $global:SiteConfig.WpDir
	Set-Location $wpDir

	# Normalize to array and add flags we always want to use
	if ($command -is [string]) { $command = $command -split '\s+' }
	$command += @("--skip-plugins", "--skip-themes")

	DebugMessage -Message "Running WP-CLI command: wp $command"
	
	try {
		& wp @command 2>&1 | ForEach-Object {
			$output = "$_"
			# Using the WP-CLI --color flag doesn't colour the full message, and does warnings as cyan,
			# so let's pipe the output through our our own function and adjust the messages
			# (also I just don't really want the success: warning: etc prefixes, or activation double-ups, etc)
			if ($output -eq "Success: Activated 1 of 1 plugins.") {
				# do nothing, each individual plugin gets its own so I don't want "1 of 1" success message directly after it
			}
			elseif ($output -match "^Success") {
				SuccessMessage $output.Replace("Success: ", "")
			}
			elseif ($output -match "^Plugin '.*' activated.$") {
				SuccessMessage $output
			}
			elseif ($output -match "^Error|^Fatal") {
				ErrorMessage $output.Replace("Error: ", "")
			} 
			elseif ($output -match "^Warning") {
				WarningMessage $output.Replace("Warning: ", "")
			} 
			elseif ($output -match "^Comet Components core config:") {
				# Suppress
			}
			elseif ($output -match "^PHP Notice:  Function add_theme_support( 'title-tag' ) was called <strong>incorrectly</strong>") {
				# Suppress
			}
			else {
				Write-Host $output
			}
		}
	}
	catch {
		ErrorMessage "WP-CLI command failed: wp $command"
		ErrorMessage $_
	}
}

Export-ModuleMember -Function Update-WpConfig, Run-WordPress-Installation, Run-Postinstall-Cleanup, Copy-Plugin-From-Local-Path, Copy-Theme-From-Local-Path, Copy-Uploads-Directory-From-Local-Path, Create-And-Activate-Child-Theme, Run-Wp-Cli-Command-With-Custom-Output