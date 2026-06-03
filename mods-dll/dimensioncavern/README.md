# Cavern Dimension Demo

Demo consumer mod for DimensionLib. It owns the cavern terrain generator, cavern block asset, and root-only test commands that used to live inside DimensionLib while the API was being proven.

DimensionLib should remain a mechanics library. Keep cavern-specific terrain, visuals, blocks, and tuning in this mod unless a primitive clearly becomes reusable across multiple consumers.

## Commands

- `/caverndemo create [dimensionId] [sizeChunks] [seed]` creates and prepares a cavern dimension.
- `/caverndemo enter [dimensionId]` enters the cavern dimension.
- `/caverndemo prepare [dimensionId]` prepares the cavern dimension without entering it.

Default dimension id: `dimensioncavern:demo-cavern`.

## Assets

- `dimensioncavern:cavernrock` is a demo terrain block. It is intentionally not shipped by DimensionLib.
