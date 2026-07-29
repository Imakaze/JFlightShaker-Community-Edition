# Contributing to JFlightShaker C.E

Thank you for helping improve device compatibility.

## Before opening an issue

1. Use the latest release.
2. Open **Test Device** and confirm what DirectInput reports.
3. Reproduce the problem with a low output volume.
4. Check `Config/Logs/JFlightShaker.log`.
5. Remove personal information before attaching configuration or logs.

Use the appropriate issue template. For device support, include:

- Exact manufacturer and model.
- Windows version.
- Driver/software version.
- Device name and GUID shown by JFlightShaker.
- Axes, buttons and hats reported by **Test Device**.
- Expected and actual behavior.

## Pull requests

1. Create a branch from `main`.
2. Keep changes focused.
3. Preserve existing behavior unless the issue explicitly changes it.
4. Add or update tests for effect calculations.
5. Run:

```powershell
dotnet test JFlightShaker.sln -c Release
dotnet build JFlightShaker.csproj -c Release
```

6. Update `CHANGELOG.md` when behavior changes.

Do not commit `bin`, `obj`, runtime `Config/*.json`, logs, personal device GUIDs
or published executables.

## Device profiles

Profiles must remain generic and must not include personal GUIDs. Document
driver-specific mappings clearly. Avoid hardcoding a layout in generic
DirectInput code unless a device-specific profile selects that behavior.

## Languages

Interface changes should retain English, Spanish and Catalan strings. UTF-8
characters must be stored directly without mojibake.

## Español

Antes de enviar una incidencia, comprueba el dispositivo mediante **Test
Device**, revisa el log y elimina información personal. Las aportaciones deben
incluir pruebas cuando modifiquen cálculos de efectos y no deben contener GUIDs
personales, configuraciones de uso ni binarios compilados.
