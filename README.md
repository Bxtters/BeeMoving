# BeeMoving

Download the Unity project: [Camera_MoCap.zip](Camera_MoCap.zip)

Unzip it and open the `Camera_MoCap` folder in Unity Hub with editor **6000.0.82f1**. Do not use **Assets → Import Package**.

This repository folder is that same Unity project if you already have it checked out.

1. In Unity Hub, click **Open** and choose this folder. It contains `Assets`, `Packages`, and `ProjectSettings` side by side. Use editor **6000.0.82f1**. Do not pick 6000.5.10f1.
2. Leave the project alone until the first import finishes. It needs an internet connection once, so Package Manager can download TensorFlow Lite.
3. `Assets/Scenes/Camera_MoCap.unity` opens by itself. Press Play.

The window is the camera stage: a black field, yellow type, and the skeleton. Press **Settings** or **Tab**. Press **Pair phone** and the pairing steps appear on that card. If the skeleton is sideways, press **Q**. If your left and right feel swapped, press **F**. The stage does not show the camera picture.

## Camera

`Camera_MoCap` reads MoveNet MultiPose Lightning from `Assets/StreamingAssets/movenet_multipose.tflite`. The model reports up to six people. The stage draws shoulders, elbows, wrists, hips, knees, and ankles.

The camera stays on this machine. The picture is not shown and it is not saved. Nose, eye, and ear points are thrown away before anything is drawn. MoveNet Lightning measures a hand at the wrist. It does not output finger joints, so the display does not invent any.

The settings card is drawn in screen pixels so the type stays sharp. Black is the ground and yellow is the accent.

| Row | What it changes |
|---|---|
| Resolution | 640×480, 1280×720, or 1920×1080 |
| Frame rate | 15, 30, or 60 |
| Mirror | Flip the skeleton left and right |
| Rotate | Turn the crop 0°, 90°, 180°, or 270° |
| People | How many bodies to ask for, from 1 to 6 |
| Tracking | Relaxed, normal, or firm joint confidence |
| Camera | Turn capture off or on |

Choices are saved in PlayerPrefs on this machine. The device name is shown only inside the settings card so you can pick a camera. The stage itself says “camera 1 of 2”, not the device name.

| Key | Action |
|---|---|
| Tab | Open or close settings |
| C | Next camera |
| F | Mirror |
| Q | Rotate 90° |
| Esc | Close settings, or stop the camera |
| Enter or Space | Start the camera again |

Colors distinguish people who are on screen together. A color follows the torso for the current camera session only. It is not a name, and it is cleared when the camera stops.

## Pair a phone

**Pair phone** is the only place the steps are shown. The computer must already list the phone as a webcam. This project does not open a network stream and it does not install a driver.

**iPhone on a Mac**

1. On the iPhone, open Settings, then General, then AirPlay & Continuity.
2. Turn Continuity Camera on.
3. Sign the iPhone and the Mac into the same Apple ID.
4. Turn Wi-Fi and Bluetooth on for both. Unlock the iPhone, leave it near the Mac, and do not let it sleep.
5. Press Refresh on the pairing page and choose the iPhone.

**iPhone on Windows**

Continuity Camera is a Mac feature. Windows does not see an iPhone until a webcam app publishes it.

1. On the PC, install Iriun Webcam from [iriun.com](https://iriun.com) and open it. If Windows asks to install a camera driver, allow it.
2. On the iPhone, install Iriun Webcam from the App Store.
3. Join the same Wi-Fi on the phone and the PC. Or plug the iPhone in with a data cable and tap Trust.
4. Open Iriun on the iPhone and allow the camera.
5. Leave Iriun open on Windows so the phone is registered as a webcam.
6. Press Refresh on the pairing page and select the camera named Iriun.

**Android**

1. Install Iriun Webcam or DroidCam on the phone and on this computer.
2. Open the app on both. Allow the camera on the phone.
3. Use the same Wi-Fi, or USB when the app offers it.
4. Press Refresh and select that camera.

**USB**

1. Use a data cable, not a charge-only cable.
2. On the phone, pick USB webcam or File transfer if it asks.
3. Allow the permission prompt.
4. Press Refresh.

If the phone is not in the list yet, the stage keeps watching and selects it when it appears.

## Chromebook page

`skeleton.html` is a separate page. It shows the camera picture. The Unity scene does not.

Chrome blocks the camera if you double-click the file in Downloads. From this folder:

```bash
python3 -m http.server 8741
```

Open http://localhost:8741/skeleton.html and allow the camera. On a Chromebook, turn on Linux under Settings → Advanced → Developers if `python3` is not there yet, copy the file into the Linux files folder, and run that command in the Terminal app.

## Model

`Assets/StreamingAssets/movenet_lightning.tflite` is MoveNet SinglePose Lightning, float16. The camera scene uses `Assets/StreamingAssets/movenet_multipose.tflite`, which reports up to six people. If the multi-person file cannot be read, the stage falls back to the single-person file.

- Source: https://tfhub.dev/google/lite-model/movenet/singlepose/lightning/tflite/float16/4
- License: Apache License 2.0
- Input: `uint8 [1, 192, 192, 3]`
- Output: `float32 [1, 1, 17, 3]` as `(y, x, score)` for one person. The multi-person file returns up to six poses.

The TensorFlow Lite interpreter comes from [TensorFlow Lite for Unity](https://github.com/asus4/tf-lite-unity-sample) by Koki Ibukuro, Apache License 2.0. It is declared in `Packages/manifest.json` and is not copied into this repo.

Older lane scripts remain in the project and are not part of the build. Play starts the camera stage.

## Troubleshooting

**The project does not compile, and `TensorFlowLite` is missing.** Wait for Package Manager to finish. The scoped registry in `Packages/manifest.json` points at `https://registry.npmjs.com` for `com.github.asus4`. You need a network connection the first time you open the project.

**The skeleton is sideways or left and right are swapped.** Press Q to rotate, and F if the mirror is backwards. The skeleton is drawn on the same crop the model sees.

**No camera in the list.** Plug one in, or finish the pairing steps and press Refresh. The list also refreshes on its own.

**The phone never appears on Windows.** Continuity Camera will not show up there. Use Iriun on the PC and on the iPhone, on the same Wi-Fi or a trusted USB cable, then press Refresh.

**Pose tracking failed to load.** The status line names the reason. On a phone build the model is read out of StreamingAssets. On desktop it is read from `Assets/StreamingAssets`.
