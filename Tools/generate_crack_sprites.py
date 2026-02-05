#!/usr/bin/env python3
"""Generate 10 crack stage sprites (16x16 RGBA) for block breaking overlay."""

import os
import random
from PIL import Image


def generate_full_crack_pattern(width, height, seed=42):
    """
    Generate a full crack pattern using random walks from multiple origins.
    Returns a list of (x, y) tuples ordered by when they should appear.
    """
    random.seed(seed)

    ordered_pixels = []
    visited = set()

    # Start 4 crack lines from random edges
    origins = []
    for _ in range(4):
        side = random.randint(0, 3)
        if side == 0:
            origins.append((random.randint(0, width - 1), 0))
        elif side == 1:
            origins.append((random.randint(0, width - 1), height - 1))
        elif side == 2:
            origins.append((0, random.randint(0, height - 1)))
        else:
            origins.append((width - 1, random.randint(0, height - 1)))

    # Each origin does a random walk across the tile
    for ox, oy in origins:
        x, y = ox, oy
        walk_length = width * height // 2
        for step in range(walk_length):
            if (x, y) not in visited:
                visited.add((x, y))
                ordered_pixels.append((x, y))

            dx = random.choice([-1, -1, 0, 0, 1, 1, 1])
            dy = random.choice([-1, -1, 0, 0, 1, 1, 1])
            if dx == 0 and dy == 0:
                dx = random.choice([-1, 1])

            x = max(0, min(width - 1, x + dx))
            y = max(0, min(height - 1, y + dy))

            # Occasionally branch
            if step > 0 and step % 12 == 0:
                bx = max(0, min(width - 1, x + random.choice([-2, -1, 1, 2])))
                by = max(0, min(height - 1, y + random.choice([-2, -1, 1, 2])))
                if (bx, by) not in visited:
                    visited.add((bx, by))
                    ordered_pixels.append((bx, by))

    # Fill remaining pixels randomly (for high stages)
    all_pixels = [(px, py) for px in range(width) for py in range(height)]
    random.shuffle(all_pixels)
    for p in all_pixels:
        if p not in visited:
            ordered_pixels.append(p)

    return ordered_pixels


def generate_crack_sprites(output_dir):
    """Generate 10 crack stage sprites with progressive cracking."""
    os.makedirs(output_dir, exist_ok=True)

    width, height = 16, 16
    total_pixels = width * height

    pattern = generate_full_crack_pattern(width, height)

    # Stage coverage: stage 0 ~6%, stage 9 ~65%
    stage_coverage = [
        int(total_pixels * pct)
        for pct in [0.06, 0.10, 0.15, 0.21, 0.28, 0.35, 0.42, 0.50, 0.58, 0.65]
    ]

    for stage in range(10):
        img = Image.new("RGBA", (width, height), (0, 0, 0, 0))

        num_pixels = stage_coverage[stage]
        crack_alpha = min(255, 120 + stage * 15)

        for i in range(min(num_pixels, len(pattern))):
            x, y = pattern[i]
            img.putpixel((x, y), (0, 0, 0, crack_alpha))

        filename = f"crack_stage_{stage}.png"
        img.save(os.path.join(output_dir, filename))

        actual_opaque = sum(
            1
            for cx in range(width)
            for cy in range(height)
            if img.getpixel((cx, cy))[3] > 0
        )
        print(
            f"  {filename}: {actual_opaque}/{total_pixels} crack pixels "
            f"({100 * actual_opaque // total_pixels}%), alpha={crack_alpha}"
        )

    print(f"\nGenerated 10 crack sprites in {output_dir}")


if __name__ == "__main__":
    generate_crack_sprites("Assets/_Project/Art/Textures/Blocks/Crack")
