using ChatRoom.API.Controllers.Message;

namespace ChatRoom.API.ReusableClass
{
    public class Message
    {
        public Message()
        {
        }

        public Message(string messageId, Sender sender, DateTime createdTime, bool isCustom, bool isRecalled, bool isReply, bool isMediaMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, Message? messageReplied, string? messageText, List<MediaMetadata>? messageMedias, string? messageVoice)
        {
            MessageId = messageId;
            Sender = sender;
            CreatedTime = createdTime;
            IsCustom = isCustom;
            IsRecalled = isRecalled;
            IsReply = isReply;
            IsMediaMessage = isMediaMessage;
            IsVoiceMessage = isVoiceMessage;
            CustomType = customType;
            MinimumSupportVersion = minimumSupportVersion;
            TextOnError = textOnError;
            CustomMessageContent = customMessageContent;
            MessageReplied = messageReplied;
            MessageText = messageText;
            MessageMedias = messageMedias;
            MessageVoice = messageVoice;
        }

        public string MessageId { get; set; }
        public Sender Sender { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsCustom { get; set; }
        public bool IsRecalled { get; set; }
        public bool IsReply { get; set; }
        public bool IsMediaMessage { get; set; }
        public bool IsVoiceMessage { get; set; }
        public string? CustomType { get; set; }
        public string? MinimumSupportVersion { get; set; }
        public string? TextOnError { get; set; }
        public string? CustomMessageContent { get; set; }
        public Message? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public List<MediaMetadata>? MessageMedias { get; set; }
        public string? MessageVoice { get; set; }
    }
}
