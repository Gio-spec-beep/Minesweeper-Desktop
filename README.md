# Mines

A native Windows game with a polished dark interface, animated tile hover, three difficulty levels, timer, flags, first-click safety, and best-time tracking.

## Download the Windows app

1. Open the repository's **Actions** tab.
2. Select **Build Mines for Windows**.
3. Open the latest successful run.
4. Under **Artifacts**, download **Mines-Windows-x64**.
5. Extract the ZIP and run `Mines.exe`.

The app is built as a self-contained Windows x64 executable. You do not need to install Python or .NET.

## Play

- Left-click a tile to reveal it.
- Right-click a tile to place/remove a flag.
- The first reveal is always safe.
- Choose Beginner, Intermediate, or Expert at the top.
- The smiley button restarts the current difficulty.

GitHub Actions builds a fresh EXE on each push to the default branch, and can also be started manually from Actions.