# Quick VR Fix Guide

## Problem 1: Camera Breaking (Fixed)

**Issue**: VRSetup script creates additional cameras that conflict with XR Rig
**Solution**: Disable/Remove VRSetup GameObject

## Problem 2: Main Menu Canvas Not Visible

**Issue**: Canvas not properly positioned or configured for VR

## Quick Fix Steps:

### Step 1: Remove Problematic VR Scripts

1. **Find GameObject with VRSetup script** in your scene
2. **Disable or Delete it** (keep only XR Rig)
3. **Remove VRTest script** as well if present

### Step 2: Fix Main Menu Canvas

Your canvas needs these exact settings for VR:

```
Canvas Settings:
- Render Mode: World Space
- Position: (0, 1.5, 2)  // In front of player at eye level
- Rotation: (0, 0, 0)
- Scale: (0.001, 0.001, 0.001)  // Very small for VR
- Canvas Scaler: Constant Physical Size
```

### Step 3: Canvas Component Check

Ensure your canvas has:

- ✅ Canvas component
- ✅ GraphicRaycaster component
- ✅ Canvas Group component (optional, for fading)

### Step 4: EventSystem Check

- ✅ EventSystem exists in scene
- ✅ No duplicate EventSystems

## Test Canvas Visibility Script

Add this to your canvas to debug visibility:
