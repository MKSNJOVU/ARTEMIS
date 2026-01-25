# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MKSSecureShare is an Android application for encrypting and sharing files securely. Written in Kotlin, targeting Android 7.0+ (API 24).

## Build Commands

All commands run from `MKSSecureShare/` directory:

```bash
# Build debug APK
./gradlew assembleDebug

# Build release APK
./gradlew assembleRelease

# Clean build
./gradlew clean build

# Run Android Lint
./gradlew lint
```

## Testing

```bash
# Run all unit tests
./gradlew test

# Run specific unit test class
./gradlew test --tests com.example.mkssecureshare.ExampleUnitTest

# Run instrumented tests (requires device/emulator)
./gradlew connectedAndroidTest
```

Test locations:
- Unit tests: `app/src/test/java/`
- Instrumented tests: `app/src/androidTest/java/`

## Architecture

### Core Components

**CryptoManager** (`app/src/main/java/CryptoManager.kt`)
- Singleton object handling AES-GCM encryption
- Uses Android KeyStore for secure key storage
- `getKey()`: Retrieves or generates AES secret key
- `encrypt()`: Encrypts data with randomly generated IV

**MainActivity** (`app/src/main/java/com/example/mkssecureshare/MainActivity.kt`)
- Entry point with encrypt/decrypt button UI
- Uses edge-to-edge display with window insets handling

### Build Configuration

- Gradle 8.13 with Kotlin DSL
- Version catalog: `gradle/libs.versions.toml`
- Java 11 / Kotlin 2.0.21
- Compile/Target SDK: 36

## Development Status

The project is in early development. Per the learning guide (`SecureShare_Guide.md`), remaining work includes:
- Complete MainActivity click handlers
- Implement file picker functionality
- Finish CryptoManager encryption/decryption methods
- Add FileProvider configuration for sharing
- Declare required permissions in AndroidManifest.xml
