// Parametros do indicador de inimigos fora da tela (OffscreenEnemyIndicator, scripts/ui).
public static partial class DevParameterRegistry
{
    private const string GHud = "HUD";

    private static void RegisterOffscreenIndicator()
    {
        P("hud.offscreen.enabled", "Indicador fora da tela: ligado (0/1)", GHud, 1, 0, 1, 1, true);
        P("hud.offscreen.depth", "Indicador fora da tela: profundidade do brilho (px lógicos)", GHud, 12, 4, 80, 2, false, DevApplyMode.Live, "Tela lógica de 480 px de largura.");
        P("hud.offscreen.sharpness", "Indicador fora da tela: estreiteza do arco", GHud, 18, 1, 40, 0.5, false, DevApplyMode.Live, "Maior = brilho mais concentrado na direção do inimigo.");
        P("hud.offscreen.intensity", "Indicador fora da tela: intensidade", GHud, 1, 0.2, 2, 0.05, false);
        P("hud.offscreen.near_distance", "Indicador fora da tela: distância 'perto' (m)", GHud, 10, 0, 100, 1, false);
        P("hud.offscreen.far_distance", "Indicador fora da tela: distância 'longe' (m)", GHud, 40, 1, 200, 1, false);
        P("hud.offscreen.max_count", "Indicador fora da tela: máx. de inimigos", GHud, 3, 1, 8, 1, true);
    }
}
