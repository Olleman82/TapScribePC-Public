[Setup]
AppName=TapScribe
AppVersion=0.2.7
AppPublisher=AIOlle
DefaultDirName={localappdata}\TapScribe
DefaultGroupName=TapScribe
DisableProgramGroupPage=yes
OutputDir=.\dist
OutputBaseFilename=TapScribe-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
SetupIconFile=..\WsprPc\Assets\tapscribe.ico
WizardStyle=modern
InfoBeforeFile=..\TapScribe_README.txt

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs; Excludes: "appsettings.json"
Source: "..\publish\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{userdesktop}\TapScribe"; Filename: "{app}\TapScribe.exe"
Name: "{userprograms}\TapScribe\TapScribe"; Filename: "{app}\TapScribe.exe"

[Run]
Filename: "{app}\TapScribe.exe"; Description: "Starta TapScribe"; Flags: nowait postinstall skipifsilent
