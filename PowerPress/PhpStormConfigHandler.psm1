function Maybe-Update-PhpStorm-Deployment-Config {
	# Find deployment.xml in site directory -> .idea
	$deploymentXmlPath = Join-Path $global:SiteConfig.SiteDir ".idea\deployment.xml"
	if (-not (Test-Path $deploymentXmlPath)) {
		$Logger.WarningMessage("deployment.xml not found at $deploymentXmlPath, skipping update");
		return
	}

	# And webServers.xml
	$webServersXmlPath = Join-Path $global:SiteConfig.SiteDir ".idea\webServers.xml"
	if (-not (Test-Path $webServersXmlPath)) {
		$Logger.WarningMessage("webServers.xml not found at $webServersXmlPath, skipping update");
		return
	}

	$serverIp = Prompt-For-Text "Enter the IP address or hostname of your production server"
	if ( [string]::IsNullOrEmpty($serverIp)) {
		$Logger.WarningMessage("No server IP entered for PhpStorm deployment config");
		return
	}

	# Find YOUR_SERVER_IP in the files and replace it with the provided IP
	try {
		(Get-Content $deploymentXmlPath) -replace "YOUR_SERVER_IP", $serverIp | Set-Content $deploymentXmlPath
		(Get-Content $webServersXmlPath) -replace "YOUR_SERVER_IP", $serverIp | Set-Content $webServersXmlPath
		$Logger.SuccessMessage("Updated PhpStorm deployment config with server IP $serverIp");
	}
	catch {
		$Logger.ErrorMessage("Failed to update PhpStorm deployment config");
		$Logger.ErrorMessage($_);
	}

	# Find SOME_UNIQUE_ID and replace it with a random string
	$randomId = [Guid]::NewGuid().ToString()
	try {
		(Get-Content $webServersXmlPath) -replace "SOME_UNIQUE_ID", $randomId | Set-Content $webServersXmlPath
		$Logger.SuccessMessage("Updated PhpStorm deployment config with unique ID $randomId");
	}
	catch {
		$Logger.ErrorMessage("Failed to update PhpStorm deployment config with unique ID");
		$Logger.ErrorMessage($_);
	}

	# Replace https://your-production-url in the files with the production URL
	$productionUrl = $global:SiteConfig:ProductionUrl
	if ( [string]::IsNullOrEmpty($productionUrl)) {
		$Logger.WarningMessage("No production URL entered for PhpStorm deployment config");
		return
	}
	try {
		(Get-Content $webServersXmlPath) -replace "https://your-production-url", "https://$productionUrl" | Set-Content $webServersXmlPath
		$Logger.SuccessMessage("Updated PhpStorm deployment config with production URL https://$productionUrl");
	}
	catch {
		$Logger.ErrorMessage("Failed to update PhpStorm deployment config with production URL");
		$Logger.ErrorMessage($_);
	}
}

function Update-PhpStorm-Workspace-Config {
	# Find workspace.xml in site directory -> .idea
	$workspaceXmlPath = Join-Path $global:SiteConfig.SiteDir ".idea\workspace.xml"
	if (-not (Test-Path $workspaceXmlPath)) {
		$Logger.WarningMessage("workspace.xml not found at $workspaceXmlPath, skipping update");
		return
	}

	# Replace all instances of C:/Users/leesa/PhpStormProjects/wordpress-canvas with the site directory
	try {
		$canonicalPath1 = "C:/Users/leesa/PhpStormProjects/wordpress-canvas"
		$canonicalPath2 = "C:\Users\leesa\PhpStormProjects\wordpress-canvas"

		$formatted1 = $global:SiteConfig.SiteDir -replace "\\", "/"
		$formatted2 = $global:SiteConfig.SiteDir -replace "/", "\"

		(Get-Content $workspaceXmlPath) `
			             -replace [regex]::Escape($canonicalPath1), $formatted1 `
    		             -replace [regex]::Escape($canonicalPath2), $formatted2 | Set-Content $workspaceXmlPath
	}
	catch {
		$Logger.ErrorMessage("Failed to update PhpStorm workspace config");
		$Logger.ErrorMessage($_);
	}

	# Check that the file does not contain wordpress-canvas anymore
	try {
		$content = Get-Content $workspaceXmlPath
		if ($content -match "wordpress-canvas") {
			$Logger.WarningMessage( ".idea/workspace.xml still contains references to wordpress-canvas, please check the file manually"
		}
		else {
			$Logger.SuccessMessage("PhpStorm workspace config updated successfully");
		}
	}
	catch {
		$Logger.ErrorMessage("Failed to verify PhpStorm workspace config");
		$Logger.ErrorMessage($_);
	}
}

Export-ModuleMember -Function Maybe-Update-PhpStorm-Deployment-Config, Update-PhpStorm-Workspace-Config