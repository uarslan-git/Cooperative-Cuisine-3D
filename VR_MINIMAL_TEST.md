# Minimal VR Test Scene Setup

## Create New Scene
1. **File → New Scene → Basic (Built-in)**
2. **Save as:** `VRTestScene.unity`

## Step 1: Delete Default Objects
- Delete the default **Main Camera**
- Delete the default **Directional Light** (we'll add our own)

## Step 2: Add VR Rig
1. **Right-click in Hierarchy → XR → Room-Scale XR Rig (Action-based)**
2. **Name it:** "XR Rig"
3. **Position:** (0, 0, 0)

## Step 3: Add Basic Environment
1. **Right-click → 3D Object → Plane** (floor)
   - Scale: (10, 1, 10)
   - Position: (0, 0, 0)

2. **Right-click → 3D Object → Cube** (test object)
   - Position: (0, 1, 2) 
   - Scale: (1, 1, 1)
   - Add bright material (Create → Material, set Albedo to bright color)

3. **Right-click → Light → Directional Light**
   - Rotation: (50, -30, 0)

## Step 4: Add Test Canvas (Optional)
1. **Right-click → UI → Canvas**
2. **Set Render Mode:** World Space
3. **Position:** (0, 2, 3)
4. **Scale:** (0.01, 0.01, 0.01)
5. **Add child:** UI → Text - TextMeshPro
   - Text: "VR Test Scene"
   - Font Size: 36

## Step 5: Build Settings
1. **File → Build Settings**
2. **Add Open Scenes** (add your VRTestScene)
3. **Platform:** Android
4. **Switch Platform** if needed

## Step 6: Test Build
1. **Build and Run** or **Build** then install via ADB
2. **Expected result:** No flickering, smooth VR experience

## If This Works:
- The issue is in your main scene (complex objects, scripts, cameras)
- Copy the working VR rig setup to your main scene
- Gradually add elements back until you find what causes flickering

## If This Still Flickers:
- Check **XR Plugin Management** (Edit → Project Settings → XR Plug-in Management)
- Verify **Meta** is checked for Android platform
- Try different **Stereo Rendering Mode** in Meta XR settings

## Quick Build Command (if ADB connected):
```bash
# After building, install via:
adb install -r YourApp.apk
adb shell am start -n com.YourCompany.VRTestScene/com.unity3d.player.UnityPlayerActivity
```
