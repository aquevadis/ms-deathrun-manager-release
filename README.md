## CS2 Deathrun Manager for ModSharp

### Description

Deathrun Manager for CS2 servers. This is port of [this](https://forums.alliedmods.net/showthread.php?t=78197) plugin with few changes and improvements.

### Commands
#### Chat
```config

!respawn, /respawn - Respawns the caller if they are dead and have enough extra lives

```

```config

!kill, /kill - Kills the caller if they are alive

```

### Installation

1. Download the latest release from [Releases](https://github.com/aquevadis/ms-deathrun-manager-release/releases).
2. Extract the `.zip` archive or Copy-Paste it directly into your server's root directory.
3. Restart server to generate config files.
4. Adjust the configuration files to your linking and restart the server again to reflect all changes.

### Configuration

The configurations values do exactly what they are labeled, so I believe it doesn't need further explanation.

#### deathrun.json(default)
```json
{
  "GiveWeaponToCTs": true,
  "RemoveBuyZones": true,
  "RemoveMoneyFromGameAndHud": true,
  "SetRoundTimeOneHour": true,
  "EnableClippingThroughTeamMates": true,
  "EnableAutoBunnyHopping": true,
  "EnableKillCommandForCTs": true,
  "EnableKillCommandForTs": false,
  "Prefix": "{GREEN}[Deathrun]{DEFAULT}"
}
```

#### lives_system.json(default)
```json
{
  "EnableLivesSystem": false,
  "StartLivesNum": 1,
  "ShowLivesCounter": true,
  "SaveLivesToDatabase": false,
  "Spacer": "// If SaveLivesToDatabase is true, you have to configure the database connection details below too.",
  "Host": "localhost",
  "Database": "database_name",
  "User": "database_user",
  "Password": "database_password",
  "Port": 3306,
  "TableName": "deathrun_players"
}
```

### Shared API
- The plugin focuses more on the API it provides, that you can use to create and plug-in all kinds of addons and features.

#### Getting started
##### 1. First download the Nuget package. You can also visit the package's page [**here**](https://www.nuget.org/packages/DeathrunManager.Shared/0.1.3).
```csharp
dotnet add package DeathrunManager.Shared --version 0.1.3
```


##### 2. Add Project Reference, or manually inside your .csproj:
```csharp
dotnet add reference DeathrunManager.Shared.csproj
```
or
```csharp
<ProjectReference Include="..\DeathrunManager.Shared\DeathrunManager.Shared.csproj" />
```

---

If you'd like more exposed methods/events/hooks in the API, feel free to raise issue, open pull request, etc..
