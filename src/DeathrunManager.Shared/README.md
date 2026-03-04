## CS2 DeathrunManager APIv0.1.2 Documentation

# Overview

The Deathrun Shared API acts as the abstraction layer between:

- The game engine (`IGameClient`, `IPlayerController`, `IPlayerPawn`)
- The Deathrun game mode logic

It ensures:

- Clean separation of concerns
- Read-only safe access to collections
- Role-based logic isolation
- Per-player UI management
- Flexible round lifecycle handling

---

# 📦 Installation

## 1️⃣ Add Project Reference

```bash
dotnet add reference DeathrunManager.Shared.csproj
```

Or manually inside your `.csproj`:

```xml
<ProjectReference Include="..\DeathrunManager.Shared\DeathrunManager.Shared.csproj" />
```

---

## 2️⃣ Register in Dependency Injection (Optional)

```csharp
services.AddSingleton<IDeathrunManager, DeathrunManager>();
```

---

## 3️⃣ Access in Your Plugin

```csharp
public class MyPlugin
{
    private IModSharpModuleInterface<IDeathrunManager>? _deathrunManagerApi;

    public void OnAllModulesLoaded()
    {    
        //capture the API's instance
        _deathrunManagerApi = Bridge.SharpModuleManager.GetOptionalSharpModuleInterface<IDeathrunManager>(IDeathrunManager.Identity);
    
        //validate the API's instance
        if (_deathrunManagerApi?.Instance is { } deathrunManagerApi)
        {
            //we can now safely call the API's methods
            
            //get all alive deathrun players
            var allAliveDeathrunPlayers = deathrunManagerApi.GetAllAliveDeathrunPlayers();
    
            //do something with the alive deathrun players
            foreach (var aliveDeathrunPlayer in allAliveDeathrunPlayers)
            {
                //change deathrun player's class, team, etc. 
            }
        }
    }
}
```

---

# 🏗 Architecture

```
Game Engine
   │
   ├── IGameClient
   ├── IPlayerController
   ├── IPlayerPawn
   │
   ▼
IDeathrunPlayer
   │
   ├── Lives System
   ├── Role Management
   ├── UI Rendering
   │
   ▼
IDeathrunManager
   │
   ├── Player Filtering
   ├── Round Lifecycle
   ├── Game Master Selection
```

---

# 🔌 Core Interfaces

---

# IDeathrunManager

Central access point for the Deathrun game mode.

```csharp
public interface IDeathrunManager
```

## Identity

```csharp
static string Identity => typeof(IDeathrunManager).FullName ?? nameof(IDeathrunManager);
```

Used for service registration, plugin discovery, and logging.

---

## Player Access

### GetDeathrunPlayer

```csharp
IDeathrunPlayer? GetDeathrunPlayer(IGameClient client);
```

Returns the Deathrun wrapper for an engine client.

---

### GetAllValidDeathrunPlayers

```csharp
IReadOnlyCollection<IDeathrunPlayer> GetAllValidDeathrunPlayers();
```

Returns all valid connected players.

---

### GetAllAliveDeathrunPlayers

```csharp
IReadOnlyCollection<IDeathrunPlayer> GetAllAliveDeathrunPlayers();
```

Returns players who are valid and alive.

---

## Gameplay

### GetRoundState

```csharp
DRoundState GetRoundState();
```

Returns the current round lifecycle state.

---

### GetGameMaster

```csharp
IDeathrunPlayer? GetGameMaster();
```

Returns the currently assigned Game Master.

---

# IDeathrunPlayer

Represents a Deathrun participant.

```csharp
public interface IDeathrunPlayer
```

---

## Engine Bindings

```csharp
IGameClient Client { get; }
IPlayerController? Controller { get; }
IPlayerPawn? PlayerPawn { get; }
```

Provides access to underlying engine objects.

---

## Role System

```csharp
DPlayerClass Class { get; set; }
void ChangeClass(DPlayerClass newClass, bool force = false);
```

Roles:

- `Contestant`
- `GameMaster`

---

## Validation Helpers

```csharp
bool IsValid { get; }
bool IsValidAndAlive { get; }
```

Used for safe filtering and win-condition checks.

---

## Lives Integration

```csharp
bool InitLivesSystem();
ILivesSystem? LivesSystem { get; }
```

Attaches and manages player lives.

---

## Game Master Selection Control

```csharp
bool SkipNextGameMasterPickUp { get; set; }
```

Prevents player from being selected in next rotation.

---

## Center UI API

```csharp
void SetCenterMenuTopRowHtml(string? htmlString);
void SetCenterMenuMiddleRowHtml(string? htmlString);
void SetCenterMenuBottomRowHtml(string? htmlString);
```

Passing `null` clears the row.

---

## Thinker

```csharp
void PlayerThink();
```

Responsible for:

- Rendering UI
- Updating lives counter
- Per-player periodic logic

---

# ILivesSystem

Manages extra lives and respawn logic.

```csharp
public interface ILivesSystem
```

---

## Core Properties

```csharp
IDeathrunPlayer? Owner { get; }
int GetLivesNum { get; }
```

---

## Life Management

```csharp
void SetLivesNum(int amount);
void AddLivesNum(int amount);
void RemoveLife();
void RemoveLives(int amount = 0, bool allLives = false);
```

---

## Respawn

```csharp
bool Respawn(bool useLife = true);
```

Attempts respawn depending on:

- Round state
- Available lives

---

## UI Rendering

```csharp
string GetLivesCounterHtmlString();
```

Returns HTML representation of remaining lives.

---

# 📘 Enums

---

# DPlayerClass

```csharp
public enum DPlayerClass
{
    Contestant,
    GameMaster
}
```

Defines player role.

---

# DRoundState

```csharp
public enum DRoundState
```

## Lifecycle States

| State | Description |
|-------|-------------|
| `Unset` | Uninitialized |
| `StartPre` | Preparing to start |
| `CheckGameModeRequirements` | Validating requirements |
| `PickingGameMaster` | Selecting Game Master |
| `PickedGameMaster` | Game Master selected |
| `StartPost` | Post initialization |
| `EndPre` | Preparing to end |
| `EndPost` | Cleanup phase |

---