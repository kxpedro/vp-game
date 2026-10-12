using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

// Autoteste do algoritmo (so debug): F:/GODOT/App/Godot.exe --headless --path . -- --dlg-selftest
// Usa definicoes em memoria (11.2 da spec); sem slot ativo nada e gravado em disco.
public partial class DialogueManager
{
    private bool _selfTestFailed;

    private void RunSelfTest()
    {
        DialogueProgress.Reset();
        try { SelfTestSteps(); }
        catch (Exception exception) { Report(0, false, "excecao: " + exception); }
        try { SelfTestSaveIo(); }
        catch (Exception exception) { Report(0, false, "excecao (save io): " + exception); }
        finally { SaveGameManager.Instance?.EndTestSlots(); CleanTestSlotFiles(); }
        DialogueProgress.Reset();
        GD.Print(_selfTestFailed ? "DLG-SELFTEST RESULT FAIL" : "DLG-SELFTEST RESULT PASS");
        GetTree().Quit(_selfTestFailed ? 1 : 0);
    }

    private void Report(int step, bool ok, string detail)
    {
        if (!ok) _selfTestFailed = true;
        GD.Print($"DLG-SELFTEST {(ok ? "PASS" : "FAIL")} {step} {detail}");
    }

    private static DialogueDefinition Def(string id, DialogueCategory category, DialoguePriorityClass cls, int priority, bool repeatable,
        string requirementsJson = "[]", string speaker = "odalric", float chance = 1.0f, DialogueTrigger trigger = DialogueTrigger.None,
        DialogueChoice[] choices = null, Dictionary<string, DialogueBranch> branches = null, string[] flags = null)
    {
        var requirements = new List<DialogueCondition>();
        using (var document = JsonDocument.Parse(requirementsJson))
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var parsed = DialogueConditions.Parse(item, out var error);
                if (parsed == null) throw new InvalidOperationException($"{id}: {error}");
                requirements.Add(parsed);
            }
        return new DialogueDefinition(id, speaker, category, trigger, cls, priority, repeatable, 4, chance, false,
            requirements.ToArray(), flags ?? Array.Empty<string>(),
            new[] { new DialogueLine("dlg.selftest." + id, "", "", "") },
            choices ?? Array.Empty<DialogueChoice>(),
            branches ?? new Dictionary<string, DialogueBranch>());
    }

    private void SelfTestSteps()
    {
        var curt = new DialogueChoice("curt", "dlg.selftest.curt", new[] { "was_curt_with_odalric" }, "curt_reply");
        var who = new DialogueChoice("who", "dlg.selftest.who", new[] { "asked_odalric_name" }, "");
        var branches = new Dictionary<string, DialogueBranch>
        {
            ["curt_reply"] = new DialogueBranch("curt_reply", new[] { new DialogueLine("dlg.selftest.curt_reply", "", "", "") }, Array.Empty<string>())
        };
        var first = Def("first_hub_arrival", DialogueCategory.Hub, DialoguePriorityClass.Essential, 90, false,
            choices: new[] { who, curt }, branches: branches, flags: new[] { "met_odalric" });
        var seenFirst = "[{\"type\":\"seen\",\"id\":\"first_hub_arrival\"}]";
        var evergreens = new[]
        {
            Def("odalric_lineages_a", DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, true, seenFirst),
            Def("odalric_lineages_b", DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, true, seenFirst),
            Def("odalric_armand_rumor", DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, true, seenFirst),
            Def("odalric_bench_book", DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, true, "[{\"type\":\"weapon\",\"id\":\"longsword\"}]")
        };
        var firstDeath = Def("odalric_first_death", DialogueCategory.Death, DialoguePriorityClass.Specific, 50, false,
            "[{\"type\":\"deaths\",\"min\":1},{\"type\":\"last_run_outcome\",\"value\":\"died\"}]");
        var deathByBoss = Def("odalric_death_by_boss1", DialogueCategory.Death, DialoguePriorityClass.Specific, 70, false,
            "[{\"type\":\"last_run_outcome\",\"value\":\"died\"},{\"type\":\"died_at_boss\",\"id\":\"invisible_woman\"}]");
        var afterBoss = Def("odalric_after_first_boss", DialogueCategory.Hub, DialoguePriorityClass.Specific, 60, false,
            "[{\"type\":\"boss_defeated\",\"id\":\"invisible_woman\"}]");
        var all = new List<DialogueDefinition> { first, firstDeath, deathByBoss, afterBoss };
        all.AddRange(evergreens);
        var rng = new RandomNumberGenerator { Seed = 1 };
        var evergreenIds = evergreens.Select(d => d.Id).ToHashSet();

        // 1
        var pick = SelectForNpc(all, "odalric", rng);
        Report(1, pick?.Id == "first_hub_arrival", $"peek={pick?.Id}");

        // 2
        var playback = CreatePlayback(first, false);
        playback.Advance();
        var onChoice = playback.Frame.IsChoice;
        playback.Choose(1);
        var finishedBranch = !playback.IsFinished;
        playback.Advance();
        EndPlayback(playback);
        Report(2, DialogueProgress.GetSeen("first_hub_arrival") == 1 && DialogueProgress.GetPlays("npc") == 1
            && DialogueProgress.HasFlag("met_odalric") && DialogueProgress.HasFlag("was_curt_with_odalric")
            && !DialogueProgress.HasFlag("asked_odalric_name") && onChoice && finishedBranch && playback.IsFinished && CurrentPlayback == null,
            $"seen={DialogueProgress.GetSeen("first_hub_arrival")} plays={DialogueProgress.GetPlays("npc")} choice={playback.ChosenChoiceId}");

        // 3 e 4: quatro visitas = as quatro evergreen, uma vez cada
        var order = new List<string>();
        for (var visit = 0; visit < 4; visit++)
        {
            var next = SelectForNpc(all, "odalric", rng);
            if (next == null) break;
            order.Add(next.Id);
            DialogueProgress.MarkSeen(next);
        }
        Report(3, order.Count > 0 && order[0] != "first_hub_arrival" && evergreenIds.Contains(order[0]), $"primeira={order.FirstOrDefault()}");
        Report(4, order.Count == 4 && order.Distinct().Count() == 4 && order.All(evergreenIds.Contains), string.Join(",", order));

        // 5: cooldown 4. Plays=5; sem fallback ninguem volta; com fallback a mais antiga; apos 2 fillers a mais antiga elegivel vence
        var pool = all.Where(d => d.SpeakerId == "odalric" && d.Category == DialogueCategory.Hub);
        var none = DialogueSelector.Select(pool, new DialogueContext(-1), rng, false);
        var oldest = DialogueSelector.Select(pool, new DialogueContext(-1), rng, true);
        for (var i = 0; i < 2; i++) DialogueProgress.MarkSeen(Def("filler_" + i, DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, false));
        var eligible = DialogueSelector.Select(pool, new DialogueContext(-1), rng, false);
        Report(5, none == null && oldest?.Id == order[0] && eligible?.Id == order[0],
            $"semFallback={none?.Id} fallback={oldest?.Id} aposCooldown={eligible?.Id} esperado={order[0]}");

        // 6
        RunState.StartRun();
        RunState.FailRun();
        Report(6, DialogueProgress.RunsStarted == 1 && DialogueProgress.Deaths == 1 && DialogueProgress.LastRun?.Outcome == RunOutcome.Died,
            $"runs={DialogueProgress.RunsStarted} deaths={DialogueProgress.Deaths} last={DialogueProgress.LastRun?.Outcome}");

        // 7
        pick = SelectForNpc(all, "odalric", rng);
        Report(7, pick?.Id == "odalric_first_death", $"peek={pick?.Id}");
        DialogueProgress.MarkSeen(pick);

        // 8: morte no boss 1 (sala 7 do mapa 0 = sala de boss)
        DialogueProgress.MarkBossDefeated("invisible_woman");
        DialogueProgress.OnRunEnded(true, 0, 7, true);
        var a = SelectForNpc(all, "odalric", rng);
        DialogueProgress.MarkSeen(a);
        var b = SelectForNpc(all, "odalric", rng);
        Report(8, a?.Id == "odalric_death_by_boss1" && b?.Id == "odalric_after_first_boss", $"primeira={a?.Id} segunda={b?.Id}");

        // 9: ida e volta pelo JSON do save
        var json = JsonSerializer.Serialize(DialogueProgress.ToSaveData());
        var before = DialogueProgress.ToSaveData();
        DialogueProgress.LoadSaveData(JsonSerializer.Deserialize<DialogueSaveData>(json));
        var after = DialogueProgress.ToSaveData();
        var same = before.RunsStarted == after.RunsStarted && before.Deaths == after.Deaths && before.RunsCompleted == after.RunsCompleted
            && before.Seen.Count == after.Seen.Count && before.Seen.All(p => after.Seen.TryGetValue(p.Key, out var v) && v == p.Value)
            && before.Plays.Count == after.Plays.Count && before.Plays.All(p => after.Plays.TryGetValue(p.Key, out var v) && v == p.Value)
            && before.Flags.SequenceEqual(after.Flags) && before.BossesDefeated.SequenceEqual(after.BossesDefeated);
        Report(9, same && DialogueProgress.LastRun == null && after.Seen.Count > 0, $"seen={after.Seen.Count} flags={after.Flags.Count} lastRun={DialogueProgress.LastRun}");

        // 10: chance 0,6 em 20 sorteios (semente fixa) e regras globais de bark
        var bark = Def("bark_low_health_1", DialogueCategory.Bark, DialoguePriorityClass.Evergreen, 0, true, speaker: "virgil", chance: 0.6f, trigger: DialogueTrigger.LowHealth);
        rng.Seed = 1;
        var hits = 0;
        for (var i = 0; i < 20; i++)
            if (DialogueSelector.Select(new[] { bark }, new DialogueContext(-1), rng, false) != null) hits++;
        var gates = !BarkAllowed(10.0, 0.0, 0) && BarkAllowed(18.0, 0.0, 0) && !BarkAllowed(100.0, 0.0, 2) && BarkAllowed(100.0, 0.0, 1);
        Report(10, hits >= 7 && hits <= 17 && gates, $"acertos={hits}/20 gates={gates}");

        // 11: save v2 sem o campo / com null
        var legacy = JsonSerializer.Deserialize<GameSaveData>("{\"Version\":2,\"CursedBlood\":3}");
        DialogueProgress.LoadSaveData(legacy.Dialogue);
        var okLegacy = legacy.Dialogue != null && DialogueProgress.RunsStarted == 0 && DialogueProgress.Deaths == 0 && !DialogueProgress.IsSeen("first_hub_arrival");
        var withNull = JsonSerializer.Deserialize<GameSaveData>("{\"Version\":2,\"Dialogue\":null}");
        DialogueProgress.LoadSaveData(withNull.Dialogue);
        var withEmpty = JsonSerializer.Deserialize<GameSaveData>("{\"Version\":2,\"Dialogue\":{\"Seen\":null,\"Flags\":null,\"BossesDefeated\":null,\"Plays\":null}}");
        DialogueProgress.LoadSaveData(withEmpty.Dialogue);
        Report(11, okLegacy && DialogueProgress.RunsStarted == 0, $"legacyDialogueNull={legacy.Dialogue == null}");

        // 12: barks nao emudecem (save antigo travado: 4 barks vistos, Plays=4, cooldown 6) com o relogio por oportunidade
        DialogueProgress.Reset();
        var barks = new List<DialogueDefinition>();
        for (var i = 1; i <= 4; i++)
            barks.Add(Def("stuck_bark_" + i, DialogueCategory.Bark, DialoguePriorityClass.Evergreen, 0, true, speaker: "virgil", trigger: DialogueTrigger.RoomEnter));
        DialogueProgress.LoadSaveData(new DialogueSaveData
        {
            Plays = new Dictionary<string, int> { ["bark"] = 4 },
            Seen = new Dictionary<string, int> { ["stuck_bark_1"] = 1, ["stuck_bark_2"] = 2, ["stuck_bark_3"] = 3, ["stuck_bark_4"] = 4 }
        });
        var stuckNow = DialogueSelector.Select(barks, new DialogueContext(-1), rng, false) == null;
        var lateShown = 0;
        for (var i = 0; i < 40; i++)
        {
            DialogueProgress.AdvancePlays("bark");
            var shown = DialogueSelector.Select(barks, new DialogueContext(-1), rng, false);
            if (shown == null) continue;
            DialogueProgress.MarkSeen(shown);
            if (i >= 30) lateShown++;
        }
        Report(12, stuckNow && lateShown > 0, $"travadoSemRelogio={stuckNow} exibidosNoFim={lateShown}");
    }

    private const string TestPrefix = "dlg_selftest_slot_";

    private static void CleanTestSlotFiles()
    {
        foreach (var name in DirAccess.GetFilesAt("user://"))
            if (name.StartsWith("dlg_selftest_slot_", StringComparison.Ordinal))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://" + name));
    }

    private static void WriteTestFile(string name, string text)
    {
        using var file = FileAccess.Open("user://" + name, FileAccess.ModeFlags.Write);
        file.StoreString(text);
    }

    private static string ReadTestFile(string name)
    {
        if (!FileAccess.FileExists("user://" + name)) return null;
        using var file = FileAccess.Open("user://" + name, FileAccess.ModeFlags.Read);
        return file.GetAsText();
    }

    // I/O real em slots de teste (user://dlg_selftest_slot_N.*); os saves reais nunca sao tocados.
    private void SelfTestSaveIo()
    {
        var save = SaveGameManager.Instance;
        if (save == null) { Report(13, false, "SaveGameManager.Instance nulo"); return; }
        CleanTestSlotFiles();
        save.BeginTestSlots(TestPrefix);

        // 13 (a): round-trip pelo caminho de producao (StartSlot -> SaveCurrentSlot atomico -> StartSlot de novo)
        save.StartSlot(1);
        DialogueProgress.Reset();
        var def = Def("rt_a", DialogueCategory.Hub, DialoguePriorityClass.Evergreen, 0, false);
        DialogueProgress.MarkSeen(def);
        DialogueProgress.MarkSeen(def);
        DialogueProgress.AdvancePlays("bark");
        DialogueProgress.SetFlag("flag_x");
        DialogueProgress.SetFlag("flag_y");
        DialogueProgress.MarkBossDefeated("invisible_woman");
        DialogueProgress.OnRunStarted();
        DialogueProgress.OnRunEnded(true, 1, 3, false);
        var expected = JsonSerializer.Serialize(DialogueProgress.ToSaveData());
        save.FlushPendingSave();
        var file1 = ReadTestFile(TestPrefix + "1.json");
        var noTmp = !FileAccess.FileExists("user://" + TestPrefix + "1.json.tmp");
        DialogueProgress.Reset();
        save.StartSlot(1);
        var actual = JsonSerializer.Serialize(DialogueProgress.ToSaveData());
        Report(13, file1 != null && file1.Contains("\"Dialogue\"") && noTmp && expected == actual && DialogueProgress.LastRun == null
            && DialogueProgress.RunsStarted == 1 && DialogueProgress.Deaths == 1 && DialogueProgress.GetSeen("rt_a") == 2 && DialogueProgress.HasFlag("flag_y"),
            $"igual={expected == actual} semTmp={noTmp} json={actual}");

        // 14 (b): save v2 antigo sem o campo Dialogue
        const string v2 = "{\"Version\":2,\"CursedBlood\":7,\"Attributes\":{},\"SkillRanks\":{},\"EquippedWeaponId\":\"longsword\",\"Discoveries\":{}}";
        WriteTestFile(TestPrefix + "2.json", v2);
        var info = save.GetSlotInfo(2);
        save.StartSlot(2);
        var intact = FileAccess.FileExists("user://" + TestPrefix + "2.json") && !FileAccess.FileExists("user://" + TestPrefix + "2.v1.json")
            && !FileAccess.FileExists("user://" + TestPrefix + "2.unreadable.json");
        Report(14, info.Exists && !info.Corrupted && info.CursedBlood == 7 && intact && CharacterSheet.CursedBlood == 7
            && DialogueProgress.RunsStarted == 0 && DialogueProgress.Deaths == 0 && DialogueProgress.GetPlays("npc") == 0 && !DialogueProgress.HasFlag("flag_x"),
            $"exists={info.Exists} corrupted={info.Corrupted} cb={CharacterSheet.CursedBlood} intact={intact}");

        // 15 (c): Dialogue parcial / colecoes nulas / Dialogue null, lidos do disco
        var partial = "{\"Version\":2,\"CursedBlood\":1,\"Dialogue\":{\"Deaths\":3,\"Seen\":null,\"Flags\":null,\"BossesDefeated\":null,\"Plays\":{\"npc\":2}}}";
        WriteTestFile(TestPrefix + "3.json", partial);
        save.StartSlot(3);
        var partialOk = DialogueProgress.Deaths == 3 && DialogueProgress.GetPlays("npc") == 2 && DialogueProgress.RunsStarted == 0;
        var nullDlg = "{\"Version\":2,\"CursedBlood\":1,\"Dialogue\":null}";
        WriteTestFile(TestPrefix + "3.json", nullDlg);
        var nullInfo = save.GetSlotInfo(3);
        save.StartSlot(3);
        Report(15, partialOk && nullInfo.Exists && !nullInfo.Corrupted && DialogueProgress.Deaths == 0 && DialogueProgress.GetPlays("npc") == 0,
            $"parcial={partialOk} nullExists={nullInfo.Exists} deaths={DialogueProgress.Deaths}");

        // 16 (d): novo jogo (ResetPermanentProgress) limpa o Dialogue; slot vazio tambem
        DialogueProgress.SetFlag("f1"); DialogueProgress.MarkBossDefeated("b1"); DialogueProgress.OnRunStarted();
        var dirty = DialogueProgress.RunsStarted == 1;
        CharacterSheet.ResetPermanentProgress();
        var clean = DialogueProgress.RunsStarted == 0 && !DialogueProgress.HasFlag("f1") && !DialogueProgress.IsBossDefeated("b1");
        DialogueProgress.SetFlag("f2");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://" + TestPrefix + "3.json"));
        save.StartSlot(3);
        Report(16, dirty && clean && !DialogueProgress.HasFlag("f2") && DialogueProgress.RunsStarted == 0, $"dirty={dirty} clean={clean} f2={DialogueProgress.HasFlag("f2")}");

        // 17 (e): autosave agrupado (nada em disco antes do Flush; varias mudancas -> uma escrita com tudo)
        save.StartSlot(1);
        var beforeText = ReadTestFile(TestPrefix + "1.json");
        DialogueProgress.SetFlag("auto_a");
        DialogueProgress.SetFlag("auto_b");
        DialogueProgress.AdvancePlays("npc");
        var stillOld = ReadTestFile(TestPrefix + "1.json") == beforeText;
        save.FlushPendingSave();
        var flushed = ReadTestFile(TestPrefix + "1.json");
        DialogueProgress.Reset();
        save.StartSlot(1);
        Report(17, stillOld && flushed != beforeText && DialogueProgress.HasFlag("auto_a") && DialogueProgress.HasFlag("auto_b"),
            $"antesDoFlush={stillOld} gravou={flushed != beforeText}");

        // 18 (f): save v1 continua arquivado como .v1.json e o slot comeca vazio
        const string v1 = "{\"Version\":1,\"CursedBlood\":5}";
        WriteTestFile(TestPrefix + "2.json", v1);
        var infoV1 = save.GetSlotInfo(2);
        var archived = ReadTestFile(TestPrefix + "2.v1.json") == v1 && !FileAccess.FileExists("user://" + TestPrefix + "2.json");
        save.StartSlot(2);
        Report(18, !infoV1.Exists && archived && DialogueProgress.RunsStarted == 0 && CharacterSheet.CursedBlood == 0, $"exists={infoV1.Exists} arquivado={archived}");

        save.EndTestSlots();
        CleanTestSlotFiles();
        var leftovers = DirAccess.GetFilesAt("user://").Count(n => n.StartsWith(TestPrefix, StringComparison.Ordinal));
        Report(19, leftovers == 0, $"restantes={leftovers}");
    }
}
