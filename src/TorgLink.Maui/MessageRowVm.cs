using ShortP2P.Client.Data;

namespace TorgLink.Maui;

/// <summary>Строка списка сообщений в чате (текст, изображение или файл).</summary>
public sealed class MessageRowVm
{
    public required string CaptionLine { get; init; }
    public required string TextBody { get; init; }

    /// <summary>MauiImage resource name for attachment icon (e.g. attach_video.png).</summary>
    public string AttachmentIconSource { get; init; } = "";

    /// <summary>First line of attachment text: file name and optional duration.</summary>
    public string FileTitleText { get; init; } = "";

    /// <summary>Second line: size and action hint.</summary>
    public string FileMetaText { get; init; } = "";

    public bool ShowTextBody { get; init; }

    /// <summary>Картинка или видео: в списке не декодируем, открываем только по нажатию «Скачать».</summary>
    public bool IsImage { get; init; }

    /// <summary>Вложение; байты грузятся только по нажатию, см. <see cref="MessageId" />.</summary>
    public bool IsFile { get; init; }

    public bool IsTransferOffer { get; init; }

    /// <summary>Голосовое (.ogg / opus): download or play icon via <see cref="AttachmentIconSource"/>.</summary>
    public bool IsVoice { get; init; }

    /// <summary>True — локально доступно для воспроизведения; false — нужно скачать.</summary>
    public bool VoiceReady { get; init; }

    public int MessageId { get; init; }
    public required Color MessageColor { get; init; }
    public bool ShowDelivery { get; init; }
    public required string DeliveryGlyph { get; init; }
    public required Color DeliveryGlyphColor { get; init; }
    public bool Outgoing { get; init; }
    /// <summary>0 = мои (слева), 2 = ответы пира (справа). Без DataTrigger — на Android он роняет CollectionView.</summary>
    public int BubbleColumn { get; init; }
    public MessageDeliveryStatus DeliveryStatus { get; init; }
    public bool IsRetryable => Outgoing && DeliveryStatus == MessageDeliveryStatus.Failed && MessageId > 0;
    public Color BubbleColor { get; init; } = Colors.White;
    public string TimeLabel { get; init; } = "";
}
