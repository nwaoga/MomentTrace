# MomentTrace Prompts

## Project Guardrails

- Treat `MomentTrace.Desktop` as the main current UI surface.
- Keep stable project docs in `README.md`; keep active planning and continuity in `ai/`.
- Keep platform-neutral timeline and overview shaping in `MomentTrace.Presentation`.
- Keep recorder/platform APIs in platform projects, not UI controls.
- Keep captured data local-first.
- Keep optional AI features hidden unless an `IAiProvider` exists.
- Prefer small incremental stories and commits.
- Preserve the current visual direction unless the user explicitly asks for a redesign.
- Do not commit `bin`, `obj`, `.vs`, `publish`, `dist`, local screenshots, or local secrets.

## Current Prompt Focus

- Immediate refresh pass for pin, bookmark, note, delete, and view-switch changes across Timeline and Overview.
- Stability QA sweep across empty, sparse, normal, and dense data states.
- Windows custom chrome must keep resize handles and avoid duplicate native caption buttons.
- macOS `Cmd+,` Settings shortcut still needs real-device verification.

## Handoff Summary

- Product name: MomentTrace.
- Theme direction: local-first memory timeline with a polished Windows/Avalonia shell.
- Current milestone: late-alpha hardening and polish.
- Main shell: `MomentTrace.Desktop/MainWindow.axaml` and `MainWindow.axaml.cs`.
- Presentation: `MomentTrace.Presentation` owns timeline/overview/detail shaping.
- Storage: local SQLite through `MomentTrace.Storage`.
- Recorder: Windows foreground recorder through `MomentTrace.Windows`; macOS capture remains exploratory in `MomentTrace.MacProbe`.

## Useful Start Prompt

```text
Read ai/project-plan.md, ai/architecture.md, ai/stories.md, and ai/decisions.md.
Pick the next highest-priority story, inspect the relevant code first, and keep changes scoped.
Run the narrowest useful build/test command before summarizing.
```
