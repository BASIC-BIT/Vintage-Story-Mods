using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Models;

public record Language(
    string Name,
    string Description,
    string Prefix,
    string[] Syllables,
    string Color,
    bool Default,
    bool Hidden,
    bool IsSignLanguage = false,
    int SignLanguageRange = 60,
    bool UseItalics = false
);