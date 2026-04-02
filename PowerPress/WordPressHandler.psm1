# TODO: This is here as a reference for possible updates to the C# version 
# Delete when that is finalised
function Run-Wp-Cli-Command-With-Custom-Output {
	param (
		[object]$command # expected to be a string or string[]
	)

	$wpDir = $SiteConfig.WpDir
	Set-Location $wpDir

	# Normalize to array and add flags we always want to use
	if ($command -is [string]) {
		$command = $command -split '\s+'
	}
	$command += @("--skip-plugins", "--skip-themes")

	$Logger.DebugMessage("Running WP-CLI command: wp $command");

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
				$Logger.SuccessMessage( $output.Replace("Success: ", ""));
			}
			elseif ($output -match "^Plugin '.*' activated.$") {
				$Logger.SuccessMessage($output);
			}
			elseif ($output -match "^Error|^Fatal") {
				$Logger.ErrorMessage( $output.Replace("Error: ", ""));
			}
			elseif ($output -match "^Warning") {
				$Logger.WarningMessage( $output.Replace("Warning: ", ""));
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
		$Logger.ErrorMessage("WP-CLI command failed: wp $command");
		$Logger.ErrorMessage($_);
	}
}