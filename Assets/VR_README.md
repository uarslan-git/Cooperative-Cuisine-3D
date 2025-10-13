# VR Setup for Cooperative Cuisine 3D - Meta Quest 3

## Overview

This project now supports VR gameplay using Meta Quest 3 headset. The VR system allows players to experience the cooperative cooking game in immersive virtual reality.

## Setup Instructions

### 1. Unity XR Plugin Manager

1. Open Unity and go to **Window > XR > XR Plug-in Management**
2. In the XR Plug-in Management settings:
   - Enable **Oculus** provider for Android platform
   - Enable **OpenXR** provider for additional compatibility

### 2. XR Interaction Toolkit (Optional)

For enhanced VR interactions, install the XR Interaction Toolkit:

1. Open **Window > Package Manager**
2. Click the **+** button and select **Add package by name**
3. Enter: `com.unity.xr.interaction.toolkit`
4. Click **Add**

### 3. Oculus Integration (Recommended)

For best Meta Quest 3 support:

1. Download Oculus Integration from Unity Asset Store
2. Import the package to your project
3. Follow the Oculus setup wizard

## VR Components

### VRSetup.cs

- Main setup script that creates and configures the VR rig
- Attach to an empty GameObject in your scene
- Configure settings in the inspector:
  - **Enable VR On Start**: Automatically start in VR mode
  - **Eye Height**: Adjust for player height (default 1.7m)
  - **Hand Prefabs**: Assign custom hand controller models

### VRCameraController.cs

- Manages VR camera positioning relative to the controlled player
- Automatically follows the player character
- Handles smooth camera transitions

### VRInputController.cs

- Translates VR controller input to game actions
- Maps Meta Quest 3 controls:
  - **Left/Right Thumbsticks**: Movement
  - **Triggers**: Interact
  - **Grip Buttons**: Hold interactions
  - **A/X Buttons**: Primary actions
  - **B/Y Buttons**: Secondary actions

### VRManager.cs

- Coordinates all VR functionality
- Handles VR/Desktop mode switching
- Manages VR rig lifecycle

## Meta Quest 3 Controls

| Action           | Control                            |
| ---------------- | ---------------------------------- |
| Move Player      | Left/Right Thumbstick              |
| Interact         | Trigger (Left/Right)               |
| Hold Interaction | Grip (Left/Right)                  |
| Primary Action   | A Button (Right) / X Button (Left) |
| Secondary Action | B Button (Right) / Y Button (Left) |

## Build Settings for Meta Quest 3

### Android Platform Settings

1. Switch to **Android** platform in Build Settings
2. Set **Texture Compression** to ASTC
3. Set **Target API Level** to API level 32 or higher

### XR Settings

1. In **Project Settings > XR Plug-in Management**:
   - Enable **Oculus** for Android
2. In **Project Settings > Oculus**:
   - Enable **Target Quest 3**
   - Set **Stereo Rendering Mode** to Multiview
   - Enable **Shared Depth Buffer**

### Player Settings

1. **Company Name**: Set your company name
2. **Product Name**: Set to "Cooperative Cuisine 3D VR"
3. **Package Name**: Use format `com.yourcompany.cooperativecuisine3d`
4. **Minimum API Level**: Android 10.0 (API level 29)
5. **Target API Level**: Android 13.0 (API level 33)
6. **Scripting Backend**: IL2CPP
7. **Target Architectures**: ARM64

## Testing VR

### In Editor

1. Add VRSetup script to an empty GameObject in your scene
2. Press Play to test VR functionality
3. Use keyboard 'V' key to toggle between VR and Desktop modes

### On Device

1. Enable Developer Mode on your Meta Quest 3
2. Connect headset via USB or use wireless debugging
3. Build and Run to deploy directly to headset

## Troubleshooting

### Common Issues

1. **VR not starting**:

   - Ensure Oculus XR Plugin is enabled
   - Check that headset is properly connected
   - Verify Android build settings

2. **Controller input not working**:

   - Check that VRInputController is properly configured
   - Ensure Input System package is installed
   - Verify controller bindings in Input Actions

3. **Camera positioning issues**:

   - Adjust eye height in VRSetup component
   - Check that player transform is properly assigned
   - Verify VRCameraController is enabled

4. **Performance issues**:
   - Reduce graphics quality settings
   - Enable Fixed Foveated Rendering in Oculus settings
   - Optimize render pipeline for mobile VR

### Performance Optimization

1. **Graphics**:

   - Use Mobile/VR shaders
   - Reduce texture resolution
   - Limit draw calls and polygons

2. **Rendering**:

   - Enable Multiview rendering
   - Use conservative LOD settings
   - Optimize lighting and shadows

3. **Physics**:
   - Reduce physics simulation complexity
   - Use simpler colliders where possible
   - Limit active physics objects

## Development Notes

- The VR system is designed to work alongside the existing multiplayer functionality
- VR players can interact with desktop players seamlessly
- Camera follows the controlled player character, maintaining the game's perspective
- Input system translates VR controller actions to existing game input events

## Future Enhancements

- Hand tracking support for finger-level interactions
- Haptic feedback for cooking actions
- Room-scale play area boundary system
- Gesture-based interactions for cooking tasks
