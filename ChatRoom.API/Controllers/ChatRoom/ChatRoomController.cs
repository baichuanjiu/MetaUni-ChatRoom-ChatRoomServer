using ChatRoom.API.MinIO;
using ChatRoom.API.Protos.Authentication;
using ChatRoom.API.Protos.ChatRequest;
using ChatRoom.API.Redis;
using ChatRoom.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using static ChatRoom.API.Filters.JWTAuthFilter;

namespace ChatRoom.API.Controllers.ChatRoom
{
    public class ChatRoomInfo
    {
        public ChatRoomInfo(string avatar, string name, string displayName, int onlineNumber)
        {
            Avatar = avatar;
            Name = name;
            DisplayName = displayName;
            OnlineNumber = onlineNumber;
        }

        public string Avatar { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public int OnlineNumber { get; set; }
    }
    public class GetChatRoomListResponseData
    {
        public GetChatRoomListResponseData(List<ChatRoomInfo> dataList)
        {
            DataList = dataList;
        }

        public List<ChatRoomInfo> DataList { get; set; }
    }
    public class GetChatRoomRulesResponseData
    {
        public GetChatRoomRulesResponseData(List<string> dataList)
        {
            DataList = dataList;
        }

        public List<string> DataList { get; set; }
    }
    public class GetNicknameGroups
    {
        public GetNicknameGroups(List<string> firstOfNickname, List<string> middleOfNickname, List<string> lastOfNickname)
        {
            FirstOfNickname = firstOfNickname;
            MiddleOfNickname = middleOfNickname;
            LastOfNickname = lastOfNickname;
        }

        public List<string> FirstOfNickname { get; set; }
        public List<string> MiddleOfNickname { get; set; }
        public List<string> LastOfNickname { get; set; }
    }
    public class TargetUser 
    {
        public TargetUser(int UUID, string avatar, string nickname)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
    }

    public class HoldReferendumRequestData 
    {
        public HoldReferendumRequestData(TargetUser targetUser, string reason)
        {
            TargetUser = targetUser;
            Reason = reason;
        }

        public TargetUser TargetUser { get; set; }
        public string Reason { get; set; }
    }

    public class SendChatRequestRequestData 
    {
        public SendChatRequestRequestData(int targetUser, string greetText)
        {
            TargetUser = targetUser;
            GreetText = greetText;
        }

        public int TargetUser { get; set; }
        public string GreetText { get; set; }
    }

    [ApiController]
    [Route("/chatRoom")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class ChatRoomController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly RedisConnection _redisConnection;
        private readonly WebSocketsManager _webSocketsManager;
        private readonly UserAvatarMinIOService _userAvatarMinIOService;
        private readonly SendChatRequest.SendChatRequestClient _rpcChatRequestClient;
        private readonly ILogger<ChatRoomController> _logger;

        /*
         * 获取列表（8个聊天室，信息包括：聊天室头像、聊天室名、当前在线人数）
         * （聊天室头像与聊天室名固定）
         * （聊天室头像采用相对路径，前缀从config中读取）
         * 获取聊天室规定（8个聊天室，规定存储在Redis中）
         * 获取供用户选择的Nickname组合（存储在Redis中）
         * 连接聊天室WebSocket
         */
        private readonly static List<string> chatRoomNameList = new()
        {
            "Family",
            "Pantry",
            "Treehole",
            "Nijigen",
            "PartnerCorner",
            "BoxingGym",
            "Circus",
            "Confessional"
        };

        private readonly static Dictionary<string, string> chatRoomDisplayNameMap = new()
        {
            {chatRoomNameList[0],"相亲相爱一家人"},
            {chatRoomNameList[1],"办公休息茶水间"},
            {chatRoomNameList[2],"纯洁友善好树洞"},
            {chatRoomNameList[3],"泛二次元交际圈"},
            {chatRoomNameList[4],"结伴搭搭搭子角"},
            {chatRoomNameList[5],"社会热点拳击馆"},
            {chatRoomNameList[6],"舔狗乌龟马戏团"},
            {chatRoomNameList[7],"神父修女忏悔室"},
        };

        private readonly static Dictionary<string, string> chatRoomAvatarMap = new()
        {
            {chatRoomNameList[0],"/family.jpg"},
            {chatRoomNameList[1],"/pantry.png"},
            {chatRoomNameList[2],"/treehole.png"},
            {chatRoomNameList[3],"/nijigen.jpg"},
            {chatRoomNameList[4],"/parterCorner.png"},
            {chatRoomNameList[5],"/boxingGym.png"},
            {chatRoomNameList[6],"/circus.png"},
            {chatRoomNameList[7],"/confessional.png"},
        };

        public ChatRoomController(IConfiguration configuration, RedisConnection redisConnection, WebSocketsManager webSocketsManager, UserAvatarMinIOService userAvatarMinIOService, SendChatRequest.SendChatRequestClient rpcChatRequestClient, ILogger<ChatRoomController> logger)
        {
            _configuration = configuration;
            _redisConnection = redisConnection;
            _webSocketsManager = webSocketsManager;
            _userAvatarMinIOService = userAvatarMinIOService;
            _rpcChatRequestClient = rpcChatRequestClient;
            _logger = logger;
        }

        //[HttpGet("initData")]
        //public IActionResult InitData([FromHeader] string JWT, [FromHeader] int UUID)
        //{
        //    var chatRoomDatabase = _redisConnection.GetChatRoomDatabase();
        //    {
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "开朗");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "阴沉");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "活泼");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "可爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "天真");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "浪漫");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "无邪");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "纯真");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "善良");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "愚蠢");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "呆滞");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "可怜");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "快乐");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "失望");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "卑鄙");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "下作");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "低劣");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "痴情");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "沉默");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "伤心");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "难过");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "悲痛欲绝");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "苦痛");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "残忍");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "坚持不懈");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "屑");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "不屑");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "开放");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "内敛");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "美丽");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "动人");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "帅气");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "丑陋");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "高大");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "矮小");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "痛恨");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "嫉恶");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "嫉妒");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "心胸狭隘");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "憎");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恨");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "可恨");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "赞美");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "诋毁");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "下水道");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "老鼠");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "鼠鼠");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "国色天香");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "鸭鸭");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "龙");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "虎");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "偶像");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "御宅");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "肥胖");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "花心");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "富有");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "贫穷");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "贫贱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "奇");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "奇怪");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "奇妙");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "奇异");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "游戏");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "O");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "漫画");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "乙女");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "忍者");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "梦");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "萌");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恶心");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "任性");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "柔韧");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "坚强");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "健全");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "成年");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "未成年");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "中二");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大一");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大二");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大三");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大四");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "学长");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "学姐");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "学弟");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "学妹");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "放浪");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "痴心绝对");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "眉清目秀");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "技惊四座");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "悲伤");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "痛苦");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "机械");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "木讷");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "高达");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "木头");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "铁血");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "水星");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "火星");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "清秀");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "美少女");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "美少年");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "叔叔");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "阿姨");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "狗");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "猫");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "小狗");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "小猫");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "喜爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "玩");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "自宅");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "宅家");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "闲人");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "失意");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "诗意");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "失忆");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "释怀");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "沉重");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "启动");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "义眼");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "孤高");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "祖安");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大陆");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "小岛");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "独");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "中立");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "重力");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "愉快");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "愉悦");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "低气压");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "KY");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "一眼");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "学习");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "保研");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "考研");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "自闭");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "自卑");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "大胆");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "真实");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "天然");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "孤独");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "清高");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "有");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "没有");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "优雅");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "阴森");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "隔壁");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "武装");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "理智");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "客观");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "千金");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "富豪");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "富家");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恶意");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恶役");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恶女");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "靠谱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "不靠谱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "希望");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "草");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "太阳");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "真");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "假");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "一瞬间");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "心软");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "打工");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "绿色");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "忧郁");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "犹豫");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "可悲");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "黄金");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "霸总");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "霸道");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "电子");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "传统");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "苍天");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "传说");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "舔");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "甜");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "喜欢");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "领先");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "遥遥领先");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "非凡");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "醉");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "醉酒");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "尖叫");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "美妙");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "超级");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "抱歉");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "虚无");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "飘渺");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "飘摇");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "离异");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "中年");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "中年离异");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "怀念");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "失恋");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "热恋");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "恋爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "网吧");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "容易受伤");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "易碎");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "南方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "北方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "东方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "西方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "强大");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "智慧");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "正义");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "狂热");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "热情");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "冰冷");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "人偶");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "土");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "土气");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "土味");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "社会");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "深渊");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "宿舍");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "哭泣");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "社恐");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "精致");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "帝国");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "小镇");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "摸鱼");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "有名");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "年少有为");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "欢呼");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "江南");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "一般路过");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "兴趣使然");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "人");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "浪费时间");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "闪耀");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "舞台");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "摘星");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "科技");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "魔力");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "亲爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "相亲相爱");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "可恶");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "雨");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "雨中");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "麻烦");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "你");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "我");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "地方");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "高贵");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "空");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "月球");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "逆天");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "知心");
        //        chatRoomDatabase.SetAdd("FirstOfNickname", "无敌");
        //    }

        //    {
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "的");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "之");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "の");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "大");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "小");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "爱");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "恨");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "不");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "有");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "没有");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "无");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "梦");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "与");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "和");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "上");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "上的");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "中");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "中的");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "下");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "下的");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "被");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "把");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "老");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "想");
        //        chatRoomDatabase.SetAdd("MiddleOfNickname", "若");
        //    }

        //    {
        //        chatRoomDatabase.SetAdd("LastOfNickname", "P");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "OP");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "天才");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "新手");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "高手");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "高人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "学长");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "学姐");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "学弟");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "学妹");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "叔叔");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "少女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "阿姨");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "传奇");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "公子");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "同学");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "月兔");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "打工人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "做题家");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "工具人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "宝贝");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "狼人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "狠人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "狼");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "浪人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "忍者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "侠客");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "舔狗");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "乌龟");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "小猫");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "小狗");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "猫");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "狗");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "传人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "二次元");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "死宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "御宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "警备员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "仙人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "忍者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "忍");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "龙");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大米");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "小丑");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "美少女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "美少年");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "雪豹");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "动物朋友");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "钢琴家");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "画师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "写手");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "乐队");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "乐团");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "演员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "心动");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "丞相");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "故事");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "父亲");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "母亲");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "玩家");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "舞者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "武者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "直升机");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "千金");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大小姐");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "恶役");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "搞笑役");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "吐槽役");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "外卖员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "女仆");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "老师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "男");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "成年人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "未成年人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "花");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "草");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "树");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "木头");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "糖");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "青春");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "神");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鬼怪");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鬼");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "幽灵");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "战士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "老人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "霸总");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "总裁");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "力量");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大哥");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "小弟");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "姐姐");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "哥哥");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "弟弟");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "妹妹");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "烟");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "律师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "法师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "射手");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "龙骑士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "龙骑");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "暴龙战士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "SSR");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "妹");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "蛋糕");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "摄影");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "服务员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "学生");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "网红");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "摇子");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "医生");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "厨师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "土拨鼠");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "旋律");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "音律");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "黑客");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "反派角色");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "反派");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "爱意");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "男人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "女人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "国王");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "王妃");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "垃圾桶");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "驴");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "牛");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "马");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "牛马");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "伙伴");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "敌人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "粉丝");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "心");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "心心");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鹿");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "人偶");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "玩具");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "红");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "兔");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "哥");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "姐");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大王");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "小王");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "双子星");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "四天王");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "观测者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "勇者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "剑客");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "剑士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "牧师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "坦克");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "偶像");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "夏");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "夏天");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "女儿");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "英雄");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "主播");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "忠犬");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鼠鼠");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鸭");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鸭鸭");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鱼");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "秦始皇");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "刺客");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "那位");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "患者");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "情痴");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "朋友");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "组长");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "组员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "如来");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "猴子");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大叔");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "菠萝");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "修女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "神父");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "吸血鬼");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "临时工");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "无关人员");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "才子");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "卧龙");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "凤雏");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "骑士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "假面骑士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "公主");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "王子");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "光头");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "嘉宾");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "薯条");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "海鸥");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "烟花");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "害虫");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "魔法使");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "魔术师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "魔龙");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "宝藏");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "金币");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "爱人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "哥布林");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "鉴赏家");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "裁判");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "高达");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "机器人");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "武士");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "巫师");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "巫女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "魔女");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "艺术家");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "少年");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "长颈鹿");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "肥宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "宅宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "废宅");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "力量");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "米线");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "猪");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "幕后黑手");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "你");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "我");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "汤姆");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "露西");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "大卫");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "保安");
        //        chatRoomDatabase.SetAdd("LastOfNickname", "恋人");
        //    }

        //    {
        //        var familyDatabase = _redisConnection.GetFamilyDatabase();
        //        familyDatabase.SortedSetAdd("Rules", "1、友善是第一要务。",1);
        //        familyDatabase.SortedSetAdd("Rules", "2、禁止攻击性语句。",2);
        //        familyDatabase.SortedSetAdd("Rules", "3、掌握交流分寸感。",3);
        //    }

        //    {
        //        var pantryDatabase = _redisConnection.GetPantryDatabase();
        //        pantryDatabase.SortedSetAdd("Rules", "1、休息摸鱼，我的最爱。", 1);
        //        pantryDatabase.SortedSetAdd("Rules", "2、勿谈工作，勿谈国事。", 2);
        //        pantryDatabase.SortedSetAdd("Rules", "3、闲谈为主，八卦为辅。", 3);
        //        pantryDatabase.SortedSetAdd("Rules", "4、饮料自取，零食管够。", 4);
        //    }

        //    {
        //        var treeholeDatabase = _redisConnection.GetTreeholeDatabase();
        //        treeholeDatabase.SortedSetAdd("Rules", "1、请谨慎评估你要倾诉的内容会给聊天室的各位带来怎样的影响。", 1);
        //        treeholeDatabase.SortedSetAdd("Rules", "2、倾诉内容可以轻松、可以沉重，但过激内容请出门左转忏悔室。", 2);
        //        treeholeDatabase.SortedSetAdd("Rules", "3、作为倾听者，请减少情绪输出，多为当事人考虑。", 3);
        //        treeholeDatabase.SortedSetAdd("Rules", "4、提出解决方案或提供情绪价值比输出自身观点更为有效。", 4);
        //        treeholeDatabase.SortedSetAdd("Rules", "5、“当局者迷，旁观者清”，多一个视点看待问题能得到不同的思路，但请万分注意提出观点时的措辞。", 5);
        //    }

        //    {
        //        var nijigenDatabase = _redisConnection.GetNijigenDatabase();
        //        nijigenDatabase.SortedSetAdd("Rules", "1、KY，来自于日语『空気が読めない』，是“不会阅读气氛”的意思。", 1);
        //        nijigenDatabase.SortedSetAdd("Rules", "2、在讨论相关作品时，请注意避免各种形式的剧透。", 2);
        //        nijigenDatabase.SortedSetAdd("Rules", "3、不同的人看待相同的作品时抱有不同意见是很正常的一件事情。", 3);
        //        nijigenDatabase.SortedSetAdd("Rules", "4、禁止讨论过激内容，『お前のような悪いオタクがいるから、オタクがみんなに誤解されるんだ』。", 4);
        //    }

        //    {
        //        var partnerCornerDatabase = _redisConnection.GetPartnerCornerDatabase();
        //        partnerCornerDatabase.SortedSetAdd("Rules", "1、约饭、约球、约车、约游戏、约图书馆，总之就是搭搭搭搭搭起来。", 1);
        //        partnerCornerDatabase.SortedSetAdd("Rules", "2、害人之心不可有，防人之心不可无。", 2);
        //    }

        //    {
        //        var boxingGymDatabase = _redisConnection.GetBoxingGymDatabase();
        //        boxingGymDatabase.SortedSetAdd("Rules", "1、谨慎键政，禁止随意嘲讽。",1);
        //        boxingGymDatabase.SortedSetAdd("Rules", "2、允许性别议题，但请理性讨论。",2);
        //        boxingGymDatabase.SortedSetAdd("Rules", "3、复读玩梗不构成有效讨论。",3);
        //        boxingGymDatabase.SortedSetAdd("Rules", "4、用一个魔法打败另一个魔法，会招致更强烈的魔法打击。",4);
        //        boxingGymDatabase.SortedSetAdd("Rules", "5、经历不同会导致思想不同。",5);
        //        boxingGymDatabase.SortedSetAdd("Rules", "6、好好想清楚自己到底想表达些什么。",6);
        //        boxingGymDatabase.SortedSetAdd("Rules", "7、可以发表自身观点，但请放弃说服对方。",7);
        //    }

        //    {
        //        var circusDatabase = _redisConnection.GetCircusDatabase();
        //        circusDatabase.SortedSetAdd("Rules", "1、骗哥们可以，别把自己也骗到了就行。",1);
        //        circusDatabase.SortedSetAdd("Rules", "2、遇事信你自己，别信哥们的建议，其实哥们也没谈过恋爱。",2);
        //        circusDatabase.SortedSetAdd("Rules", "3、哥们只是来陪你喝酒的，哥们不知道最近这里有马戏团在搞动物表演。",3);
        //        circusDatabase.SortedSetAdd("Rules", "4、哥们不拦着你继续这段感情，下次还想讲故事就再来找哥们。",4);
        //    }

        //    {
        //        var confessionalDatabase = _redisConnection.GetConfessionalDatabase();
        //        confessionalDatabase.SortedSetAdd("Rules", "1、请谨慎评估你要忏悔的内容会给聊天室的各位带来怎样的影响。",1);
        //        confessionalDatabase.SortedSetAdd("Rules", "2、仅允许适度过激内容，太过炸裂的内容请在忏悔开始前及时通知聊天室的各位进行避难。",2);
        //        confessionalDatabase.SortedSetAdd("Rules", "3、忏悔结束后，聊天室的各位将会判决你的所作所为会使你去往天堂还是地狱。",3);
        //    }

        //    return Ok();
        //}

        [HttpGet("chatRoomList")]
        public IActionResult GetChatRoomList([FromHeader] string JWT, [FromHeader] int UUID)
        {
            List<ChatRoomInfo> dataList = new();
            foreach (string name in chatRoomNameList)
            {
                dataList.Add(new(_configuration["ChatRoomAvatarPrefix"] + chatRoomAvatarMap[name], name, chatRoomDisplayNameMap[name], _webSocketsManager.webSockets[name].Count));
            }
            return Ok(new ResponseT<GetChatRoomListResponseData>(0, "获取成功", new(dataList)));
        }

        [HttpGet("chatRoomRules/{chatRoomName}")]
        public IActionResult GetChatRoomRules([FromRoute] string chatRoomName, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            IDatabase? database = _redisConnection.GetDatabaseByChatRoomName(chatRoomName);
            if (database == null) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在查询不存在的聊天室[ {chatRoomName} ]的规定", UUID, chatRoomName);
                return Ok(new ResponseT<string>(2, "该聊天室不存在"));
            }
            List<string> rules = new();
            var members = database.SortedSetRangeByRank("Rules");
            foreach (var member in members)
            {
                rules.Add(member.ToString());
            }
            return Ok(new ResponseT<GetChatRoomRulesResponseData>(0, "获取成功", new(rules)));
        }

        [HttpGet("nicknameGroups")]
        public IActionResult GetNicknameGroups([FromHeader] string JWT, [FromHeader] int UUID)
        {
            var database = _redisConnection.GetChatRoomDatabase();
            var batch = database.CreateBatch();

            var firstOfNicknameMembers = batch.SetMembersAsync("FirstOfNickname");
            var middleOfNicknameMembers = batch.SetMembersAsync("MiddleOfNickname");
            var lastOfNicknameMembers = batch.SetMembersAsync("LastOfNickname");

            batch.Execute();
            batch.WaitAll(firstOfNicknameMembers, middleOfNicknameMembers, lastOfNicknameMembers);

            Random rng = new Random();

            List<string> firstOfNickname = new();
            foreach (var member in firstOfNicknameMembers.Result)
            {
                firstOfNickname.Add(member.ToString());
            }
            int n = firstOfNickname.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (firstOfNickname[k], firstOfNickname[n]) = (firstOfNickname[n], firstOfNickname[k]);
            }
            firstOfNickname.Insert(0, "");

            List<string> middleOfNickname = new();
            foreach (var member in middleOfNicknameMembers.Result)
            {
                middleOfNickname.Add(member.ToString());
            }
            n = middleOfNickname.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (middleOfNickname[k], middleOfNickname[n]) = (middleOfNickname[n], middleOfNickname[k]);
            }
            middleOfNickname.Insert(0, "");

            List<string> lastOfNickname = new();
            foreach (var member in lastOfNicknameMembers.Result)
            {
                lastOfNickname.Add(member.ToString());
            }
            n = lastOfNickname.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (lastOfNickname[k], lastOfNickname[n]) = (lastOfNickname[n], lastOfNickname[k]);
            }
            lastOfNickname.Insert(0, "");

            return Ok(new ResponseT<GetNicknameGroups>(0, "获取成功", new(firstOfNickname, middleOfNickname, lastOfNickname)));
        }

        [HttpGet("checkExiledStatus/{chatRoomName}")]
        public IActionResult CheckExiledStatus([FromRoute] string chatRoomName, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            IDatabase? database = _redisConnection.GetDatabaseByChatRoomName(chatRoomName);
            if (database == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在检查封禁状态时失败，原因为尝试进入不存在的聊天室[ {chatRoomName} ]", UUID, chatRoomName);
                ResponseT<string> checkExiledStatusFailed = new(2, "该聊天室不存在");
                return Ok(checkExiledStatusFailed);
            }

            ResponseT<bool> checkExiledStatusSucceed = new(0, "检查封禁状态成功",database.KeyExists($"{UUID}BeExiled"));
            return Ok(checkExiledStatusSucceed);
        }

        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            if (!avatar.ContentType.Contains("image"))
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]上传头像失败，原因为用户上传了图片以外的媒体文件，疑似正绕过前端进行操作。", UUID);
                ResponseT<string> uploadAvatarFailed = new(2, "禁止上传规定格式以外的头像文件");
                return Ok(uploadAvatarFailed);
            }

            string extension = Path.GetExtension(avatar.FileName);

            Stream stream = avatar.OpenReadStream();

            DateTime now = DateTime.Now;

            string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            string fileName = now.ToString("yyyy-MM-dd") + "/" + UUID.ToString() + "_" + timestamp + extension;

            if (await _userAvatarMinIOService.UploadImageAsync(fileName, stream))
            {
                ResponseT<string> uploadAvatarSucceed = new(0, "头像上传成功", _configuration["MinIO:UserAvatarURLPrefix"]! + fileName);
                return Ok(uploadAvatarSucceed);
            }
            else
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]上传头像时发生错误，头像上传失败。", UUID);
                ResponseT<string> uploadAvatarFailed = new(3, "发生错误，头像上传失败");
                return Ok(uploadAvatarFailed);
            }
        }

        //连接WebSocket
        [HttpGet("ws/{chatRoomName}/{nickname}")]
        public async Task ConnectWebSocket([FromRoute] string chatRoomName, [FromRoute] string nickname, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                if (_webSocketsManager.webSockets.TryGetValue(chatRoomName,out var webSockets))
                {
                    IDatabase? database = _redisConnection.GetDatabaseByChatRoomName(chatRoomName);
                    if (!database!.KeyExists($"{UUID}BeExiled"))
                    {
                        if (webSockets.TryGetValue(UUID, out WebSocket? oldWebSocket))
                        {
                            _ = oldWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "多次连接", CancellationToken.None);
                            webSockets.Remove(UUID);
                        }

                        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                        webSockets.Add(UUID, webSocket);
                        BroadcastMembersChanged(chatRoomName, nickname, true);

                        await MaintainConnection(chatRoomName, nickname, UUID, webSocket);
                    }
                    else 
                    {
                        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    }
                }
                else
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]尝试进入不存在的聊天室[ {chatRoomName} ]。", UUID, chatRoomName);
                    HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]未使用ws或wss协议，尝试获取WebSocket连接。", UUID);
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        //发动公投
        [HttpPost("referendum/{chatRoomName}")]
        public IActionResult HoldReferendum([FromRoute] string chatRoomName, [FromBody] HoldReferendumRequestData requestData, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            IDatabase? database = _redisConnection.GetDatabaseByChatRoomName(chatRoomName);
            if (database == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]向用户[ {targetUser} ]发动公投时失败，原因为尝试在不存在的聊天室[ {chatRoomName} ]发动公投", UUID, requestData.TargetUser.UUID, chatRoomName);
                ResponseT<string> holdReferendumFailed = new(2, "发动公投失败，该聊天室不存在");
                return Ok(holdReferendumFailed);
            }

            if (!_webSocketsManager.webSockets[chatRoomName].ContainsKey(requestData.TargetUser.UUID)) 
            {
                ResponseT<string> holdReferendumFailed = new(3, "发动公投失败，目标用户已离开该聊天室");
                return Ok(holdReferendumFailed);
            }

            //同一用户在五分钟内无法受到来自同一间聊天室的多次放逐公投
            if (database.KeyExists($"Exile{requestData.TargetUser.UUID}ReferendumCD")) 
            {
                ResponseT<string> holdReferendumFailed = new(4, "同一用户在五分钟内无法受到来自同一间聊天室的多次放逐公投");
                return Ok(holdReferendumFailed);
            }

            var judgeEmptyStringList = Regex.Split(requestData.Reason, " +").ToList();
            judgeEmptyStringList.RemoveAll(key => key == "");

            if (judgeEmptyStringList.Count == 0) 
            {
                requestData.Reason = "未填写";
            }

            //发动放逐公投
            var batch = database.CreateBatch();
            _ = batch.StringSetAsync($"Exile{requestData.TargetUser.UUID}ReferendumCD","",expiry: TimeSpan.FromMinutes(5));
            _ = batch.StringSetAsync($"Exile{requestData.TargetUser.UUID}Referendum", "", expiry: TimeSpan.FromSeconds(65));
            batch.Execute();

            //设置定时器，65s后结算投票结果
            System.Timers.Timer timer = new(65000);
            timer.Elapsed += (Object source, ElapsedEventArgs e) =>{
                SettleReferendumResult(database, chatRoomName, requestData.TargetUser);
                timer.Close();
                timer.Dispose();
            };
            timer.AutoReset = false;
            timer.Enabled = true;

            DateTime now = DateTime.Now;

            string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            JsonSerializerOptions jsonSerializerOptions = new(){ PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var sendDataJson = JsonSerializer.Serialize(new { type = "NewMessage", data = new ReusableClass.Message(timestamp, new(0, "", ""), now, true, false, false, false, false, "Referendum", "1.0.0", "应用版本过低，请升级至V1.0.0版本以上以阅读此消息",
                JsonSerializer.Serialize(new {
                    chatRoomName,
                    uuid = requestData.TargetUser.UUID,
                    avatar = requestData.TargetUser.Avatar,
                    nickname = requestData.TargetUser.Nickname,
                    reason = requestData.Reason,
                    deadline = now.AddSeconds(60),
                }, jsonSerializerOptions)
                , null, null, null, null) }, jsonSerializerOptions);
            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
            var sendData = new ArraySegment<byte>(sendDataBytes);
            var webSockets = _webSocketsManager.webSockets[chatRoomName].Values;
            foreach (var webSocket in webSockets)
            {
                _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            ResponseT<string> holdReferendumSucceed = new(0, "发动公投成功");
            return Ok(holdReferendumSucceed);
        }

        [HttpGet("referendum/vote/{chatRoomName}&{targetUUID}&{action}")]
        public IActionResult VoteReferendum([FromRoute] string chatRoomName, [FromRoute] int targetUUID, [FromRoute] string action,[FromHeader] string JWT, [FromHeader] int UUID) 
        {
            if (action != "agree" && action != "disagree") 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在对用户[ {targetUser} ]的放逐公投进行投票时失败，原因为传递了不合理的action参数[ {action} ]", UUID, targetUUID, action);
                ResponseT<string> voteReferendumFailed = new(2, "投票失败，传递参数有误");
                return Ok(voteReferendumFailed);
            }

            IDatabase? database = _redisConnection.GetDatabaseByChatRoomName(chatRoomName);
            if (database == null)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在对用户[ {targetUser} ]的放逐公投进行投票时失败，原因为尝试在不存在的聊天室[ {chatRoomName} ]发动公投", UUID, targetUUID, chatRoomName);
                ResponseT<string> voteReferendumFailed = new(3, "投票失败，该聊天室不存在");
                return Ok(voteReferendumFailed);
            }

            if (!database.KeyExists($"Exile{targetUUID}Referendum")) 
            {
                ResponseT<string> voteReferendumFailed = new(4, "投票失败，投票时间已结束");
                return Ok(voteReferendumFailed);
            }

            if (action == "agree")
            {
                _ = database.SetAddAsync($"Exile{targetUUID}ReferendumAgreeSet", UUID);
            }
            else 
            {
                _ = database.SetAddAsync($"Exile{targetUUID}ReferendumDisagreeSet", UUID);
            }
            ResponseT<string> voteReferendumSucceed = new(0, "投票成功");
            return Ok(voteReferendumSucceed);
        }

        //向某人发送私聊请求
        [HttpPost("chatRequest")]
        public async Task<IActionResult> SendChatRequest([FromBody] SendChatRequestRequestData requestData, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            IDatabase database = _redisConnection.GetChatRequestDatabase();

            //同一用户在十分钟内无法受到来自相同用户的多次私聊请求
            if (database.KeyExists($"{UUID}SendChatRequestTo{requestData.TargetUser}"))
            {
                ResponseT<string> sendChatRequestFailed = new(2, "发送私聊请求失败，您向该用户发送私聊请求的操作太过频繁");
                return Ok(sendChatRequestFailed);
            }

            //发送RPC请求
            SendChatRequestSingleRequest request = new()
            {
                SenderUUID = UUID,
                TargetUUID = requestData.TargetUser,
                GreetText = $"打招呼内容：{requestData.GreetText}",
                MessageText = "从聊天室收到了新的私聊请求",
            };

            GeneralReply reply = await _rpcChatRequestClient.SendChatRequestSingleAsync(
                          request);

            switch (reply.Code) 
            {
                case 0:
                    {
                        _ = database.StringSetAsync($"{UUID}SendChatRequestTo{requestData.TargetUser}", "", expiry: TimeSpan.FromMinutes(10));
                        ResponseT<string> sendChatRequestSucceed = new(0, "发送私聊请求成功");
                        return Ok(sendChatRequestSucceed);
                    }
                default: 
                    {
                        ResponseT<string> sendChatRequestFailed = new(3, reply.Message);
                        return Ok(sendChatRequestFailed);
                    }
            }
        }

        //负责维持WebSocket的连接
        private async Task MaintainConnection(string chatRoomName, string nickname, int UUID, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];

            try {
                WebSocketReceiveResult receiveResult;

                do {
                    receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, receiveResult.Count));
                        Dictionary<string, dynamic>? json = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(message);
                        string type = JsonSerializer.Deserialize<string>(json!["type"]);

                        switch (type)
                        {
                            case "CheckOnlineNumber":
                                var sendDataJson = JsonSerializer.Serialize(new { type = "CheckOnlineNumber", data = _webSocketsManager.webSockets[chatRoomName].Keys.Count }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
                                var sendData = new ArraySegment<byte>(sendDataBytes);
                                _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
                                break;
                        }
                    }
                } while (!receiveResult.CloseStatus.HasValue);

                _webSocketsManager.webSockets[chatRoomName].Remove(UUID);

                _ = webSocket.CloseAsync(
                    receiveResult.CloseStatus.Value,
                    receiveResult.CloseStatusDescription,
                    CancellationToken.None);

                BroadcastMembersChanged(chatRoomName, nickname, false);
            } catch (Exception ex) {
                _logger.LogError("Error：用户[ {UUID} ]在使用WebSocket进行通信时发生错误，报错信息为[ {ex} ]", UUID, ex);

                _webSocketsManager.webSockets[chatRoomName].Remove(UUID);

                _ = webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "服务器内部错误",
                    CancellationToken.None);

                BroadcastMembersChanged(chatRoomName, nickname, false);
            }
        }

        //广播消息：某人进入/离开了聊天室
        private void BroadcastMembersChanged(string chatRoomName, string nickname, bool isJoin)
        {
            DateTime now = DateTime.Now;

            string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            var sendDataJson = JsonSerializer.Serialize(new { type = "NewMessage", data = new ReusableClass.Message(timestamp, new(0, "", ""), now, true, false, false, false, false, "MembersChanged", "1.0.0", "应用版本过低，请升级至V1.0.0版本以上以阅读此消息", isJoin ? $"{nickname} 进入了聊天室" : $"{nickname} 离开了聊天室", null, null, null, null) }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
            var sendData = new ArraySegment<byte>(sendDataBytes);
            var webSockets = _webSocketsManager.webSockets[chatRoomName].Values;
            foreach (var webSocket in webSockets)
            {
                _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        //结算放逐公投投票结果
        private async void SettleReferendumResult(IDatabase database,string chatRoomName,TargetUser targetUser) 
        {
            var batch = database.CreateBatch();
            Task<long> getAgreeNumberTask = batch.SetLengthAsync($"Exile{targetUser.UUID}ReferendumAgreeSet");
            Task<long> getDisagreeNumberTask = batch.SetLengthAsync($"Exile{targetUser.UUID}ReferendumDisagreeSet");
            _ = batch.KeyExpireAsync($"Exile{targetUser.UUID}ReferendumAgreeSet", TimeSpan.FromSeconds(20));
            _ = batch.KeyExpireAsync($"Exile{targetUser.UUID}ReferendumDisagreeSet", TimeSpan.FromSeconds(20));
            batch.Execute();
            batch.WaitAll(getAgreeNumberTask,getDisagreeNumberTask);

            JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            //同意票数大于等于反对票数，实施放逐
            if (getAgreeNumberTask.Result >= getDisagreeNumberTask.Result) 
            {
                await database.StringSetAsync($"{targetUser.UUID}BeExiled","",expiry:TimeSpan.FromHours(1));
                var sendDataJsonForTargetUser = JsonSerializer.Serialize(new
                {
                    type = "BeExiled",
                }, jsonSerializerOptions);
                var sendDataBytesForTargetUser = Encoding.UTF8.GetBytes(sendDataJsonForTargetUser);
                var sendDataForTargetUser = new ArraySegment<byte>(sendDataBytesForTargetUser);
                if (_webSocketsManager.webSockets[chatRoomName].TryGetValue(targetUser.UUID, out var webSocket)) 
                {
                    _ = webSocket.SendAsync(sendDataForTargetUser, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }

            //广播结果
            DateTime now = DateTime.Now;

            string timestamp = (now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

            var sendDataJson = JsonSerializer.Serialize(new
            {
                type = "NewMessage",
                data = new ReusableClass.Message(timestamp, new(0, "", ""), now, true, false, false, false, false, "ReferendumResult", "1.0.0", "应用版本过低，请升级至V1.0.0版本以上以阅读此消息",
                JsonSerializer.Serialize(new
                {
                    uuid = targetUser.UUID,
                    avatar = targetUser.Avatar,
                    nickname = targetUser.Nickname,
                    agreeNumber = getAgreeNumberTask.Result,
                    disagreeNumber = getDisagreeNumberTask.Result,
                }, jsonSerializerOptions)
                , null, null, null, null)
            }, jsonSerializerOptions);
            var sendDataBytes = Encoding.UTF8.GetBytes(sendDataJson);
            var sendData = new ArraySegment<byte>(sendDataBytes);
            var webSockets = _webSocketsManager.webSockets[chatRoomName].Values;
            foreach (var webSocket in webSockets)
            {
                _ = webSocket.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
