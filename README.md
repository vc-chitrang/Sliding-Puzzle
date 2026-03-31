# Sliding Puzzle

## Overview

This Unity game module is a sliding puzzle built from a single tile prefab, one material, and one active source image. The board is generated at runtime by `Scripts/GameManager.cs`, which creates the pieces, remaps their UVs, handles click-to-slide movement, checks for completion, and shuffles the board.

The current version now supports dynamic image-driven layout selection:

- landscape images use a `4 x 3` grid
- portrait images use a `3 x 4` grid
- square images use a `4 x 4` grid

The puzzle reads the active texture from the material assigned to the tile prefab and scales the board to preserve the image aspect ratio without stretching.

## Folder Structure

- `Materials/ImageMaterial.mat`
  Main puzzle material. Its `mainTexture` is the image source used by the board at runtime.
- `Prefabs/GamePiece.prefab`
  Quad-based tile prefab with a `MeshRenderer` and `BoxCollider2D`.
- `Scenes/SampleScene.unity`
  Scene containing `Main Camera`, `GameBoard`, and `GameManager`.
- `Scripts/GameManager.cs`
  Main gameplay script for generation, movement, completion detection, shuffling, and UV slicing.
- `Textures/`
  Image assets available for the puzzle. The runtime uses whichever texture is currently assigned to `ImageMaterial.mat`.

## Scene Setup

`SampleScene.unity` contains three root objects:

- `Main Camera`
  Orthographic camera at `(0, 0, -10)`.
- `GameBoard`
  Parent transform used for all runtime-created pieces.
- `GameManager`
  Holds the `GameManager` MonoBehaviour.

The `GameManager` component references:

- `gameTransform` -> `GameBoard`
- `piecePrefab` -> `Prefabs/GamePiece.prefab`

## Prefab Setup

`GamePiece.prefab` contains:

- `Transform`
- `MeshFilter`
  Uses Unity's built-in quad mesh.
- `MeshRenderer`
  Uses `Materials/ImageMaterial.mat`.
- `BoxCollider2D`
  Allows mouse clicks to hit the tile through `Physics2D.Raycast`.

There is no script on the tile prefab. All behavior is controlled from `GameManager`.

## Runtime Flow

### 1. Read the active texture

At startup, `GameManager` reads `piecePrefab.GetComponent<MeshRenderer>().sharedMaterial.mainTexture`.

From that texture it uses:

- `texture.width`
- `texture.height`

It logs the detected resolution and selected grid shape with:

`Debug.Log($"Resolution: {width}x{height}, Grid: {size}");`

### 2. Pick the puzzle grid

The board now uses `Vector2Int size` instead of a single integer:

- `4 x 3` when `width > height`
- `3 x 4` when `height > width`
- `4 x 4` when `width == height`

This keeps the layout aligned with the image orientation.

### 3. Preserve image aspect ratio

The board scale is adjusted from the texture aspect ratio before pieces are created:

- landscape images keep full board width and reduce board height
- portrait images keep full board height and reduce board width
- square images keep both dimensions equal

This means the puzzle fits within the existing board bounds while preserving the source image proportions. The image is not stretched.

### 4. Build the pieces

`CreateGamePieces` now works with rectangular grids:

- loops run across `size.x` columns and `size.y` rows
- total piece count is `size.x * size.y`
- the bottom-right tile becomes the empty slot
- each tile position is calculated from per-cell width and height
- each tile scale is calculated independently on X and Y so non-square boards still fit correctly

### 5. Slice the image with UVs

Every tile still shares the same material and texture. The script updates UVs per tile so each piece shows only its own portion of the image:

- `uvWidth = 1f / size.x`
- `uvHeight = 1f / size.y`

This allows the system to support `4 x 3`, `3 x 4`, and `4 x 4` without needing multiple materials or separate texture assets per tile.

### 6. Move and shuffle

The movement system still uses the same core logic:

- click a tile
- raycast to detect the selected piece
- allow moves only into the empty slot
- prevent horizontal row wrapping
- swap both the logical list entries and the local positions

The only structural change is that vertical movement now uses `size.x` as the row stride, which keeps movement valid for rectangular boards.

Shuffling also remains move-based, so the board stays reachable while supporting non-square layouts.

## Current Functionality Summary

The current implementation supports:

- dynamic grid selection from the active image resolution
- `Vector2Int` board sizing
- aspect-ratio-preserving board scaling
- UV-based image slicing from one shared material
- rectangular puzzle layouts (`4 x 3` and `3 x 4`)
- square puzzle layout (`4 x 4`)
- click-to-slide movement
- automatic solved-state detection
- automatic reshuffling after startup and after every solve

## Current Limitations

- The runtime uses the texture currently assigned to `ImageMaterial.mat`; it does not yet randomly choose from the `Textures` folder.
- Input is still mouse-based only.
- There is still no dedicated win UI, timer, move counter, or restart menu.
- The project has not been validated through a live Unity MCP session in this change because no Unity Editor instance was connected at the time of implementation.

## Practical Next Steps

Natural follow-up improvements would be:

- random texture selection from the available images at startup
- touch input support for mobile
- a visible solved state instead of immediate reshuffle
- UI for restart, shuffle, move count, and elapsed time
- a small image-selection system so level art can be swapped without editing the material manually

## Verification Notes

This README reflects the current file-level implementation.

At the time of this update:

- the Unity MCP server resources were available
- no active Unity Editor instance was connected, so in-editor compile and scene verification could not be run
- local file validation and code review were used instead
