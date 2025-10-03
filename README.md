# HaptiX  
Real-time multi-hand haptic interaction system using smartphones and Unity3D. Turns everyday phones into intuitive, low-cost haptic controllers.

<img width="1281" height="652" alt="fig  3" src="https://github.com/user-attachments/assets/61ec5100-5818-4beb-847d-c61cf22b81a7" />

## Overview
HaptiX is a smartphone-based real-time haptic interaction system. Instead of relying on costly, cumbersome haptic devices, HaptiX uses everyday phones to control skinned 3D hand models in Unity3D. Pinch, drag, and rotation gestures are transmitted via UDP, enabling translation, rotation, and grasp/release. Bidirectional haptic feedback—phone vibration and sound—provides immersive touch cues. With two phones, users can independently control left and right hands for coordinated bimanual interaction.


## Features
- #### Smartphone-based haptic control
  Uses smartphones as controllers without requiring expensive haptic devices.
- #### Multi-hand coordination with two smartphones
  Each smartphone independently controls left or right hand for coordinated bimanual interaction.
- #### Bidirectional haptic feedback
  When collisions occur, smartphones provide vibration and audio feedback for immersive touch experience.
- #### Real-time UDP communication
  Ensures low-latency transmission of gestures and feedback between smartphones and Unity3D.
- #### Convex collider generation with V-HACD
  Skinned hand meshes are decomposed into convex colliders for robust, physics-based contact handling.


## Pipeline
The system operates in the following steps:

<img width="1239" height="328" alt="슬라이드1" src="https://github.com/user-attachments/assets/224cde9f-8504-4b39-9893-4c2bc336b845" />

### Step 1 - Smartphone Input/Output
Detect multi-touch gestures (pinch, drag, rotation). Convert them into control commands with selected hand (Left/Right) and axis mode (Y or Z). When the touch ends, the state returns to Release. Phones also receive haptic commands to trigger vibration and audio.

### Step 2 - UDP Communication
Transmit gesture packets from smartphone to Unity and return haptic events from Unity to the smartphone over the same UDP channel for low-latency, bidirectional updates.

### Step 3 - Unity Processing & Hand Model
The UDP receiver syncs packets to the main thread, maps gestures to hand translation/rotation, and switches states (Grab/Release). The hand animation controller plays context-appropriate motions for natural interaction.

### Step 4 - Interaction & Feedback Generation
Bake the skinned hand mesh and use V-HACD convex colliders for robust physics contact. On Grab, objects follow the hand; on Release, they detach and fall naturally. Collision events are sent back to the phone to trigger vibration and audio, closing the loop.


## Requirements
- OS: Windows 10/11

- Unity: 2022.3.57f1 LTS

- Android Studio 2024.1.2, JDK 17

- Visual Studio: 2022

- Android SDK: Android SDK 34+ (minSdk 24+)

- Android device: Any recent Android phone (tested on Samsung Galaxy series)


## Installation
#### 1. Open Unity Project
Open the project in Unity, load SampleScene, and press Play.

#### 2. Build and install Android app
Build a Release APK in Android Studio, and install it on your phone.
In the app, set the Unity PC's IP:
- On Windows, open Command Prompt and run: ipconfig
- Copy the IPv4 Address of your Wi-Fi. (e.g., 192.168.x.x)
- Enter this IPv4 in the app's IP field and Save.

#### 3. Connect devices to same Wi-Fi
Ensure both phone and PC are on the same Wi-Fi.

#### 4. Allow UDP port 7777
- Turn off Windows Defender Firewall.
- UDP port is fixed to 7777.
- Add Inbound rule → UDP → port 7777 → Allow.

#### 5. Run and test
Launch the app, select Left/Right hand and Y/Z axis. Perform pinch/drag/rotation and check that Unity hands move. On collisions, the phone should vibrate.


## Results
Using two smartphones, HaptiX controls left and right virtual hands independently. Each hand follows its configured axis mode (Y or Z) for translation and rotation. The system supports grasping objects, lifting them, rotating while held, and then releasing to drop — all driven by real-time touch input from the phones.

## Demo Video
https://youtube.com/shorts/u9O4cB-bul0?feature=share


