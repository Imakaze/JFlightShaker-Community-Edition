namespace JFlightShaker.UI;

public static class UiText
{
    public static string Language { get; set; } = "es";

    public static string Get(string key)
    {
        var english = new Dictionary<string, string>
        {
            ["Edit"] = "Edit", ["Unbind"] = "Unbind selected",
            ["EditBinding"] = "Edit binding", ["Device"] = "Device:",
            ["Type"] = "Type:", ["EditControl"] = "Configure control",
            ["Reset"] = "Reset defaults", ["Cancel"] = "Cancel", ["Save"] = "Save",
            ["Button"] = "Button:", ["Detect"] = "Detect input",
            ["Detecting"] = "Waiting for input…", ["Trigger"] = "Trigger:",
            ["Press"] = "Press", ["Hold"] = "Hold",
            ["Threshold"] = "High-G threshold:", ["Axis"] = "Axis",
            ["SelectOne"] = "Select exactly one effect to edit.",
            ["Effect.ThrottleAxis"] = "Throttle",
            ["Effect.PitchAndRoll"] = "Pitch & Roll",
            ["Effect.Gun"] = "Gun Fire", ["Effect.Missile"] = "Missile",
            ["Effect.HighGTurn"] = "High-G Turn",
            ["Effect.MuteEffects"] = "Mute Effects",
            ["Status.Stopped"] = "Stopped", ["Status.Running"] = "Running",
            ["Status.Active"] = "Active", ["Status.Disabled"] = "Disabled",
            ["Status.Muted"] = "Muted", ["Status.On"] = "On",
            ["Status.Off"] = "Off"
        };
        var spanish = new Dictionary<string, string>
        {
            ["Edit"] = "Editar", ["Unbind"] = "Borrar asignación",
            ["EditBinding"] = "Editar asignación", ["Device"] = "Dispositivo:",
            ["Type"] = "Tipo:", ["EditControl"] = "Configurar control",
            ["Reset"] = "Restaurar valores", ["Cancel"] = "Cancelar",
            ["Save"] = "Guardar", ["Button"] = "Botón:",
            ["Detect"] = "Detectar entrada",
            ["Detecting"] = "Esperando pulsación…", ["Trigger"] = "Activación:",
            ["Press"] = "Al pulsar", ["Hold"] = "Mientras se mantiene",
            ["Threshold"] = "Umbral High-G:", ["Axis"] = "Eje",
            ["SelectOne"] = "Selecciona exactamente un efecto para editarlo.",
            ["Effect.ThrottleAxis"] = "Acelerador",
            ["Effect.PitchAndRoll"] = "Cabeceo y alabeo",
            ["Effect.Gun"] = "Cañón", ["Effect.Missile"] = "Misil",
            ["Effect.HighGTurn"] = "Maniobra High-G",
            ["Effect.MuteEffects"] = "Silenciar efectos",
            ["Status.Stopped"] = "Detenido", ["Status.Running"] = "Funcionando",
            ["Status.Active"] = "Activo", ["Status.Disabled"] = "Desactivado",
            ["Status.Muted"] = "Silenciado", ["Status.On"] = "Activo",
            ["Status.Off"] = "Inactivo"
        };
        var catalan = new Dictionary<string, string>
        {
            ["Edit"] = "Edita", ["Unbind"] = "Esborra l'assignació",
            ["EditBinding"] = "Edita l'assignació", ["Device"] = "Dispositiu:",
            ["Type"] = "Tipus:", ["EditControl"] = "Configura el control",
            ["Reset"] = "Restaura els valors", ["Cancel"] = "Cancel·la",
            ["Save"] = "Desa", ["Button"] = "Botó:",
            ["Detect"] = "Detecta l'entrada",
            ["Detecting"] = "Esperant una pulsació…", ["Trigger"] = "Activació:",
            ["Press"] = "En prémer", ["Hold"] = "Mentre es manté",
            ["Threshold"] = "Llindar High-G:", ["Axis"] = "Eix",
            ["SelectOne"] = "Selecciona exactament un efecte per editar-lo.",
            ["Effect.ThrottleAxis"] = "Accelerador",
            ["Effect.PitchAndRoll"] = "Capcineig i balanceig",
            ["Effect.Gun"] = "Canó", ["Effect.Missile"] = "Míssil",
            ["Effect.HighGTurn"] = "Maniobra High-G",
            ["Effect.MuteEffects"] = "Silencia els efectes",
            ["Status.Stopped"] = "Aturat", ["Status.Running"] = "Funcionant",
            ["Status.Active"] = "Actiu", ["Status.Disabled"] = "Desactivat",
            ["Status.Muted"] = "Silenciat", ["Status.On"] = "Actiu",
            ["Status.Off"] = "Inactiu"
        };

        var selected = Language == "es"
            ? spanish
            : Language == "ca"
                ? catalan
                : english;
        return selected.GetValueOrDefault(
            key,
            english.GetValueOrDefault(key, key));
    }
}
