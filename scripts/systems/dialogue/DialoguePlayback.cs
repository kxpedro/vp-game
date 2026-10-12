using System;
using System.Collections.Generic;

public sealed record DialogueFrame(
    bool IsChoice, string SpeakerId, string SpeakerName, string Text, string Emotion, string PortraitVariant,
    IReadOnlyList<string> ChoiceTexts, int LineNumber, int LineCount);

/// <summary>Roteiro que o HUD percorre: linhas do bloco principal, quadro de escolha e ramo de resposta.</summary>
public sealed class DialoguePlayback
{
    private readonly Action<string> _setFlag;
    private DialogueLine[] _lines;
    private int _index;
    private bool _inChoice;
    private bool _inBranch;

    public DialogueDefinition Definition { get; }
    public bool IsPreview { get; }
    public bool IsFinished { get; private set; }
    /// <summary>"" se nenhuma.</summary>
    public string ChosenChoiceId { get; private set; } = "";

    /// <param name="setFlag">Grava flags (null no preview: nada e gravado).</param>
    public DialoguePlayback(DialogueDefinition definition, bool preview, Action<string> setFlag)
    {
        Definition = definition;
        IsPreview = preview;
        _setFlag = preview ? null : setFlag;
        _lines = definition.Lines;
    }

    /// <summary>Recalculado a cada leitura (acompanha a troca de idioma).</summary>
    public DialogueFrame Frame
    {
        get
        {
            if (IsFinished) return new DialogueFrame(false, "", "", "", "", "", Array.Empty<string>(), 0, 0);
            if (_inChoice)
            {
                var texts = new List<string>();
                foreach (var choice in Definition.Choices) texts.Add(DialogueManager.FormatText(choice.TextKey));
                return new DialogueFrame(true, Definition.SpeakerId, SpeakerName(Definition.SpeakerId), "", "", "default", texts, _lines.Length, _lines.Length);
            }
            var line = _lines[_index];
            var speakerId = string.IsNullOrEmpty(line.SpeakerId) ? Definition.SpeakerId : line.SpeakerId;
            var variant = !string.IsNullOrEmpty(line.Portrait) ? line.Portrait : (!string.IsNullOrEmpty(line.Emotion) ? line.Emotion : "default");
            if (variant == "neutral") variant = "default";
            return new DialogueFrame(false, speakerId, SpeakerName(speakerId), DialogueManager.FormatText(line.TextKey),
                line.Emotion ?? "", variant, Array.Empty<string>(), _index + 1, _lines.Length);
        }
    }

    public void Advance()
    {
        if (IsFinished || _inChoice) return;
        _index++;
        if (_index < _lines.Length) return;
        if (!_inBranch && Definition.Choices.Length > 0)
        {
            _inChoice = true;
            _index = _lines.Length - 1;
            return;
        }
        IsFinished = true;
    }

    public void Choose(int index)
    {
        if (!_inChoice || index < 0 || index >= Definition.Choices.Length) return;
        var choice = Definition.Choices[index];
        ChosenChoiceId = choice.Id;
        foreach (var flag in choice.SetsFlags) _setFlag?.Invoke(flag);
        _inChoice = false;
        if (!string.IsNullOrEmpty(choice.NextBranchId) && Definition.Branches.TryGetValue(choice.NextBranchId, out var branch))
        {
            foreach (var flag in branch.SetsFlags) _setFlag?.Invoke(flag);
            _lines = branch.Lines;
            _index = 0;
            _inBranch = true;
            return;
        }
        IsFinished = true;
    }

    /// <summary>Avanca ate o proximo quadro de escolha ou o fim.</summary>
    public void Skip()
    {
        while (!IsFinished && !_inChoice) Advance();
    }

    private static string SpeakerName(string speakerId)
        => DialogueCatalog.TryGetSpeaker(speakerId, out var speaker) ? speaker.DisplayName : speakerId;
}
