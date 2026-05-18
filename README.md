# GunZ League

GunZ League is an ASP.NET Core MVC web panel for a GunZ server community. It provides public ranking pages, player and clan details, account registration/login, password recovery, clan management, clan emblem upload, download/donation pages, and Twitch stream status integration.

## Features

- Home page with server/community overview.
- Player, clan, and Player War rankings.
- Player profile and clan detail pages.
- Account login, registration, logout, password change, and password reset flow.
- Session-based account area.
- Clan member management for authorized clan members.
- Clan emblem upload for clan leaders.
- Twitch stream status panel.
- SMTP support for password reset emails.
- Optional mock-data mode for development.

## Tech Stack

- .NET 8
- ASP.NET Core MVC / Razor Views
- Entity Framework Core 8
- SQL Server
- Bootstrap/static CSS

## Project Structure

```text
GLProject/
+-- GLProject.sln
+-- GunZLeague/
|   +-- Controllers/
|   +-- Data/
|   +-- Models/
|   +-- Services/
|   +-- Views/
|   +-- wwwroot/
|   +-- Program.cs
|   +-- GunZLeague.csproj
+-- README.md
```

## Requirements

- .NET SDK 8.0+
- SQL Server or SQL Server Express
- Existing GunZ database schema compatible with the models in `GunZLeague/Models/GunZ`

## Configuration

The app uses `appsettings.json` and `appsettings.Development.json`.

Main settings:

```json
{
  "UseMockData": false,
  "App": {
    "PublicBaseUrl": "http://localhost:5193"
  },
  "ConnectionStrings": {
    "GunZDatabase": "Server=localhost\\SQLEXPRESS;Database=LeagueDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Smtp": {
    "Host": "",
    "Port": 587,
    "EnableSsl": true,
    "UserName": "",
    "Password": "",
    "From": ""
  },
  "Twitch": {
    "ClientId": "",
    "ClientSecret": ""
  }
}
```

For production, configure sensitive values through environment variables, user secrets, or your hosting provider settings instead of committing real credentials.

Useful environment variable examples:

```powershell
$env:ConnectionStrings__GunZDatabase="Server=localhost\SQLEXPRESS;Database=LeagueDB;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
$env:App__PublicBaseUrl="https://your-domain.com"
$env:Smtp__Host="smtp.example.com"
$env:Smtp__UserName="user@example.com"
$env:Smtp__Password="your-password"
$env:Twitch__ClientId="your-client-id"
$env:Twitch__ClientSecret="your-client-secret"
```

## Run Locally

From the repository root:

```powershell
dotnet restore .\GLProject.sln
dotnet build .\GLProject.sln
dotnet run --project .\GunZLeague\GunZLeague.csproj --urls http://localhost:5193
```

Then open:

```text
http://localhost:5193
```

## Database

The application connects to the GunZ database through:

```json
"ConnectionStrings": {
  "GunZDatabase": "..."
}
```

The EF Core context is located at:

```text
GunZLeague/Data/GunZDbContext.cs
```

The database models are located at:

```text
GunZLeague/Models/GunZ
```

## Clan Emblems

Clan leaders can upload PNG emblems from the account profile page.

Current validation:

- PNG only
- 64x64 pixels
- Maximum size: 256 KB
- Only the clan leader can update the emblem

Uploaded emblems are stored under `wwwroot` and the clan table stores the public URL plus checksum.

## Publish

Example publish command:

```powershell
dotnet publish .\GunZLeague\GunZLeague.csproj -c Release -o .\publish
```

Before deploying:

- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Configure `ConnectionStrings__GunZDatabase`.
- Configure `App__PublicBaseUrl`.
- Configure SMTP settings if password reset emails are enabled.
- Configure Twitch credentials if the stream status panel is enabled.
- Make sure the app can write to its `wwwroot` emblem directory and `DataProtectionKeys` directory.

## Security Notes

- Do not commit production database passwords, SMTP passwords, API secrets, or private keys.
- Rotate any credential that was committed publicly.
- Keep `appsettings.Development.json`, logs, build output, and data protection keys out of public repositories.

## License

No license has been specified yet.
