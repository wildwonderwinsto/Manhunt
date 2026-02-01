# ?? VIEW BOB INTENSITY - CAMERA TURNING OPTIMIZATION

## What Changed

Now tracks **camera rotation (mouse movement)** to keep bob intensity HIGH during turns

---

## The Behavior Now

### Walking Straight (No Camera Turn)
- **Full intensity** for first 2 seconds
- **Gradually fades** to 10% over 5 more seconds
- Realistic for repetitive motion

### Walking + Turning Camera
- **Always at full intensity** while turning
- Bob **doesn't fade** while mouse moving
- More dynamic and engaged feeling
- Intensity resets when you stop turning

### Switching Movement Direction
- Bob intensity **resets to full**
- Works fresh for new direction

---

## How It Works

```csharp
// Detect camera turning (horizontal mouse movement)
bool isTurning = Mathf.Abs(lookInput.x) > 0.1f;

if (isTurning)
{
    _bobIntensity = 1f;  // Keep at full intensity
}
else if (isMoving && !isTurning)
{
    // Only fade if moving straight (not turning)
    _bobIntensity gradually fades
}
```

---

## Behavior Table

| Scenario | Bob Intensity | Fades? |
|----------|--------------|--------|
| Walk straight 3+ sec | 10% | ? Yes |
| Walk + turn camera | 100% | ? No |
| Walk + stop turning | Fades | ? Yes (after 2 sec) |
| Stop moving | 0% | ? Yes |
| Change WASD input | 100% | ? No |

---

## Why This Feels Better

? **Turning keeps you engaged** - full bob intensity  
? **Straight walking gets subtle** - fades after 2 sec  
? **Dynamic feel** - bob responds to camera activity  
? **Less repetitive** - fading reduces boredom on long walks  
? **More immersive** - actively turning = active bob

---

## Files Changed

**Assets/Scripts/Player/Camera/PlayerCamera.cs**
- Added `_lastLookInput` tracking
- Added `_timeSinceCameraMove` counter
- Modified UpdateViewBob() to detect camera turning
- Added turn detection: `Mathf.Abs(lookInput.x) > 0.1f`
- Only fade when NOT turning

---

## Configuration

If you want to adjust turn sensitivity:

```csharp
// Current: 0.1f sensitivity
bool isTurning = Mathf.Abs(lookInput.x) > 0.1f;

// More sensitive (even slight turns):
bool isTurning = Mathf.Abs(lookInput.x) > 0.05f;

// Less sensitive (only big turns):
bool isTurning = Mathf.Abs(lookInput.x) > 0.3f;
```

---

## Build Status

```
? Compilation: 0 errors, 0 warnings
? Bob intensity: HIGH during turns
? Bob fade: Only on straight movement
? Camera turning: Fully responsive
? Ready to test: YES
```

---

## Summary

Your view bob now intelligently responds to camera movement:
- **Turning** ? Full bob intensity (engaging)
- **Straight walking** ? Bob fades (realistic)
- **Best of both worlds** ?

This creates a more dynamic and immersive first-person experience!
