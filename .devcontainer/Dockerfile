FROM mcr.microsoft.com/devcontainers/base:debian-12

# 1. Install Native Dependencies
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y \
       # Avalonia X11 requirements
       libx11-6 libice6 libsm6 libfontconfig1 \
       # Build requirements for Android SDK
       unzip wget \
       # Java is required *during* the Dockerfile build for sdkmanager
       openjdk-17-jdk \
       # Native Cryptography libraries (Argon2id / AES-GCM bindings)
       libsodium-dev build-essential \
    && apt-get clean -y && rm -rf /var/lib/apt/lists/*

# 2. Set up the Android SDK for Kotlin Mobile Compilation
ENV ANDROID_HOME="/opt/android-sdk"
ENV PATH="${PATH}:${ANDROID_HOME}/cmdline-tools/latest/bin:${ANDROID_HOME}/platform-tools"

RUN mkdir -p ${ANDROID_HOME}/cmdline-tools \
    # Download Android Command Line Tools
    && wget -q https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip -O android_tools.zip \
    && unzip -q android_tools.zip -d ${ANDROID_HOME}/cmdline-tools \
    && mv ${ANDROID_HOME}/cmdline-tools/cmdline-tools ${ANDROID_HOME}/cmdline-tools/latest \
    && rm android_tools.zip \
    # Accept licenses and install platform-tools and build-tools
    && yes | sdkmanager --licenses \
    && sdkmanager "platform-tools" "platforms;android-34" "build-tools;34.0.0"

# 3. Grant the default 'vscode' user ownership of the SDK
RUN chown -R vscode:vscode ${ANDROID_HOME}