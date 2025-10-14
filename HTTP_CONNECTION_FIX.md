# Fix HTTP Connection Issue

## The Error:

`Non-secure network connections disabled in Player Settings`
`InvalidOperationException: Insecure connection not allowed`

## Solution 1: Enable HTTP in Player Settings (Recommended)

1. **Go to:** Edit → Project Settings → Player
2. **Find:** Configuration → Internet Access → Allow downloads over HTTP
3. **Set to:** Always allowed
4. **Build and test**

## Solution 2: Use HTTPS URLs (Alternative)

Since ngrok provides HTTPS, we can modify the code to use HTTPS instead of HTTP.

## Quick Fix Steps:

1. Enable "Allow downloads over HTTP: Always allowed" in Player Settings
2. Your ngrok URL `https://7f2d5e2334ec.ngrok-free.app` should work
3. Make sure StudyClient port is set correctly (80 for ngrok, not 8080)

## Why This Happens:

- Unity blocks HTTP connections on mobile platforms for security
- Quest 3 (Android) enforces this restriction
- ngrok provides both HTTP and HTTPS, but Unity needs permission for HTTP

## After Fix:

Your VR app should successfully connect to the ngrok server and start the multiplayer cooking game!
