using System;
using thebasics.Models;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Bridge between CharacterSheetDialog and the ChatUiSystem-owned upload/fetch pipeline.
/// The host system populates these delegates; the dialog invokes them on user actions.
/// </summary>
public sealed class HeadshotDialogCallbacks
{
    public bool UrlUploadAllowed { get; init; }

    // Receives the dialog instance directly because the owning system's static dialog field
    // isn't yet assigned during the very first constructor call.
    public Action<CharacterSheetDialog, CharacterSheetViewMessage> RequestHeadshotForView { get; init; }

    public Action<string, string> UploadFromUrl { get; init; }

    public Action<string> ClearHeadshot { get; init; }
}
