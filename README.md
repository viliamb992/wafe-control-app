# Recuperation System Controller

A cross-platform application suite for controlling your Wafe recuperation system via its REST API.

## Overview

This solution provides both Desktop (Windows/Linux/macOS) and Android applications to monitor and control your recuperation system remotely.

## Projects

### RecuperationSystem.Shared
- Common library containing API client, models, and business logic
- Wafe API integration with HttpClient
- Authentication and session management

### RecuperationSystem.Desktop
- Cross-platform desktop application built with Avalonia UI
- Supports Windows, Linux, and macOS
- Modern Fluent design interface

### RecuperationSystem.Android
- Native Android application for .NET
- Mobile-friendly interface
- Same functionality as desktop app

## Features

- **Authentication**: Secure login to Wafe system
- **System Control**: Start/Stop the recuperation system
- **Flow Speed**: Adjust flow speed between 50-220
- **Operating Modes**: Switch between Intelligent, Manual, and Schedule modes
- **Boost Function**: Quick boost activation (15/30 minutes)
- **Real-time Status**: Monitor current system state

## Requirements

- .NET 8.0 SDK or later
- For Desktop: Windows 10+, macOS 10.15+, or Linux
- For Android: Android 5.0 (API 21) or higher

## Building the Solution

### Desktop Application

```powershell
cd src/RecuperationSystem.Desktop
dotnet restore
dotnet build
dotnet run
```

### Android Application

```powershell
cd src/RecuperationSystem.Android
dotnet restore
dotnet build -f net8.0-android
# Deploy to connected device or emulator
dotnet build -f net8.0-android -t:Run
```

## Configuration

No configuration files needed. Enter your Wafe credentials directly in the application:
- Email/Username
- Password

The app connects to: `https://go2my.wafe.eu/api`

## API Integration

The application uses the following Wafe API endpoints:
- Authentication: `POST /auth/context`
- System Status: `GET /api/v1/main`
- Start/Stop: `PUT /api/v1/main/stop-active`
- Flow Speed: `PUT /api/v1/main/flow-requested`
- Operating Mode: `PUT /api/v1/main/authority`
- Boost: `PUT /api/v1/main/boost-remaining`

## Usage

1. Launch the application (Desktop or Android)
2. Enter your Wafe credentials
3. Click "Connect"
4. Use the control panel to manage your system:
   - Start/Stop the system
   - Adjust flow speed with slider
   - Change operating mode
   - Activate boost mode

## Security Notes

- Credentials are not stored locally
- Session managed via HTTP cookies
- All communication over HTTPS

## License

For personal use only.

## Support

This is a personal project for controlling Wafe recuperation systems. Refer to Wafe's official documentation for API details.
