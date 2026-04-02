function Import-Classes {
	$projectFile = Join-Path $PSScriptRoot "PowerPress.csproj"

	try {
		dotnet build $projectFile
		if ($LASTEXITCODE -ne 0) {
			throw "dotnet build failed with exit code $LASTEXITCODE"
		}

		$dll = Get-ChildItem -Path (Join-Path $PSScriptRoot "bin\Debug") -Recurse -Filter "PowerPress.dll" | Select-Object -First 1
		if ($null -eq $dll) {
			throw "Could not find PowerPress.dll after build"
		}

		Add-Type -Path $dll.FullName
	}
	catch {
		Write-Host "✖  $( $_.Exception.Message )" -ForegroundColor Red
		if ($_.Exception.InnerException) {
			Write-Host "   $( $_.Exception.InnerException.Message )" -ForegroundColor Red
		}
		exit 1
	}
}

Export-ModuleMember -Function Import-Classes