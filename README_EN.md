[**中文**](README.md) | English

# AI Spire 🃏🤖
![alt text](front_page_v2.png)
An AI Mod that uses LLMs to automatically play Slay the Spire 2.

The AI analyzes combat state, hand cards, and enemy intents in real time, making decisions through LLM APIs (DeepSeek, OpenAI-compatible, etc.) to automatically play cards, use potions, choose map paths, and handle all game events.

## Features

- **LLM-Driven Decisions**: Calls DeepSeek/OpenAI-compatible APIs to make card-play decisions based on the full game state
- **Multi-Turn Conversations**: Maintains full context per combat, so the AI remembers previous decisions and outcomes
- **Codex Data Integration**: Loads [spire-codex](https://github.com/ptrlrd/spire-codex) card/monster/power/relic data, providing the LLM with precise damage, block, move-set values
- **Real-Time Intent Recognition**: Extracts exact enemy intents (attack damage/type) from the game; codex supplements possible future moves for long-term planning
- **Rule Engine Fallback**: Automatically falls back to a rule-based engine if the LLM call fails, ensuring the game never gets stuck
- **In-Game Overlay**: A semi-transparent panel at the top center displays each AI decision and reasoning in real time
- **Power/Relic Understanding**: Provides the LLM with detailed Buff/Debuff and relic effect descriptions, enabling multi-turn synergy planning
- **Bilingual Support**: Automatically detects game language (Chinese/English), prompts and UI adapt accordingly

## Prerequisites

- **Slay the Spire 2** (Steam, v0.98+)

## Installation & Usage

### 1. Download the Mod

Go to the [Releases page](https://github.com/biolbe1230/ai-spire/releases/tag/v0.1.0) and download the latest **AISpire.zip**.

### 2. Place Files

Extract the archive into the Slay the Spire 2 `mods` directory:

```
{Slay the Spire 2 install directory}/mods/AISpire/
```

> **Finding the install directory**: Right-click the game in Steam → Manage → Browse local files

After extraction, the directory structure should be:

```
mods/
└── AISpire/
    ├── AISpire.dll          # Mod binary
    ├── AISpire.json         # Mod manifest (do not delete)
    ├── config.json          # Configuration file (edit this)
    └── data/                # Game data
        ├── en/              # English data
        │   ├── cards.json
        │   ├── monsters.json
        │   ├── powers.json
        │   └── relics.json
        └── zhs/             # Chinese data
            ├── cards.json
            ├── monsters.json
            ├── powers.json
            └── relics.json
```

> ⚠️ All the above files are required. Do not delete any of them.

### 3. Configure API Key

Edit `mods/AISpire/config.json` and fill in your LLM API key:

```json
{
  "api_key": "sk-your-api-key",
  "api_endpoint": "https://api.deepseek.com/v1/chat/completions",
  "model": "deepseek-chat"
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `api_key` | LLM API key | (required) |
| `api_endpoint` | API URL, OpenAI-compatible format | DeepSeek |
| `model` | Model name | `deepseek-chat` |
| `api_timeout_ms` | API request timeout (ms) | `15000` |
| `max_retries` | Number of retries on failure | `1` |
| `enabled` | Master AI toggle | `true` |
| `action_delay_ms` | Delay between actions (ms) to prevent too-fast play | `500` |
| `verbose_logging` | Verbose logging | `true` |
| `max_history_messages` | Max history messages in multi-turn conversation | `40` |
| `language` | Language: `"auto"` (follow game), `"en"`, or `"zhs"` | `"auto"` |

### 4. Launch the Game

Start Slay the Spire 2 normally. The mod loads automatically. Once in battle, the AI takes over — it also handles events, map navigation, shops, rest sites, treasures, and card rewards.

---

<details>
<summary><b>Building from Source (Developers)</b></summary>

#### Requirements

- .NET 9.0 SDK
- Godot .NET SDK 4.5.1 (auto-restored via NuGet)

#### Steps

1. Clone the repo: `git clone --recurse-submodules https://github.com/biolbe1230/ai-spire.git`
2. Edit `AISpire.csproj`, change `Sts2Dir` to your Slay the Spire 2 install path:
   ```xml
   <Sts2Dir>D:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Dir>
   ```
3. Copy config template: `cp config.example.json config.json`, fill in your API key
4. Build: `dotnet build`

After building, the DLL, config.json, and game data are automatically copied to `{Sts2Dir}/mods/AISpire/`.

</details>

## Project Structure

```
ai-spire/
├── Scripts/
│   ├── Entry.cs                 # Mod entry point, Harmony patch registration
│   ├── Config/
│   │   ├── AIConfig.cs          # Configuration (reads config.json)
│   │   └── Loc.cs               # Bilingual localization (Chinese/English)
│   └── AI/
│       ├── AIDecisionEngine.cs  # Decision engine, multi-turn conversation manager
│       ├── LLMClient.cs         # LLM API client
│       ├── PromptBuilder.cs     # Prompt builder (game rules, strategy guidance)
│       ├── GameStateExtractor.cs# Game state extraction (hand/enemies/powers/relics)
│       ├── GameStateModel.cs    # State data models
│       ├── GameDataLoader.cs    # spire-codex JSON data loader
│       ├── RuleEngine.cs        # Rule engine (LLM fallback)
│       ├── ActionExecutor.cs    # Action executor (play cards, end turn, etc.)
│       ├── ScreenHandler.cs     # Overlay/screen handler (rewards, upgrades, etc.)
│       └── AIOverlay.cs         # In-game decision display panel
├── spire-codex/                 # Game data (git submodule)
├── config.json                  # User config (not committed to git)
├── config.example.json          # Config template
├── AISpire.csproj               # Project file
├── AISpire.json                 # Mod manifest
└── project.godot                # Godot project file
```

## How It Works

```
Turn Start
    │
    ▼
Extract game state (hand, energy, enemy intents, powers, relics)
    │
    ▼
Build prompt (codex values + game rules + strategy guidance)
    │
    ▼
Call LLM API (multi-turn conversation with combat history)
    │         │
    │ Success │ Failure
    ▼         ▼
LLM Decision  Rule Engine Fallback
    │         │
    ▼         ▼
Execute action (play card / potion / end turn)
    │
    ▼
Overlay displays reasoning
    │
    ▼
Loop (until end turn)
```

## Roadmap

- [x] **Bilingual Support** — English prompts / codex data, auto-detect game language
- [x] **Full Game Flow** — Events, shops, card rewards, rest sites, treasures, map navigation
- [ ] **Non-Ironclad Characters** — Currently only Ironclad is supported due to simpler interaction logic; other characters coming later
- [ ] **Diverse Strategies** — Configurable strategy profiles (aggressive/defensive/speedrun), character/archetype-specific prompts
- [ ] **Real-Time User Control** — In-game hotkeys to pause AI, manually take over a turn, adjust AI tendencies

## Acknowledgments

- [spire-codex](https://github.com/ptrlrd/spire-codex) — Slay the Spire 2 game data

## License

MIT
