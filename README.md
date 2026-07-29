<p align="center">
  <img src="Assets/JFlightCE-Logo.png" width="420" alt="JFlightShaker Community Edition">
</p>

# JFlightShaker C.E

JFlightShaker C.E is a portable Windows application that converts DirectInput
throttle, stick and button inputs into arcade-style bass-shaker effects. It does
not require game telemetry, so it can work with any flight game that accepts the
same controller.

> Public beta: device layouts vary between drivers. The Logitech/Saitek X56 is
> the primary tested HOTAS, and community device profiles are welcome.

## Features

- Throttle/engine rumble with a strong afterburner band above 85%.
- Independent X56 throttle levers: either lever contributes 50%, both reach 100%.
- Pitch and roll vibration with stereo left/right roll response.
- Pitch/roll strength scales moderately with throttle as an airspeed estimate.
- Hold-to-fire gun effect and one-shot missile launch effect.
- High-G effect driven by held button plus pitch threshold and hysteresis.
- Mute effect with controller or keyboard binding, in Trigger or Hold mode.
- Separate left and right audio shakers, while remaining compatible with one shaker.
- Live generic device tester for axes, buttons and POV hats.
- X56 test profile and a generic default profile.
- Per-effect intensity, tooltip, active indicator, preview and reset defaults.
- Automatic DirectInput reconnection and audio-device status indicator.
- English, Spanish and Catalan interface.
- Automatic recovery of malformed JSON configuration with diagnostic backups.
- Local diagnostic log; no telemetry or network communication.

## Safety

Bass shakers and amplifiers can produce unexpectedly strong output.

1. Start with the amplifier and all effect sliders low.
2. Use the **Test** buttons before entering a game.
3. Increase levels gradually.
4. Stop immediately if the shaker, amplifier or mounting surface distorts,
   overheats or makes mechanical knocking sounds.

You are responsible for the limits of your audio equipment.

## Download and installation

1. Open the repository's **Releases** page.
2. Download `JFlightShaker-CE-v0.5.0-beta.1-win-x64.zip`.
3. Extract the entire ZIP to a writable folder.
4. Run `JFlightShaker.exe`.

The release is self-contained: users do not need to install .NET separately.
Windows SmartScreen may warn about unsigned community builds. Verify that the
download comes from this repository and compare its SHA-256 checksum with the
release checksum file.

## Quick start

1. Connect the HOTAS and the audio output used by the bass-shaker amplifier.
2. Select the audio device.
3. Select an input device and use **Test Device** to verify its controls.
4. Right-click an effect and choose **Edit**.
5. Assign its device and control, then save.
6. Set a low intensity and use the effect's **Test** button.
7. Press **Start**.

Middle-click an effect row to enable or disable it. Right-click selected rows to
edit or remove bindings.

## X56 throttle notes

Some X56 drivers expose the two physical throttle levers as `X` and `Y`, while
the binding editor shows the throttle binding as `Axis X`. JFlightShaker
recognizes an X56 throttle and combines both internally:

| Left lever | Right lever | Combined contribution |
|---:|---:|---:|
| 0% | 0% | 0% |
| 100% | 0% | 50% |
| 0% | 100% | 50% |
| 100% | 100% | 100% |

Do not assign the X56 throttle device to High-G: High-G needs a button binding,
but reads pitch from the device assigned to **Pitch & Roll**.

## Configuration and diagnostics

Runtime files are stored next to the executable:

```text
Config/
├── appsettings.json
├── bindings.json
├── Logs/
│   ├── JFlightShaker.log
│   └── JFlightShaker.previous.log
└── profiles/
    ├── throttle_effect.json
    ├── gun_effect.json
    ├── missile_effect.json
    └── high_g_effect.json
```

If a JSON file cannot be read, the application renames it to
`*.broken-YYYYMMDD-HHMMSS.json`, records the problem in the log and creates safe
defaults. Use **Open Config Folder** to reach these files.

Configuration contains local device names and GUIDs. Do not attach it publicly
without checking its contents.

## Building from source

Requirements:

- Windows 10 or 11
- .NET 8 SDK

```powershell
dotnet restore JFlightShaker.sln
dotnet test JFlightShaker.sln -c Release
dotnet publish JFlightShaker.csproj -c Release -o publish
```

## Privacy

JFlightShaker does not contain telemetry, analytics or an updater. Controller
states, configuration and logs remain on the local computer.

## Contributing

Bug reports, translations and additional HOTAS profiles are welcome. Read
[CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Please use the
provided issue templates and attach the diagnostic log only after reviewing it.

## License

Source code is available under the [MIT License](LICENSE). Third-party packages
retain their own licenses.

---

# Español

JFlightShaker C.E convierte entradas DirectInput del acelerador, joystick y
botones en efectos arcade para bass shakers. No utiliza telemetría del juego,
por lo que puede funcionar con cualquier simulador compatible con el mismo
controlador.

## Funciones principales

- Vibración de motor según el acelerador y postcombustión por encima del 85%.
- Dos palancas del X56 combinadas: cada una aporta el 50% y juntas el 100%.
- Cabeceo y alabeo estéreo, modulados moderadamente por el acelerador.
- Efectos de cañón, misil y maniobra High-G.
- Silenciado mediante botón del HOTAS o teclado, con Trigger o Hold.
- Prueba en directo de ejes, botones y POV hats.
- Previsualización, intensidad, estado activo y restauración por efecto.
- Reconexión automática de dispositivos.
- Interfaz en inglés, español y catalán.
- Recuperación automática de configuraciones JSON dañadas.
- Registro local de errores, sin telemetría.

## Instalación rápida

1. Descarga el ZIP desde **Releases**.
2. Extrae todo su contenido en una carpeta con permisos de escritura.
3. Ejecuta `JFlightShaker.exe`.
4. Selecciona el dispositivo de audio conectado al amplificador.
5. Usa **Test Device** para comprobar el HOTAS.
6. Haz clic derecho sobre cada efecto para asignarlo.
7. Prueba con intensidad y volumen bajos antes de pulsar **Start**.

La versión publicada incluye .NET y no requiere instalarlo por separado.

## Ayuda y errores

Los archivos se encuentran en la carpeta `Config` situada junto al ejecutable.
El registro de diagnóstico está en `Config/Logs/JFlightShaker.log`. Si una
configuración está dañada, se conserva automáticamente como archivo `.broken`.

Consulta [CONTRIBUTING.md](CONTRIBUTING.md) para comunicar errores o aportar
perfiles de otros HOTAS.
