using System;

namespace thebasics.Configs;

public static class ProximityChatPresentationModes
{
    public const string StandardRoleplay = "StandardRoleplay";
    public const string SimpleSpeech = "SimpleSpeech";
    public const string PlainProximity = "PlainProximity";
    public const string Prose = "Prose";

    public static string Normalize(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return StandardRoleplay;
        }

        return mode.Trim() switch
        {
            var value when value.Equals(StandardRoleplay, StringComparison.OrdinalIgnoreCase) => StandardRoleplay,
            var value when value.Equals(SimpleSpeech, StringComparison.OrdinalIgnoreCase) => SimpleSpeech,
            var value when value.Equals(PlainProximity, StringComparison.OrdinalIgnoreCase) => PlainProximity,
            var value when value.Equals(Prose, StringComparison.OrdinalIgnoreCase) => Prose,
            _ => StandardRoleplay
        };
    }

    public static bool UsesSpeechQuotes(string mode)
    {
        var normalized = Normalize(mode);
        return normalized is StandardRoleplay or SimpleSpeech;
    }
}
