function Test-Dotnet-Prerequisites {
	$dotnet = Get-Command dotnet | Select-Object -ExpandProperty Version
	$source = Get-Command dotnet | Select-Object -ExpandProperty Source
	$versionNumber = [int]$dotnet.Major
	if ($versionNumber -lt 10) {
		Write-Host "`n✖  PowerPress requires .NET 10 or higher." -ForegroundColor Red
		Write-Host "   Download installer from: https://dotnet.microsoft.com/en-us/download/dotnet"
		Write-Host "   Or run: choco install dotnet"
		exit 1
	}
	else {
		Write-Host "✔  .NET version is $dotnet [$source]" -ForegroundColor Green
	}

	$sdk = dotnet --list-sdks | Select-Object -First 1
	if ($sdk -match "10\.\d+\.\d+") {
		Write-Host "✔  .NET SDK version is $sdk" -ForegroundColor Green
	}
	else {
		Write-Host "`n✖  .NET SDK 10 is required to build and run PowerPress." -ForegroundColor Red
		Write-Host "   Download installer from: https://dotnet.microsoft.com/en-us/download/dotnet" -ForegroundColor Red
		Write-Host "   Or run: choco install dotnet-sdk" -ForegroundColor Red
		exit 1
	}
}

Export-ModuleMember -Function Test-Dotnet-Prerequisites