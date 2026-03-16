using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Runs in the sender phase for placed environmental messages (!! prefix / /envhere).
/// Performs a server-side raycast from the player's look direction to find a surface.
/// On hit: stores the position in metadata for use by RecipientDeterminationTransformer and dispatch.
/// On miss: clears the placed flag so the message falls back to a standard environmental message.
/// </summary>
public class PlacedEnvironmentTransformer : MessageTransformerBase
{
    public PlacedEnvironmentTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var maxDistance = _config.MaxEnvironmentPlacementDistance;
        var hitPos = RaycastUtils.RaycastFromPlayerLook(context.SendingPlayer, maxDistance);

        if (hitPos != null)
        {
            context.SetMetadata(MessageContext.PLACED_POSITION, hitPos);

            // Clear any inherited clientData so the vanilla EntityShapeRenderer.OnChatMessage
            // does not render an additional above-head bubble. This matters for the !! prefix
            // path where Event_PlayerChat populates clientData from the vanilla payload.
            // The /envhere command path creates a fresh context without clientData, so this
            // is a no-op there.
            context.SetMetadata("clientData", (string)null);
        }
        else
        {
            // No surface hit — fall back to standard above-head environmental message.
            context.SetFlag(MessageContext.IS_PLACED_ENVIRONMENTAL, false);

            // Notify the sender about the fallback.
            context.SendingPlayer?.SendMessage(
                _chatSystem.ProximityChatId,
                Vintagestory.API.Config.Lang.Get("thebasics:chat-env-placement-fallback"),
                Vintagestory.API.Common.EnumChatType.Notification
            );
        }

        return context;
    }
}
