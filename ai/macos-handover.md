# MomentTrace macOS Handover

## Purpose

Use this handover after pulling the latest `main` on a real Mac.

The current top Mac-specific priority is **Option D — macOS shortcut verification** from `ai/project-plan.md`.

## Current Goal

Re-verify whether the Avalonia shell still has the known macOS shortcut issue where `Cmd+,` does not reliably open Settings from the main window.

Also re-check the nearby shortcuts while the shell is open:

- `Cmd+,`
- `Cmd+F`
- `Cmd+1`
- `Cmd+2`
- `Cmd+W`

## What Is Already Done On Windows

These do **not** need to be repeated on the Mac unless you see a Mac-specific regression:

- Immediate refresh pass for pin, bookmark, note, delete, and view-mode changes
- Windows stability QA sweep
- README preview refresh with sanitized SVG mockups

## Known Open Mac Issue

Current known issue before this handover:

- `Cmd+,` may fail to open Settings from the main shell

Current expectation:

- Settings windows on macOS should use native traffic-light window chrome
- The main shell should use native traffic-light controls rather than the Windows custom caption buttons

## Pull And Build

From the repo root on the Mac:

```bash
git pull origin main
dotnet build ./MomentTrace.Desktop/MomentTrace.Desktop.csproj
```

If you want a full solution build on the Mac:

```bash
dotnet build MomentTrace.slnx -p:EnableWindowsTargeting=true
```

## Run

From the repo root on the Mac:

```bash
dotnet run --project ./MomentTrace.Desktop/MomentTrace.Desktop.csproj
```

## Verification Checklist

Run this in order and note pass/fail for each item.

1. Confirm the main window opens normally.
2. Confirm the main window uses native macOS traffic-light controls.
3. Press `Cmd+,` from the main window.
4. Record whether Settings opens reliably.
5. Press `Cmd+F` and confirm search focuses.
6. Press `Cmd+1` and confirm Timeline becomes active.
7. Press `Cmd+2` and confirm Overview becomes active.
8. Press `Cmd+W` and confirm the current window closes as expected.
9. If Settings opens, confirm it also uses native macOS window chrome.
10. While Settings is focused, press `Cmd+,` again and note whether it closes, reopens, or does nothing.

## What To Capture

Please capture:

- macOS version
- whether the shell built successfully
- whether the shell ran successfully
- pass/fail for each shortcut above
- whether `Cmd+,` failed always, failed intermittently, or worked reliably
- any visible chrome regressions around native traffic lights or duplicate caption buttons

## Where To Update After Verification

Once verified, update:

- `ai/stories.md`
- `ai/decisions.md`
- `README.md` Mac validation notes if behavior changed

If `Cmd+,` still fails, leave `ai/project-plan.md` with Option D as the top remaining priority.

If `Cmd+,` works reliably and the other shortcuts pass, Option D can be marked complete.
