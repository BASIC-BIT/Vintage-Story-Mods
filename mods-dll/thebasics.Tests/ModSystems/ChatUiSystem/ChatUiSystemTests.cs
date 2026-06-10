using System.Reflection;
using FluentAssertions;
using thebasics.Configs;
using ChatUiModSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ChatUiSystemTests
{
    [Theory]
    [InlineData(50, 30, 50)]
    [InlineData(20, 30, 20)]
    [InlineData(30, 0, 30)]
    [InlineData(-1, 30, 0)]
    public void GetEffectiveTypingIndicatorRange_UsesConfiguredTypingRange(
        int typingRange,
        int nametagRange,
        int expectedRange)
    {
        var config = new ModConfig
        {
            TypingIndicatorMaxRange = typingRange,
            NametagRenderRange = nametagRange,
        };

        var range = ChatUiModSystem.GetEffectiveTypingIndicatorRange(config);

        range.Should().Be(expectedRange);
    }

    [Fact]
    public void CharacterSheetDialogClosed_ClearsCharacterDialogAutoOpenState()
    {
        SetStaticField("_pendingCharacterSheetOpenFromCharacterDialog", true);
        SetStaticField("_characterSheetOpenedFromCharacterDialog", true);

        InvokeStaticMethod("OnCharacterSheetDialogClosed");

        GetStaticField<bool>("_pendingCharacterSheetOpenFromCharacterDialog").Should().BeFalse();
        GetStaticField<bool>("_characterSheetOpenedFromCharacterDialog").Should().BeFalse();
    }

    private static void SetStaticField<T>(string name, T value)
    {
        var field = GetStaticFieldInfo(name);
        field.SetValue(null, value);
    }

    private static T GetStaticField<T>(string name)
    {
        var field = GetStaticFieldInfo(name);
        return (T)field.GetValue(null)!;
    }

    private static FieldInfo GetStaticFieldInfo(string name)
    {
        return typeof(ChatUiModSystem).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ChatUiModSystem).FullName, name);
    }

    private static void InvokeStaticMethod(string name)
    {
        var method = typeof(ChatUiModSystem).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(ChatUiModSystem).FullName, name);
        method.Invoke(null, null);
    }
}
