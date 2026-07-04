# Sticky Notes Plus Plus

Advanced Windows desktop notes application built with C#, .NET, WPF, XAML, SQLite, SignalR, and native Windows Shell integration.

This repository is a portfolio-quality Windows desktop application that demonstrates rich UI engineering, local data persistence, media handling, native Windows API integration, and real-time collaboration patterns in a practical productivity app.

## What It Does

Sticky Notes Plus Plus is a power-user sticky notes system for Windows. It extends the familiar desktop sticky note concept with rich document editing, image and video backgrounds, reminders, todo metadata, secure/private note concepts, shared note collaboration, drag-and-drop media handling, taskbar notifications, and extensibility through local plug-ins.

The app is designed around persistent note windows. Each note can remember its content, window placement, styling, visibility, font settings, background, task/reminder data, and security metadata across sessions.

## Core Capabilities

### Rich Desktop Notes

- Persistent floating note windows that can stay above other applications.

- Chromeless/translucent WPF windows with custom resize and toolbar behavior.

- Rich text editing using WPF `FlowDocument` and a customized rich text editor.

- Font controls, font sizing, text styling, spell check toggling, and formatting toolbar support.

- Context menus and keyboard shortcuts using WPF routed commands.

- Save, refresh, hide, close, delete, show all, show private, and show public note workflows.

### Video, Image, and Media Features

- Image backgrounds for notes.

- Support for inserted image and media elements inside rich note content.

- Drag-and-drop handling for images, videos, audio files, folders, files, and URLs.

- File and folder hyperlinks embedded directly into note documents.

- Shell-generated thumbnails and icons for linked files.

- Ctrl + mouse wheel resizing behavior for text and embedded file thumbnails.

- Safe image loading strategy that copies image files to temporary paths to avoid locking original files.

### Shared and Private Note Concepts

- Note model includes security and ownership metadata.

- Permission model includes read, modify, full, and private states.

- Notes can be grouped into private/public visibility workflows.

- The UI includes commands for showing all notes, private notes, and public notes.

- The data model tracks creator, updater, owner SID, timestamps, and permission state.

### Todo and Reminder Elements

- Each note can be associated with task/todo data.

- Todo metadata includes status, priority, start date, due date, completed date, total work, and actual work.

- Reminder metadata includes reminder state, message, reminder date/time, sound, image URI, long notification mode, snooze count, and snooze interval.

- Reminder and settings panels are exposed through XAML property grids.

- Notification sounds are included as application assets.

### Real-Time Collaboration

- SignalR client integration for collaborative note messaging.

- Automatic reconnect behavior for hub connections.

- Broadcast message handling with dispatcher-safe UI updates.

- Taskbar progress and badge overlays for unread collaboration notifications.

- Balloon notification support through Windows notification-area APIs.

- Sound alerts for incoming collaboration messages.

### Local Data Persistence

- SQLite-backed local data storage.

- Embedded SQLite template database bundled as an application resource.

- First-run database creation under the user's local application data folder.

- Notes and tasks are saved with insert/update upsert-style persistence.

- WPF `FlowDocument` content is serialized to XAML and stored in SQLite.

- UX settings are serialized to JSON for persistence.

### Advanced Window and Desktop Behavior

- Multi-monitor placement support.

- Window bounds and restore state persistence.

- Topmost/pinned note support.

- Smart placement logic for new note windows.

- New note title generation that avoids duplicates.

- Support for note inheritance and composition through `SuperID` and `ParentID`.

### Plug-In and Automation Framework

- Local plug-in directory processing.

- Scheduled plug-in execution using a WPF dispatcher timer.

- Support for `.exe`, `.cmd`, and PowerShell-style plug-in files.

- User-specific plug-in folders derived from the Windows identity.

- Local copy-and-run behavior for plug-ins and supporting files.

- Process tracking and cleanup after plug-ins exit.

## Technology Stack

### Application Platform

- C#

- .NET 6

- WPF

- Windows Forms interop

- XAML

- MSBuild

### UI and Desktop Frameworks

- WPF `Window`, `Grid`, `DockPanel`, `StackPanel`, `ContextMenu`, `MenuItem`, `Expander`, `ToggleButton`, `Image`, and `MediaElement`.

- WPF `FlowDocument`, `Paragraph`, `Run`, `Hyperlink`, `InlineUIContainer`, `TextPointer`, and `TextRange`.

- WPF routed commands and keyboard gestures.

- Custom WPF value converters.

- Custom control templates and styles.

- Xceed WPF Toolkit controls, including `PropertyGrid`, `ColorPicker`, `TimePicker`, `DateTimePicker`, and rich text formatting features.

### Data and Serialization

- SQLite through `System.Data.SQLite`.

- XAML serialization with `XamlWriter` and `XamlReader`.

- JSON serialization with Newtonsoft.Json.

- Embedded database resource bootstrapping.

### Collaboration and Notifications

- Microsoft ASP.NET Core SignalR Client.

- Microsoft.Extensions.Logging.

- Windows taskbar progress indicators and icon overlays.

- Windows notification area icon APIs.

- Application sound assets.

### Native Windows Integration

- Win32 P/Invoke.

- Shell32 integration.

- Vanara PInvoke Shell32.

- Windows API Code Pack.

- Windows Shell file icons and thumbnails.

- Shell file type detection.

- COM interop for shell shortcut creation.

- Monitor and work-area detection through native Windows APIs.

- Jump List tasks in XAML.

## Notable Technical Features

### Advanced XAML UI

The app uses XAML extensively for more than static layout. The UI includes custom resource definitions, image resources, dynamic bindings, value converters, control templates, command bindings, custom toolbar behavior, expandable tool panels, property-grid based settings panels, tooltip composition, and jump list tasks.

The main note window is a WPF `Window` with transparency, custom chrome behavior, a toolbar, expandable action controls, rich document editing, reminder/settings panels, and context menus.

### Custom Rich Text Editing

The application extends the rich text editing model with a custom `RichTextBox` implementation. It handles drag-and-drop files, URL drops, embedded media, file links, thumbnail insertion, font scaling, selection-aware formatting, and background updates.

The result is a note editor that behaves more like a small desktop document surface than a plain text note box.

### FlowDocument Storage

Rather than storing only plain text, the application serializes WPF `FlowDocument` content as XAML. This allows notes to preserve rich document structure, embedded elements, hyperlinks, styling, backgrounds, and document-level formatting.

This approach demonstrates advanced use of WPF's document model and XML/XAML serialization.

### SQLite Repository Layer

The repository layer loads and saves notes and tasks into a local SQLite database. It uses typed model loading, command parameters, upsert-style SQL, and a shared application model containing notebooks, notes, and tasks.

### Native Shell Features

The utility layer demonstrates deep Windows integration:

- File type detection through Shell APIs.

- File and folder icon extraction.

- Shell thumbnail generation.

- Notification-area icon creation, update, and deletion.

- Taskbar icon location detection.

- COM-based shortcut creation.

- Monitor bounds and work area detection.

These features require understanding of WPF, Win32 interop, marshaling, COM interfaces, Shell constants, handles, icon lifetime management, and desktop window behavior.

### SignalR Collaboration

The SignalR client demonstrates real-time app behavior in a Windows desktop context. It connects to a hub, receives broadcast messages, updates the WPF UI safely through the dispatcher, and raises taskbar/sound notifications when collaboration messages arrive.

### Plug-In Execution Architecture

The plug-in subsystem scans a configured directory, creates a user-specific plug-in location, copies plug-ins and support files locally, starts supported executable/script files in hidden process mode, tracks running processes, and cleans up local plug-in copies after exit.

This shows practical knowledge of Windows process automation, scheduled background behavior, app configuration, identity-aware paths, and defensive filesystem handling.

## Codebase Highlights

- `App.xaml` defines application-level resources, jump list tasks, custom expander styles, converters, and WPF control templates.

- `NoteViewer.xaml` defines the primary note window, toolbar, context menu, property panels, rich text editor, commands, bindings, and UI resources.

- `NoteViewer.xaml.cs` contains note window behavior and event handling.

- `RichTextBox.cs` customizes rich text editing, drag-and-drop behavior, media insertion, and font handling.

- `FlowDocument.cs` adds media, hyperlink, image, and file/document behaviors to rich note content.

- `Models/Note.cs`, `Models/Task.cs`, `Models/Entity.cs`, and `Models/Repository.cs` define the domain model and SQLite persistence layer.

- `SignalRClient.cs` implements collaboration messaging, notifications, and taskbar integration.

- `Utilities.cs` contains Shell API, graphics, monitor, COM, filesystem, and exception utility code.

- `AppPlugIns.cs` implements the plug-in processing and execution framework.

- `XamlConverters.cs` contains WPF value converters used by the XAML UI.

## Build Requirements

This is a Windows desktop application and should be built on Windows.

Recommended environment:

- Windows 10 or later.

- .NET 6 SDK.

- Visual Studio 2022 or VS Code with the C# extension.

- Access to NuGet package restore.

Basic build command:

```powershell
dotnet restore
dotnet build
```

The project targets:

```xml
<TargetFramework>net6.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

## Project Status

This repository is presented as a code and architecture portfolio project. It demonstrates a significant amount of Windows desktop engineering: advanced WPF/XAML UI design, C# application architecture, rich document handling, native Shell integration, SQLite persistence, SignalR collaboration, taskbar notifications, and local plug-in automation.

Some paths and settings reflect the original development environment. A production handoff would typically include configuration cleanup, installer packaging, security review, automated tests, and updated deployment scripts for the target client environment.

## What This Demonstrates for Clients

This project is a strong example of building complex native Windows software with modern C# and WPF. It shows the ability to combine user-facing polish with low-level Windows engineering, including:

- Desktop UI architecture.

- Advanced XAML/XML usage.

- Custom WPF controls and behaviors.

- Local database persistence.

- Media-rich document editing.

- Real-time collaboration.

- Native Windows Shell integration.

- Process automation and plug-in execution.

- Taskbar and notification UX.

- Practical object-oriented C# design.

For clients, this codebase demonstrates the ability to design and build sophisticated Windows applications that integrate deeply with the operating system while still providing a usable, visual, feature-rich desktop experience.
