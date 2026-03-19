# PowerPress

PowerShell utility to set up Composer-managed WordPress sites for development on Windows machines using my [WordPress Canvas](https://github.com/doubleedesign/wordpress-canvas) template repository as the base, and [PhpStorm](https://www.jetbrains.com/phpstorm) and [Laravel Herd Pro](https://herd.laravel.com) for development.

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Setup and usage](#setup-and-usage)
  - [Option 1: Run the script directly](#option-1-run-the-script-directly)
  - [Option 2: Set up a global command](#option-2-set-up-a-global-command)
  - [Option 3: Run from a C# IDE](#option-3-run-from-a-c-ide)
- [Troubleshooting](#troubleshooting)

---
## Features
PowerPress provides a user-friendly interactive command-line interface to get your site up and running in a local development environment as quickly and automatically as possible.

The steps broadly cover:
- basic site configuration
- database creation
- cloning the template repository into the project root and deleting its sexisting `.git` directory, `composer.lock`, uploads, etc. to get a clean slate
- updating `composer.json` and `README.md` to match your project
- installing WordPress
- installing and activating plugins (as defined in `composer.json`)
- installing the [Comet Canvas](https://github.com/doubleedesign/comet-canvas-blocks) parent theme
- scaffolding a child theme 
- setting the permalink structure
- deleting default themes and plugins (Akismet and Hello Dolly)
- activating ACF Pro with your licence key
- setting some default settings for Ninja Forms (if active)
- setting up workspace and FTP deployment configuration for PhpStorm (except username and password)
- registering and securing the site in Laravel Herd
- initialising a Git repository
- saving admin credentials to BitWarden for new sites (optional)

> [!NOTE]
> Some values are assumed, some are derived from the kebab-case site name you provide when you run the script, and some are prompted for during the script execution.

> [!TIP]
> If you have an ACF Pro Developer licence (i.e., your key is always, or almost always, the same) you can save it in your system environment variables as `ACF_PRO_KEY` and the script will use it automatically without prompting you each time.
> You can find this in the Windows control panel -> System -> Advanced system settings -> Environment Variables (or just search the Start menu for "Environment variables", it should come up).
> **Note:** It should be its own standalone variable, not in your PATH.
> You can also do this for any other values that rarely change, and adjust the scripts to look for those instead of my hard-coded defaults.

Options are provided for:
- deleting existing matching project databases and directories to start fresh (or not)
- importing from an existing site backup (database and/or `wp-content` directory)
- using symlinked local copies of Double-E Design's plugins and parent theme (if you have them on your machine) instead of downloading the latest versions from Packagist or GitHub.

When importing from an existing site backup, it also handles:
- updating the site URL in the local database
- deleting conflicting or unnecessary plugins (currently the config for when to delete a plugin is hard-coded, but can be adjusted easily), including removing them from `composer.json` if present.

Upon successful completion:
- `/wp-admin` will open in your default browser
- the project will automatically open in PhpStorm if it's installed on your machine in the assumed location
- the script will show a summary including a small number of manual steps you may need to take.

---
## Prerequisites

- [Laravel Herd](https://herd.laravel.com/windows) (preferably Pro)
- PHP, MySQL/MariaDB, [Git](https://git-scm.com/install/windows), [Composer](https://getcomposer.org/), [WP-CLI](https://wp-cli.org/), and [robocopy](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy) installed and available via command line*
- PowerShell 7+
- Permission to execute PowerShell scripts
- [ACF Pro](https://www.advancedcustomfields.com/pro/) on your machine and a valid licence key
- [BitWarden CLI](https://bitwarden.com/help/cli/) and a [personal API key](https://bitwarden.com/help/personal-api-key/) if wanting to save admin credentials for new sites automatically.

_* Laravel Herd takes care of installing PHP and Composer automatically. If using the Pro edition, it can also take care of MySQL/MariaDB via the Services feature - you just need to configure it._

The script will check the command-line prerequisites as the very first step, and stop if any are missing.

> [!NOTE]
> If you are using BitWarden CLI, the script is configured to look for your API credentials and master password in your system environment variables as `BW_CLIENTID`, `BW_CLIENTSECRET`, and `BW_PASSWORD` to save you having to enter credentials every time.
> You can find this in the Windows control panel -> System -> Advanced system settings -> Environment Variables (or just search the Start menu for "Environment variables", it should come up).
> Storing your master password there is optional - you should be promoted for it if it's not found.

---
## Setup and usage

There are some one-off setup steps to install PowerPress on your machine, depending how you want to run it.

In call cases, the first step is to clone this repository to your local machine (or just download it).

> [!NOTE]  
> If you want to use a different local web server setup (e.g. XAMPP, Local by Flywheel, etc.) you will just need to adjust the script to not look for Herd or try to run its commands, and do any other necessary configuration for your setup.

> [!IMPORTANT] 
> The site name passed into the script should be in kebab-case, as it will be used to generate the Title Case site name as well as a "short name" used for some configuration such as the database name (e.g., `test-site` will have a database called `test_dev`) and child theme directory.
> There's nothing stopping you using underscores if you prefer the name not to be split, but definitely do not use spaces.

### Script parameters
The script takes the following parameters:
- `-SiteName` (required): The kebab-case name of the site
- `-Debug`: Whether to show additional debugging output during script execution
- `-Dev`: Whether to use Double-E Design's developer mode for setting up with local copies of plugins. Intended for working on those plugins and the site simultaneously.
- `-Help`: Show all available options.

### Option 1: Run the script directly

In PowerShell, `cd` to the `PowerPress` directory (where `main.ps1` is located) and run:
```powershell
.\main.ps1 your-site-name
```

Alternatively, from anywhere else, run it using the full path:
```powershell
C:\path\to\PowerPress\main.ps1 your-site-name
```

### Option 2: Set up a global command

Create an "entry point" batch file in a directory that your system `PATH` knows about, such as your Herd `bin` directory (e.g., `C:\Users\leesa\.config\herd\bin\`).

Call the file `powerpress.bat` and put the following content in it, updating the path if necessary.

```batch
@echo off
pwsh.exe -ExecutionPolicy Bypass -File "%~dp0/PowerPress/PowerPress/main.ps1" %*
```

Then you can run it from anywhere in PowerShell like so:

```powershell
powerpress your-site-name
```

### Option 3: Run from a C# IDE

This one is mostly useful if you are making changes to the scripts to suit your own requirements. To develop and maintain PowerPress I use [Jetbrains Rider](https://www.jetbrains.com/rider/) with [this PowerShell plugin](https://plugins.jetbrains.com/plugin/10249-powershell). I haven't tested this in any other IDEs but theoretically it should work with any that supports C# and PowerShell.

In Rider, open the solution (the root `PowerPress` directory, not `PowerPress/PowerPress` - that's the project within the solution) and use the built-in **Run** command to start the script.

Under the hood this uses the `Program.cs` file to prompt you for the site name, launch PowerShell with your user and system environment variables, and start the script with the site name argument you just provided.


---
## Troubleshooting

### Script debugging

Most feedback messages (other than some that come from external processes) are output using custom functions which add the name of the calling function to the end of the line, so you know where to start in the codebase to troubleshoot.

For additional debugging output, run the script with the `-Debug` flag, e.g.:

```powershell
powerpress your-site-name -Debug
```

### Other troubleshooting help

See [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) for common errors and how to fix them.
