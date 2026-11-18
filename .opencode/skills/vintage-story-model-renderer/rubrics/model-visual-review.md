# Vintage Story model visual review

Review all eight views in wireframe, material-ID, and textured modes.

- Fail for a clearly missing or reversed visible face, unintended clipping/interpenetration, a disconnected or floating
  structural part, a visible gap where two authored parts should meet, or a frozen/camera-jumping video.
- Fail for visible Z-fighting or a UV/texture defect that makes the intended material identity unreadable.
- Flag for human review, rather than failing, when proportions, construction plausibility, or visual taste are debatable.
- Check opposing views for one-sided rendering and inconsistent color. Check both isometrics for silhouette and depth.
- In video, cite timestamps for camera discontinuity, animation-loop discontinuity, body rotation confused with camera
  motion, or geometry that clips only during motion.
- Do not fail intended internal joints, ordinary shared edges, fallback lighting differences, fixed-view label overlays,
  or limitations already declared in renderer metadata.
- Do not infer collision, selection, mechanical alignment, handbook appearance, inventory transforms, or in-game lighting
  unless the supplied representation explicitly depicts them.
