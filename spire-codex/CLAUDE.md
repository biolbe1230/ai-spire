 ## Project overview  game data from Slay the Spire 2 (Godot 4 / C#/.NET 8) and expose it through a FastAPI backend + Next.js frontend.

## Key Discovery: Game is C#/.NET, NOT GDScript
- All game logic is in `sts2.dll` (C#/.NET 8), not GDScript
- Only 48 GDScript files (VFX testers), the DLL decompiles cleanly with ILSpy
- Uses Spine for skeletal animations, FMOD for audio, Sentry for error tracking

## Project Structure
```
spire-codex/
  extraction/
    raw/                    # GDRE extracted Godot project (9,947 files)
      images/               # Game images (card portraits, relics, potions, monsters)
      animations/monsters/  # Spine skeletal animations (.skel, .atlas, .png)
      localization/eng/     # Localization JSON (names, descriptions)
    decompiled/             # ILSpy decompiled C# (3,298 files)
  backend/
    app/
      main.py               # FastAPI app + CORS middleware
      models/schemas.py      # Pydantic models
      routers/               # API routes: cards, characters, relics, monsters, potions, enchantments, encounters, events, powers, keywords, intents, orbs, afflictions, modifiers, achievements
      services/              # data_service (loads JSON)
      parsers/               # C# -> JSON parsers
        card_parser.py       # Cards with DynamicVars, upgrades, description resolution
        character_parser.py
        monster_parser.py    # Monsters with HP, moves, damage, encounter types
        relic_parser.py      # Relics with var extraction + description resolution
        potion_parser.py     # Potions with var extraction + description resolution
        enchantment_parser.py  # Enchantments with card type, stackability
        encounter_parser.py    # Encounters with monsters, act mapping, room types
        event_parser.py        # Events with choices, descriptions, act mapping
        power_parser.py      # Powers with type, stack type, descriptions
        keyword_parser.py    # Keywords, intents, orbs, afflictions, modifiers, achievements
        description_resolver.py  # Shared SmartFormat template resolver
        parse_all.py         # Runs all parsers
    static/images/           # Served static images
      cards/                 # Card portraits (573 files)
      cards/beta/            # Beta card art (265 files)
      relics/                # Relic icons (314 files)
      potions/               # Potion icons (63 files)
      monsters/              # Monster idle poses rendered from Spine (105+ files)
      characters/            # Character select, combat, and rest site images
      misc/                  # NPCs (Neow, Tezcatara, Merchant), boss icons, backgrounds
        ancients/            # Ancient portrait icons (8 files)
        bosses/              # Boss encounter icons (12 files)
      renders/               # Full render output (mirrors animation directory structure)
      icons/                 # Energy icons, star icons (12 files)
    scripts/
      copy_images.py         # Copies/organizes images from extraction/raw -> static/
    Dockerfile
  frontend/
    app/
      cards/page.tsx         # Cards page with upgrade toggle + beta art toggle
      characters/page.tsx    # Characters with relic/card hover tooltips
      monsters/page.tsx      # Monsters grid with sprites
      relics/page.tsx        # Relics with rich descriptions
      potions/page.tsx       # Potions with rich descriptions
      enchantments/page.tsx  # Enchantments with card type badges
      encounters/page.tsx    # Encounters with monster lists, act/type filters
      events/page.tsx        # Events with multi-page decision trees, dialogue, relic offerings
      powers/page.tsx        # Powers with type/stack filters
      reference/page.tsx     # Keywords, orbs, afflictions, intents, modifiers, achievements
      components/
        CardGrid.tsx         # Card grid with inline icons, upgrade rendering
        RichDescription.tsx  # Tokenizer + tree builder for nested rich text tags (colors, effects, icons)
        SearchFilter.tsx     # Reusable search + filter bar
        Navbar.tsx
    lib/api.ts              # API client + TypeScript interfaces
    Dockerfile
    next.config.ts          # output: "standalone" for Docker
  tools/
    spine-renderer/          # Headless Spine renderer (Node.js)
      render.mjs             # Renders monster idle poses from .skel files
      render_all.mjs         # Universal renderer — finds ALL .skel files and renders them
      render_skins2.mjs      # Skin-specific renderer (Cultists, Bowlbugs, Cubex)
      package.json           # @esotericsoftware/spine-canvas + canvas
  data/                     # Parsed JSON output
  docker-compose.yml        # Local dev (build from source)
  docker-compose.prod.yml   # Production (Docker Hub images + nginx network)
  .forgejo/workflows/
    build.yml               # CI: buildah builds -> Docker Hub (ptrlrd/spire-codex-*)
  venv/                     # Python virtual environment
```

## Data Parsed
- **576 cards** — cost, type, rarity, target, damage, block, keywords, tags, upgrades, X-cost, vars, resolved descriptions
- **5 characters** — Ironclad, Silent, Defect, Necrobinder, Regent (HP, gold, energy, deck, relics)
- **289 relics** — rarity, pool, resolved descriptions with [gold]/[energy] markers
- **111 monsters** — HP ranges, ascension scaling, moves, damage values, idle pose sprites
- **63 potions** — rarity, resolved descriptions
- **22 enchantments** — card type restrictions, stackability, descriptions
- **87 encounters** — monster compositions, room type (Boss/Elite/Monster), act placement, tags
- **66 events** — multi-page decision trees (56 have multiple pages), choices with outcomes, act placement, ancient vs regular types
  - **8 Ancients** with epithets, character-specific dialogue, relic offerings, portrait icons
  - `StringVar` model references resolved to display names (enchantment/card/relic/potion names from localization)
  - Runtime-dynamic variables preserved as readable placeholders (e.g., `[Card]`, `[Relic]`)
- **260 powers** — type (Buff/Debuff/None), stack type (Counter/Single/None), resolved descriptions
- **8 keywords** — card keyword definitions (Exhaust, Ethereal, Innate, Retain, Sly, Eternal, Unplayable)
- **14 intents** — monster intent types and descriptions
- **5 orbs** — Dark, Frost, Glass, Lightning, Plasma with passive/evoke descriptions
- **9 afflictions** — Bound, Entangled, Galvanized, Hexed, Ringing, Smog + stackability, extra card text
- **16 modifiers** — run modifier descriptions (All Star, Big Game Hunter, Cursed Run, etc.)
- **33 achievements** — unlock conditions and descriptions

## Data Sources
### Decompiled C# (`extraction/decompiled/`)
- `MegaCrit.Sts2.Core.Models.Cards/` — Card models with DynamicVars (DamageVar, BlockVar, PowerVar, EnergyVar, CardsVar, etc.)
- `MegaCrit.Sts2.Core.Models.Characters/` — Starting HP/gold/energy/deck/relics
- `MegaCrit.Sts2.Core.Models.Monsters/` — HP ranges, MoveStateMachine, damage/block values
- `MegaCrit.Sts2.Core.Models.Relics/` — Rarity, CanonicalVars, effects
- `MegaCrit.Sts2.Core.Models.Potions/` — Rarity, CanonicalVars, effects
- `MegaCrit.Sts2.Core.Models.Encounters/` — Maps monsters to Boss/Elite/Normal types
- `MegaCrit.Sts2.Core.Models.Events/` — Ancient events (Darv, Neow, etc.) with relic offerings, dialogue
- `MegaCrit.Sts2.Core.Models.Powers/` — 260 power models with PowerType, PowerStackType, DynamicVars
- `MegaCrit.Sts2.Core.Models.Orbs/` — 5 orb models with PassiveVal, EvokeVal
- `MegaCrit.Sts2.Core.Models.Afflictions/` — 6 affliction models with stackability
- `MegaCrit.Sts2.Core.Models.Modifiers/` — 17 run modifier models
- `MegaCrit.Sts2.Core.Models.Achievements/` — 9 achievement models (localization has 33 total)

### Localization JSON (`extraction/raw/localization/eng/`)
- SmartFormat templates: `{Damage:diff()}`, `{Energy:energyIcons()}`, `{Cards:plural:card|cards}`, `[gold]text[/gold]`
- `ancients.json` — Ancient dialogue (character-specific, visit-indexed), epithets, The Architect dialogue

### Game Icons (`extraction/raw/images/ui/run_history/`)
- Ancient portrait icons (8): darv, neow, nonupeipe, orobas, pael, tanx, tezcatara, vakuu
- Boss encounter icons (12): ceremonial_beast, doormaker, kaiser_crab, knowledge_demon, lagavulin_matriarch, queen, soul_fysh, test_subject, the_insatiable, the_kin, vantom, waterfall_giant

## Description Resolution Pipeline
1. **Backend parsers** extract DynamicVar values from C# source (e.g., `new DamageVar(8m)` → `{"Damage": 8}`)
2. **`event_parser.py`** also extracts `StringVar` model references (e.g., `new StringVar("Enchantment1", ModelDb.Enchantment<Sharp>().Title)` → `{"Enchantment1": "Sharp"}`) by looking up class names in localization title mappings
3. **`description_resolver.py`** resolves SmartFormat templates (supports `dict[str, int | str]` for mixed var types):
   - `{Var}` → numeric or string value (or `[Readable Name]` if runtime-dynamic)
   - `{Var:diff()}` → numeric value
   - `{Var:energyIcons()}` → `[energy:N]` marker
   - `{Var:starIcons()}` → `[star:N]` marker
   - `{Var:plural:singular|plural with {} count}` → resolved plural form
   - `{IfUpgraded:show:A|B}` → conditional text
4. **All parsers preserve** Godot BBCode rich text tags for frontend rendering:
   - Colors: `[gold]`, `[red]`, `[blue]`, `[green]`, `[purple]`, `[orange]`, `[pink]`, `[aqua]`
   - Effects: `[sine]` (wavy), `[jitter]` (shake), `[b]` (bold)
   - Icons: `[energy:N]`, `[star:N]`
   - Only non-renderable tags stripped: `[thinky_dots]`, `[i]`, `[font_size]`, `[rainbow]`
5. **Frontend `RichDescription.tsx`** — tokenizer + tree builder renders nested tags as styled React nodes
   - Tokenizer parses open/close tags, icons, placeholders into a token stream
   - Tree builder creates a nested node tree for proper handling of `[jitter][red]text[/red][/jitter]`
   - Colors map to Tailwind classes (`text-red-400`, `text-[var(--accent-gold)]`, etc.)
   - Effects map to CSS animation classes (`rich-sine`, `rich-jitter` in `globals.css`)
   - `[Card]`, `[Relic]`, etc. → muted italic text (runtime-dynamic placeholders)

## Spine Rendering
- Game sprites are **Spine skeletal animations** (.skel + .atlas + .png spritesheet), NOT static images
- `tools/spine-renderer/render_all.mjs` — universal renderer, finds ALL 158 .skel files and renders them (125 succeed)
- `tools/spine-renderer/render.mjs` — monster-specific renderer (legacy, uses directory name as skel name)
- `tools/spine-renderer/render_skins2.mjs` — renders skin-based variants (Cultists: coral/slug, Bowlbugs: cocoon/goop/rock/web, Cubex: circleeye/diamondeye/squareeye)
- Skeletons are Spine 4.2.x binary format; runtime is 4.2.106
- Renders at 2x supersampling (1024px) then downscales to 512x512 to reduce triangle seam artifacts
- Excludes shadow/ground slots from bounding box for tighter framing
- 33 skels skip due to: no atlas, no bounds (mesh-only/VFX), or missing .skel files

### Render commands
```bash
cd tools/spine-renderer
node render_all.mjs          # Render ALL skeletons (outputs to backend/static/images/renders/)
node render.mjs              # Render all monsters (outputs to backend/static/images/monsters/)
node render_skins2.mjs       # Render skin variants (Bowlbugs, Cultists, Cubex, Sculptor)
```

### Monster image aliases
Some monsters share skeletons or have mismatched filenames. The monster parser uses IMAGE_ALIASES:
- Cultists: coral skin → calcified_cultist.png, slug skin → damp_cultist.png
- Bowlbugs: cocoon/goop/rock/web skins → bowlbug_egg/nectar/rock/silk.png
- Globe Head → orb_head.png, Torch Head Amalgam → amalgam.png, Living Fog → living_smog.png
- Skulking Colony → skulkling_colomy.png (typo in original filename)
- Devoted Sculptor: skel has typo `devoted_scultpor.skel`, atlas is `devoted_sculptor.atlas`

## Image Pipeline
1. **`backend/scripts/copy_images.py`** — Copies card portraits, relics, potions, characters, icons, ancients, bosses from `extraction/raw/images/` to `backend/static/images/`
   - Card beta art separated into `cards/beta/` subfolder
   - Energy/star icons from `packed/sprite_fonts/`
   - Ancient icons from `ui/run_history/` (8 ancients)
   - Boss icons from `ui/run_history/` (12 bosses)
2. **`tools/spine-renderer/render.mjs`** — Renders monster idle poses from Spine animations to `backend/static/images/monsters/`
3. Backend serves all via FastAPI `StaticFiles` mount at `/static`

## Docker & Deployment
- **Local dev**: `docker compose up -d --build` (builds from source, ports 8000 + 3000)
- **Production**: `docker compose -f docker-compose.prod.yml up -d` (uses Docker Hub images)
- **CI**: Forgejo Actions (`.forgejo/workflows/build.yml`) builds with buildah, pushes to `docker.io/ptrlrd/spire-codex-{frontend,backend}:latest`
- **CORS**: Custom `CORSStaticMiddleware` in `main.py` adds `Access-Control-Allow-Origin: *` to all responses (including static files)
- **Frontend images**: All `<img>` tags need `crossOrigin="anonymous"` to avoid OpaqueResponseBlocking

## Commands
```bash
# Local development (without Docker)
source venv/bin/activate && cd backend && uvicorn app.main:app --reload  # Backend (port 8000)
cd frontend && npm run dev                                                # Frontend (port 3000)

# Docker development
docker compose up -d --build

# Parse all data
cd backend/app/parsers && python3 parse_all.py

# Copy images from extraction to static
python3 backend/scripts/copy_images.py

# Render monster sprites
cd tools/spine-renderer && npm install && node render.mjs

# Decompile DLL (if needed)
~/.dotnet/tools/ilspycmd -p -o extraction/decompiled "<path>/sts2.dll"

# Extract PCK (if needed)
/Applications/Godot\ RE\ Tools.app/Contents/MacOS/Godot\ RE\ Tools --headless --recover="<pck>" --output-dir=extraction/raw
```

## API Endpoints
- `GET /api/stats` — Data counts
- `GET /api/cards?color=&rarity=&type=&keyword=&search=` — Cards with filtering
- `GET /api/cards/{id}` — Single card
- `GET /api/characters` / `GET /api/characters/{id}`
- `GET /api/relics?rarity=&pool=&search=` — Relics with filtering
- `GET /api/relics/{id}`
- `GET /api/monsters?type=&search=` — Monsters with filtering
- `GET /api/monsters/{id}`
- `GET /api/potions?rarity=&search=` — Potions with filtering
- `GET /api/potions/{id}`
- `GET /api/enchantments?card_type=&search=` — Enchantments with filtering
- `GET /api/enchantments/{id}`
- `GET /api/encounters?room_type=&act=&search=` — Encounters with filtering
- `GET /api/encounters/{id}`
- `GET /api/events?type=&act=&search=` — Events with filtering (Ancient events include image_url, relics, epithet, dialogue)
- `GET /api/events/{id}`
- `GET /api/powers?type=&stack_type=&search=` — Powers with filtering
- `GET /api/powers/{id}`
- `GET /api/keywords` / `GET /api/keywords/{id}`
- `GET /api/intents` / `GET /api/intents/{id}`
- `GET /api/orbs` / `GET /api/orbs/{id}`
- `GET /api/afflictions` / `GET /api/afflictions/{id}`
- `GET /api/modifiers` / `GET /api/modifiers/{id}`
- `GET /api/achievements` / `GET /api/achievements/{id}`
- Docs: `http://localhost:8000/docs`

## Key Technical Patterns
- **Card DynamicVars**: `new DamageVar(8m)`, `new BlockVar(5m)`, `new PowerVar<VulnerablePower>(2m)`, `new EnergyVar(1)`, `new CardsVar(2)`
- **Card upgrades**: `dict[str, str | int | None]` (NOT a Pydantic model — was stripping unknown keys)
- **Monster HP scaling**: `AscensionHelper.GetValueIfAscension(level, ascension_val, normal_val)`
- **Card keywords**: Exhaust, Ethereal, Innate, Unplayable, Retain, Sly, Eternal
- **Card tags**: Strike, Defend, Minion, OstyAttack, Shiv
- **ID format**: PascalCase class name → UPPER_SNAKE_CASE (e.g., `FrogKnight` → `FROG_KNIGHT`)

## Tools Installed
- GDRE Tools v2.4.0, Godot 4.6.1
- .NET SDK 10.0.103 + .NET 8.0, ILSpy CLI v9.1.0
- Python 3.10 venv, FastAPI, Next.js 16 + Tailwind

## Known Limitations
- 6 monsters lack images entirely (Crusher, Doormaker, Flyconid, Ovicopter, Rocket — no Spine data; Decimillipede — segmented, non-standard skel naming)
- Some card descriptions have unresolved conditionals (`{InCombat:...}`, `{IsTargeting:...}`)
- Card color "event" has no matching energy icon — falls back to "colorless" in frontend
- Devoted Sculptor renders blank (default skin has no visible attachments)

## Versioning
Uses `1.X.Y` — 1=codex major (stays unless rewrite), X=bumps on Mega Crit game patch, Y=our fixes/improvements.

## Future Enhancements
- Database (SQLite/Postgres) instead of JSON files
