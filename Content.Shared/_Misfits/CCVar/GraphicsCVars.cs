// #Cythisiax Added - Client toggles for the screen distortion pass (explosion shockwaves, heat haze
// and the explosion flash). Reduced motion in accessibility settings turns all of it off as well.

using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Misfits.CCVar;

[CVarDefs]
public sealed class GraphicsCVars : CVars
{
    public static readonly CVarDef<bool> ScreenDistortion =
        CVarDef.Create("misfits.screen_distortion", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ScreenFlash =
        CVarDef.Create("misfits.screen_flash", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    // #Cythisiax Added - fire light flicker (FireFlickerSystem); off leaves fires at steady brightness
    public static readonly CVarDef<bool> FireFlicker =
        CVarDef.Create("misfits.fire_flicker", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
