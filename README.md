# Sliding Puzzle

This module is set up as a dynamic image-based sliding puzzle that builds its board at runtime from the textures inside `Textures/`.

## Features

- Dynamic grid selection from texture aspect ratio:
  - landscape images -> `4 x 3`
  - portrait images -> `3 x 4`
  - square images -> `4 x 4`
- Single-material UV slicing for all tiles
- Preserved image aspect ratio on the game board
- Two movement inputs:
  - click/tap tiles adjacent to the empty slot
  - press the green arrow buttons around the empty slot
- Touch and mouse input use the same move validation rules
- Runtime shuffle using only valid moves to keep boards solvable
- Timer that starts on the first player move
- Best-time tracking per image and grid size with `PlayerPrefs`
- Runtime UI with title, timer, best score, reset, preview, and random-image switching
- Full-image preview overlay that temporarily pauses interaction and keeps the source image aspect ratio

## Runtime Scripts

- `Scripts/GameManager.cs`
  - Loads textures from `Assets/Games/Sliding-Puzzle/Textures`
  - Chooses the correct grid size for the active image
  - Builds and shuffles the board
  - Handles tile clicks, move validation, timer flow, and solved-state checks
- `Scripts/TileController.cs`
  - Owns per-tile UV slicing and animated movement
- `Scripts/ArrowController.cs`
  - Renders the directional arrow buttons and click handlers
- `Scripts/UIManager.cs`
  - Builds the full puzzle HUD and preview overlay at runtime

## Scene Expectations

- `GameManager` stays on the `GameManager` scene object
- `GameBoard` remains the parent for spawned tiles
- `GamePiece` continues to provide:
  - a quad mesh
  - `MeshRenderer`
  - `BoxCollider2D`
- `Materials/ImageMaterial.mat` remains the source material used for the runtime material instance

## Controls

- Left click / tap an adjacent tile to slide it into the empty slot
- Use the green arrow buttons to move a valid neighbor into the empty slot
- `Reset` reshuffles the current image
- `Preview` opens the full image overlay
- `New Image` swaps to another image from `Textures/` and rebuilds the puzzle
