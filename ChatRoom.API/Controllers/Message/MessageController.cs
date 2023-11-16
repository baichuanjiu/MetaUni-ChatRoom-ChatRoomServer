using ChatRoom.API.MinIO;
using ChatRoom.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using static ChatRoom.API.Filters.JWTAuthFilter;

namespace ChatRoom.API.Controllers.Message
{
    public class SendMessageMediaMetadata
    {
        public SendMessageMediaMetadata()
        {
        }

        public SendMessageMediaMetadata(IFormFile file, double aspectRatio, IFormFile? previewImage, int? timeTotal)
        {
            File = file;
            AspectRatio = aspectRatio;
            PreviewImage = previewImage;
            TimeTotal = timeTotal;
        }

        public IFormFile File { get; set; }
        public double AspectRatio { get; set; }
        public IFormFile? PreviewImage { get; set; }
        public int? TimeTotal { get; set; }
    }

    // 适用于文字消息、带有图片或视频的消息、回复某条消息
    // 特殊消息与语音消息走其它接口
    public class SendMessageRequestData
    {
        public SendMessageRequestData()
        {
        }


        public string ChatRoom { get; set; }
        public Sender Sender { get; set; }
        public ReusableClass.Message? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public List<SendMessageMediaMetadata>? MessageMedias { get; set; }
    }

    [ApiController]
    [Route("/message")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class MessageController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly WebSocketsManager _webSocketsManager;
        private readonly MessageMediasMinIOService _messageMediasMinIOService;
        private readonly ILogger<MessageController> _logger;

        public MessageController(IConfiguration configuration, WebSocketsManager webSocketsManager, MessageMediasMinIOService messageMediasMinIOService, ILogger<MessageController> logger)
        {
            _configuration = configuration;
            _webSocketsManager = webSocketsManager;
            _messageMediasMinIOService = messageMediasMinIOService;
            _logger = logger;
        }

        // 适用于文字消息、带有图片或视频的消息、回复某条消息
        // 特殊消息与语音消息走其它接口
        [HttpPost]
        public IActionResult SendMessage([FromForm] SendMessageRequestData formData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            if (formData.MessageText != null && formData.MessageText.Length == 0)
            {
                formData.MessageText = null;
            }
            formData.MessageMedias ??= new();

            if (!_webSocketsManager.webSockets.ContainsKey(formData.ChatRoom)) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为尝试向不存在的聊天室[ {chatRoom} ]发送消息", UUID,formData.ChatRoom);
                ResponseT<string> sendMessageFailed = new(2, "发送消息失败，该聊天室不存在");
                return Ok(sendMessageFailed);
            }

            if (formData.MessageText == null && formData.MessageMedias.Count == 0)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为消息文字内容与媒体文件内容同时为空", UUID);
                ResponseT<string> sendMessageFailed = new(3, "发送消息失败，文字内容与媒体文件内容不能同时为空");
                return Ok(sendMessageFailed);
            }

            if (formData.MessageMedias.Count > 9)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为用户上传了超过限制数量的媒体文件", UUID);
                ResponseT<string> sendMessageFailed = new(4, "发送消息失败，上传媒体文件数超过限制");
                return Ok(sendMessageFailed);
            }

            for (int i = 0; i < formData.MessageMedias.Count; i++)
            {
                if ((!formData.MessageMedias[i].File.ContentType.Contains("image") && !formData.MessageMedias[i].File.ContentType.Contains("video")) || (formData.MessageMedias[i].File.ContentType.Contains("video") && (formData.MessageMedias[i].PreviewImage == null || (formData.MessageMedias[i].PreviewImage != null && !formData.MessageMedias[i].PreviewImage!.ContentType.Contains("image")))))
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，原因为用户上传了图片或视频以外的媒体文件", UUID);
                    ResponseT<string> sendMessageFailed = new(5, "发送消息失败，禁止上传规定格式以外的文件");
                    return Ok(sendMessageFailed);
                }
            }

            List<Task<bool>> tasks = new();
            List<MediaMetadata> medias = new();
            List<string> paths = new();
            for (int i = 0; i < formData.MessageMedias.Count; i++)
            {
                if (formData.MessageMedias[i].File.ContentType.Contains("image"))
                {
                    IFormFile file = formData.MessageMedias[i].File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    DateTime now = DateTime.Now;

                    string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = formData.ChatRoom + "/" + now.ToString("yyyy-MM-dd") + "/" + UUID.ToString() + "_" + timestamp + extension;

                    paths.Add(fileName);

                    string url = _configuration["MinIO:MessageMediasURLPrefix"]! + fileName;

                    tasks.Add(_messageMediasMinIOService.UploadImageAsync(fileName, stream));

                    medias.Add(new MediaMetadata("image", url, formData.MessageMedias[i].AspectRatio, null, null));
                }
                else if (formData.MessageMedias[i].File.ContentType.Contains("video"))
                {
                    IFormFile file = formData.MessageMedias[i].File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    DateTime now = DateTime.Now;

                    string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = formData.ChatRoom + "/" + now.ToString("yyyy-MM-dd") + "/" + UUID.ToString() + "_" + timestamp + extension;

                    paths.Add(fileName);

                    string url = _configuration["MinIO:MessageMediasURLPrefix"]! + fileName;

                    tasks.Add(_messageMediasMinIOService.UploadVideoAsync(fileName, stream));

                    IFormFile preview = formData.MessageMedias[i].PreviewImage!;

                    string previewExtension = Path.GetExtension(preview.FileName);

                    Stream previewStream = preview.OpenReadStream();

                    string previewTimestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string previewFileName = formData.ChatRoom + "/" + now.ToString("yyyy-MM-dd") + "/" + previewTimestamp + previewExtension;

                    paths.Add(previewFileName);

                    string previewURL = _configuration["MinIO:MessageMediasURLPrefix"]! + previewFileName;

                    tasks.Add(_messageMediasMinIOService.UploadImageAsync(previewFileName, previewStream));

                    medias.Add(new MediaMetadata("video", url, formData.MessageMedias[i].AspectRatio, previewURL, formData.MessageMedias[i].TimeTotal));
                }
            }

            Task.WaitAll(tasks.ToArray());
            bool isStoreMediasSucceed = true;
            foreach (var task in tasks)
            {
                if (!task.Result)
                {
                    isStoreMediasSucceed = false;
                    break;
                }
            }
            if (!isStoreMediasSucceed)
            {
                _ = _messageMediasMinIOService.DeleteFilesAsync(paths);
                _logger.LogWarning("Warning：用户[ {UUID} ]发送消息时失败，MinIO存储媒体文件时发生错误。", UUID);
                ResponseT<string> sendMessageFailed = new(6, "发生错误，消息发送失败");
                return Ok(sendMessageFailed);
            }

            DateTime nowForMessage = DateTime.Now;

            string timestampForMessage = (nowForMessage - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            ReusableClass.Message message = new(timestampForMessage,formData.Sender,nowForMessage,false,false,formData.MessageReplied != null,medias.Count != 0,false,null,null,null,null,formData.MessageReplied,formData.MessageText,medias.Count==0?null:medias,null);

            //向聊天室的所有人发送消息
            var sendDataJson = JsonSerializer.Serialize(new { type = "NewMessage", data = message }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
            var sendData = new ArraySegment<byte>(sendDataBytes);
            foreach (var keyValuePair in _webSocketsManager.webSockets[formData.ChatRoom]) 
            {
                if (keyValuePair.Key != UUID) 
                {
                    _ = keyValuePair.Value.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            ResponseT<ReusableClass.Message> sendMessageSucceed = new(0, "消息发送成功",message);
            return Ok(sendMessageSucceed);
        }

        [HttpPut("recall/{chatRoomName}/{messageId}")]
        public IActionResult RecallMessage([FromRoute] string chatRoomName, [FromRoute] string messageId, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            if (!_webSocketsManager.webSockets.ContainsKey(chatRoomName))
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]撤回消息时失败，原因为尝试在不存在的聊天室[ {chatRoomName} ]撤回消息", UUID,chatRoomName);
                ResponseT<string> recallMessageFailed = new(2, "撤回消息失败，该聊天室不存在");
                return Ok(recallMessageFailed);
            }

            //向聊天室的所有人发送消息
            var sendDataJson = JsonSerializer.Serialize(new { type = "MessageBeRecalled", data = messageId }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
            var sendData = new ArraySegment<byte>(sendDataBytes);
            foreach (var keyValuePair in _webSocketsManager.webSockets[chatRoomName])
            {
                if (keyValuePair.Key != UUID) 
                {
                    _ = keyValuePair.Value.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            ResponseT<string> recallMessageSucceed = new(0, "消息撤回成功");
            return Ok(recallMessageSucceed);
        }
    }
}
