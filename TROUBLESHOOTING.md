# Troubleshooting PowerPress

- [PowerShell errors](#powershell-errors)
- [WP-CLI errors](#wp-cli-errors)
- [Laravel Herd errors](#laravel-herd-errors)

---
## PowerShell errors

### "Running scripts is disabled on this system"

#### Error
> Unhandled exception. System.Management.Automation.PSSecurityException: main.ps1 cannot be loaded because running scripts is disabled on this system.

#### Solution
Open PowerShell as an administrator and update the script execution policy for the current user (yourself) to allow running scripts.

First you can check the current execution policy with:
```powershell
Get-ExecutionPolicy -Scope CurrentUser
```

Set it to `Bypass` to allow running scripts without restrictions:

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope CurrentUser
```

To disable it again when you're done, you can run the following (replacing `Restricted` with the previous value if it was different):

```powershell
Set-ExecutionPolicy -ExecutionPolicy Restricted -Scope CurrentUser
```

### Errors related to PowerShell profile

#### Errors

```
Set-PSReadLineOption: O:\OneDrive\Documents\PowerShell\Microsoft.PowerShell_profile.ps1:2
Line |
2 |  Set-PSReadLineOption -PredictionSource History
|  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
| The predictive suggestion feature cannot be enabled because the console output doesn't support virtual terminal processing or it's redirected.
```
```
Set-PSReadLineOption: O:\OneDrive\Documents\PowerShell\Microsoft.PowerShell_profile.ps1:3
Line |
3 |  Set-PSReadLineOption -PredictionViewStyle ListView
|  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
| The handle is invalid.
```

...and similar.

#### Cause 
If you get errors related to your PowerShell profile at the Composer install step, they likely relate to the Composer postinstall script from WordPress Canvas trying to run in your current context, meaning its `-NoProfile` directive doesn't work.

#### Solution
You can work around this by silencing errors in your profile like so:

```powershell
# If using oh-my-posh themes, they're fine
oh-my-posh init pwsh --config "$env:POSH_THEMES_PATH\catppuccin_mocha.omp.json" | Invoke-Expression
try {
	# Examples
    Set-PSReadLineOption -PredictionSource History
    Set-PSReadLineOption -PredictionViewStyle ListView
	# ...Your options here
} catch {}
```

Alternatively you could update your configuration for calling the PowerPress script to include `-NoProfile` and `-ErrorAction SilentlyContinue` to prevent your profile from being loaded for this particular script.

---
## WP-CLI errors

### "Not a registered command"

#### Error
> Error: 'core' is not a registered wp command 
> Error: 'search-replace' is not a registered wp command 
> Error: 'plugin' is not a registered wp command 

...or any other `wp` command not registered.

#### Cause
This can occur if you installed WP-CLI via Composer, as it doesn't actually install everything you are likely to need.

#### Solution
The [WP-CLI Bundle](https://packagist.org/packages/wp-cli/wp-cli-bundle) package contains most of what PowerPress requires, except for the search-replace command. You can install both like so:

```powershell
composer global require wp-cli/wp-cli-bundle wp-cli/search-replace-command
```

If you prefer not to use the bundle, you can install just what PowerPress requires:

```powershell
composer global require wp-cli/core-command wp-cli/search-replace-command wp-cli/db-command wp-cli/extension-command wp-cli/rewrite-command
```

You can find all WP-CLI Composer packages by [searching for "wp-cli" on Packagist](https://packagist.org/?query=wp-cli).

---
## Laravel Herd errors

### Herd is not running...but it is

#### Error
> The Herd Desktop application is not running. Please start Herd and try again.

#### Cause
If you get this error and Herd is definitely running, something else may be using the port that Herd uses to communicate with the CLI (usually 9001). You can confirm in Herd under General → Internal API Port.

#### Solution
To find what is using the port, open a PowerShell instance with admin privileges and run:

```powershell
netstat -aon | findstr :9001
```
If it's something you can't stop, you can change the port Herd uses in the settings and stop and restart all services in Herd to work around it.

If nothing comes up, you can also try changing the port in Herd and restarting all services; if that doesn't work try exiting Herd completely and restarting it.
