using System;
using System.Collections.Generic;
using Moq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using Tests.TestDoubles;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Xunit;

namespace Tests;

public class TransformersTests
{
    [Fact]
    public void LanguageTransformer_Should_ProcessLanguage()
    {
        // Arrange
        var languageSystemMock = new Mock<LanguageSystem>(null, null, null);
        var mockLang = new Language("test", "Test Language", "test", Array.Empty<string>(), "#FF0000", false, false, false, 10, false);
        
        languageSystemMock.Setup(x => x.GetSpeakingLanguage(
                It.IsAny<IServerPlayer>(), 
                It.IsAny<int>(), 
                ref It.Ref<string>.IsAny))
            .Returns(mockLang);
        
        var transformer = new LanguageTransformer(languageSystemMock.Object);
        var context = new MessageContext 
        { 
            Message = "Hello",
            SendingPlayer = Mock.Of<IServerPlayer>(),
            ReceivingPlayer = Mock.Of<IServerPlayer>(),
            GroupId = 1
        };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.Equal(mockLang, result.Metadata["language"]);
    }
    
    [Fact]
    public void ObfuscationTransformer_Should_ObfuscateMessage()
    {
        // Arrange
        var obfuscationSystemMock = new Mock<DistanceObfuscationSystem>(null, null, null);
        var transformer = new ObfuscationTransformer(obfuscationSystemMock.Object);
        var context = new MessageContext 
        { 
            Message = "Hello",
            SendingPlayer = Mock.Of<IServerPlayer>(),
            ReceivingPlayer = Mock.Of<IServerPlayer>(),
            Metadata = { ["isEmote"] = true }
        };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Message);
    }
    
    [Fact]
    public void FormatTransformer_Should_FormatMessage()
    {
        // Arrange
        var chatSystemMock = new Mock<RPProximityChatSystem>();
        var transformer = new FormatTransformer(chatSystemMock.Object);
        var context = new MessageContext { Message = "hello world" };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.StartsWith("Hello", result.Message);
        Assert.EndsWith("world.", result.Message);
    }
    
    [Fact]
    public void EmoteTransformer_Should_FormatEmote()
    {
        // Arrange
        var chatSystemMock = new Mock<RPProximityChatSystem>();
        chatSystemMock.Setup(x => x.GetFormattedNickname(It.IsAny<IServerPlayer>()))
            .Returns("TestPlayer");
            
        var transformer = new EmoteTransformer(chatSystemMock.Object);
        var context = new MessageContext 
        { 
            Message = "waves hello",
            SendingPlayer = Mock.Of<IServerPlayer>(),
            Metadata = { ["isEmote"] = true }
        };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.StartsWith("TestPlayer waves hello", result.Message);
    }
    
    [Theory]
    [InlineData(ProximityChatMode.Normal, "says", ".")]
    [InlineData(ProximityChatMode.Whisper, "whispers", "...")]
    [InlineData(ProximityChatMode.Yell, "yells", "!")]
    public void ChatModeTransformer_Should_UseCorrectVerbAndPunctuation(ProximityChatMode mode, string expectedVerb, string expectedPunctuation)
    {
        // Arrange
        var chatSystemMock = new Mock<RPProximityChatSystem>();
        chatSystemMock.Setup(x => x.GetFormattedNickname(It.IsAny<IServerPlayer>()))
            .Returns("TestPlayer");
            
        var playerMock = new Mock<IServerPlayer>();
        var transformer = new ChatModeTransformer(chatSystemMock.Object);
        var context = new MessageContext 
        { 
            Message = "Hello",
            SendingPlayer = playerMock.Object,
            Metadata = { ["chatMode"] = mode }
        };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.Contains(expectedVerb, result.Message);
        Assert.EndsWith(expectedPunctuation, result.Message);
    }

    [Fact]
    public void EmoteTransformer_Should_HandleQuotedText()
    {
        // Arrange
        var chatSystemMock = new Mock<RPProximityChatSystem>();
        chatSystemMock.Setup(x => x.GetFormattedNickname(It.IsAny<IServerPlayer>()))
            .Returns("TestPlayer");
            
        var transformer = new EmoteTransformer(chatSystemMock.Object);
        var context = new MessageContext 
        { 
            Message = "waves and says \"hello there\"",
            SendingPlayer = Mock.Of<IServerPlayer>(),
            Metadata = { 
                ["isEmote"] = true,
                ["language"] = new Language("test", "Test Language", "test", Array.Empty<string>(), "#FF0000", false, false, false, 10, false)
            }
        };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.StartsWith("TestPlayer waves and says", result.Message);
        Assert.Contains("\"hello there\"", result.Message);
    }

    [Fact]
    public void FormatTransformer_Should_HandleAccents()
    {
        // Arrange
        var chatSystemMock = new Mock<RPProximityChatSystem>();
        var transformer = new FormatTransformer(chatSystemMock.Object);
        var context = new MessageContext { Message = "hello |world| and +strong+" };
        
        // Act
        var result = transformer.Transform(context);
        
        // Assert
        Assert.Contains("<i>world</i>", result.Message);
        Assert.Contains("<strong>strong</strong>", result.Message);
    }
}
