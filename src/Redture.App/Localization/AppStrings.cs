namespace Redture.App.Localization;

/// <summary>
/// Every piece of text the interface shows, in one language.
/// </summary>
/// <remarks>
/// <para>
/// A plain object with two instances rather than resource files. Resource files
/// win when translators need to work without the codebase and when there are
/// dozens of languages; with two languages maintained by the same person, they
/// buy tooling nobody is using and cost the ability to see both wordings side
/// by side while writing them.
/// </para>
/// <para>
/// Several entries are formats rather than sentences, because word order
/// differs between the two languages and building sentences by concatenation
/// produces text that reads like a machine wrote it.
/// </para>
/// </remarks>
public sealed class AppStrings
{
    public required string LanguageCode { get; init; }

    // --- Header ---------------------------------------------------------
    public required string Tagline { get; init; }

    public required string HelpTooltip { get; init; }

    // --- Master switch --------------------------------------------------
    public required string CorrectionsOn { get; init; }

    public required string CorrectionsOff { get; init; }

    public required string CorrectionsHint { get; init; }

    // --- Brightness -----------------------------------------------------
    public required string Brightness { get; init; }

    public required string OverlayHint { get; init; }

    public required string BacklightNone { get; init; }

    /// <summary>{0} = list of displays, {1} = handover percentage.</summary>
    public required string BacklightSummaryFormat { get; init; }

    public required string MechanismDdcCi { get; init; }

    public required string MechanismPanel { get; init; }

    // --- Colour temperature ---------------------------------------------
    public required string ColourTemperature { get; init; }

    public required string ScaleWarm { get; init; }

    public required string ScaleCool { get; init; }

    public required string GammaHint { get; init; }

    /// <summary>{0} = neutral temperature in kelvin.</summary>
    public required string TemperatureNeutralFormat { get; init; }

    public required string TemperatureUnsupported { get; init; }

    public required string TemperatureRejected { get; init; }

    /// <summary>{0} = display names.</summary>
    public required string TemperatureHdrFormat { get; init; }

    // --- Gamma range ----------------------------------------------------
    public required string GammaRangeTitle { get; init; }

    public required string GammaRangeRestricted { get; init; }

    public required string GammaRangePending { get; init; }

    public required string GammaRangeUnlocked { get; init; }

    public required string GammaRangeButton { get; init; }

    // --- Conflict -------------------------------------------------------
    public required string ConflictTitle { get; init; }

    /// <summary>{0} = application names.</summary>
    public required string ConflictNamedFormat { get; init; }

    public required string ConflictAnonymous { get; init; }

    // --- Automation -----------------------------------------------------
    public required string AutomationOn { get; init; }

    public required string AutomationOff { get; init; }

    public required string ScheduleDisabled { get; init; }

    public required string ScheduleWaiting { get; init; }

    public required string PhaseDay { get; init; }

    public required string PhaseSunset { get; init; }

    public required string PhaseNight { get; init; }

    public required string PhaseSunrise { get; init; }

    /// <summary>{0} = phase, {1} = kelvin, {2} = time of next change.</summary>
    public required string ScheduleStatusFormat { get; init; }

    /// <summary>{0} = override description, {1} = time remaining.</summary>
    public required string OverrideTimedFormat { get; init; }

    /// <summary>{0} = override description.</summary>
    public required string OverrideIndefiniteFormat { get; init; }

    public required string ScheduleNoLocation { get; init; }

    public required string SchedulePolarDay { get; init; }

    public required string Daytime { get; init; }

    public required string Night { get; init; }

    public required string TransitionLength { get; init; }

    public required string TransitionHint { get; init; }

    public required string AnchorSun { get; init; }

    public required string AnchorClock { get; init; }

    public required string Latitude { get; init; }

    public required string Longitude { get; init; }

    public required string Sunrise { get; init; }

    public required string Sunset { get; init; }

    public required string LocationPrivacy { get; init; }

    public required string OverrideTitle { get; init; }

    public required string PauseHour { get; init; }

    public required string PauseUntilMorning { get; init; }

    public required string CinemaMode { get; init; }

    public required string ResumeSchedule { get; init; }

    public required string OverrideHint { get; init; }

    public required string PausedForAnHour { get; init; }

    public required string PausedUntilMorning { get; init; }

    public required string CinemaModeLabel { get; init; }

    public required string MinutesShort { get; init; }

    public required string HoursMinutesShort { get; init; }

    public required string LessThanAMinute { get; init; }

    // --- Startup --------------------------------------------------------
    public required string StartupOn { get; init; }

    public required string StartupOff { get; init; }

    public required string StartupHint { get; init; }

    // --- Displays -------------------------------------------------------
    public required string NoDisplays { get; init; }

    public required string OneDisplay { get; init; }

    /// <summary>{0} = count.</summary>
    public required string DisplaysFormat { get; init; }

    public required string Refresh { get; init; }

    public required string Primary { get; init; }

    public required string NoDisplayBackend { get; init; }

    public static AppStrings English { get; } = new()
    {
        LanguageCode = "en",
        Tagline = "Colour temperature and below-minimum brightness, in one control.",
        HelpTooltip =
            "Press Ctrl + Alt + Shift + R at any time to reset brightness and colour to neutral.\n\n"
            + "Closing this window keeps Redture running in the tray. Use the tray menu to exit.",

        CorrectionsOn = "Corrections enabled",
        CorrectionsOff = "Corrections disabled",
        CorrectionsHint = "Master switch. When off, Redture leaves the display completely untouched.",

        Brightness = "Brightness",
        OverlayHint =
            "Below the handover the screen is dimmed with a click-through black overlay, which is a pure "
            + "multiply: black stays black and contrast is preserved.",
        BacklightNone = "No backlight control detected on this display, so the whole range is dimmed in software.",
        BacklightSummaryFormat =
            "Backlight control: {0}. Above {1:0}% the slider drives the real backlight; below it, the overlay takes over.",
        MechanismDdcCi = "DDC/CI",
        MechanismPanel = "built-in panel",

        ColourTemperature = "Colour temperature",
        ScaleWarm = "red",
        ScaleCool = "cool",
        GammaHint =
            "Applied through the display's colour lookup table, before compositing — so the tint costs nothing "
            + "to draw and never lifts the black level.",
        TemperatureNeutralFormat = "{0} K is the neutral white point: no tint is applied.",
        TemperatureUnsupported =
            "No display accepted a colour lookup table, so colour temperature cannot be applied on this machine.",
        TemperatureRejected =
            "Windows refused this ramp. It limits how far a gamma ramp may deviate from linear, which is what "
            + "strongly warm settings run into.",
        TemperatureHdrFormat =
            "{0} is in HDR mode. Windows ignores gamma ramps there, so colour temperature has no effect on it. "
            + "Turning HDR off restores it.",

        GammaRangeTitle = "Extended gamma range",
        GammaRangeRestricted =
            "Windows limits how far a gamma ramp may deviate from linear, which caps how warm any colour tool "
            + "can go. Lifting it is a machine-wide change that needs administrator rights and a sign-out.",
        GammaRangePending = "The extended range is set but not yet active. Sign out and back in to apply it.",
        GammaRangeUnlocked = "The extended gamma range is active, so the full warmth of the slider is available.",
        GammaRangeButton = "Unlock extended range…",

        ConflictTitle = "Colour conflict",
        ConflictNamedFormat =
            "{0} is also adjusting your display's colour. Both applications write the same table, so whichever "
            + "writes last wins. Close one of them.",
        ConflictAnonymous =
            "Another application is also adjusting your display's colour. Both write the same table, so "
            + "whichever writes last wins.",

        AutomationOn = "Follow time of day",
        AutomationOff = "Manual control only",
        ScheduleDisabled = "The schedule is off; the slider above is in charge.",
        ScheduleWaiting = "Waiting for the first evaluation…",
        PhaseDay = "Daytime",
        PhaseSunset = "Warming for the evening",
        PhaseNight = "Night",
        PhaseSunrise = "Cooling for the morning",
        ScheduleStatusFormat = "{0} — {1} K, next change at {2}.",
        OverrideTimedFormat = "{0} — resuming in {1}.",
        OverrideIndefiniteFormat = "{0} — until you resume it.",
        ScheduleNoLocation = "No location set, so the fixed times below are being used instead of the sun.",
        SchedulePolarDay = "The sun does not cross the horizon here today, so the fixed times below are being used.",

        Daytime = "Daytime",
        Night = "Night",
        TransitionLength = "Transition length",
        TransitionHint =
            "The change begins at sunset and sunrise and takes this long. Interpolated in mired rather than "
            + "kelvin, so it feels evenly paced from start to finish.",
        AnchorSun = "Anchor to the sun",
        AnchorClock = "Anchor to fixed times",
        Latitude = "Latitude",
        Longitude = "Longitude",
        Sunrise = "Sunrise",
        Sunset = "Sunset",
        LocationPrivacy = "Used locally to compute sunrise and sunset. Redture makes no network requests.",
        OverrideTitle = "Override",
        PauseHour = "Pause 1 hour",
        PauseUntilMorning = "Until morning",
        CinemaMode = "Cinema mode",
        ResumeSchedule = "Resume schedule",
        OverrideHint = "Pausing returns the screen to the daytime setting. Brightness keeps working throughout.",
        PausedForAnHour = "Paused for an hour",
        PausedUntilMorning = "Paused until morning",
        CinemaModeLabel = "Cinema mode",
        MinutesShort = "{0} min",
        HoursMinutesShort = "{0} h {1} min",
        LessThanAMinute = "less than a minute",

        StartupOn = "Starts with Windows",
        StartupOff = "Does not start with Windows",
        StartupHint =
            "Registered per user, with no arguments, so a launch at sign-in goes straight to the tray without "
            + "opening this panel. It appears in Task Manager's startup tab like anything else.",

        NoDisplays = "No displays detected",
        OneDisplay = "1 display detected",
        DisplaysFormat = "{0} displays detected",
        Refresh = "Refresh",
        Primary = "primary",
        NoDisplayBackend = "No display backend on this platform yet.",
    };

    public static AppStrings Spanish { get; } = new()
    {
        LanguageCode = "es",
        Tagline = "Temperatura de color y brillo por debajo del mínimo, en un solo control.",
        HelpTooltip =
            "Presioná Ctrl + Alt + Shift + R en cualquier momento para devolver el brillo y el color a neutro.\n\n"
            + "Cerrar esta ventana deja Redture corriendo en la bandeja. Usá el menú de la bandeja para salir.",

        CorrectionsOn = "Correcciones activadas",
        CorrectionsOff = "Correcciones desactivadas",
        CorrectionsHint = "Interruptor principal. Apagado, Redture no toca la pantalla en absoluto.",

        Brightness = "Brillo",
        OverlayHint =
            "Por debajo del punto de traspaso la pantalla se oscurece con una capa negra que deja pasar los "
            + "clics. Es una multiplicación pura: el negro sigue siendo negro y el contraste se mantiene.",
        BacklightNone =
            "No se detectó control de retroiluminación en esta pantalla, así que todo el rango se oscurece por software.",
        BacklightSummaryFormat =
            "Control de retroiluminación: {0}. Por encima del {1:0}% el slider maneja la retroiluminación real; "
            + "por debajo, toma el control la capa de oscurecimiento.",
        MechanismDdcCi = "DDC/CI",
        MechanismPanel = "panel integrado",

        ColourTemperature = "Temperatura de color",
        ScaleWarm = "rojo",
        ScaleCool = "frío",
        GammaHint =
            "Se aplica en la tabla de color de la pantalla, antes de componer — así el tinte no cuesta nada de "
            + "dibujar y nunca levanta el nivel de negro.",
        TemperatureNeutralFormat = "{0} K es el punto blanco neutro: no se aplica ningún tinte.",
        TemperatureUnsupported =
            "Ninguna pantalla aceptó una tabla de color, así que la temperatura no se puede aplicar en esta máquina.",
        TemperatureRejected =
            "Windows rechazó esta curva. Limita cuánto puede alejarse de lineal, que es justo con lo que chocan "
            + "los ajustes muy cálidos.",
        TemperatureHdrFormat =
            "{0} está en modo HDR. Windows ignora las tablas de color ahí, así que la temperatura no le hace "
            + "efecto. Apagando HDR vuelve a funcionar.",

        GammaRangeTitle = "Rango de gamma extendido",
        GammaRangeRestricted =
            "Windows limita cuánto puede alejarse de lineal una curva de gamma, y eso pone un techo a lo cálida "
            + "que puede ponerse cualquier herramienta de color. Levantarlo es un cambio de toda la máquina que "
            + "necesita permisos de administrador y cerrar sesión.",
        GammaRangePending = "El rango extendido quedó configurado pero todavía no está activo. Cerrá sesión y volvé a entrar.",
        GammaRangeUnlocked = "El rango de gamma extendido está activo, así que tenés toda la calidez del slider disponible.",
        GammaRangeButton = "Desbloquear rango extendido…",

        ConflictTitle = "Conflicto de color",
        ConflictNamedFormat =
            "{0} también está ajustando el color de tu pantalla. Las dos aplicaciones escriben la misma tabla, "
            + "así que gana la que escribe última. Cerrá una de las dos.",
        ConflictAnonymous =
            "Otra aplicación también está ajustando el color de tu pantalla. Las dos escriben la misma tabla, "
            + "así que gana la que escribe última.",

        AutomationOn = "Seguir la hora del día",
        AutomationOff = "Solo control manual",
        ScheduleDisabled = "La automatización está apagada; manda el slider de arriba.",
        ScheduleWaiting = "Esperando la primera evaluación…",
        PhaseDay = "Día",
        PhaseSunset = "Entrando en calidez para la noche",
        PhaseNight = "Noche",
        PhaseSunrise = "Enfriando para la mañana",
        ScheduleStatusFormat = "{0} — {1} K, próximo cambio a las {2}.",
        OverrideTimedFormat = "{0} — se reanuda en {1}.",
        OverrideIndefiniteFormat = "{0} — hasta que lo reanudes.",
        ScheduleNoLocation = "No hay ubicación configurada, así que se usan los horarios fijos de abajo en vez del sol.",
        SchedulePolarDay = "Hoy el sol no cruza el horizonte acá, así que se usan los horarios fijos de abajo.",

        Daytime = "De día",
        Night = "De noche",
        TransitionLength = "Duración de la transición",
        TransitionHint =
            "El cambio empieza al atardecer y al amanecer, y dura esto. Se interpola en mired en vez de kelvin, "
            + "así que se siente parejo de principio a fin.",
        AnchorSun = "Anclar al sol",
        AnchorClock = "Anclar a horarios fijos",
        Latitude = "Latitud",
        Longitude = "Longitud",
        Sunrise = "Amanecer",
        Sunset = "Atardecer",
        LocationPrivacy = "Se usa localmente para calcular el amanecer y el atardecer. Redture no hace ninguna petición de red.",
        OverrideTitle = "Pausar",
        PauseHour = "Pausar 1 hora",
        PauseUntilMorning = "Hasta la mañana",
        CinemaMode = "Modo cine",
        ResumeSchedule = "Reanudar",
        OverrideHint = "Pausar devuelve la pantalla al ajuste de día. El brillo sigue funcionando igual.",
        PausedForAnHour = "Pausado por una hora",
        PausedUntilMorning = "Pausado hasta la mañana",
        CinemaModeLabel = "Modo cine",
        MinutesShort = "{0} min",
        HoursMinutesShort = "{0} h {1} min",
        LessThanAMinute = "menos de un minuto",

        StartupOn = "Arranca con Windows",
        StartupOff = "No arranca con Windows",
        StartupHint =
            "Se registra por usuario y sin argumentos, así que al iniciar sesión va directo a la bandeja sin "
            + "abrir este panel. Aparece en la pestaña de inicio del Administrador de tareas como cualquier otra.",

        NoDisplays = "No se detectaron pantallas",
        OneDisplay = "1 pantalla detectada",
        DisplaysFormat = "{0} pantallas detectadas",
        Refresh = "Actualizar",
        Primary = "principal",
        NoDisplayBackend = "Todavía no hay soporte de pantallas en esta plataforma.",
    };

    /// <summary>
    /// Resolves a stored language code, falling back to English for anything
    /// unrecognised — including an empty settings file.
    /// </summary>
    public static AppStrings For(string? languageCode) =>
        string.Equals(languageCode, "es", StringComparison.OrdinalIgnoreCase) ? Spanish : English;
}
