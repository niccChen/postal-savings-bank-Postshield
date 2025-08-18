
# PostShield Desktop

**PostShield** is a cross-platform .NET/Avalonia desktop app for **automatic redaction** of contact information.  
Drag one or more folders into the app, click **Encrypt**, and PostShield batch-processes every file using rule-based blurring/masking (e.g., blur phone numbers except the last four digits, mask first names). A built-in search lets you quickly locate a specific file before or after processing.

---

## Key Features
- **Rule-based redaction**
  - Phone numbers → keep last 4 digits, mask the rest
  - First names → /mask
  - Additional entity types can have **different masking rules**
- **Batch processing**
  - Drag-and-drop folders; all files inside are processed in one run
- **Search**
  - Find a specific file by name directly inside the app
- **Non-destructive output**
  - Users may undo the masking process
- **Cross-platform**
  - Works on Windows, macOS, and Linux (Avalonia UI)

---

## Tech Stack
- **.NET** 7/8 (SDK)
- **Avalonia UI** (XAML + C#)
- C# 10/11

> If you don’t have the .NET SDK installed, download it from Microsoft .NET.  
> Avalonia workload is restored automatically via NuGet on first build.

---

## Project Structure
```text
POSTAL-SAVINGS-BANK-POSTSHIELD/
└── PostShieldDesktop/
    ├── App.axaml                 # App-level resources
    ├── App.axaml.cs              # App bootstrap
    ├── MainWindow.axaml          # Root window view
    ├── MainWindow.axaml.cs       # View code-behind
    ├── Config/                   # App configuration (JSON or env files)
    ├── PostShield.csproj         # Project definition
    ├── Program.cs                # Entry point (Main)
    ├── bin/                      # Build output (generated)
    └── obj/                      # Intermediate build files (generated)
```
