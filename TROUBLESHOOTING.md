# Troubleshooting PowerPress

- [.NET, C#, and Rider](#net-c-and-rider)
- [PowerShell](#powershell)
- [WP-CLI](#wp-cli)
- [Laravel Herd(#laravel-herd)
- [BitWarden CLI](#bitwarden-cli)

---
## .NET, C#, and Rider

### .NET version problems

Note that if you have .NET and SDK versions installed by Rider, they may be in different location to the system-wide ones. This means things may work in Rider's integrated terminal (or by running the project as a C# app) but then not work in an independent PowerShell terminal.

You can check if you have .NET 10+ installed system-wide by running the following command in PowerShell:

```powershell
dotnet --info
```

Make sure this lists both the runtime and SDK, and that they are version 10+.

If not you can [download the installer](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or install the latest using Chocolatey:

```powershell
choco install dotnet
```
```powershell
choco install dotnet-sdk
```

If you are still seeing inconsistencies, it could be a simple version difference between Rider and system.

---
## PowerShell

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
## WP-CLI

### "Not a registered command" error

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
## Laravel Herd

### Error saying Herd is not running...but it is

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

---
## BitWarden

### Error saying credentials are incorrect...but they're not

#### Error
You have double-checked that the credentials in your environment variables match those in your BitWarden account, and that PowerShell is able to read them correctly, but you get:

> client_id or client_secret is incorrect. Try again.

#### Cause
This can occur if your account is on the BitWarden EU server or a self-hosted instance, because the default server for the BitWarden CLI is the US one.

#### Solution
For the EU server, update the config like so:

```powershell
bw config server https://vault.bitwarden.eu
```