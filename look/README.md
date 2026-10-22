# LOOK

Face mocap attractiveness rater (1–10). Uses [MediaPipe Face Landmarker](https://developers.google.com/edge/mediapipe/solutions/vision/face_landmarker/web_js) in the browser — 478 landmarks, on-device only.

## Open on your phone

After GitHub Pages is enabled, open:

**https://bxtters.github.io/look/**

Add to Home Screen for an app-like fullscreen experience. Allow the camera when prompted (HTTPS is required for the camera on mobile).

## Local

```bash
python3 -m http.server 8741
```

Open http://localhost:8741/ — then **Allow camera** or **Try sample face**.

## Controls

| Action | What it does |
|---|---|
| Allow camera | Live face tracking + score |
| Try sample face | Rates `samples/demo-face.jpg` without a webcam |
| Hold score | Freeze the current number |
| Flip | Front / back camera |

## Stack

- `@mediapipe/tasks-vision` Face Landmarker
- Symmetry / proportion / harmony scoring from landmarks + blendshapes
- PWA shell (`manifest.webmanifest` + `sw.js`) for mobile install
