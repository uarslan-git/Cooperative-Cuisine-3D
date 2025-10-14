# Flickering Debugging Plan

## Since minimal VR scene works, the issue is in your Cooperative Cuisine scene

## Step 1: Add FlickerDebugger to Cooperative Cuisine Scene

1. **Add FlickerDebugger.cs** to an empty GameObject in your scene
2. **Build and run** - check Unity Console logs for the analysis report
3. **Look for these red flags:**
   - Multiple active cameras
   - Both VR and legacy cameras present
   - Camera depth conflicts

## Step 2: Likely Culprits to Check

### A. Multiple Cameras (Most Likely Cause)

**Check these locations:**

- Any remaining "Main Camera" objects
- StudyClient creating fallback cameras
- VRSetup creating additional cameras
- Multiple XR Rigs in scene

### B. StudyClient Camera Creation

**In StudyClient.cs, this code was removed but check if it's still elsewhere:**

```csharp
// Look for this problematic code:
if (Camera.main == null)
{
    GameObject cameraObj = new GameObject("Main Camera");
    // ... camera creation
}
```

### C. VR Component Conflicts

**Multiple VR initialization scripts:**

- VRSetup.cs
- VRManager.cs
- VRCameraController.cs
- Check if multiple scripts are trying to create/manage cameras

## Step 3: Quick Fixes to Try

### Fix 1: Disable StudyClient at Start

```csharp
// In Cooperative Cuisine scene, disable StudyClient initially
studyClient.enabled = false;
```

### Fix 2: Remove All Legacy Cameras

1. **Search hierarchy** for any GameObject named "Main Camera"
2. **Delete them** (XR Rig provides cameras)
3. **Check prefabs** - they might be creating cameras

### Fix 3: Camera Depth Priority

If you find multiple cameras, set depths properly:

- **VR Cameras:** Depth = 0 (highest priority)
- **Any other cameras:** Depth = -1 or disable them

## Step 4: Systematic Elimination

### Test 1: Minimal Cooperative Cuisine Scene

1. **Duplicate your scene**
2. **Delete everything except:**
   - XR Rig
   - Basic environment (floor, walls)
   - Essential lighting
3. **Build and test** - does it still flicker?

### Test 2: Add Components Back Gradually

1. Start with working minimal scene
2. Add back components one by one:
   - GameManager
   - StudyClient (disabled)
   - UI Elements
   - Other scripts
3. **Test after each addition** to find the culprit

## Step 5: Common Solutions

### Solution A: Camera Management Script

```csharp
// Add this to a GameObject in scene
void Start() {
    // Force disable all non-VR cameras
    Camera[] cameras = FindObjectsOfType<Camera>();
    foreach(Camera cam in cameras) {
        if (!cam.name.Contains("Center") && !cam.name.Contains("XR")) {
            cam.enabled = false;
            Debug.Log($"Disabled camera: {cam.name}");
        }
    }
}
```

### Solution B: VR-First Scene Setup

1. **Start with working XR Rig from minimal scene**
2. **Copy it to Cooperative Cuisine scene**
3. **Remove all other cameras before adding**

## Expected Debug Output

The FlickerDebugger will show:

```
=== FLICKER ANALYSIS REPORT ===
--- CAMERA ANALYSIS ---
⚠️ MULTIPLE ACTIVE CAMERAS DETECTED - LIKELY CAUSE OF FLICKERING!
⚠️ BOTH VR AND LEGACY CAMERAS PRESENT - CONFLICT DETECTED!
```

## Next Steps Based on Results

- **Multiple cameras found:** Delete/disable extras
- **Script conflicts:** Disable conflicting VR scripts
- **Canvas issues:** Check world camera assignments
- **Still flickering:** Check rendering settings & quality levels

Run the FlickerDebugger first - it will tell us exactly what's wrong!
