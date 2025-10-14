# VR Main Menu Setup Guide

## Overview

This guide will help you create a proper VR main menu to fix the splash screen flickering and provide a better user experience for connecting to your server.

## Step 1: Create VR Rig

1. **In Unity Scene:**
   - Right-click in Hierarchy → XR → Room-Scale XR Rig (Action-based) or Device-based XR Rig
   - This will create a proper VR camera setup that works with Meta XR SDK
   - Position it at (0, 0, 0) in your scene

## Step 2: Create Main Menu Canvas

1. **Create Canvas:**

   - Right-click in Hierarchy → UI → Canvas
   - Name it "VR Main Menu Canvas"
   - Set Render Mode to "World Space"
   - Set Canvas Scaler to scale with screen size
   - Position it at (0, 1.5, 2) to be in front of player
   - Scale it to (0.01, 0.01, 0.01) for appropriate VR size

2. **Create UI Elements:**
   - Add a Panel child to the Canvas
   - Add these UI elements as children of the Panel:
     - **Title Text (TextMeshPro):** "Cooperative Cuisine VR"
     - **Server URL Input Field (TMP_InputField):** Default text "682e89727181.ngrok-free.app"
     - **Connect Button:** "Connect to Server"
     - **Status Text (TextMeshPro):** "Enter server URL and click Connect"

## Step 3: Setup Components

1. **Add VRMainMenu Script:**

   - Attach `VRMainMenu.cs` to the Canvas
   - Drag UI elements to their respective fields in the inspector
   - Set default server URL

2. **Add VRSceneManager:**

   - Create empty GameObject named "VR Scene Manager"
   - Attach `VRSceneManager.cs`
   - If you have prefabs, assign VR Rig and Menu Canvas prefabs

3. **Add VR UI Interaction:**
   - Find your VR controller/hand in the VR Rig
   - Add `VRUIPointer.cs` to right controller
   - Assign the controller transform as the pointer

## Step 4: Modify StudyClient

The StudyClient has been updated to:

- Not automatically connect on Start()
- Wait for VRMainMenu to trigger connection
- Not create fallback cameras (let VR handle this)

## Step 5: Scene Setup

1. **Remove any existing Main Camera** (the VR Rig provides cameras)
2. **Ensure Canvas has GraphicRaycaster component**
3. **Make sure EventSystem exists in scene** (usually auto-created with Canvas)
4. **Set Canvas layer to UI** for proper raycasting

## Step 6: Testing

1. **In Unity Editor:**
   - Use XR Device Simulator if available
   - Or test with mouse clicks on UI
2. **On Quest 3:**
   - Build and deploy
   - Use controller to point and click on UI elements
   - Input field should allow typing with VR keyboard

## Expected Behavior

1. **App starts** → VR rig initializes properly (no flickering)
2. **Main menu appears** in VR space with server URL input
3. **User can modify URL** and click connect
4. **Connection status** is displayed
5. **On successful connection** → menu hides and game starts

## Troubleshooting

- **Flickering:** Make sure only VR cameras exist, no legacy main camera
- **Can't click UI:** Ensure Canvas has GraphicRaycaster and proper layer
- **Menu not visible:** Check Canvas position and scale in world space
- **VR not working:** Verify XR Plugin Management has Meta provider enabled

## File Structure

```
Assets/Scripts/
├── VRMainMenu.cs          # Main menu UI controller
├── VRSceneManager.cs      # VR scene initialization
├── VRUIPointer.cs         # VR UI interaction
└── StudyClient.cs         # Modified to work with menu
```

This setup will give you a professional VR main menu experience that eliminates the splash screen flickering and provides a clean way to connect to different servers.
