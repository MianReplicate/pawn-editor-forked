# 🐾 Pawn Editor Forked

> A maintained fork of [Pawn Editor](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385655) for **RimWorld 1.6**

[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-blue?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113)
[![Latest Release](https://img.shields.io/github/v/release/segaswolf/pawn-editor-forked)](https://github.com/segaswolf/pawn-editor-forked/releases/latest)

---

## 📋 What is this?

This is an **unofficial community fork** of the original Pawn Editor mod. The original is an incredibly useful tool for editing colonists, but had stability issues with large modlists and certain DLC interactions.

This fork aims to **fix those problems**, improve compatibility with popular mods, and keep the editor running smoothly — even with 1000+ mods loaded.

> ⚠️ **Important:** Use either this version or the original, but **not both at the same time.**

---

## 📢 Stay in the loop

- **→ [WIP / Known Issues thread](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113)** on Steam Discussions — check here before reporting
- **→ [Bug Reports](https://github.com/segaswolf/pawn-editor-forked/issues)** — open an issue and include your mod list + `Player.log`

---

## ✨ What's been fixed

### 🔧 Stability
- Fixed crashes when duplicating pawns (`Collection modified`, `Sequence no matching element`)
- Fixed crash in Bio tab when traits change during render
- Fixed startup crashes with TacticalGroups and other mods
- Fixed duplicate pawn ID conflicts that corrupted save files
- Fixed null reference exceptions with xenotypes and gene lists
- Starting Preset load no longer crashes the game on failure
- Fixed `ListingMenu_Items` `TypeInitializationException` from mods with null style entries

### 🧬 Pawn Duplication (Clone)
- Clones correctly copy gender, appearance, hair, skin color, and melanin
- Clones copy clothing, armor, and weapons with quality, color, and HP
- Ideology certainty preserved accurately
- Biological and chronological age no longer get swapped

### 💾 Blueprint Save/Load
- Brand new XML-based blueprint format (replaced crash-prone Scribe system)
- Saves everything: bio, traits, skills, genes, hediffs, abilities, apparel, equipment, relations, work priorities, inventory, royal titles, records, mod list
- Missing mods/DLCs are gracefully skipped on load — no more crashes
- Blueprints can be shared between different modlists

### 🎨 Appearance
- Hair, head type, body type, skin color, and fur properly saved and restored
- Melanin values preserved for accurate skin tones
- Style elements (beard, tattoos) correctly handled
- Genes load before appearance to prevent override conflicts

### 🛡️ Mod Compatibility
- **Facial Animations** — face type, eye color, brow, lid, mouth, skin, and head controllers all copy correctly on duplicate and blueprint load
- **VE Hussar / Giant gene** — visual body offset applies immediately after blueprint load, no reload needed
- **TacticalGroups** — Harmony finalizers prevent colonist bar crashes
- **Vanilla Skills Expanded** — passion system compatible
- Tested with 1000+ mods loaded

### 🗺️ Faction & NPC Support
- Replacing an NPC faction leader via blueprint now correctly preserves the leader role on the new pawn

### 🖥️ UI
- Edit button requires Dev Mode + God Mode (no accidental edits)
- Social tab defaults to showing all relations
- Custom hotkey picker in settings, persists across sessions
- Graphics refresh prevents null texture spam on clones

---

## 📦 Installation

1. Remove the original Pawn Editor mod
2. Subscribe on [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3667931113)
3. Place it **after Harmony** in your load order
4. That's it!

---

## 🗺️ Roadmap

- ✅ Stable blueprint save/load system
- ✅ Pawn duplication with full appearance/gear copy
- ✅ Facial Animations compatibility
- 🚧 Social relations copy (opinions, family ties)
- 🚧 Modded race visuals (tails, ears, custom body parts)
- 🚧 Migrate Starting Preset to Blueprint format
- ⬜ GradientHair support (dual color)
- ⬜ VE Aspirations integration
- ⬜ Clone suspicion debuff (optional, lore)

---

## ❤️ Credits & Attribution

All original credit belongs to the authors of [Pawn Editor](https://steamcommunity.com/sharedfiles/filedetails/?id=2920385655):

- **ISOR3X** — Project lead
- **legodude17** — Main coder
- **Taranchuk** — Coder
- **TreadTheDawnGames** — Community contributor
- **mycroftjr** — Community contributor
- **Inglix** — Community contributor
- **fofajardo** — Community contributor

**Fork maintained by:** [Segas Wolf](https://steamcommunity.com/id/SegasWolf)

> This fork does not claim ownership of the original concept or implementation.  
> If there are any concerns regarding attribution, permissions, or credit, please contact me directly.

---

## 📄 License

The [original repository](https://github.com/ISOR3X/pawn-editor) does not include an explicit license. All original code rights belong to their respective authors. This fork is provided as-is for community use and bug fixing purposes.
