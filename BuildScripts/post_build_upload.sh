#!/bin/bash
set -e

echo "=== Draco Online World TestFlight Auto-Uploader ==="

API_KEY_ID="7B3D9LQ8HH"
ISSUER_ID="354a8957-c515-4976-9a5d-9223d6eb6b60"

KEY_FILE="BuildScripts/AuthKey_${API_KEY_ID}.p8"

mkdir -p ~/.appstoreconnect/private_keys
mkdir -p ~/.private_keys
cp "$KEY_FILE" ~/.appstoreconnect/private_keys/AuthKey_${API_KEY_ID}.p8 2>/dev/null || true
cp "$KEY_FILE" ~/.private_keys/AuthKey_${API_KEY_ID}.p8 2>/dev/null || true

echo "Searching for built IPA file..."
IPA_FILE=""
if [ -n "$UNITY_BUILD_OUTPUT_PATH" ] && [ -d "$UNITY_BUILD_OUTPUT_PATH" ]; then
    IPA_FILE=$(find "$UNITY_BUILD_OUTPUT_PATH" -name "*.ipa" | head -n 1)
fi

if [ -z "$IPA_FILE" ]; then
    IPA_FILE=$(find . -name "*.ipa" | head -n 1)
fi

echo "Found IPA: $IPA_FILE"

if [ -n "$IPA_FILE" ] && [ -f "$IPA_FILE" ]; then
    # Inject 1024 icon into IPA root as iTunesArtwork@2x and into App Payload as failsafe
    ICON_SRC="BuildScripts/AppIcon1024.png"
    if [ -f "$ICON_SRC" ]; then
        echo "Injecting 1024x1024 icon into IPA package..."
        TMP_DIR=$(mktemp -d)
        unzip -q "$IPA_FILE" -d "$TMP_DIR"
        
        # Copy to Payload/*.app/
        APP_DIR=$(find "$TMP_DIR/Payload" -name "*.app" -type d | head -n 1)
        if [ -n "$APP_DIR" ]; then
            cp "$ICON_SRC" "$APP_DIR/AppIcon-1024.png"
            cp "$ICON_SRC" "$APP_DIR/AppIcon60x60@2x.png"
            cp "$ICON_SRC" "$APP_DIR/AppIcon76x76@2x~ipad.png"
            cp "$ICON_SRC" "$TMP_DIR/iTunesArtwork@2x"
            cp "$ICON_SRC" "$TMP_DIR/iTunesArtwork"
            
            # Re-zip IPA
            (cd "$TMP_DIR" && zip -q -r "$IPA_FILE" Payload iTunesArtwork iTunesArtwork@2x 2>/dev/null || zip -q -r "$IPA_FILE" .)
            echo "IPA package updated with 1024x1024 icon."
        fi
        rm -rf "$TMP_DIR"
    fi

    echo "Uploading IPA to TestFlight via xcrun altool..."
    xcrun altool --upload-app -f "$IPA_FILE" -t ios --apiKey "$API_KEY_ID" --apiIssuer "$ISSUER_ID" --show-progress
    echo "=== TestFlight Upload Complete! ==="
else
    echo "ERROR: Could not locate .ipa file"
    exit 1
fi
