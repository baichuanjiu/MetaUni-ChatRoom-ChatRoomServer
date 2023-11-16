namespace ChatRoom.API
{
    public class WebSocketsManager
    {
        public Dictionary<string, Dictionary<int, System.Net.WebSockets.WebSocket>> webSockets = new() 
        {
            {"Family",new()},
            {"Pantry",new()},
            {"Treehole",new()},
            {"Nijigen",new()},
            {"PartnerCorner",new()},
            {"BoxingGym",new()},
            {"Circus",new()},
            {"Confessional",new()},
        };
    }
    /*
     * {
     *      "聊天室名":{
     *                      UUID: WebSocket,
     *                      ...
     *                  },
     *      ...
     * }
     */
}
