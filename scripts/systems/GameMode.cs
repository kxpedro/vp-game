using System.Collections.Generic;

// O 2D em pixel art (kit da cripta, sprite do Virgil) é a versão oficial do jogo: o menu sempre abre nele.
// O 3D original continua no projeto (scenes/game), mas não é mais oferecido no menu; só as ferramentas de debug o usam
// (--ui-mode=3d no UiPreview). Cada modo tem as próprias cenas (scenes/game e scenes/pixel) e os próprios saves.
public static class GameMode
{
    public enum Kind
    {
        ThreeD,
        PixelArt
    }

    public static Kind Current { get; set; } = Kind.PixelArt;
    public static bool IsPixelArt => Current == Kind.PixelArt;

    // As cenas 2D têm o mesmo nome de arquivo das 3D (HUD e áudio reconhecem a cena pelo nome).
    private static readonly Dictionary<string, string> PixelScenes = new()
    {
        ["res://scenes/game/CrowsNest.tscn"] = "res://scenes/pixel/CrowsNest.tscn",
        ["res://scenes/game/RunStartBlessingRoom.tscn"] = "res://scenes/pixel/RunStartBlessingRoom.tscn",
        ["res://scenes/game/TestArena.tscn"] = "res://scenes/pixel/TestArena.tscn",
        ["res://scenes/game/IntermissionChamber.tscn"] = "res://scenes/pixel/IntermissionChamber.tscn",
    };

    // Caminho da cena no modo atual; quem troca de cena passa o caminho 3D por aqui.
    public static string Scene(string path)
    {
        if (!IsPixelArt || string.IsNullOrEmpty(path))
            return path;
        return PixelScenes.TryGetValue(path, out var pixel) ? pixel : path;
    }

    // Saves separados: o 2D grava em user://pixel_save_slot_N.json.
    public static string SavePrefix => IsPixelArt ? "user://pixel_save_slot_" : "user://save_slot_";
}
