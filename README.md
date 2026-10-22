# BeeMoving

Download the Unity project: [Camera_MoCap.zip](Camera_MoCap.zip)

Unzip it and open the `Camera_MoCap` folder in Unity Hub with editor **6000.0.82f1**. Do not use **Assets → Import Package**.

This repository folder is that same Unity project if you already have it checked out.

1. In Unity Hub, click **Open** and choose this folder. It contains `Assets`, `Packages`, and `ProjectSettings` side by side. Use editor **6000.0.82f1**. Do not pick 6000.5.10f1.
2. Leave the project alone until the first import finishes. It needs an internet connection once, so Package Manager can download TensorFlow Lite.
3. `Assets/Scenes/Camera_MoCap.unity` opens by itself. Press Play.

Turn Continuity Camera on before you press Play, then press **C** to use the phone. If the skeleton is sideways, press **Q**. If your left and right hands feel swapped, press **F**. The stage does not show the camera picture.

## Motion capture

`Assets/Scenes/Camera_MoCap.unity` is the motion-capture scene. The `Camera_MoCap` object in that scene reads MoveNet MultiPose Lightning from `Assets/StreamingAssets/movenet_multipose.tflite`. The model reports up to six people. The stage draws shoulders, elbows, wrists, hips, knees, and ankles.

The camera stays on this machine. The picture is not shown and it is not saved. Nose, eye, and ear points are thrown away before anything is drawn. MoveNet Lightning measures a hand at the wrist. It does not output finger joints, so the display does not invent any.

| Key | Action |
|---|---|
| C | Next camera |
| F | Mirror |
| Q | Rotate 90° |
| Esc | Stop the camera and release the pose model |
| Enter or Space | Start the camera again |

Colors distinguish people who are on screen together. A color follows the torso for the current camera session only. It is not a name, and it is cleared when the camera stops.

The lane is `Assets/Scenes/Main.unity`. Open that scene and press Play to run it.

## Chromebook skeleton

`skeleton.html` is a separate page. It shows the camera picture. The Unity scene does not.

Chrome blocks the camera if you double-click the file in Downloads. From this folder:

```bash
python3 -m http.server 8741
```

Open http://localhost:8741/skeleton.html and allow the camera. On a Chromebook, turn on Linux under Settings → Advanced → Developers if `python3` is not there yet, copy the file into the Linux files folder, and run that command in the Terminal app.

## The game

A three-lane runner controlled by a camera. MoveNet Lightning (TensorFlow Lite) reads the person in frame, and their hands become the controls: jump, sprint, slide, and lane changes.

Any camera the operating system exposes will do. Built-in webcams, USB cameras, phone cameras in a player build, and virtual cameras such as OBS all show up in the camera list.

## Play the lane

Open `Assets/Scenes/Main.unity` and press Play.

The first time, Unity downloads the TensorFlow Lite plugin (`com.github.asus4.tflite` 2.21.0-rc1) from npm. That import is what makes the pose model run.

Allow the camera when the OS asks. Stand far enough back that your head, both hands, and hips are in the preview. The skeleton should sit on your body. If it does not, press **F** to flip the mirror or **Q** to rotate the image 90 degrees, then **C** to try the next camera.

Raise both hands, or press Space, to start.

| What you do | What the runner does |
|---|---|
| Both hands above your head | Jump |
| Pump your arms, opposite arms trading places | Sprint |
| Clasp both hands together below your hips | Slide |
| Reach one hand out to the side | Move one lane that way |

Lower the reaching hand before you reach again. Holding it does not keep stepping. A jump starts when your hands go up, so put them down before the next jump.

The track throws three kinds of obstacles:

- **Crates** — jump
- **Teal bars** — slide
- **Gray walls** — change lane

Coins are worth 25. Sprinting covers more ground and the track gets faster. Three hits end the run. Hold both hands up, or press Space, to run again.

Keyboard still works if the camera is missing or you want to practice the course:

| Key | Action |
|---|---|
| A / D or arrows | Change lane |
| Space, W, or Up | Jump |
| S, Down, or Ctrl | Slide |
| Shift | Sprint |
| Esc | Pause |
| F | Mirror the camera |
| Q | Rotate the camera 90° |
| C | Next camera |
| R or Space on the result | Run again |

Landscape is the comfortable layout. The preview stays in the lower left.

## How a frame becomes a button

1. The camera frame is rotated upright, center-cropped to a square, and mirrored so your left hand is on the left of the preview.
2. That square is scaled to the model input, 192×192 RGB.
3. MoveNet SinglePose Lightning returns 17 body points. Each point is `(y, x, score)`, origin at the top left.
4. `GestureClassifier` watches the wrists: hands over the nose, hands clasped under the hips, a hand outside the shoulders, and arms swinging in opposite directions.
5. `RunnerSim` turns that into a jump, a slide, a single lane step, and a sprint multiplier.

The model sees wrists, elbows, and the rest of the body. It does not see individual fingers. The gestures are whole-hand poses, which is what Lightning can track fast enough for a game.

The rules live in `Assets/Scripts/Handline.Core` and do not reference Unity, so they can be tested on their own:

```bash
dotnet test tests/GestureTests
```

Tuning constants are at the top of `GestureClassifier` and `RunnerSim`.

## Model

`Assets/StreamingAssets/movenet_lightning.tflite` is MoveNet SinglePose Lightning, float16. The motion-capture scene uses `Assets/StreamingAssets/movenet_multipose.tflite`, which reports up to six people.

- Source: https://tfhub.dev/google/lite-model/movenet/singlepose/lightning/tflite/float16/4
- License: Apache License 2.0
- Input: `uint8 [1, 192, 192, 3]`
- Output: `float32 [1, 1, 17, 3]` as `(y, x, score)`

The TensorFlow Lite interpreter comes from [TensorFlow Lite for Unity](https://github.com/asus4/tf-lite-unity-sample) by Koki Ibukuro, Apache License 2.0. It is declared in `Packages/manifest.json` and is not copied into this repo.

## Troubleshooting

**The project does not compile, and `TensorFlowLite` is missing.** Wait for Package Manager to finish. The scoped registry in `Packages/manifest.json` points at `https://registry.npmjs.com` for `com.github.asus4`. You need a network connection the first time you open the project.

**The preview is sideways or your left hand moves you right.** Press Q to rotate, and F if the mirror is backwards. The skeleton is drawn on the same crop the model sees, so once the skeleton matches your body the lanes will too.

**No camera in the list.** Plug one in or start a virtual camera, then wait a moment. The list refreshes on its own. The keyboard controls still run the lane.

**Pose tracking failed to load.** The status line under the preview shows the reason. On a phone build the model is read out of StreamingAssets. On desktop it is read from `Assets/StreamingAssets`.
