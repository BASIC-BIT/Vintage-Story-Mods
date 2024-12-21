// using Vintagestory.API.Client;
// using Vintagestory.API.Common;
// using Vintagestory.API.Common.Entities;
// using Vintagestory.API.MathTools;
// using Vintagestory.GameContent;
//
// namespace thebasics.ModSystems.ChatUiSystem;
//
// public class RpTextEntityPlayerShapeRenderer : EntityPlayerShapeRenderer
// {
//     public RpTextEntityPlayerShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
//     {
//         capi.Logger.Debug($"THEBASICS - Player Renderer constructed");
//         
//         this.capi.Event.ChatMessage -= new ChatLineDelegate(this.OnChatMessage);
//         this.capi.Event.ChatMessage += new ChatLineDelegate(this.NewOnChatMessage);
//     }
//     
//     protected void NewOnChatMessage(int groupId, string message, EnumChatType chattype, string data)
//     {
//         capi.Logger.Debug($"THEBASICS - Handling Chat");
//         if (data == null || !data.Contains("from:") || this.entity.Pos.SquareDistanceTo(this.capi.World.Player.Entity.Pos.XYZ) >= 400.0 || message.Length <= 0)
//             return;
//         string[] strArray1 = data.Split(new char[1]{ ',' }, 2);
//         if (strArray1.Length < 2)
//             return;
//         string[] strArray2 = strArray1[0].Split(new char[1]
//         {
//             ':'
//         }, 2);
//         string[] strArray3 = strArray1[1].Split(new char[1]
//         {
//             ':'
//         }, 2);
//         if (strArray2[0] != "from")
//             return;
//         int result;
//         int.TryParse(strArray2[1], out result);
//         if (this.entity.EntityId != (long) result)
//             return;
//         message = strArray3[1];
//         message = message.Replace("&lt;", "<").Replace("&gt;", ">");
//         LoadedTexture loadedTexture = this.capi.Gui.TextTexture.GenTextTexture(message, new CairoFont(25.0, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble), 350, new TextBackground()
//         {
//             FillColor = GuiStyle.DialogLightBgColor,
//             Padding = 100,
//             Radius = GuiStyle.ElementBGRadius
//         }, EnumTextOrientation.Center);
//         this.messageTextures.Insert(0, new MessageTexture()
//         {
//             tex = loadedTexture,
//             message = message,
//             receivedTime = this.capi.World.ElapsedMilliseconds
//         });
//     }
//
//
//     public override void Dispose()
//     {
//         if (this.DisplayChatMessages)
//         {
//             this.capi.Event.ChatMessage -= new ChatLineDelegate(this.NewOnChatMessage);
//         }
//     }
// }