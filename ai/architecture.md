# MomentTrace Architecture

## High-Level Structure

- repository root: source code, stable product docs, and release/build notes
- `ai/`: planning and continuity docs for agent-driven work
- `README.md`: stable project orientation and user/developer setup

## Runtime Responsibilities

### Avalonia desktop shell

- `MomentTrace.Desktop` is the main current UI surface.
- Renders Timeline, Overview, Settings, custom chrome, detail pane, shell navigation, and reusable Avalonia controls.
- Binds shared presentation models from `MomentTrace.Presentation`.
- Owns desktop interaction state such as selected date, filters, detail pane visibility, and Overview drill-ins.

### Presentation layer

- `MomentTrace.Presentation` transforms stored activities into display-ready models.
- Owns timeline grouping, moment rows, detail context, overview metrics, heatmap blocks, category breakdowns, focus rhythm blocks, and top applications.
- Keep this platform-neutral so Windows and future macOS shells can reuse it.

### Core domain

- `MomentTrace.Core` owns shared models, activity kinds/categories, local classifiers, summarizers, search contracts, privacy filters, and optional AI provider abstractions.
- Current product behavior is local-first and deterministic; AI remains optional and hidden unless a provider exists.

### Storage

- `MomentTrace.Storage` owns local SQLite persistence for captured activity history.
- Activity history, screenshots, settings, and privacy blocklists remain local to the user machine.

### Windows integration

- `MomentTrace.Windows` owns Windows foreground recording, startup registration, and Windows-specific helpers such as icon/provider behavior.
- WPF-specific tray/installer ownership remains in `MomentTrace.App` while Avalonia shell parity is active.

### macOS Platform Service

- `MomentTrace.Mac` owns macOS foreground recording and capture behavior for the Avalonia desktop shell.
- It validates bundle IDs, app identity, browser URLs, screenshot capture behavior, and shared SQLite persistence through the cross-platform desktop host.

## Design Constraints

- Prioritize `MomentTrace.Desktop` for UI polish.
- Keep platform-neutral shaping in `MomentTrace.Presentation`.
- Keep recorder/platform APIs out of Avalonia controls.
- Keep shell adapters thin: adapt controls, icons, images, and platform behavior around shared presentation output.
- Keep captured data local-first unless a future feature explicitly asks for sync.
- Do not show AI surfaces when no AI provider is registered.
- Empty, sparse, normal, and dense days should all feel deliberately designed.

## Code Style

- Avoid new god files. `MainWindow.axaml.cs` is already large, so prefer extracting focused controls/services when adding meaningful new behavior.
- Split code by responsibility: capture, storage, presentation shaping, shell interaction, reusable controls, and platform adapters.
- Prefer existing local patterns before adding new abstractions.
- Add comments only where the reason is not obvious.
- Keep XAML layout changes scoped and verify resize/chrome behavior after shell edits.
- Do not commit `bin`, `obj`, `.vs`, `publish`, `dist`, screenshots from local user data, or local secrets.

## Important Paths

| Area | Path |
| --- | --- |
| Main shell | `MomentTrace.Desktop/MainWindow.axaml` |
| Main shell code-behind | `MomentTrace.Desktop/MainWindow.axaml.cs` |
| Overview builder | `MomentTrace.Presentation/OverviewPresentationBuilder.cs` |
| Overview model | `MomentTrace.Presentation/OverviewPresentationModel.cs` |
| Timeline builder | `MomentTrace.Presentation/TimelinePresentationBuilder.cs` |
| Windows recorder | `MomentTrace.Windows/` |
| Storage | `MomentTrace.Storage/` |
| Core rules/models | `MomentTrace.Core/` |
| Stable project docs | `README.md` |
| Active plan | `ai/project-plan.md` |

