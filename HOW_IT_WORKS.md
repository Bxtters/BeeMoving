# How Camera MoCap was built, and how it works

This is the whole story in plain language. It covers what was asked for, what was tried, why each piece exists, and what happens from the moment you press Play until the character moves.

You do not need to know Unity or machine learning to follow it.

## The idea, in one sentence

A camera on this computer watches a person. A small program guesses where the shoulders, elbows, wrists, hips, knees, and ankles are. A blocky person on a floor copies that pose. The picture stays on this computer. It is shown in a corner. It is not saved.

## What you see when it works

1. Press Play in Unity. A pale yellow start card appears.
2. Press **Start** (or Enter). The computer turns the camera on.
3. A small picture of you sits in the bottom-left, with a colored stick figure on top of it.
4. A bigger block person stands on a dark floor in the middle of the screen and copies you.
5. Press **Tab** for settings: which camera, how big the picture is, mirror, rotate, how many people, and how picky the tracker is.
6. **Pair phone** and **USB cable** are instruction pages. They do not install anything. They tell you how to make the phone show up as a normal webcam.

## The pieces, like a factory line

Think of four workers standing in a line. Each one only does one job, then hands the result to the next.

| Worker | File | What they are handed | What they hand on |
|---|---|---|---|
| The camera | `Camera_MoCap.cs` | A live picture from a webcam | A square, upright, mirrored crop |
| The pose reader | `MoveNetEstimator.cs` through `PoseEngine.cs` | That square | Up to 17 dots per person: nose, eyes, ears, shoulders, elbows, wrists, hips, knees, ankles, each with a confidence score |
| The bouncer | still `Camera_MoCap.cs` | Those dots | The same dots, but the face is erased, and a leg dot that is really an arm is thrown out |
| The puppet | `StagePuppet.cs` | The cleaned dots | A block person whose arms, legs, and head have moved |

The screens around that line live in `Camera_MoCap.Ui.cs`. The saved choices and the phone instructions live in `Camera_MoCap.Guides.cs`. They are still one Unity component. Splitting the files was only so a person can find the camera code without scrolling through button layout.

## What a "joint" is

MoveNet does not understand "a person." It returns 17 numbered dots.

| Number | Name | Used? |
|---|---|---|
| 0 | Nose | Only to aim the head. Never drawn as a face. |
| 1–4 | Eyes and ears | Thrown away. This is the privacy rule. |
| 5–6 | Shoulders | Yes. They place the chest. |
| 7–8 | Elbows | Yes. |
| 9–10 | Wrists | Yes. These are the hands. MoveNet has no fingers, so none are invented. |
| 11–12 | Hips | Yes. They are the anchor of the body. |
| 13–14 | Knees | Yes, unless the dot is really an arm. |
| 15–16 | Ankles | Yes, same rule. |

Each dot is three numbers: how far across the picture it is, how far down it is, and how sure the model is. Across and down run from 0 to 1. Down grows toward the bottom of the picture. A score of 0.9 means "I am sure." A score of 0.1 means "this is a guess," and the stage ignores it.

## One frame, slowly

### 1. The camera hands over a picture

Unity's webcam gives a rectangle of colored pixels. Phones and laptops often store that rectangle sideways or upside down. `FrameOrient` is a map. It says, "the pixel you want in the upright square is actually this pixel in the raw buffer."

The map does four things:

- Turns the picture by 0, 90, 180, or 270 degrees so the head is up.
- Cuts the middle out into a square, because MoveNet only eats squares.
- Flips left and right when mirror is on, so your left hand is on the left of the screen. That is the selfie habit.
- Remembers the map until the camera size or the rotation changes, so it does not recompute hundreds of thousands of pixels every frame for no reason.

The square is 384 pixels on a side. MoveNet itself wants 192 (one person) or 256 (several people). The estimator shrinks the 384 square down. Showing the 384 square in the corner keeps the picture sharper than the tiny model input.

### 2. MoveNet guesses the dots

`PoseEngine` is a translator. The camera code does not talk to TensorFlow directly. It asks `PoseEngine`, and `PoseEngine` finds `MoveNetEstimator` only if the TensorFlow package finished downloading. If the package is missing, the stage stays open and the status line says why. It does not crash.

One person uses `movenet_lightning.tflite`. Two to six people use `movenet_multipose.tflite`. The six-person file can see six bodies. We keep only as many as the People dropdown asks for, and we keep the ones the model is most sure about.

The model writes each dot as "down, across, score." The estimator swaps the first two numbers so the rest of the program can say "across, then down," which matches how we draw.

After the dots are copied out, the picture bytes and the raw model output are wiped. The only thing kept is the list of dots.

### 3. The face is removed, and arms are not allowed to be legs

Before anything is drawn:

- Nose, eyes, and ears are set back to empty. The head block still exists. It aims from the shoulders toward the nose, but no eyes or mouth are drawn.
- If a knee or an ankle is above the shoulders, it is not a leg. It is up by the head. That reading is thrown away.
- If a knee or an ankle is in the upper half of the body and is closer to an elbow or a wrist than to its own hip, it is the arm being counted twice. It is thrown away. The arm keeps the dot. The leg eases back down to standing.

That second rule exists because a raised arm used to drag a leg up to the head. A real high kick stays, because that knee is still below the chest and closer to the hip than to the hand.

### 4. The block person is already standing

The character is built when the app starts, before anyone is in frame. It is a stack of boxes: pelvis, chest, neck, head, upper arms, forearms, hands, thighs, shins, feet. It stands on a flat floor. If the camera sees nobody, it stays in that rest pose. Parts do not vanish.

Bone lengths are fixed. An upper arm is 30 centimeters. A forearm is 27. A thigh is 42. A shin is 40. The picture only has across and down, not depth. The missing length is turned into "toward the camera." That is how a punch comes out of the screen instead of staying a flat stick.

### 5. How an arm decides where to be

For each arm:

- The shoulder has already moved to where the camera saw it.
- The elbow is placed one upper-arm length from that shoulder, toward the elbow dot. If the dot is close to the shoulder in the picture, the leftover length comes forward.
- If the wrist is almost a full arm's length from the shoulder in the picture, the arm is straight. The elbow is pinned onto the line between shoulder and wrist. A noisy elbow dot cannot kink a straight arm. That was the "elbow bugs out" bug.
- If the hand's picture position sits over the chest, the hand and the elbow are pushed in front of the chest, toward the camera.
- If the hand sits over the face, they are pushed in front of the head.
- Reaching behind the back is not specially handled. Arms are less able to do that, and the picture cannot tell "in front" from "behind" when the hand is over the body. We always choose in front.

Then the boxes are drawn between those points. The box length is the distance between the joints, so the elbow stays connected to both the upper arm and the forearm.

### 6. Motion is eased, not snapped

MoveNet sends a new guess many times a second, and each guess jitters a little. If the character jumped to every guess, it would buzz.

Each joint remembers how fast it was moving. The next frame does not teleport. It speeds up toward the new spot and then slows down as it arrives. Hips and shoulders lead. Hands and feet take a little longer, so they trail, the way a real swing has follow-through. If a joint disappears, it glides back to the standing pose over about a fifth of a second. It is not frozen in the last weird position. That frozen-leg bug is why the "stay forever" idea was thrown out.

The whole body also slides a little left or right on the floor as you step sideways, but it cannot walk off the plane.

### 7. The corner picture

The same square MoveNet saw is drawn in the bottom-left card, flipped upright. The stick figure uses the same dots, so if the stick figure matches your body, the block person is reading the same pose. Facial bones are not drawn there either.

## The screens

`Camera_MoCap.Ui.cs` builds every rectangle, label, and dropdown. The look is a pale yellow sheet with a thin gold edge, closer to a simple web app than to a game HUD.

| Control | What it changes |
|---|---|
| Start | Turns the camera on and hides the title card. The character was already there. |
| Settings | Opens the card. Tab does the same thing. |
| Resolution | Auto, 640×480, 1280×720, or 1920×1080. If the camera refuses a size, the stage tries a smaller one. |
| Frame rate | 15, 30, or 60 pictures a second. |
| Mirror | Flips left and right. |
| Rotate | Turns the crop by 90 degrees when the picture is sideways. |
| People | 1 through 6. One person uses the small model. More than one uses the six-person model. |
| Tracking | How sure a dot must be before it counts. Relaxed keeps more dots. Firm ignores shaky ones. |
| Pair phone / USB cable | Shows instructions. They do not open a network stream and they do not install a driver. |

Choices are saved on this computer in PlayerPrefs, under keys that start with `CameraMoCap.`. That is `Camera_MoCap.Guides.cs`. The pairing pages are long strings of steps for an iPhone on a Mac, an iPhone on Windows, and Android. The Windows cable path needs Apple's phone service and then Iriun, because Windows does not see an iPhone as a camera by itself.

## Why it was built in this order

Each change answered a problem the previous version actually had. The thought process was always: name the failure in one sentence, then change only the part that caused it.

1. **A camera stage, not a game.** The repo began as a lane runner. The request was a motion-capture display. The runner was left in the project at first and simply not started, so the pose code could be reused. Later, once nothing in the camera stage called it, the runner files and the old lane scene were deleted so they could not be started by accident.

2. **A real Unity project you can open.** A loose script is not a project. The folder needed `Assets`, `Packages`, `ProjectSettings`, the MoveNet files, and a scene. Editor `6000.0.82f1` is pinned because a newer editor would retarget the project. The zip is that same folder, so you can download it and open it in Unity Hub. Do not use Import Package. That would nest a project inside a project.

3. **Settings you can actually click.** The first settings page was blurry, dark, and sometimes drawn off the screen, so the dropdowns could not be used. The fix was a normal Unity canvas: real buttons, a scroll view when the list is taller than the window, and a light yellow card with a hard edge so it reads as a sheet on the page. The Input System needs its own event module or the buttons look fine and do nothing.

4. **The picture has to be the same picture the model sees.** Early on, the Game view stayed empty or the skeleton did not sit on the body. The cause was drawing a different crop than the one MoveNet read. The corner card now shows that exact square. The 3D view is a separate camera looking at the floor.

5. **Privacy is a delete, not a promise.** "We will not draw the face" is weaker than "the face numbers are wiped before anything else can read them." Nose, eyes, and ears are cleared as soon as the model returns. The nose is remembered only long enough to aim the head block, and it is not drawn.

6. **A character, not a cloud of dots.** The first 3D attempts placed every joint wherever the picture said, with made-up depth. Bones stretched. The chest fell sideways. The head vanished. The redo was a person made of fixed-length boxes who is standing before anyone is detected. The picture is allowed to rotate a bone. It is not allowed to change how long the bone is.

7. **Depth without lying.** A flat picture cannot see depth. If we invent depth for every joint separately, the body tears apart. The rule now is: keep the on-screen position, and spend only the *leftover* bone length on depth, toward the camera. A punch gets shorter in the picture and longer toward you. A T-pose stays in the picture plane because the picture already shows the whole arm.

8. **Do not freeze a bad pose.** An early request was "if you cannot see it, leave it where it was." That felt safe, and then a leg stuck in a kick after it left the frame. Lost joints now glide home. The character never disappears. Only the unseen part returns to standing.

9. **The elbow is a hinge, not a free dot.** When the arm is almost straight, a tiny error in the elbow dot used to throw the joint at the camera. Straight arms now lock the elbow onto the shoulder-to-wrist line. A real bend, where the wrist comes back toward the body, still follows the elbow.

10. **Fluid means "remember your speed."** Snapping to each new dot is accurate and ugly. A plain fade is smooth and late. Remembering velocity lets a limb accelerate into a reach and settle when it arrives. Hands and feet are given a little extra time so they follow the limb instead of arriving first.

11. **One limb, one label.** MoveNet sometimes reports the same raised arm as an arm and as a leg. The leg then reaches the head. The filter keeps the arm and drops the leg point. The thought was: a knee above the shoulders is impossible for this standing character, and a knee that is closer to the wrist than to the hip is the arm.

12. **In front of the body is a special case.** Hands clasped at the chest still look fairly long in the picture, elbow out and wrist back, so the leftover-length trick does not push them forward. If the hand's dot lies on top of the chest or the face, we place it in front on purpose. Behind the back is ignored because the picture cannot tell the two apart and the body cannot reach back as far.

13. **Comments, then a split.** After the behavior settled, every source file was commented with what that piece adds. Then the one giant file was split. The scene still points at one component. The other two files are the rest of that same component. That avoids a new scene setup and avoids a second object that could forget to start.

14. **Delete the runner only after nothing calls it.** Deleting it earlier would have broken the pose types the stage still shared. The last step checked every remaining file for the runner's names, removed the "if the runner is open, do not start the camera" line, and took the old scene out of the build list.

## What is still in the project, and why

| File | Why it is still here |
|---|---|
| `Camera_MoCap.cs` | Camera, crop, pose cleanup, corner skeleton, and the call into the character. |
| `Camera_MoCap.Ui.cs` | Start card, settings, dropdowns, buttons. |
| `Camera_MoCap.Guides.cs` | Saved settings and the phone instructions. |
| `StagePuppet.cs` | The block person and the floor. |
| `PoseEngine.cs` | The bridge to TensorFlow, so a missing plugin does not crash Play. |
| `MoveNetEstimator.cs` | The actual model run. |
| `FrameOrient.cs` | Turns a sideways webcam buffer into the upright square. |
| `Joints.cs` | The 17 dot names. |
| `Keys.cs` | Keyboard presses. |
| `OpenHandlineScene.cs` | In the editor only. Opens the camera scene if you have not opened one yet. |
| `skeleton.html` | A separate browser page. Unity does not run it. |
| `tests/GestureTests/FrameOrientTests.cs` | Checks the crop map. The old gesture and runner tests were deleted with the runner. |

`Packages/manifest.json` and the assembly files are JSON. A comment inside them would stop Unity from reading them, so they have no comments.

## What was deliberately not done

- Fingers. The model does not measure them.
- A drawn face. The dots are deleted.
- Saving or uploading the picture.
- Reaching behind the back as its own pose.
- The lane runner. It is gone, not hidden.
