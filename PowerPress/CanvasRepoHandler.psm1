function Initialise-From-Template-Repo {
	# Check that directory exists
	if (-not (Test-Path $global:SiteConfig.SiteDir)) {
		ErrorMessage "Site directory does not exist: $($global:SiteConfig.SiteDir)"
		exit 1
	}

	# Move to it
	Set-Location $global:SiteConfig.SiteDir
	$location = Get-Location
	DebugMessage "Working from $location"

	# Clone template repo into the site directory (Note: the dot clones the contents directly in, so we don't get a wordpress-canvas folder inside the project folder)
	InfoMessage "Cloning template repository from GitHub"
	git clone https://github.com/doubleedesign/wordpress-canvas .
	
	# Confirm successful clone
	if (Test-Path (Join-Path $global:SiteConfig.SiteDir ".git")) {
		SuccessMessage "Successfully cloned template repository into site directory"
	}
	else {
		ErrorMessage "Failed to clone template repository into site directory"
		exit 1
	}

	# Delete template repo's git directory and some other files we don't need or are going to refresh anyway
	$toDelete = @(".git", "sql", "composer.lock", "composer.dev.lock", "app/wp-content/uploads")
	foreach ($item in $toDelete) {
		$path = Join-Path $global:SiteConfig.SiteDir $item
		Remove-With-Wait -path $path
	}
}

function Maybe-Remove-Plugin {
	param (
		[string]$ifInstalled,
		[string]$thenRemove
	)
	
	$pluginPath1 = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$ifInstalled"
	$pluginPath2 = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$thenRemove"
	if ((Test-Path $pluginPath1) -and (Test-Path $pluginPath2)) {
		Remove-With-Wait -path $pluginPath2
		Remove-Dep-From-ComposerJson -composerJsonPath (Join-Path $global:SiteConfig.WpDir "composer.json") -packageName $thenRemove
		Remove-Dep-From-ComposerJson -composerJsonPath (Join-Path $global:SiteConfig.WpDir "composer.dev.json") -packageName $thenRemove
	}
}

Export-ModuleMember -Function Initialise-From-Template-Repo, Maybe-Remove-Plugin