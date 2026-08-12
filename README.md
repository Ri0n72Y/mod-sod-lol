# Shape of Dreams × League fan mod experiments

Experimental, community-driven modding research for **Shape of Dreams**.

The first proof of concept uses **Lacerta** as the host Traveler and explores a Caitlyn-inspired identity kit:

- Identity / passive: Headshot
- Q: Piltover Peacemaker
- W: Yordle Snap Trap
- E: 90 Caliber Net
- R: Ace in the Hole

## Current experiment

Phase 0 focuses on the content plumbing required before implementing the final combat kit:

1. Register five new runtime `SkillTrigger` types in `DewResourceDatabase`.
2. Expose the Headshot identity in Lacerta's identity loadout.
3. Register the custom Memories with the active profile/content filters.
4. Detect whether Headshot is actually equipped by a human player.
5. Add/remove Q/W/E/R from the server loot pool while the identity is active.
6. Register runtime Mirror spawn handlers for the custom skill prefabs.
7. Restore Lacerta's original trait loadout and remove runtime resources on mod unload.

The active skills currently borrow selected Lacerta `TriggerConfig` data as **proxy behavior**. This is intentional: it lets us validate registration, equipping, casting and networking before replacing each proxy with Caitlyn-specific gameplay.

## Known limitations

- Q/W/E/R do not implement their final Caitlyn mechanics yet.
- Custom localization, icons, model, animations, VFX and audio are not wired yet.
- Identity gating is currently run/team-wide: if any human player equips Headshot, the four experimental Memories enter the server loot pool. Per-player eligibility still needs an interaction/loot-context filter.
- The profile registry currently adds temporary custom unlock keys while the mod is loaded; persistence behavior still needs an in-game test.

See `docs/architecture.md` and `docs/test-plan.md`.

## Asset policy

Model, animation, audio and other third-party binary assets are intentionally excluded until their redistribution terms are checked. A free/open-source project still needs to respect the license or permission attached to each community asset.

`mod-sod-lol` was created under Riot Games' "Legal Jibber Jabber" policy using assets owned by Riot Games. Riot Games does not endorse or sponsor this project.

The repository currently redistributes no Riot model/audio binaries.

## License

Source-code license is not selected yet. Do not assume third-party art/model/audio assets will share the eventual source-code license.
