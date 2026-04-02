function Maybe-Remove-Plugin {
	param (
		[string]$ifInstalled,
		[string]$thenRemove
	)

	$pluginPath1 = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$ifInstalled"
	$pluginPath2 = Join-Path $global:SiteConfig.WpDir "wp-content\plugins\$thenRemove"
	if ((Test-Path $pluginPath1) -and (Test-Path $pluginPath2)) {
		$FileHandler.MaybeDeleteFolder($pluginPath2)
		$FileHandler.RemoveDependency($thenRemove)
	}
}

Export-ModuleMember -Function  Maybe-Remove-Plugin